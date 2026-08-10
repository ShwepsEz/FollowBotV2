using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory;
using ExileCore.Shared.Enums;
using GameOffsets.Native;
using SharpDX;
using FollowBotV2.Config;
using FollowBotV2.Helpers;
using FollowBotV2.Services;

namespace FollowBotV2.Services
{
    public class TransitionService : ITransitionService
    {
        private readonly IGameContext _gameContext;
        private readonly ILogService _log;
        private readonly FollowerSettings _settings;
        private readonly IPartyService _partyService;
        private readonly INavigationService _navigationService;
        private readonly IMouseService _mouseService;

        private List<Entity> _cachedTransitions = new List<Entity>();
        private DateTime _lastTransitionRefresh = DateTime.MinValue;
        private const int TRANSITION_REFRESH_INTERVAL_MS = 500;

        private DateTime _lastTransitionClickTime = DateTime.MinValue;
        private int _transitionClickAttempts = 0;
        private const int MAX_TRANSITION_CLICK_ATTEMPTS = 10;
        private const int TRANSITION_CLICK_RETRY_INTERVAL_MS = 500;

        private Entity _currentPortal = null;
        private DateTime _lastClickTime = DateTime.MinValue;
        private int _clickAttempts = 0;
        private const int MAX_CLICK_ATTEMPTS = 15;
        private const int CLICK_RETRY_INTERVAL_MS = 250;

        public TransitionService(IGameContext gameContext, ILogService log, FollowerSettings settings,
                                 IPartyService partyService, INavigationService navigationService,
                                 IMouseService mouseService)
        {
            _gameContext = gameContext;
            _log = log;
            _settings = settings;
            _partyService = partyService;
            _navigationService = navigationService;
            _mouseService = mouseService;
        }

        public int GetTransitionCount() => _cachedTransitions.Count;

        public Vector2i? GetNearestTransitionTarget()
        {
            RefreshTransitions();
            if (_cachedTransitions.Count == 0)
                return null;

            var player = _gameContext.Player;
            if (player == null) return null;
            var playerPos = player.GetComponent<Positioned>();
            if (playerPos == null) return null;
            var currentGrid = new Vector2i((int)playerPos.GridPosNum.X, (int)playerPos.GridPosNum.Y);

            Entity nearest = null;
            float minDist = float.MaxValue;

            foreach (var trans in _cachedTransitions)
            {
                if (trans == null || !trans.IsValid) continue;
                var posComp = trans.GetComponent<Positioned>();
                if (posComp == null) continue;
                var gridPos = new Vector2i((int)posComp.GridPosNum.X, (int)posComp.GridPosNum.Y);
                float dist = Distance(currentGrid, gridPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = trans;
                }
            }

            if (nearest == null) return null;

            var transPosComp = nearest.GetComponent<Positioned>();
            if (transPosComp == null) return null;
            var transGrid = new Vector2i((int)transPosComp.GridPosNum.X, (int)transPosComp.GridPosNum.Y);

            var walkable = _navigationService.FindNearestWalkable(transGrid, 15);
            return walkable ?? transGrid;
        }

        public bool ShouldClickTransition(Vector2i currentPosition, Vector2i transitionPosition)
        {
            float distance = Distance(currentPosition, transitionPosition);
            return distance <= (_settings.ImGui.StopDistance?.Value ?? 23);
        }

        public void ClickTransition(Vector2i transitionPosition)
        {
            if ((DateTime.Now - _lastTransitionClickTime).TotalMilliseconds < TRANSITION_CLICK_RETRY_INTERVAL_MS)
                return;

            if (_transitionClickAttempts >= MAX_TRANSITION_CLICK_ATTEMPTS)
            {
                _log.Warn("Max transition click attempts reached, giving up.");
                _transitionClickAttempts = 0;
                return;
            }

            try
            {
                var worldPos = GridToWorld(transitionPosition);
                var camera = _gameContext.GameController.Game.IngameState.Camera;
                if (camera == null) return;

                var screenPos = camera.WorldToScreen(new SharpDX.Vector3(worldPos.X, worldPos.Y, 0));
                if (screenPos == SharpDX.Vector2.Zero) return;

                var windowRect = _gameContext.GameController.Window.GetWindowRectangle();
                var targetScreen = new SharpDX.Vector2(
                    screenPos.X + windowRect.Location.X,
                    screenPos.Y + windowRect.Location.Y
                );

                _mouseService.MoveCursorSmooth(new MouseVector2(targetScreen.X, targetScreen.Y), 10);
                Thread.Sleep(100);
                _mouseService.LeftClick();

                _lastTransitionClickTime = DateTime.Now;
                _transitionClickAttempts++;
            }
            catch (Exception ex)
            {
                _log.Error($"Error clicking transition: {ex.Message}");
            }
        }

        public void RefreshTransitions()
        {
            if ((DateTime.Now - _lastTransitionRefresh).TotalMilliseconds < TRANSITION_REFRESH_INTERVAL_MS)
                return;

            _lastTransitionRefresh = DateTime.Now;
            _cachedTransitions.Clear();

            try
            {
                var entities = _gameContext.GameController.Entities
                    .Where(e => e != null && e.IsValid)
                    .Where(e => e.Type == EntityType.AreaTransition)
                    .ToList();

                _cachedTransitions.AddRange(entities);
            }
            catch (Exception ex)
            {
                _log.Error($"Error refreshing transitions: {ex.Message}");
            }
        }

        public void DrawTransitions(ExileCore.Graphics graphics)
        {
            if (_cachedTransitions.Count == 0) return;

            var camera = _gameContext.GameController.Game.IngameState.Camera;
            if (camera == null) return;

            foreach (var transition in _cachedTransitions)
            {
                if (transition == null || !transition.IsValid) continue;

                var posComp = transition.GetComponent<Positioned>();
                if (posComp == null) continue;

                var gridPos = new Vector2i((int)posComp.GridPosNum.X, (int)posComp.GridPosNum.Y);
                var worldPos = GridToWorld(gridPos);
                var screenPos = camera.WorldToScreen(new SharpDX.Vector3(worldPos.X, worldPos.Y, 0));

                if (screenPos == SharpDX.Vector2.Zero || screenPos.X <= 0 || screenPos.Y <= 0)
                    continue;

                float size = 12f;
                var color = SharpDX.Color.Cyan;

                graphics.DrawLine(
                    new SharpDX.Vector2(screenPos.X - size, screenPos.Y),
                    new SharpDX.Vector2(screenPos.X + size, screenPos.Y),
                    2f, color);
                graphics.DrawLine(
                    new SharpDX.Vector2(screenPos.X, screenPos.Y - size),
                    new SharpDX.Vector2(screenPos.X, screenPos.Y + size),
                    2f, color);
                graphics.DrawCircle(
                    new System.Numerics.Vector2(screenPos.X, screenPos.Y),
                    4f, color, 1f);

                string zoneName = GetTransitionTargetZone(transition);
                if (string.IsNullOrEmpty(zoneName))
                    zoneName = "Unknown";
                string id = transition.Address.ToString("X");

                string worldAreaId = "?";
                string transitionType = "?";
                string worldAreaName = "";

                try
                {
                    var areaComp = transition.GetComponent<AreaTransition>();
                    if (areaComp != null)
                    {
                        worldAreaId = areaComp.WorldAreaId.ToString();
                        transitionType = areaComp.TransitionType.ToString();
                        if (areaComp.WorldArea != null)
                            worldAreaName = areaComp.WorldArea.Name;
                        else
                        {
                            var worldAreaById = _gameContext.GameController.Files.WorldAreas.GetByAddress(areaComp.WorldAreaId);
                            if (worldAreaById != null)
                                worldAreaName = worldAreaById.Name;
                        }
                    }
                }
                catch { }

                var textPos = new SharpDX.Vector2(screenPos.X + size + 5, screenPos.Y - 6);
                graphics.DrawText($"{zoneName} (0x{id})", textPos, SharpDX.Color.White);
                textPos.Y += 16;
                graphics.DrawText($"WorldAreaId: {worldAreaId}, Type: {transitionType}", textPos, SharpDX.Color.LightGray);
                if (!string.IsNullOrEmpty(worldAreaName))
                {
                    textPos.Y += 16;
                    graphics.DrawText($"WorldArea: {worldAreaName}", textPos, SharpDX.Color.LightGray);
                }
            }
        }

        public Vector2i? GetPortalTarget(string leaderName)
        {
            if (string.IsNullOrEmpty(leaderName)) return null;

            if (!_settings.ImGui.UsePortals.Value)
                return null;

            bool sameZone = _partyService.IsPlayerInSameZone(leaderName);
            if (sameZone)
            {
                _currentPortal = null;
                return null;
            }

            string leaderZone = _partyService.GetPlayerZoneName(leaderName);
            if (string.IsNullOrEmpty(leaderZone))
            {
                _currentPortal = null;
                return null;
            }

            var portal = FindPortalToZone(leaderZone);
            if (portal == null || !portal.IsValid)
            {
                _currentPortal = null;
                return null;
            }

            _currentPortal = portal;

            var posComp = portal.GetComponent<Positioned>();
            if (posComp == null)
            {
                _currentPortal = null;
                return null;
            }

            var gridPos = new Vector2i((int)posComp.GridPosNum.X, (int)posComp.GridPosNum.Y);
            return gridPos;
        }

        public bool ShouldClickPortal(Vector2i currentPosition, Vector2i portalPosition)
        {
            if (_currentPortal != null && !_currentPortal.IsValid)
                return false;

            float distance = Distance(currentPosition, portalPosition);
            return distance <= (_settings.ImGui.StopDistance?.Value ?? 23);
        }

        public void ClickPortal(Vector2i portalPosition)
        {
            if ((DateTime.Now - _lastClickTime).TotalMilliseconds < CLICK_RETRY_INTERVAL_MS)
                return;

            if (_clickAttempts >= MAX_CLICK_ATTEMPTS)
            {
                _log.Warn("Max click attempts reached, giving up on this portal.");
                _clickAttempts = 0;
                return;
            }

            try
            {
                var worldPos = GridToWorld(portalPosition);
                var camera = _gameContext.GameController.Game.IngameState.Camera;
                if (camera == null) return;

                var screenPos = camera.WorldToScreen(new SharpDX.Vector3(worldPos.X, worldPos.Y, 0));
                if (screenPos == SharpDX.Vector2.Zero) return;

                var windowRect = _gameContext.GameController.Window.GetWindowRectangle();
                int offset = _settings.ImGui.PortalClickOffset.Value;
                var targetScreen = new SharpDX.Vector2(
                    screenPos.X + windowRect.Location.X + offset,
                    screenPos.Y + windowRect.Location.Y
                );

                _mouseService.MoveCursorSmooth(new MouseVector2(targetScreen.X, targetScreen.Y), 10);
                Thread.Sleep(100);
                _mouseService.LeftClick();

                _lastClickTime = DateTime.Now;
                _clickAttempts++;
            }
            catch (Exception ex)
            {
                _log.Error($"Error clicking portal: {ex.Message}");
            }
        }

        public void Reset()
        {
            _currentPortal = null;
            _clickAttempts = 0;
            _lastClickTime = DateTime.MinValue;
            _transitionClickAttempts = 0;
            _lastTransitionClickTime = DateTime.MinValue;
        }

        private Entity FindPortalToZone(string targetZone)
        {
            try
            {
                var entities = _gameContext.GameController.Entities
                    .Where(e => e != null && e.IsValid)
                    .Where(e => e.Type == EntityType.Portal ||
                                e.Type == EntityType.TownPortal ||
                                e.Type == EntityType.AreaTransition)
                    .ToList();

                foreach (var entity in entities)
                {
                    string target = GetTransitionTargetZone(entity);
                    if (!string.IsNullOrEmpty(target) &&
                        target.Contains(targetZone, StringComparison.OrdinalIgnoreCase))
                    {
                        return entity;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error finding portal: {ex.Message}");
            }
            return null;
        }

        public string GetTransitionTargetZone(Entity entity)
        {
            if (!string.IsNullOrEmpty(entity.RenderName))
            {
                string name = entity.RenderName;
                if (name.StartsWith("To ", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(3);
                return name.Trim();
            }
            if (!string.IsNullOrEmpty(entity.Path))
            {
                var parts = entity.Path.Split('/');
                if (parts.Length > 0)
                {
                    var lastPart = parts.Last();
                    var dotIndex = lastPart.LastIndexOf('.');
                    if (dotIndex > 0)
                        lastPart = lastPart.Substring(0, dotIndex);
                    return lastPart;
                }
            }
            return "";
        }

        private SharpDX.Vector2 GridToWorld(Vector2i gridPos)
        {
            const float multiplier = 250f / 23f;
            return new SharpDX.Vector2(gridPos.X * multiplier, gridPos.Y * multiplier);
        }

        private float Distance(Vector2i a, Vector2i b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}