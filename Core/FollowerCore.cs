using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Components;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FollowBotV2.Config;
using FollowBotV2.Services;
using GameOffsets.Native;
using ImGuiNET;

namespace FollowBotV2.Core
{
    public enum FollowerState
    {
        Stopped,
        Following,
        WaitingForPath,
        Transitioning,
        Portaling,
        Cooldown
    }

    public class FollowerCore : BaseSettingsPlugin<FollowerSettings>
    {
        private ILogService _log;
        private IPartyService _partyService;
        private IGameContext _gameContext;
        private INavigationService _navigationService;
        private ITransitionService _transitionService;
        private IInputService _inputService;
        private IMouseService _mouseService;
        private ISkillService _skillService;
        private ISkillUsageService _skillUsageService;

        private FollowerState _state = FollowerState.Stopped;
        private DateTime _stateEnterTime = DateTime.Now;
        private DateTime _cooldownUntil = DateTime.MinValue;
        private DateTime _lastLeaderCheck = DateTime.Now;

        private string _lastLeaderName = "";
        private bool _lastLeaderFound = false;

        private ImGuiOverlay _imGuiOverlay;

        public FollowerState CurrentState => _state;
        public float CooldownRemaining => (_cooldownUntil > DateTime.Now) ? (float)(_cooldownUntil - DateTime.Now).TotalSeconds : 0f;

        public override bool Initialise()
        {
            var serviceLocator = new ServiceLocator();

            _gameContext = new GameContext(GameController);
            serviceLocator.Register<IGameContext>(_gameContext);

            _log = new LogService(this);
            serviceLocator.Register<ILogService>(_log);

            _partyService = new PartyService(_gameContext, _log);
            serviceLocator.Register<IPartyService>(_partyService);

            _inputService = new InputService();
            _mouseService = new MouseService();
            serviceLocator.Register<IInputService>(_inputService);
            serviceLocator.Register<IMouseService>(_mouseService);

            _navigationService = new NavigationService(_gameContext, _log, Settings, _inputService, _mouseService);
            serviceLocator.Register<INavigationService>(_navigationService);

            _transitionService = new TransitionService(_gameContext, _log, Settings, _partyService, _navigationService, _mouseService);
            serviceLocator.Register<ITransitionService>(_transitionService);

            _skillService = new SkillService(_gameContext, _log);
            serviceLocator.Register<ISkillService>(_skillService);

            _skillUsageService = new SkillUsageService(_gameContext, _log, _skillService, Settings, _inputService, _mouseService, _partyService, this);
            serviceLocator.Register<ISkillUsageService>(_skillUsageService);

            _navigationService.LoadWalkabilityData();

            _imGuiOverlay = new ImGuiOverlay(Settings, _log, _partyService, _gameContext, this, _skillService);

            _log.Info("FollowBotV2 initializing...");
            _inputService.RegisterKey(Settings.ImGui.FollowKey.Value);
            _inputService.RegisterKey(Keys.F7);
            _log.Info("FollowBotV2 initialized successfully.");
            return true;
        }

        public override void Render()
        {
            if (!Settings.Enable.Value) return;

            if (_inputService.PressedOnce(Settings.ImGui.FollowKey.Value))
            {
                ToggleFollow();
            }

            if (_inputService.PressedOnce(Keys.F7))
            {
                _imGuiOverlay.ToggleVisibility();
            }

            if (_state != FollowerState.Stopped)
            {
                UpdateFollowing();
                _navigationService?.Update();
                _skillUsageService?.Update();
            }

            _navigationService?.DrawPath(Graphics);

            if (Settings.ImGui.DrawTransitions.Value)
            {
                _transitionService?.RefreshTransitions();
                _transitionService?.DrawTransitions(Graphics);
            }

            _skillUsageService?.Update();

            _imGuiOverlay?.Draw();
            _imGuiOverlay?.DrawStatusWindow();
            _skillUsageService?.Update();
        }

        public void ReloadWalkability()
        {
            _navigationService?.LoadWalkabilityData();
            _log.Info("Walkability data reloaded.");
        }

        private void UpdateFollowing()
        {
            string leaderName = Settings.ImGui.LeaderName.Value;
            if (string.IsNullOrEmpty(leaderName))
            {
                SetState(FollowerState.Stopped);
                return;
            }

            // ★★★ Если мы в кулдауне и время вышло — переключаемся обратно в Following ★★★
            if (_state == FollowerState.Cooldown && DateTime.Now >= _cooldownUntil)
            {
                SetState(FollowerState.Following);
                _log.Debug("Cooldown expired, resuming following.");
            }

            // Если кулдаун ещё активен — ничего не делаем
            if (_state == FollowerState.Cooldown)
                return;

            if ((DateTime.Now - _lastLeaderCheck).TotalMilliseconds < Settings.ImGui.LeaderCheckIntervalMs.Value)
                return;
            _lastLeaderCheck = DateTime.Now;

            bool inParty = _partyService.IsLeaderInParty(leaderName);
            if (!inParty)
            {
                if (_lastLeaderFound)
                {
                    _log.Info($"Leader '{leaderName}' left party.");
                    _lastLeaderFound = false;
                }
                // Не сбрасываем состояние, просто останавливаем движение
                _navigationService?.Stop();
                return;
            }

            var leaderPos = _partyService.GetPlayerGridPosition(leaderName);
            bool found = leaderPos.HasValue;
            float stopDist = Settings.ImGui.StopDistance?.Value ?? 23;
            float tolerance = Settings.ImGui.StopDistanceTolerance?.Value ?? 15;

            _skillUsageService.Update();

            var player = _gameContext.Player;
            if (player == null) return;
            var playerPos = player.GetComponent<Positioned>();
            if (playerPos == null) return;
            var currentGrid = new Vector2i((int)playerPos.GridPosNum.X, (int)playerPos.GridPosNum.Y);

            if (found && Distance(currentGrid, leaderPos.Value) <= stopDist + tolerance)
            {
                if (_state != FollowerState.Stopped && _state != FollowerState.Following)
                {
                    SetState(FollowerState.Following);
                }
                _navigationService?.Stop();
                _lastLeaderFound = true;
                return;
            }

            switch (_state)
            {
                case FollowerState.Stopped:
                    break;
                case FollowerState.Following:
                    HandleFollowingState(found, leaderPos, currentGrid, stopDist + tolerance);
                    break;
                case FollowerState.WaitingForPath:
                    HandleWaitingForPathState(found, leaderPos, currentGrid);
                    break;
                case FollowerState.Transitioning:
                    HandleTransitioningState();
                    break;
                case FollowerState.Portaling:
                    HandlePortalingState(leaderName);
                    break;
                    // Cooldown уже обработан выше
            }

            if (_state == FollowerState.Following || _state == FollowerState.WaitingForPath)
            {
                _skillUsageService?.Update();
            }
        }

        private void HandleFollowingState(bool found, Vector2i? leaderPos, Vector2i currentGrid, float stopDistWithTolerance)
        {
            if (!found)
            {
                bool sameZone = _partyService.IsPlayerInSameZone(Settings.ImGui.LeaderName.Value);
                if (sameZone)
                {
                    SetState(FollowerState.Transitioning);
                }
                else
                {
                    SetState(FollowerState.Portaling);
                }
                return;
            }

            if (_navigationService.HasPath)
                return;

            _navigationService?.MoveTo(leaderPos.Value);
            SetState(FollowerState.WaitingForPath);
        }

        private void HandleWaitingForPathState(bool found, Vector2i? leaderPos, Vector2i currentGrid)
        {
            if (_navigationService.HasPath)
            {
                SetState(FollowerState.Following);
                return;
            }

            if (_navigationService.IsPathfinding)
            {
                if ((DateTime.Now - _stateEnterTime).TotalMilliseconds > Settings.ImGui.PathBuildTimeoutMs.Value)
                {
                    _log.Warn("Path build timeout, switching to transition.");
                    _navigationService?.Stop();
                    SetState(FollowerState.Transitioning);
                }
                return;
            }

            _log.Warn("Path not found, switching to transition.");
            _navigationService?.Stop();
            SetState(FollowerState.Transitioning);
        }

        private void HandleTransitioningState()
        {
            if (DateTime.Now < _cooldownUntil)
            {
                SetState(FollowerState.Cooldown);
                return;
            }

            var transitionPos = _transitionService.GetNearestTransitionTarget();
            if (!transitionPos.HasValue)
            {
                _log.Warn("No transition found, stopping.");
                _navigationService?.Stop();
                SetState(FollowerState.Stopped);
                return;
            }

            if (!_navigationService.HasPath && !_navigationService.IsPathfinding)
            {
                _navigationService?.MoveTo(transitionPos.Value);
            }

            var player = _gameContext.Player;
            if (player == null) return;
            var playerPos = player.GetComponent<Positioned>();
            if (playerPos == null) return;
            var currentGrid = new Vector2i((int)playerPos.GridPosNum.X, (int)playerPos.GridPosNum.Y);

            if (_transitionService.ShouldClickTransition(currentGrid, transitionPos.Value))
            {
                _transitionService.ClickTransition(transitionPos.Value);
                _navigationService?.Stop();
                _cooldownUntil = DateTime.Now.AddSeconds(Settings.ImGui.TransitionCooldownSeconds.Value);
                _lastLeaderFound = false;
                SetState(FollowerState.Cooldown);
            }
        }

        private void HandlePortalingState(string leaderName)
        {
            if (DateTime.Now < _cooldownUntil)
            {
                SetState(FollowerState.Cooldown);
                return;
            }

            var portalPos = _transitionService.GetPortalTarget(leaderName);
            if (!portalPos.HasValue)
            {
                _log.Warn("Portal not found, stopping.");
                _navigationService?.Stop();
                SetState(FollowerState.Stopped);
                return;
            }

            if (!_navigationService.HasPath && !_navigationService.IsPathfinding)
            {
                _navigationService?.MoveTo(portalPos.Value);
            }

            var player = _gameContext.Player;
            if (player == null) return;
            var playerPos = player.GetComponent<Positioned>();
            if (playerPos == null) return;
            var currentGrid = new Vector2i((int)playerPos.GridPosNum.X, (int)playerPos.GridPosNum.Y);

            if (_transitionService.ShouldClickPortal(currentGrid, portalPos.Value))
            {
                _transitionService.ClickPortal(portalPos.Value);
                _navigationService?.Stop();
                _cooldownUntil = DateTime.Now.AddSeconds(3);
                _lastLeaderFound = false;
                SetState(FollowerState.Cooldown);
            }
        }

        private void SetState(FollowerState newState)
        {
            if (_state == newState) return;
            _state = newState;
            _stateEnterTime = DateTime.Now;
        }

        public void ToggleFollow()
        {
            if (_state == FollowerState.Stopped)
            {
                SetState(FollowerState.Following);
                _lastLeaderFound = false;
                _cooldownUntil = DateTime.MinValue;
                _log.Info($"Following started. Target: {Settings.ImGui.LeaderName.Value}");
            }
            else
            {
                _navigationService?.Stop();
                SetState(FollowerState.Stopped);
                _log.Info("Following stopped.");
            }
        }

        public override void AreaChange(AreaInstance area)
        {
            _log.Info($"Area changed: {area?.Name ?? "Unknown"}");
            _lastLeaderFound = false;
            _cooldownUntil = DateTime.MinValue;
            _transitionService?.Reset();
            _transitionService?.RefreshTransitions();
            _navigationService?.LoadWalkabilityData();
            _navigationService?.Stop();
            _skillService?.RefreshKeybindings();
            // Если бот был в состоянии Following, продолжаем следовать, иначе оставляем как есть
            if (_state != FollowerState.Stopped)
                SetState(FollowerState.Following);
        }

        public override void DrawSettings()
        {
            base.DrawSettings();
            ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.8f, 0.2f, 1f), "Press F7 to open advanced settings (ImGui)");
            ImGui.Separator();
        }

        public float Distance(Vector2i a, Vector2i b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}