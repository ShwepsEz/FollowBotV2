using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FollowBotV2.Config
{
    public enum SkillTarget
    {
        Self,
        Enemy,
        Mouse,
        Leader
    }

    public enum UseCondition
    {
        Always,
        BuffMissing,
        NearbyEnemies,
        ValourThreshold
    }

    public class SkillSettings
    {
        public bool Enabled { get; set; } = false;
        public SkillTarget Target { get; set; } = SkillTarget.Self;
        public UseCondition Condition { get; set; } = UseCondition.Always;
        public int NearbyEnemyThreshold { get; set; } = 3;
        public int EnemySearchRadius { get; set; } = 50;
        public int ValourThresholdValue { get; set; } = 105;
    }

    public class ImGuiOnlySettings
    {
        public ToggleNode DebugSkillBar { get; set; } = new ToggleNode(false);
        public ToggleNode DrawTransitions { get; set; } = new ToggleNode(false);
        public RangeNode<float> MaxLookAheadPixels { get; set; } = new RangeNode<float>(300, 100, 600);
        public RangeNode<float> MinLookAheadPixels { get; set; } = new RangeNode<float>(30, 30, 200);
        public RangeNode<float> MaxGridDistance { get; set; } = new RangeNode<float>(200, 50, 500);
        public RangeNode<float> MinGridDistance { get; set; } = new RangeNode<float>(20, 5, 50);
        public ToggleNode UsePortals { get; set; } = new ToggleNode(true);
        public RangeNode<int> PortalClickOffset { get; set; } = new RangeNode<int>(0, -50, 50);
        public ToggleNode DrawPath { get; set; } = new ToggleNode(false);
        public ToggleNode ClearTriggerableBlockades { get; set; } = new ToggleNode(true);
        public HotkeyNode MovementKey { get; set; } = new HotkeyNode(Keys.T);
        public RangeNode<float> StopDistance { get; set; } = new RangeNode<float>(20, 10, 200);
        public TextNode LeaderName { get; set; } = new TextNode("");
        public HotkeyNode FollowKey { get; set; } = new HotkeyNode(Keys.F3);
        public RangeNode<int> PathBuildTimeoutMs { get; set; } = new RangeNode<int>(1000, 500, 5000);
        public RangeNode<float> TransitionCooldownSeconds { get; set; } = new RangeNode<float>(3, 1, 10);
        public RangeNode<float> StopDistanceTolerance { get; set; } = new RangeNode<float>(15, 0, 50);
        public RangeNode<int> LeaderCheckIntervalMs { get; set; } = new RangeNode<int>(200, 100, 500);
        public ToggleNode ShowStatusWindow { get; set; } = new ToggleNode(false);
        public ToggleNode LockStatusWindow { get; set; } = new ToggleNode(false);
        public RangeNode<int> StatusWindowPosX { get; set; } = new RangeNode<int>(100, 0, 1920);
        public RangeNode<int> StatusWindowPosY { get; set; } = new RangeNode<int>(100, 0, 1080);
        public Dictionary<int, SkillSettings> SkillSettings { get; set; } = new Dictionary<int, SkillSettings>();
        public ToggleNode DebugSkills { get; set; } = new ToggleNode(false);
        public ToggleNode EnableUltimatum { get; set; } = new ToggleNode(true);
        public ToggleNode DebugUltimatum { get; set; } = new ToggleNode(false);
        public ToggleNode FollowInHideout { get; set; } = new ToggleNode(true);
        public ListNode BotMode { get; set; } = new ListNode
        {
            Values = new List<string> { "Follow", "UltimatumFarm" },
            Value = "Follow"
        };
    }

    public class FollowerSettings : ISettings
    {
        public FollowerSettings()
        {
            Enable = new ToggleNode(true);
            ImGui = new ImGuiOnlySettings();
        }

        [Menu("Enable")]
        public ToggleNode Enable { get; set; }

        [Menu(" ", "Press F7 to open advanced settings")]
        public string InfoMessage => "Open ImGui settings with F7";

        public ImGuiOnlySettings ImGui { get; set; }
    }
}