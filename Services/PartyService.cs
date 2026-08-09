using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using GameOffsets.Native;
using System;
using System.Linq;
using ExileCore.Shared.Enums;

namespace FollowBotV2.Services
{
    public class PartyService : IPartyService
    {
        private readonly IGameContext _gameContext;
        private readonly ILogService _log;

        public PartyService(IGameContext gameContext, ILogService log)
        {
            _gameContext = gameContext;
            _log = log;
        }

        public Vector2i? GetPlayerGridPosition(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return null;

            // Очищаем имя от символов после #
            string cleanName = playerName.Contains('#')
                ? playerName.Substring(0, playerName.IndexOf('#'))
                : playerName;
            cleanName = cleanName.Trim();

            try
            {
                var players = _gameContext.GameController.Entities
                    .Where(e => e != null && e.IsValid)
                    .Where(e => e.Type == EntityType.Player)
                    .ToList();

                foreach (var entity in players)
                {
                    var playerComp = entity.GetComponent<Player>();
                    if (playerComp == null) continue;

                    string name = playerComp.PlayerName;
                    if (string.IsNullOrEmpty(name))
                    {
                        var render = entity.GetComponent<Render>();
                        if (render != null) name = render.Name;
                    }

                    if (string.IsNullOrEmpty(name)) continue;

                    string cleanEntityName = name.Contains('#')
                        ? name.Substring(0, name.IndexOf('#'))
                        : name;
                    cleanEntityName = cleanEntityName.Trim();

                    if (cleanEntityName.Equals(cleanName, StringComparison.OrdinalIgnoreCase))
                    {
                        var positioned = entity.GetComponent<Positioned>();
                        if (positioned != null)
                        {
                            var gridPos = new Vector2i((int)positioned.GridPosNum.X, (int)positioned.GridPosNum.Y);
                            _log.Debug($"Found leader '{cleanName}' at grid ({gridPos.X}, {gridPos.Y})");
                            return gridPos;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error getting player position: {ex.Message}");
            }

            _log.Debug($"Player '{cleanName}' not found in current zone");
            return null;
        }

        public bool IsPlayerInSameZone(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return false;

            // Если есть позиция — значит в той же зоне
            var pos = GetPlayerGridPosition(playerName);
            if (pos.HasValue) return true;

            // Альтернатива: проверить через PartyElement
            try
            {
                var partyElement = _gameContext.IngameState?.IngameUi?.PartyElement;
                if (partyElement == null) return false;

                var playerElements = partyElement.PlayerElements;
                if (playerElements == null || playerElements.Count == 0) return false;

                string cleanName = playerName.Contains('#')
                    ? playerName.Substring(0, playerName.IndexOf('#'))
                    : playerName;
                cleanName = cleanName.Trim();

                foreach (var player in playerElements)
                {
                    if (player == null || string.IsNullOrEmpty(player.PlayerName)) continue;

                    string cleanPartyName = player.PlayerName.Contains('#')
                        ? player.PlayerName.Substring(0, player.PlayerName.IndexOf('#'))
                        : player.PlayerName;
                    cleanPartyName = cleanPartyName.Trim();

                    if (cleanPartyName.Equals(cleanName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Если ZoneName пустой — значит в той же зоне
                        bool sameZone = string.IsNullOrEmpty(player.ZoneName);
                        _log.Debug($"Player '{cleanName}' zone check: {(sameZone ? "Same zone" : player.ZoneName)}");
                        return sameZone;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error checking zone via party panel: {ex.Message}");
            }

            return false;
        }

        public bool IsLeaderInParty(string leaderName)
        {
            if (string.IsNullOrEmpty(leaderName)) return false;

            try
            {
                var partyElement = _gameContext.IngameState?.IngameUi?.PartyElement;
                if (partyElement == null) return false;

                var playerElements = partyElement.PlayerElements;
                if (playerElements == null || playerElements.Count == 0) return false;

                string cleanLeaderName = leaderName.Contains('#')
                    ? leaderName.Substring(0, leaderName.IndexOf('#'))
                    : leaderName;
                cleanLeaderName = cleanLeaderName.Trim();

                foreach (var player in playerElements)
                {
                    if (player == null || string.IsNullOrEmpty(player.PlayerName)) continue;

                    string cleanPartyName = player.PlayerName.Contains('#')
                        ? player.PlayerName.Substring(0, player.PlayerName.IndexOf('#'))
                        : player.PlayerName;
                    cleanPartyName = cleanPartyName.Trim();

                    if (cleanPartyName.Equals(cleanLeaderName, StringComparison.OrdinalIgnoreCase))
                    {
                        _log.Debug($"Leader '{cleanLeaderName}' found in party");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error checking party membership: {ex.Message}");
            }

            return false;
        }

        public string GetPlayerZoneName(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return "";

            try
            {
                var partyElement = _gameContext.IngameState?.IngameUi?.PartyElement;
                if (partyElement == null) return "";

                var playerElements = partyElement.PlayerElements;
                if (playerElements == null || playerElements.Count == 0) return "";

                string cleanName = playerName.Contains('#')
                    ? playerName.Substring(0, playerName.IndexOf('#'))
                    : playerName;
                cleanName = cleanName.Trim();

                foreach (var player in playerElements)
                {
                    if (player == null || string.IsNullOrEmpty(player.PlayerName)) continue;

                    string cleanPartyName = player.PlayerName.Contains('#')
                        ? player.PlayerName.Substring(0, player.PlayerName.IndexOf('#'))
                        : player.PlayerName;
                    cleanPartyName = cleanPartyName.Trim();

                    if (cleanPartyName.Equals(cleanName, StringComparison.OrdinalIgnoreCase))
                    {
                        return player.ZoneName ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error getting player zone: {ex.Message}");
            }

            return "";
        }
    }
}