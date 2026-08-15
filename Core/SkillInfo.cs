using ExileCore.PoEMemory.MemoryObjects;
using System.Windows.Forms;
using FollowBotV2.Core;
using System.Numerics;

namespace FollowBotV2.Core
{
    public class SkillInfo
    {
        public string Name { get; set; }
        public string InternalName { get; set; }
        public string IconPath { get; set; }
        public int SlotIndex { get; set; }
        public ActorSkill ActorSkill { get; set; }
        public Keys Key { get; set; }
    }
}