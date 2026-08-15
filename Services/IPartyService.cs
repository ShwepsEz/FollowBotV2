using System;
using GameOffsets.Native;

namespace FollowBotV2.Services
{
    public interface IPartyService
    {
        Vector2i? GetPlayerGridPosition(string playerName);
        bool IsPlayerInSameZone(string playerName);
        bool IsLeaderInParty(string leaderName);
        string GetPlayerZoneName(string playerName);
    }
}