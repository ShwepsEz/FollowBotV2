using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Components;
using System;
using System.Threading.Tasks;
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
        private IUltimatumService _ultimatumService;

        private FollowerState _state = FollowerState.Stopped;
        private DateTime _stateEnterTime = DateTime.Now;
        private DateTime _cooldownUntil = DateTime.MinValue;
        private DateTime _lastLeaderCheck = DateTime.Now;

        private TcpCommandServer _tcpServer;
        private int _lastTcpPort;

        private TcpClientManager[] _tcpClients;
        private const int MAX_TCP_CLIENTS = 5;

        // Удалено: private TcpClientManager _tcpClient;
        private string _lastTcpResponse = "";
        private DateTime _lastTcpStatusUpdate = DateTime.MinValue;

        private string _lastLeaderName = "";
        private bool _lastLeaderFound = false;

        private ImGuiOverlay _imGuiOverlay;

        public FollowerState CurrentState => _state;
        public float CooldownRemaining => (_cooldownUntil > DateTime.Now) ? (float)(_cooldownUntil - DateTime.Now).TotalSeconds : 0f;
        public IUltimatumService UltimatumService => _ultimatumService;

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

            _ultimatumService = new UltimatumService(_gameContext, _log, Settings, _mouseService);

            _navigationService.LoadWalkabilityData();

            _imGuiOverlay = new ImGuiOverlay(Settings, _log, _partyService, _gameContext, this, _skillService, _ultimatumService);

            _log.Info("FollowBotV2 initializing...");
            _inputService.RegisterKey(Settings.ImGui.FollowKey.Value);
            _inputService.RegisterKey(Keys.F7);
            _log.Info("FollowBotV2 initialized successfully.");
            _tcpServer = new TcpCommandServer(this, _log);

            _tcpClients = new TcpClientManager[MAX_TCP_CLIENTS];
            for (int i = 0; i < MAX_TCP_CLIENTS; i++)
                _tcpClients[i] = new TcpClientManager(_log);
            // Удалено: _tcpClient = new TcpClientManager(_log);

            return true;
        }

        public TcpCommandServer GetTcpServer() => _tcpServer;
        public IPartyService GetPartyService() => _partyService;
        public string GetLastTcpResponse() => _lastTcpResponse;

        // Удалены методы без индекса:
        // public async Task<string> SendTcpCommandAsync(string command) ...
        // public bool IsTcpConnected => ...
        // public async Task<bool> ConnectTcpAsync(string host, int port) ...
        // public void DisconnectTcp() ...

        public bool IsTcpConnected(int index) => index >= 0 && index < MAX_TCP_CLIENTS && _tcpClients[index].IsConnected;
        public async Task<string> SendTcpCommandAsync(int index, string command) => await _tcpClients[index].SendCommandAsync(command);
        public async Task<bool> ConnectTcpAsync(int index, string host, int port) => await _tcpClients[index].ConnectAsync(host, port);
        public void DisconnectTcp(int index) => _tcpClients[index].Disconnect();
        public string GetLastTcpResponse(int index) => _tcpClients[index].LastResponse;

        public async Task BroadcastTcpCommandAsync(string command)
        {
            var tasks = new List<Task<string>>();
            for (int i = 0; i < MAX_TCP_CLIENTS; i++)
            {
                if (_tcpClients[i].IsConnected)
                    tasks.Add(_tcpClients[i].SendCommandAsync(command));
            }
            if (tasks.Count == 0)
                _lastTcpResponse = "No clients connected.";
            else
            {
                var results = await Task.WhenAll(tasks);
                _lastTcpResponse = string.Join(" | ", results);
            }
        }

        public override void Render()
        {
            if (!Settings.Enable.Value) return;

            // --- Обработка клавиш и отрисовка интерфейса (всегда) ---
            if (_inputService.PressedOnce(Keys.F7))
            {
                _imGuiOverlay.ToggleVisibility();
            }

            // Рисуем интерфейс всегда
            _imGuiOverlay?.Draw();
            _imGuiOverlay?.DrawStatusWindow();

            // --- Обработка Ultimatum ---
            if (_ultimatumService != null && _ultimatumService.IsPanelOpen)
            {
                _ultimatumService.CheckAndHandle();
                return;
            }

            // --- Управление TCP-сервером (для режимов Follow/UltimatumFarm) ---
            if (Settings.ImGui.TcpServerEnabled.Value)
            {
                if (_tcpServer == null)
                {
                    _tcpServer = new TcpCommandServer(this, _log);
                    _tcpServer.Start(Settings.ImGui.TcpPort.Value);
                    _lastTcpPort = Settings.ImGui.TcpPort.Value;
                }
                else if (!_tcpServer.IsRunning || _lastTcpPort != Settings.ImGui.TcpPort.Value)
                {
                    _tcpServer.Stop();
                    _tcpServer.Start(Settings.ImGui.TcpPort.Value);
                    _lastTcpPort = Settings.ImGui.TcpPort.Value;
                }
            }
            else
            {
                if (_tcpServer != null)
                {
                    _tcpServer.Stop();
                    _tcpServer.Dispose();
                    _tcpServer = null;
                }
            }

            // --- Обработка режима TCPClient ---
            if (Settings.ImGui.BotMode.Value == "TCPClient")
            {
                // Останавливаем любую локальную навигацию
                if (_state != FollowerState.Stopped)
                {
                    _navigationService?.Stop();
                    SetState(FollowerState.Stopped);
                }
                // Обновляем статус с сервера раз в несколько секунд
                if ((DateTime.Now - _lastTcpStatusUpdate).TotalSeconds > 2)
                {
                    _lastTcpStatusUpdate = DateTime.Now;
                    Task.Run(async () =>
                    {
                        var responses = new List<string>();
                        for (int i = 0; i < MAX_TCP_CLIENTS; i++)
                        {
                            if (_tcpClients[i].IsConnected)
                            {
                                var resp = await _tcpClients[i].SendCommandAsync("status");
                                responses.Add($"Slot {i + 1}: {resp}");
                            }
                        }
                        _lastTcpResponse = string.Join("\n", responses);
                    });
                }
                return;
            }

            // --- Обычный режим (Follow или UltimatumFarm) ---
            if (_inputService.PressedOnce(Settings.ImGui.FollowKey.Value))
            {
                ToggleFollow();
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
        }

        public override void Dispose()
        {
            _tcpServer?.Dispose();
            if (_tcpClients != null)
            {
                foreach (var client in _tcpClients)
                    client?.Dispose();
            }
            base.Dispose();
        }

        public void ReloadWalkability()
        {
            _navigationService?.LoadWalkabilityData();
            _log.Info("Walkability data reloaded.");
        }

        private void UpdateFollowing()
        {
            string leaderName = Settings.ImGui.LeaderName.Value;

            if (Settings.ImGui.BotMode.Value == "TCPClient")
            {
                _navigationService?.Stop();
                return;
            }

            if (Settings.ImGui.BotMode.Value == "UltimatumFarm")
            {
                _navigationService?.Stop();
                return;
            }

            if (_ultimatumService != null && _ultimatumService.IsPanelOpen)
                return;

            if (string.IsNullOrEmpty(leaderName))
            {
                SetState(FollowerState.Stopped);
                return;
            }

            // Обработка кулдауна
            if (_state == FollowerState.Cooldown && DateTime.Now >= _cooldownUntil)
            {
                SetState(FollowerState.Following);
            }
            if (_state == FollowerState.Cooldown)
                return;

            // Интервал проверки лидера
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
                _navigationService?.Stop();
                return;
            }

            var leaderPos = _partyService.GetPlayerGridPosition(leaderName);
            bool found = leaderPos.HasValue;

            // ★★★ НОВАЯ ПРОВЕРКА: если мы в убежище и опция FollowInHideout выключена,
            // то бот не двигается за лидером, пока тот находится в той же зоне (убежище).
            // Если лидер не найден (ушёл на карту) – продолжаем логику к порталу.
            if (_gameContext.IsInHideout && !Settings.ImGui.FollowInHideout.Value)
            {
                if (found)
                {
                    // Лидер рядом – стоим на месте
                    _navigationService?.Stop();
                    // Если бот был в состоянии движения – ничего не меняем, просто не двигаемся
                    // (можно оставить состояние Following, но навигация остановлена)
                    return;
                }
                // Если лидер не найден – значит он уже покинул убежище, переходим к порталу
            }

            float stopDist = Settings.ImGui.StopDistance?.Value ?? 23;
            float tolerance = Settings.ImGui.StopDistanceTolerance?.Value ?? 15;

            _skillUsageService.Update();

            var player = _gameContext.Player;
            if (player == null) return;
            var playerPos = player.GetComponent<Positioned>();
            if (playerPos == null) return;
            var currentGrid = new Vector2i((int)playerPos.GridPosNum.X, (int)playerPos.GridPosNum.Y);

            // Если лидер найден и мы уже достаточно близко – останавливаемся
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

            // Обработка состояний
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
                _log.Warn("No transition found, will retry.");
                _navigationService?.Stop();
                SetState(FollowerState.Following);
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
                _log.Warn("Portal not found, will retry.");
                _navigationService?.Stop();
                SetState(FollowerState.Following);
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