using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Forms;
using System.Threading.Tasks;
using ExileCore;
using FollowBotV2.Config;
using FollowBotV2.Services;
using ImGuiNET;
using System.Linq;

namespace FollowBotV2.Core
{
    public class ImGuiOverlay
    {
        private readonly FollowerSettings _settings;
        private readonly ILogService _log;
        private readonly IPartyService _partyService;
        private readonly IGameContext _gameContext;
        private readonly FollowerCore _core;
        private readonly ISkillService _skillService;

        private Vector2 _statusWindowPos = new Vector2(100, 100);
        private bool _isDraggingStatusWindow = false;
        private Vector2 _dragStartMouse = Vector2.Zero;
        private Vector2 _dragStartPos = Vector2.Zero;
        private readonly IUltimatumService _ultimatumService;

        private string _tcpHost = "127.0.0.1";
        private readonly int[] _tcpPorts = new int[] { 8080, 8081, 8082, 8083, 8084, 8085, 8086, 8087, 8088, 8089 };
        private string _customCommand = "";

        private int _selectedTcpSlot = 0;

        private List<string> _foundServers = new List<string>();
        private bool _isScanning = false;
        private string _selectedServer = null;

        private bool _isVisible = false;
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "General", "Pathfinding", "Transitions", "Skills", "Ultimatum" };

        public ImGuiOverlay(FollowerSettings settings, ILogService log, IPartyService partyService,
                    IGameContext gameContext, FollowerCore core, ISkillService skillService,
                    IUltimatumService ultimatumService)
        {
            _settings = settings;
            _log = log;
            _partyService = partyService;
            _gameContext = gameContext;
            _core = core;
            _skillService = skillService;
            _ultimatumService = ultimatumService;
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        public void ToggleVisibility() => _isVisible = !_isVisible;

        public void Draw()
        {
            if (!_isVisible) return;

            ImGui.SetNextWindowSize(new Vector2(600, 400), ImGuiCond.FirstUseEver);
            PushCustomStyle();

            ImGui.Begin("FollowBotV2 Settings", ref _isVisible, ImGuiWindowFlags.NoCollapse);

            if (ImGui.BeginTabBar("MainTabs"))
            {
                for (int i = 0; i < _tabNames.Length; i++)
                {
                    if (ImGui.BeginTabItem(_tabNames[i]))
                    {
                        _selectedTab = i;
                        DrawTab(i);
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }

            ImGui.End();
            PopCustomStyle();
        }

        private void PushCustomStyle()
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.12f, 0.95f));
            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.2f, 0.2f, 0.25f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.3f, 0.3f, 0.4f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.25f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.3f, 0.3f, 0.4f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.4f, 0.4f, 0.5f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.4f, 0.6f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.5f, 0.7f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.6f, 0.8f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.15f, 0.15f, 0.18f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(0.3f, 0.3f, 0.4f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(0.2f, 0.4f, 0.6f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Vector4(0.4f, 0.6f, 0.8f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(0.5f, 0.7f, 0.9f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.2f, 0.3f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.3f, 0.3f, 0.4f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.4f, 0.4f, 0.5f, 1.0f));

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);
            ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 3f);
        }

        private void PopCustomStyle()
        {
            ImGui.PopStyleColor(17);
            ImGui.PopStyleVar(3);
        }

        private void DrawTab(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: DrawGeneralTab(); break;
                case 1: DrawPathfindingTab(); break;
                case 2: DrawTransitionsTab(); break;
                case 3: DrawSkillsTab(); break;
                case 4: DrawUltimatumTab(); break;
            }
        }

        private void DrawGeneralTab()
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "General Settings");
            ImGui.Separator();

            ImGui.Text("Leader Name:");
            ImGui.SameLine();
            var leaderName = _settings.ImGui.LeaderName.Value;
            if (ImGui.InputText("##LeaderName", ref leaderName, 64))
                _settings.ImGui.LeaderName.Value = leaderName;

            ImGui.Text("Follow Key:");
            ImGui.SameLine();
            ImGui.Text(_settings.ImGui.FollowKey.Value.ToString());

            ImGui.Separator();

            // ★★★ РЕЖИМЫ (добавлен TCPClient) ★★★
            string[] modeNames = { "Follow", "UltimatumFarm", "TCPClient" };
            int modeIndex = Array.IndexOf(modeNames, _settings.ImGui.BotMode.Value);
            if (modeIndex < 0) modeIndex = 0;
            if (ImGui.Combo("Bot Mode", ref modeIndex, modeNames, modeNames.Length))
                _settings.ImGui.BotMode.Value = modeNames[modeIndex];
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Select bot behavior mode");
            ImGui.Separator();

            // ---- Кнопки Start/Stop и Reload (только не в режиме TCPClient) ----
            if (_settings.ImGui.BotMode.Value != "TCPClient")
            {
                if (_core.CurrentState == FollowerState.Stopped)
                {
                    if (ImGui.Button("Start Following", new Vector2(120, 30)))
                        _core.ToggleFollow();
                }
                else
                {
                    if (ImGui.Button("Stop Following", new Vector2(120, 30)))
                        _core.ToggleFollow();
                }
                ImGui.SameLine();
                if (ImGui.Button("Reload Walkability", new Vector2(150, 30)))
                    _core.ReloadWalkability();

                bool followInHideout = _settings.ImGui.FollowInHideout.Value;
                if (ImGui.Checkbox("Follow in Hideout", ref followInHideout))
                    _settings.ImGui.FollowInHideout.Value = followInHideout;
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f),
                    "If disabled, bot will stand still in hideout until leader leaves via portal");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.8f, 1f, 0.8f, 1f), "Remote control mode");
                ImGui.Text("Connect to a TCP server to control the bot.");
            }

            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Status Window");
            bool showStatus = _settings.ImGui.ShowStatusWindow.Value;
            if (ImGui.Checkbox("Show Status Window", ref showStatus))
                _settings.ImGui.ShowStatusWindow.Value = showStatus;
            bool lockStatus = _settings.ImGui.LockStatusWindow.Value;
            if (ImGui.Checkbox("Lock Status Window (click-through)", ref lockStatus))
                _settings.ImGui.LockStatusWindow.Value = lockStatus;

            // ★★★★★ СЕКЦИЯ TCP-СЕРВЕР (только не в режиме TCPClient) ★★★★★
            if (_settings.ImGui.BotMode.Value != "TCPClient")
            {
                ImGui.Separator();
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "TCP Command Server");
                bool tcpEnabled = _settings.ImGui.TcpServerEnabled.Value;
                if (ImGui.Checkbox("Enable TCP Server", ref tcpEnabled))
                    _settings.ImGui.TcpServerEnabled.Value = tcpEnabled;

                if (tcpEnabled)
                {
                    ImGui.Indent();
                    int portIndex = Array.IndexOf(_tcpPorts, _settings.ImGui.TcpPort.Value);
                    if (portIndex < 0) portIndex = 0;
                    if (ImGui.Combo("Port", ref portIndex, _tcpPorts.Select(p => p.ToString()).ToArray(), _tcpPorts.Length))
                        _settings.ImGui.TcpPort.Value = _tcpPorts[portIndex];

                    bool isRunning = _core.GetTcpServer()?.IsRunning ?? false;
                    ImGui.TextColored(isRunning ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                        $"Status: {(isRunning ? "Running" : "Stopped")}");

                    try
                    {
                        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                        ImGui.Text("Local IPs:");
                        foreach (var ip in host.AddressList)
                        {
                            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                ImGui.Text($"  {ip}:{_settings.ImGui.TcpPort.Value}");
                        }
                    }
                    catch { }

                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Commands: start, stop, status, setleader <name>, setmode <follow|ultimatumfarm>, reload, help");
                    ImGui.Unindent();
                }
            }

            // ★★★★★ СЕКЦИЯ TCP CLIENT (только в режиме TCPClient) ★★★★★
            if (_settings.ImGui.BotMode.Value == "TCPClient")
            {
                ImGui.Separator();
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "TCP Client Control");
                ImGui.Indent();

                // ---- Сохранённые серверы ----
                if (_settings.ImGui.SavedServerIPs.Count > 0)
                {
                    ImGui.Text("Saved servers:");
                    if (ImGui.BeginCombo("##savedServers", _selectedServer ?? "Select from saved"))
                    {
                        foreach (var ip in _settings.ImGui.SavedServerIPs)
                        {
                            bool isSelected = (_selectedServer == ip);
                            if (ImGui.Selectable(ip, isSelected))
                            {
                                _selectedServer = ip;
                                if (_selectedTcpSlot >= 0 && _selectedTcpSlot < 5)
                                {
                                    _settings.ImGui.TcpClientIPs[_selectedTcpSlot] = ip;
                                    _tcpHost = ip;
                                }
                            }
                            if (isSelected) ImGui.SetItemDefaultFocus();
                        }
                        ImGui.EndCombo();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Remove selected", new Vector2(120, 30)) && !string.IsNullOrEmpty(_selectedServer))
                    {
                        _settings.ImGui.SavedServerIPs.Remove(_selectedServer);
                        _selectedServer = null;
                    }
                }

                // ---- Выбор порта и кнопка сканирования ----
                ImGui.Text("Port:");
                ImGui.SameLine();
                int portIndex = Array.IndexOf(_tcpPorts, _settings.ImGui.TcpPort.Value);
                if (portIndex < 0) portIndex = 0;
                if (ImGui.Combo("Port", ref portIndex, _tcpPorts.Select(p => p.ToString()).ToArray(), _tcpPorts.Length))
                    _settings.ImGui.TcpPort.Value = _tcpPorts[portIndex];

                ImGui.SameLine();
                if (ImGui.Button("Scan Servers", new Vector2(120, 30)))
                {
                    _isScanning = true;
                    _foundServers.Clear();
                    _selectedServer = null;
                    Task.Run(async () =>
                    {
                        try
                        {
                            var servers = await ServerDiscovery.ScanLocalNetworkAsync(_settings.ImGui.TcpPort.Value, 50);
                            // ★★★ БЕЗ ФИЛЬТРАЦИИ — сохраняем все найденные IP ★★★
                            _foundServers = servers;
                            _isScanning = false;
                        }
                        catch (Exception ex)
                        {
                            _log.Error($"Scan error: {ex.Message}");
                            _isScanning = false;
                        }
                    });
                }
                if (_isScanning)
                {
                    ImGui.TextColored(new Vector4(1, 1, 0, 1), "Scanning...");
                }
                else if (_foundServers.Count > 0)
                {
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), $"Found {_foundServers.Count} server(s)");
                }
                else
                {
                    ImGui.TextColored(new Vector4(1, 1, 0, 1), "No servers found");
                }

                // ---- Таблица слотов с выпадающим списком вместо кнопки Scan ----
                if (ImGui.BeginTable("TcpSlots", 5, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 40);
                    ImGui.TableSetupColumn("IP", ImGuiTableColumnFlags.WidthStretch);
                    // ★★★ Ширина колонки Select from scan увеличена до 160 ★★★
                    ImGui.TableSetupColumn("Select from scan", ImGuiTableColumnFlags.WidthFixed, 160);
                    ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 60);
                    ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 180);
                    ImGui.TableHeadersRow();

                    for (int i = 0; i < 5; i++)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text($"{i + 1}");

                        ImGui.TableSetColumnIndex(1);
                        string ip = _settings.ImGui.TcpClientIPs[i] ?? "";
                        if (ImGui.InputText($"##ip_{i}", ref ip, 64))
                            _settings.ImGui.TcpClientIPs[i] = ip;

                        ImGui.TableSetColumnIndex(2);
                        // Выпадающий список с найденными серверами (без фильтрации дублей)
                        if (_foundServers.Count > 0)
                        {
                            string currentIP = _settings.ImGui.TcpClientIPs[i] ?? "";
                            string preview = string.IsNullOrEmpty(currentIP) ? "Select IP" : currentIP;
                            if (ImGui.BeginCombo($"##select_{i}", preview))
                            {
                                foreach (var server in _foundServers)
                                {
                                    bool isSelected = (server == currentIP);
                                    if (ImGui.Selectable(server, isSelected))
                                    {
                                        // ★★★ Без проверки дублей – просто вставляем ★★★
                                        _settings.ImGui.TcpClientIPs[i] = server;
                                    }
                                    if (isSelected) ImGui.SetItemDefaultFocus();
                                }
                                ImGui.EndCombo();
                            }
                        }
                        else
                        {
                            ImGui.Text("Scan first");
                        }

                        ImGui.TableSetColumnIndex(3);
                        bool connected = _core.IsTcpConnected(i);
                        ImGui.TextColored(connected ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0.3f, 0.3f, 1),
                            connected ? "Online" : "Offline");

                        ImGui.TableSetColumnIndex(4);
                        if (!connected)
                        {
                            if (ImGui.Button($"Connect##{i}", new Vector2(70, 20)))
                            {
                                _ = _core.ConnectTcpAsync(i, _settings.ImGui.TcpClientIPs[i], _settings.ImGui.TcpPort.Value);
                            }
                        }
                        else
                        {
                            if (ImGui.Button($"Disconnect##{i}", new Vector2(70, 20)))
                                _core.DisconnectTcp(i);
                            ImGui.SameLine();
                            if (ImGui.Button($"Status##{i}", new Vector2(50, 20)))
                            {
                                _ = _core.SendTcpCommandAsync(i, "status");
                            }
                            ImGui.SameLine();
                            if (ImGui.Button($"Start##{i}", new Vector2(45, 20)))
                                _ = _core.SendTcpCommandAsync(i, "start");
                            ImGui.SameLine();
                            if (ImGui.Button($"Stop##{i}", new Vector2(45, 20)))
                                _ = _core.SendTcpCommandAsync(i, "stop");
                        }
                    }
                    ImGui.EndTable();
                }

                // ---- Групповые действия ----
                ImGui.Separator();
                if (ImGui.Button("Connect All", new Vector2(100, 30)))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        string ip = _settings.ImGui.TcpClientIPs[i];
                        if (!string.IsNullOrEmpty(ip))
                            _ = _core.ConnectTcpAsync(i, ip, _settings.ImGui.TcpPort.Value);
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Disconnect All", new Vector2(100, 30)))
                {
                    for (int i = 0; i < 5; i++)
                        _core.DisconnectTcp(i);
                }
                ImGui.SameLine();
                if (ImGui.Button("Start All", new Vector2(80, 30)))
                {
                    _ = _core.BroadcastTcpCommandAsync("start");
                }
                ImGui.SameLine();
                if (ImGui.Button("Stop All", new Vector2(80, 30)))
                {
                    _ = _core.BroadcastTcpCommandAsync("stop");
                }
                ImGui.SameLine();
                if (ImGui.Button("Status All", new Vector2(90, 30)))
                {
                    _ = _core.BroadcastTcpCommandAsync("status");
                }

                // ---- Последний ответ (общий) ----
                string lastResponse = _core.GetLastTcpResponse();
                if (!string.IsNullOrEmpty(lastResponse))
                {
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Last response:");
                    ImGui.TextWrapped(lastResponse);
                }

                // ---- Кнопка добавления IP в сохранённые (для текущего слота) ----
                if (_selectedTcpSlot >= 0 && _selectedTcpSlot < 5)
                {
                    string currentIP = _settings.ImGui.TcpClientIPs[_selectedTcpSlot];
                    if (!string.IsNullOrEmpty(currentIP) && !_settings.ImGui.SavedServerIPs.Contains(currentIP))
                    {
                        if (ImGui.Button($"Add slot {_selectedTcpSlot + 1} IP to saved", new Vector2(180, 30)))
                        {
                            if (_settings.ImGui.SavedServerIPs.Count >= 10)
                                _settings.ImGui.SavedServerIPs.RemoveAt(0);
                            _settings.ImGui.SavedServerIPs.Add(currentIP);
                        }
                    }
                }

                ImGui.Unindent();
            }
        }

        private void DrawPathfindingTab()
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Pathfinding Settings");
            ImGui.Separator();

            float stopDist = _settings.ImGui.StopDistance.Value;
            if (ImGui.SliderFloat("Stop Distance", ref stopDist, 10, 200, "%.0f"))
                _settings.ImGui.StopDistance.Value = stopDist;

            float tolerance = _settings.ImGui.StopDistanceTolerance.Value;
            if (ImGui.SliderFloat("Stop Tolerance", ref tolerance, 0, 50, "%.0f"))
                _settings.ImGui.StopDistanceTolerance.Value = tolerance;

            float maxLook = _settings.ImGui.MaxLookAheadPixels.Value;
            if (ImGui.SliderFloat("Max Look Ahead", ref maxLook, 100, 600, "%.0f"))
                _settings.ImGui.MaxLookAheadPixels.Value = maxLook;

            float minLook = _settings.ImGui.MinLookAheadPixels.Value;
            if (ImGui.SliderFloat("Min Look Ahead", ref minLook, 30, 200, "%.0f"))
                _settings.ImGui.MinLookAheadPixels.Value = minLook;

            float maxGrid = _settings.ImGui.MaxGridDistance.Value;
            if (ImGui.SliderFloat("Max Grid Distance", ref maxGrid, 50, 500, "%.0f"))
                _settings.ImGui.MaxGridDistance.Value = maxGrid;

            float minGrid = _settings.ImGui.MinGridDistance.Value;
            if (ImGui.SliderFloat("Min Grid Distance", ref minGrid, 5, 50, "%.0f"))
                _settings.ImGui.MinGridDistance.Value = minGrid;

            int timeout = _settings.ImGui.PathBuildTimeoutMs.Value;
            if (ImGui.SliderInt("Path Build Timeout (ms)", ref timeout, 500, 5000))
                _settings.ImGui.PathBuildTimeoutMs.Value = timeout;

            ImGui.Separator();
            bool clearBlockades = _settings.ImGui.ClearTriggerableBlockades.Value;
            if (ImGui.Checkbox("Clear Triggerable Blockades", ref clearBlockades))
                _settings.ImGui.ClearTriggerableBlockades.Value = clearBlockades;

            bool drawPath = _settings.ImGui.DrawPath.Value;
            if (ImGui.Checkbox("Draw Path", ref drawPath))
                _settings.ImGui.DrawPath.Value = drawPath;
        }

        private void DrawTransitionsTab()
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Transitions Settings");
            ImGui.Separator();

            bool usePortals = _settings.ImGui.UsePortals.Value;
            if (ImGui.Checkbox("Use Portals", ref usePortals))
                _settings.ImGui.UsePortals.Value = usePortals;

            bool drawTransitions = _settings.ImGui.DrawTransitions.Value;
            if (ImGui.Checkbox("Draw Transitions", ref drawTransitions))
                _settings.ImGui.DrawTransitions.Value = drawTransitions;

            float cooldown = _settings.ImGui.TransitionCooldownSeconds.Value;
            if (ImGui.SliderFloat("Transition Cooldown (s)", ref cooldown, 1, 10, "%.1f"))
                _settings.ImGui.TransitionCooldownSeconds.Value = cooldown;

            int offset = _settings.ImGui.PortalClickOffset.Value;
            if (ImGui.SliderInt("Portal Click Offset", ref offset, -50, 50))
                _settings.ImGui.PortalClickOffset.Value = offset;
        }

        private void DrawSkillsTab()
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Skills");
            ImGui.Separator();

            bool debugSkills = _settings.ImGui.DebugSkills.Value;
            if (ImGui.Checkbox("Debug Skills (log active buffs)", ref debugSkills))
                _settings.ImGui.DebugSkills.Value = debugSkills;

            bool debugSkillBar = _settings.ImGui.DebugSkillBar.Value;
            if (ImGui.Checkbox("Debug Skill Bar (show skills in slots)", ref debugSkillBar))
                _settings.ImGui.DebugSkillBar.Value = debugSkillBar;

            ImGui.Separator();

            var skills = _skillService.GetSkills();
            if (skills.Count == 0)
            {
                ImGui.Text("No skills found on skill bar.");
                return;
            }

            foreach (var skill in skills)
            {
                ImGui.PushID(skill.SlotIndex);

                if (!_settings.ImGui.SkillSettings.TryGetValue(skill.SlotIndex, out var skillConfig))
                {
                    skillConfig = new SkillSettings();
                    _settings.ImGui.SkillSettings[skill.SlotIndex] = skillConfig;
                }

                string keyDisplay = skill.Key switch
                {
                    Keys.LButton => "LMB",
                    Keys.RButton => "RMB",
                    Keys.MButton => "MMB",
                    Keys.None => "None",
                    _ => skill.Key.ToString()
                };

                string status = "";
                if (skillConfig.Enabled) status += "[Enabled] ";
                if (skillConfig.Condition == UseCondition.BuffMissing) status += "[Buff] ";
                if (skillConfig.Condition == UseCondition.NearbyEnemies) status += $"[{skillConfig.NearbyEnemyThreshold}+ enemies] ";
                if (skillConfig.Condition == UseCondition.ValourThreshold) status += $"[Valour ≥ {skillConfig.ValourThresholdValue}] ";
                status += $"(Target: {skillConfig.Target})";

                if (ImGui.CollapsingHeader($"{skill.Name} (Slot {skill.SlotIndex + 1}) Key: {keyDisplay}  {status}", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    bool enabled = skillConfig.Enabled;
                    if (ImGui.Checkbox("Enabled", ref enabled))
                        skillConfig.Enabled = enabled;

                    int targetIndex = (int)skillConfig.Target;
                    string[] targetNames = Enum.GetNames(typeof(SkillTarget));
                    if (ImGui.Combo("Target", ref targetIndex, targetNames, targetNames.Length))
                        skillConfig.Target = (SkillTarget)targetIndex;

                    int conditionIndex = (int)skillConfig.Condition;
                    string[] conditionNames = Enum.GetNames(typeof(UseCondition));
                    if (ImGui.Combo("Use Condition", ref conditionIndex, conditionNames, conditionNames.Length))
                        skillConfig.Condition = (UseCondition)conditionIndex;

                    // ★★★ Радиус поиска врагов (всегда доступен) ★★★
                    int radius = skillConfig.EnemySearchRadius;
                    if (ImGui.SliderInt("Enemy Search Radius (grid)", ref radius, 10, 200))
                        skillConfig.EnemySearchRadius = radius;
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Radius for enemy targeting and nearby count");

                    if (skillConfig.Condition == UseCondition.NearbyEnemies)
                    {
                        int threshold = skillConfig.NearbyEnemyThreshold;
                        if (ImGui.SliderInt("Enemy Count Threshold", ref threshold, 1, 20))
                            skillConfig.NearbyEnemyThreshold = threshold;
                    }

                    if (skillConfig.Condition == UseCondition.ValourThreshold)
                    {
                        bool isBanner = skill.Name.Contains("Banner", StringComparison.OrdinalIgnoreCase);
                        if (isBanner)
                        {
                            int valourThreshold = skillConfig.ValourThresholdValue;
                            if (ImGui.SliderInt("Valour Threshold", ref valourThreshold, 0, 105))
                                skillConfig.ValourThresholdValue = valourThreshold;
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Use when Valour stacks reach this value");
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(1, 1, 0, 1), "Valour condition only works for Banner skills.");
                        }
                    }

                    ImGui.Separator();
                }

                ImGui.PopID();
            }
        }

        private void DrawUltimatumTab()
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Ultimatum Settings");
            ImGui.Separator();

            bool enableUltimatum = _settings.ImGui.EnableUltimatum.Value;
            if (ImGui.Checkbox("Enable Ultimatum", ref enableUltimatum))
                _settings.ImGui.EnableUltimatum.Value = enableUltimatum;

            bool debugUltimatum = _settings.ImGui.DebugUltimatum.Value;
            if (ImGui.Checkbox("Debug Ultimatum", ref debugUltimatum))
                _settings.ImGui.DebugUltimatum.Value = debugUltimatum;

            ImGui.Separator();

            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Ultimatum Status:");

            if (_ultimatumService != null)
            {
                bool isOpen = _ultimatumService.IsPanelOpen;
                ImGui.Text($"Panel Open: {isOpen}");
                ImGui.Text($"Choice Made This Round: {_ultimatumService.ChoiceMadeThisRound}");
            }
            else
            {
                ImGui.Text("Ultimatum service not available");
            }
        }

        public void DrawStatusWindow()
        {
            if (!_settings.ImGui.ShowStatusWindow.Value) return;

            var posX = _settings.ImGui.StatusWindowPosX.Value;
            var posY = _settings.ImGui.StatusWindowPosY.Value;
            if (posX > 0 && posY > 0)
                _statusWindowPos = new Vector2(posX, posY);

            ImGui.SetNextWindowPos(_statusWindowPos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.Always);

            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
                        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings |
                        ImGuiWindowFlags.AlwaysAutoResize;
            if (_settings.ImGui.LockStatusWindow.Value)
                flags |= ImGuiWindowFlags.NoInputs;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5f);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.12f, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.2f, 0.2f, 0.25f, 1.0f));

            if (ImGui.Begin("StatusWindow", flags))
            {
                var state = _core.CurrentState;
                string stateText = state.ToString();
                var stateColor = state == FollowerState.Stopped ? new Vector4(1, 0.3f, 0.3f, 1) : new Vector4(0.3f, 1, 0.3f, 1);
                ImGui.TextColored(stateColor, $"State: {stateText}");

                string leaderName = _settings.ImGui.LeaderName.Value;
                ImGui.Text($"Leader: {leaderName}");

                bool inParty = _partyService.IsLeaderInParty(leaderName);
                ImGui.Text($"In Party: {(inParty ? "Yes" : "No")}");

                ImGui.Text($"Mode: {_settings.ImGui.BotMode.Value}");

                if (_settings.ImGui.BotMode.Value == "TCPClient")
                {
                    ImGui.Text($"TCP: {(_core.IsTcpConnected(0) ? "Connected" : "Disconnected")}");
                }

                if (inParty)
                {
                    var leaderPos = _partyService.GetPlayerGridPosition(leaderName);
                    if (leaderPos.HasValue)
                    {
                        ImGui.Text($"Pos: ({leaderPos.Value.X}, {leaderPos.Value.Y})");
                        var player = _gameContext.Player;
                        if (player != null)
                        {
                            var playerPos = player.GetComponent<ExileCore.PoEMemory.Components.Positioned>();
                            if (playerPos != null)
                            {
                                var currentGrid = new GameOffsets.Native.Vector2i((int)playerPos.GridPosNum.X, (int)playerPos.GridPosNum.Y);
                                float dist = _core.Distance(currentGrid, leaderPos.Value);
                                ImGui.Text($"Dist: {dist:F0} grid");
                            }
                        }
                    }
                    else
                        ImGui.Text("Leader: Not found");
                }

                ImGui.Text($"Cooldown: {(_core.CooldownRemaining > 0 ? $"{_core.CooldownRemaining:F1}s" : "None")}");

                if (!_settings.ImGui.LockStatusWindow.Value)
                {
                    if (ImGui.IsWindowHovered() && ImGui.IsMouseDown(0))
                    {
                        if (!_isDraggingStatusWindow)
                        {
                            _isDraggingStatusWindow = true;
                            _dragStartMouse = ImGui.GetMousePos();
                            _dragStartPos = _statusWindowPos;
                        }
                    }
                    if (_isDraggingStatusWindow && ImGui.IsMouseDragging(0))
                    {
                        var mouseDelta = ImGui.GetMousePos() - _dragStartMouse;
                        _statusWindowPos = _dragStartPos + mouseDelta;
                        _settings.ImGui.StatusWindowPosX.Value = (int)_statusWindowPos.X;
                        _settings.ImGui.StatusWindowPosY.Value = (int)_statusWindowPos.Y;
                    }
                    if (_isDraggingStatusWindow && !ImGui.IsMouseDown(0))
                        _isDraggingStatusWindow = false;
                }

                ImGui.End();
            }

            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar();
        }
    }
}