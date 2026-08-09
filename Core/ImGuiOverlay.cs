using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Forms;
using ExileCore;
using FollowBotV2.Config;
using FollowBotV2.Services;
using ImGuiNET;

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

        private bool _isVisible = false;
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "General", "Pathfinding", "Transitions", "Skills" };

        public ImGuiOverlay(FollowerSettings settings, ILogService log, IPartyService partyService,
                            IGameContext gameContext, FollowerCore core, ISkillService skillService)
        {
            _settings = settings;
            _log = log;
            _partyService = partyService;
            _gameContext = gameContext;
            _core = core;
            _skillService = skillService;
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
            }
        }

        // ============================================================
        // Вкладка "General"
        // ============================================================
        private void DrawGeneralTab()
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "General Settings");
            ImGui.Separator();

            ImGui.Text("Leader Name:");
            ImGui.SameLine();
            var leaderName = _settings.LeaderName.Value;
            if (ImGui.InputText("##LeaderName", ref leaderName, 64))
            {
                _settings.LeaderName.Value = leaderName;
            }

            ImGui.Text("Follow Key:");
            ImGui.SameLine();
            ImGui.Text(_settings.FollowKey.Value.ToString());

            ImGui.Separator();

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

            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Status Window");
            bool showStatus = _settings.ShowStatusWindow.Value;
            if (ImGui.Checkbox("Show Status Window", ref showStatus))
                _settings.ShowStatusWindow.Value = showStatus;
            bool lockStatus = _settings.LockStatusWindow.Value;
            if (ImGui.Checkbox("Lock Status Window (click-through)", ref lockStatus))
                _settings.LockStatusWindow.Value = lockStatus;
        }

        // ============================================================
        // Вкладка "Pathfinding"
        // ============================================================
        private void DrawPathfindingTab()
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Pathfinding Settings");
            ImGui.Separator();

            float stopDist = _settings.StopDistance.Value;
            if (ImGui.SliderFloat("Stop Distance", ref stopDist, 10, 200, "%.0f"))
                _settings.StopDistance.Value = stopDist;

            float tolerance = _settings.StopDistanceTolerance.Value;
            if (ImGui.SliderFloat("Stop Tolerance", ref tolerance, 0, 50, "%.0f"))
                _settings.StopDistanceTolerance.Value = tolerance;

            float maxLook = _settings.MaxLookAheadPixels.Value;
            if (ImGui.SliderFloat("Max Look Ahead", ref maxLook, 100, 600, "%.0f"))
                _settings.MaxLookAheadPixels.Value = maxLook;

            float minLook = _settings.MinLookAheadPixels.Value;
            if (ImGui.SliderFloat("Min Look Ahead", ref minLook, 30, 200, "%.0f"))
                _settings.MinLookAheadPixels.Value = minLook;

            float maxGrid = _settings.MaxGridDistance.Value;
            if (ImGui.SliderFloat("Max Grid Distance", ref maxGrid, 50, 500, "%.0f"))
                _settings.MaxGridDistance.Value = maxGrid;

            float minGrid = _settings.MinGridDistance.Value;
            if (ImGui.SliderFloat("Min Grid Distance", ref minGrid, 5, 50, "%.0f"))
                _settings.MinGridDistance.Value = minGrid;

            int timeout = _settings.PathBuildTimeoutMs.Value;
            if (ImGui.SliderInt("Path Build Timeout (ms)", ref timeout, 500, 5000))
                _settings.PathBuildTimeoutMs.Value = timeout;

            ImGui.Separator();
            bool clearBlockades = _settings.ClearTriggerableBlockades.Value;
            if (ImGui.Checkbox("Clear Triggerable Blockades", ref clearBlockades))
                _settings.ClearTriggerableBlockades.Value = clearBlockades;

            bool drawPath = _settings.DrawPath.Value;
            if (ImGui.Checkbox("Draw Path", ref drawPath))
                _settings.DrawPath.Value = drawPath;
        }

        // ============================================================
        // Вкладка "Transitions"
        // ============================================================
        private void DrawTransitionsTab()
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Transitions Settings");
            ImGui.Separator();

            bool usePortals = _settings.UsePortals.Value;
            if (ImGui.Checkbox("Use Portals", ref usePortals))
                _settings.UsePortals.Value = usePortals;

            bool drawTransitions = _settings.DrawTransitions.Value;
            if (ImGui.Checkbox("Draw Transitions", ref drawTransitions))
                _settings.DrawTransitions.Value = drawTransitions;

            float cooldown = _settings.TransitionCooldownSeconds.Value;
            if (ImGui.SliderFloat("Transition Cooldown (s)", ref cooldown, 1, 10, "%.1f"))
                _settings.TransitionCooldownSeconds.Value = cooldown;

            int offset = _settings.PortalClickOffset.Value;
            if (ImGui.SliderInt("Portal Click Offset", ref offset, -50, 50))
                _settings.PortalClickOffset.Value = offset;
        }

        // ============================================================
        // Вкладка "Skills"
        // ============================================================
        private void DrawSkillsTab()
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Skills");
            ImGui.Separator();

            bool debugSkills = _settings.DebugSkills.Value;
            if (ImGui.Checkbox("Debug Skills (log active buffs)", ref debugSkills))
            {
                _settings.DebugSkills.Value = debugSkills;
            }
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

                if (!_settings.SkillSettings.TryGetValue(skill.SlotIndex, out var skillConfig))
                {
                    skillConfig = new SkillSettings();
                    _settings.SkillSettings[skill.SlotIndex] = skillConfig;
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

                    if (skillConfig.Condition == UseCondition.NearbyEnemies)
                    {
                        int threshold = skillConfig.NearbyEnemyThreshold;
                        if (ImGui.SliderInt("Enemy Count Threshold", ref threshold, 1, 20))
                            skillConfig.NearbyEnemyThreshold = threshold;
                    }

                    // ★★★ НОВЫЙ БЛОК ★★★
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

        // ============================================================
        // Status Window
        // ============================================================
        public void DrawStatusWindow()
        {
            if (!_settings.ShowStatusWindow.Value) return;

            var posX = _settings.StatusWindowPosX.Value;
            var posY = _settings.StatusWindowPosY.Value;
            if (posX > 0 && posY > 0)
                _statusWindowPos = new Vector2(posX, posY);

            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
                        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;
            if (_settings.LockStatusWindow.Value)
                flags |= ImGuiWindowFlags.NoInputs;

            ImGui.SetNextWindowPos(_statusWindowPos, ImGuiCond.Always);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5f);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.12f, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.2f, 0.2f, 0.25f, 1.0f));

            if (ImGui.Begin("StatusWindow", flags))
            {
                var state = _core.CurrentState;
                string stateText = state.ToString();
                var stateColor = state == FollowerState.Stopped ? new Vector4(1, 0.3f, 0.3f, 1) : new Vector4(0.3f, 1, 0.3f, 1);
                ImGui.TextColored(stateColor, $"State: {stateText}");

                string leaderName = _settings.LeaderName.Value;
                ImGui.Text($"Leader: {leaderName}");

                bool inParty = _partyService.IsLeaderInParty(leaderName);
                ImGui.Text($"In Party: {(inParty ? "Yes" : "No")}");

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

                if (!_settings.LockStatusWindow.Value)
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
                        _settings.StatusWindowPosX.Value = (int)_statusWindowPos.X;
                        _settings.StatusWindowPosY.Value = (int)_statusWindowPos.Y;
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