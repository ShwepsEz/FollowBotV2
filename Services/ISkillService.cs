using System.Collections.Generic;
using FollowBotV2.Core;

namespace FollowBotV2.Services
{
    public interface ISkillService
    {
        IReadOnlyList<SkillInfo> GetSkills();
        void RefreshKeybindings();
        void LogCurrentSkills();
    }
}