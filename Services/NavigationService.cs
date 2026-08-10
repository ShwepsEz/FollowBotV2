using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExileCore;
using GameOffsets.Native;
using SharpDX;
using FollowBotV2.Config;
using FollowBotV2.Helpers;
using FollowBotV2.Services;

namespace FollowBotV2.Services
{
    public class NavigationService : INavigationService
    {
        private readonly IGameContext _gameContext;
        private readonly ILogService _log;
        private readonly FollowerSettings _settings;
        private readonly IInputService _inputService;
        private readonly IMouseService _mouseService;

        private DateTime _lastRebuildTime = DateTime.MinValue;
        private const int REBUILD_INTERVAL_MS = 200;
        private bool _forceRebuild = false;

        private int[][] _walkabilityData;
        private Vector2i? _areaDimensions;

        private List<Vector2i> _currentPath = new List<Vector2i>();
        private int _currentPathIndex = 0;
        private Vector2i? _currentTarget = null;
        private bool _isPathfinding = false;
        private DateTime _lastPathBuildTime = DateTime.MinValue;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        private bool _wantToMove = false;
        private DateTime _lastMouseMoveTime = DateTime.Now;
        private Vector2? _lastTargetScreenPosition = null;
        private bool _isMouseMoving = false;

        private Vector2? _smoothedScreenPosition = null;
        private const float SMOOTHING_FACTOR = 0.7f;

        private const int MIN_WALKABLE = 1;
        private const int MAX_WALKABLE = 5;
        private const float GRID_TO_WORLD_MULTIPLIER = 250f / 23f;

        public NavigationService(IGameContext gameContext, ILogService log, FollowerSettings settings,
                                 IInputService inputService, IMouseService mouseService)
        {
            _gameContext = gameContext;
            _log = log;
            _settings = settings;
            _inputService = inputService;
            _mouseService = mouseService;
        }

        public bool HasPath => _currentPath.Count > 1 && _currentPathIndex < _currentPath.Count;
        public bool IsPathfinding => _isPathfinding;

        public bool IsCloseToTarget
        {
            get
            {
                if (!_currentTarget.HasValue) return false;
                var player = _gameContext.Player;
                if (player == null) return false;
                var playerPos = player.GetComponent<ExileCore.PoEMemory.Components.Positioned>();
                if (playerPos == null) return false;
                var currentGrid = new Vector2i((int)playerPos.GridPosNum.X, (int)playerPos.GridPosNum.Y);
                return Distance(currentGrid, _currentTarget.Value) <= (_settings.ImGui.StopDistance?.Value ?? 23);
            }
        }

        public void LoadWalkabilityData()
        {
            try
            {
                var ingameData = _gameContext.GameController.IngameState.Data;
                if (ingameData == null)
                {
                    _log.Warn("IngameData is null, cannot load walkability.");
                    return;
                }

                if (_settings.ImGui.ClearTriggerableBlockades?.Value == true)
                {
                    _walkabilityData = ingameData.GetClearedPathfindingData();
                    _log.Debug("Loaded cleared walkability data (doors removed).");
                }
                else
                {
                    _walkabilityData = ingameData.RawPathfindingData;
                    _log.Debug("Loaded raw walkability data.");
                }

                _areaDimensions = ingameData.AreaDimensions;
                _log.Info($"Walkability data loaded: {_areaDimensions?.X}x{_areaDimensions?.Y}");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to load walkability data: {ex.Message}");
            }
        }

        public void MoveTo(Vector2i target)
        {
            if (!_areaDimensions.HasValue || _walkabilityData == null)
            {
                _log.Warn("Walkability data not loaded, cannot navigate.");
                return;
            }

            if (!_forceRebuild && _currentTarget.HasValue && _currentTarget.Value.Equals(target))
            {
                _log.Debug($"Target unchanged: ({target.X}, {target.Y}), skipping.");
                return;
            }

            _currentTarget = target;
            _forceRebuild = false;

            _currentPath.Clear();
            _currentPathIndex = 0;

            var player = _gameContext.Player;
            if (player == null)
            {
                _log.Warn("Player is null, cannot build path.");
                return;
            }

            var playerPos = player.GetComponent<ExileCore.PoEMemory.Components.Positioned>();
            if (playerPos == null)
            {
                _log.Warn("Player Positioned component is null.");
                return;
            }

            var start = new Vector2i((int)playerPos.GridPosNum.X, (int)playerPos.GridPosNum.Y);

            var adjustedTarget = target;
            if (!IsWalkable(target))
            {
                var nearest = FindNearestWalkable(target);
                if (nearest.HasValue)
                    adjustedTarget = nearest.Value;
                else
                {
                    _log.Warn("Target not walkable and no nearby walkable cell found.");
                    Stop();
                    return;
                }
            }

            _log.Info($"Building path from ({start.X}, {start.Y}) to ({adjustedTarget.X}, {adjustedTarget.Y})");
            _isPathfinding = true;
            _lastPathBuildTime = DateTime.Now;
            _ = BuildPathAsync(start, adjustedTarget);
        }

        public Vector2i? FindNearestWalkable(Vector2i center, int maxRadius = 10)
        {
            if (!_areaDimensions.HasValue || _walkabilityData == null)
                return null;

            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                            continue;

                        var pos = new Vector2i(center.X + dx, center.Y + dy);
                        if (pos.X < 0 || pos.X >= _areaDimensions.Value.X ||
                            pos.Y < 0 || pos.Y >= _areaDimensions.Value.Y)
                            continue;

                        if (IsWalkable(pos))
                            return pos;
                    }
                }
            }
            return null;
        }

        public void Stop()
        {
            if (_isPathfinding)
            {
                _cts.Cancel();
                _cts = new CancellationTokenSource();
                _isPathfinding = false;
            }

            _currentPath.Clear();
            _currentPathIndex = 0;
            _currentTarget = null;
            StopMovement();
            _log.Debug("Navigation stopped.");
        }

        public void Update()
        {
            if (!_currentTarget.HasValue)
            {
                StopMovement();
                return;
            }

            if (IsCloseToTarget)
            {
                if (_currentPath.Count > 0)
                {
                    _currentPath.Clear();
                    _currentPathIndex = 0;
                    _log.Debug("Cleared path (close to target)");
                }
                StopMovementOnly();
                return;
            }

            if (_isPathfinding)
                return;

            if ((DateTime.Now - _lastRebuildTime).TotalMilliseconds > REBUILD_INTERVAL_MS)
            {
                _lastRebuildTime = DateTime.Now;
                _forceRebuild = true;
                MoveTo(_currentTarget.Value);
            }

            if (_currentPath.Count > 0 && _currentPathIndex < _currentPath.Count)
            {
                MoveAlongPath();
            }
            else
            {
                StopMovement();
            }
        }

        private void MoveAlongPath()
        {
            var player = _gameContext.Player;
            if (player == null) return;

            var playerPos = player.GetComponent<ExileCore.PoEMemory.Components.Positioned>();
            if (playerPos == null) return;

            var currentGrid = new Vector2i((int)playerPos.GridPosNum.X, (int)playerPos.GridPosNum.Y);

            if (_currentPathIndex < _currentPath.Count)
            {
                var currentWaypoint = _currentPath[_currentPathIndex];
                if (Distance(currentGrid, currentWaypoint) < 20)
                {
                    _currentPathIndex++;
                    if (_currentPathIndex >= _currentPath.Count)
                    {
                        if (_currentTarget.HasValue)
                        {
                            float distToTargetAfterPath = Distance(currentGrid, _currentTarget.Value);
                            if (distToTargetAfterPath <= (_settings.ImGui.StopDistance?.Value ?? 23))
                            {
                                _log.Debug("Reached target, stopping.");
                                StopMovement();
                                return;
                            }
                            else
                            {
                                _log.Debug($"End of path but target far ({distToTargetAfterPath:F0}), rebuilding.");
                                MoveTo(_currentTarget.Value);
                                return;
                            }
                        }
                        StopMovement();
                        return;
                    }
                }
            }

            if (_currentPathIndex >= _currentPath.Count)
            {
                StopMovement();
                return;
            }

            var camera = _gameContext.GameController.Game.IngameState.Camera;
            if (camera == null) return;

            float lookAheadDistance = 50f;
            Vector2i lookAheadGrid = _currentPath[Math.Min(_currentPathIndex + 1, _currentPath.Count - 1)];
            float accumulated = 0f;
            for (int i = _currentPathIndex; i < _currentPath.Count - 1; i++)
            {
                float segDist = Distance(_currentPath[i], _currentPath[i + 1]);
                if (accumulated + segDist >= lookAheadDistance)
                {
                    float t = (lookAheadDistance - accumulated) / segDist;
                    lookAheadGrid = new Vector2i(
                        (int)(_currentPath[i].X + (_currentPath[i + 1].X - _currentPath[i].X) * t),
                        (int)(_currentPath[i].Y + (_currentPath[i + 1].Y - _currentPath[i].Y) * t)
                    );
                    break;
                }
                accumulated += segDist;
                lookAheadGrid = _currentPath[i + 1];
            }

            var currentWorld = GridToWorld(currentGrid);
            var currentScreen = camera.WorldToScreen(currentWorld);
            if (currentScreen == Vector2.Zero) return;

            var lookAheadWorld = GridToWorld(lookAheadGrid);
            var lookAheadScreen = camera.WorldToScreen(lookAheadWorld);
            if (lookAheadScreen == Vector2.Zero) return;

            Vector2 direction = lookAheadScreen - currentScreen;
            if (direction.Length() < 1) return;
            direction.Normalize();

            float targetScreenDistance;
            if (_currentTarget.HasValue)
            {
                float distToTarget = Distance(currentGrid, _currentTarget.Value);
                float maxLookAhead = _settings.ImGui.MaxLookAheadPixels.Value;
                float minLookAhead = _settings.ImGui.MinLookAheadPixels.Value;
                float maxGridDist = _settings.ImGui.MaxGridDistance.Value;
                float minGridDist = _settings.ImGui.MinGridDistance.Value;

                float t = (distToTarget - minGridDist) / (maxGridDist - minGridDist);
                t = Math.Clamp(t, 0f, 1f);
                targetScreenDistance = minLookAhead + t * (maxLookAhead - minLookAhead);
            }
            else
            {
                targetScreenDistance = _settings.ImGui.MaxLookAheadPixels.Value;
            }

            Vector2 targetScreenPos = currentScreen + direction * targetScreenDistance;

            var windowRect = _gameContext.GameController.Window.GetWindowRectangle();
            var finalTargetScreen = new Vector2(
                targetScreenPos.X + windowRect.Location.X,
                targetScreenPos.Y + windowRect.Location.Y
            );

            if (!_smoothedScreenPosition.HasValue)
            {
                _smoothedScreenPosition = finalTargetScreen;
            }
            else
            {
                _smoothedScreenPosition = Vector2.Lerp(_smoothedScreenPosition.Value, finalTargetScreen, SMOOTHING_FACTOR);
            }

            bool shouldMoveMouse = false;
            if (!_lastTargetScreenPosition.HasValue)
            {
                _lastTargetScreenPosition = _smoothedScreenPosition.Value;
                shouldMoveMouse = true;
            }
            else
            {
                float dx = _lastTargetScreenPosition.Value.X - _smoothedScreenPosition.Value.X;
                float dy = _lastTargetScreenPosition.Value.Y - _smoothedScreenPosition.Value.Y;
                float mouseDistance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (mouseDistance > 3 || (DateTime.Now - _lastMouseMoveTime).TotalMilliseconds > 30)
                {
                    shouldMoveMouse = true;
                }
            }

            if (shouldMoveMouse)
            {
                _mouseService.MoveCursorSmooth(
                    new MouseVector2(_smoothedScreenPosition.Value.X, _smoothedScreenPosition.Value.Y),
                    8
                );
                _lastTargetScreenPosition = _smoothedScreenPosition.Value;
                _lastMouseMoveTime = DateTime.Now;
                _isMouseMoving = true;
            }

            float distToTargetFinal = _currentTarget.HasValue ? Distance(currentGrid, _currentTarget.Value) : float.MaxValue;
            if (distToTargetFinal > (_settings.ImGui.StopDistance?.Value ?? 23))
            {
                if (!_wantToMove)
                {
                    _wantToMove = true;
                    _inputService.KeyDown(_settings.ImGui.MovementKey.Value);
                }
            }
            else
            {
                if (_wantToMove)
                {
                    _wantToMove = false;
                    _inputService.KeyUp(_settings.ImGui.MovementKey.Value);
                }
            }
        }

        private void StopMovement()
        {
            if (_wantToMove)
            {
                _wantToMove = false;
                _inputService.KeyUp(_settings.ImGui.MovementKey.Value);
            }
            _lastTargetScreenPosition = null;
            _isMouseMoving = false;
            _smoothedScreenPosition = null;
        }

        private void StopMovementOnly()
        {
            if (_wantToMove)
            {
                _wantToMove = false;
                _inputService.KeyUp(_settings.ImGui.MovementKey.Value);
            }
            _lastTargetScreenPosition = null;
            _isMouseMoving = false;
            _smoothedScreenPosition = null;
        }

        public void DrawPath(Graphics graphics)
        {
            if (!_settings.ImGui.DrawPath?.Value == true) return;
            if (_currentPath.Count < 2) return;

            var camera = _gameContext.GameController.Game.IngameState.Camera;
            if (camera == null) return;

            for (int i = 0; i < _currentPath.Count - 1; i++)
            {
                var p1 = GridToWorld(_currentPath[i]);
                var p2 = GridToWorld(_currentPath[i + 1]);
                var screen1 = camera.WorldToScreen(p1);
                var screen2 = camera.WorldToScreen(p2);

                if (screen1 != Vector2.Zero && screen2 != Vector2.Zero)
                {
                    graphics.DrawLine(screen1, screen2, 2, Color.Red);
                }
            }

            if (_currentPathIndex < _currentPath.Count)
            {
                var targetWorld = GridToWorld(_currentPath[_currentPathIndex]);
                var screenTarget = camera.WorldToScreen(targetWorld);
                if (screenTarget != Vector2.Zero)
                {
                    graphics.DrawFrame(
                        new RectangleF(screenTarget.X - 8, screenTarget.Y - 8, 16, 16),
                        Color.Yellow, 2);
                }
            }
        }

        // A* и вспомогательные методы (без изменений)
        private bool IsWalkable(Vector2i pos)
        {
            if (!_areaDimensions.HasValue || _walkabilityData == null)
                return false;

            if (pos.X < 0 || pos.X >= _areaDimensions.Value.X ||
                pos.Y < 0 || pos.Y >= _areaDimensions.Value.Y)
                return false;

            if (pos.Y >= _walkabilityData.Length || pos.X >= _walkabilityData[pos.Y].Length)
                return false;

            var value = _walkabilityData[pos.Y][pos.X];
            return value >= MIN_WALKABLE && value <= MAX_WALKABLE;
        }

        private async Task BuildPathAsync(Vector2i start, Vector2i target)
        {
            try
            {
                var path = await Task.Run(() => AStar(start, target, _cts.Token), _cts.Token);
                _isPathfinding = false;

                if (path != null && path.Count > 1)
                {
                    _currentPath = path;
                    _currentPathIndex = 0;
                    _log.Info($"Path found: {path.Count} waypoints.");
                }
                else
                {
                    _currentPath.Clear();
                    _currentPathIndex = 0;
                    _log.Warn($"Path not found or too short. Start=({start.X},{start.Y}) Target=({target.X},{target.Y})");
                }
            }
            catch (OperationCanceledException)
            {
                _log.Debug("Pathfinding was cancelled.");
                _isPathfinding = false;
            }
            catch (Exception ex)
            {
                _log.Error($"Pathfinding error: {ex.Message}");
                _isPathfinding = false;
            }
        }

        private List<Vector2i> AStar(Vector2i start, Vector2i target, CancellationToken ct)
        {
            var openSet = new AStarPriorityQueue<Vector2i, float>();
            var cameFrom = new Dictionary<Vector2i, Vector2i>();
            var gScore = new Dictionary<Vector2i, float>();
            var fScore = new Dictionary<Vector2i, float>();

            openSet.Enqueue(start, 0);
            gScore[start] = 0;
            fScore[start] = Heuristic(start, target);

            var maxIterations = 100000;
            var iterations = 0;

            while (openSet.Count > 0 && iterations < maxIterations)
            {
                ct.ThrowIfCancellationRequested();
                iterations++;

                var current = openSet.Dequeue();

                if (current.Equals(target))
                {
                    return ReconstructPath(cameFrom, current);
                }

                foreach (var neighbor in GetNeighbors(current))
                {
                    if (!IsWalkable(neighbor))
                        continue;

                    float tentativeGScore = gScore[current] + Distance(current, neighbor);

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, target);

                        if (!openSet.UnorderedItems.Any(x => x.Element.Equals(neighbor)))
                        {
                            openSet.Enqueue(neighbor, fScore[neighbor]);
                        }
                    }
                }
            }

            return null;
        }

        private List<Vector2i> ReconstructPath(Dictionary<Vector2i, Vector2i> cameFrom, Vector2i current)
        {
            var path = new List<Vector2i> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Insert(0, current);
            }
            return path;
        }

        private float Heuristic(Vector2i a, Vector2i b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private float Distance(Vector2i a, Vector2i b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private IEnumerable<Vector2i> GetNeighbors(Vector2i pos)
        {
            var directions = new[]
            {
                new Vector2i(0, 1), new Vector2i(1, 0), new Vector2i(0, -1), new Vector2i(-1, 0),
                new Vector2i(1, 1), new Vector2i(1, -1), new Vector2i(-1, 1), new Vector2i(-1, -1)
            };

            foreach (var dir in directions)
            {
                yield return new Vector2i(pos.X + dir.X, pos.Y + dir.Y);
            }
        }

        private Vector3 GridToWorld(Vector2i gridPos)
        {
            var x = gridPos.X * GRID_TO_WORLD_MULTIPLIER;
            var y = gridPos.Y * GRID_TO_WORLD_MULTIPLIER;

            float height = 0;
            try
            {
                var heightData = _gameContext.GameController.IngameState.Data.RawTerrainHeightData;
                if (heightData != null && gridPos.Y < heightData.Length && gridPos.X < heightData[gridPos.Y].Length)
                {
                    height = heightData[gridPos.Y][gridPos.X];
                }
            }
            catch { }

            return new Vector3(x, y, height);
        }
    }
}