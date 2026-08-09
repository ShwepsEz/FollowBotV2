using System;
using GameOffsets.Native;

namespace FollowBotV2.Services
{
    public interface IPartyService
    {
        /// <summary>Позиция игрока на сетке (если найден)</summary>
        Vector2i? GetPlayerGridPosition(string playerName);

        /// <summary>Находится ли игрок в той же зоне, что и мы</summary>
        bool IsPlayerInSameZone(string playerName);

        /// <summary>Состоит ли игрок в нашей пати</summary>
        bool IsLeaderInParty(string leaderName);

        /// <summary>Получить имя зоны, где находится игрок (из панели пати)</summary>
        string GetPlayerZoneName(string playerName);
    }
}