using System;
using System.Linq;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using FollowBotV2.Config;
using FollowBotV2.Core;
using FollowBotV2.Helpers;

namespace FollowBotV2.Services
{
    public class SkillUsageService : ISkillUsageService
    {
        private readonly IGameContext _gameContext;
        private readonly ILogService _log;
        private readonly ISkillService _skillService;
        private readonly FollowerSettings _settings;
        private readonly IInputService _inputService;
        private readonly IMouseService _mouseService;
        private readonly IPartyService _partyService;
        private readonly FollowerCore _core;

        private DateTime _lastDebugLog = DateTime.MinValue;
        private DateTime _lastSkillBarLog = DateTime.MinValue;
        private const int SKILLBAR_LOG_INTERVAL_MS = 1000;

        private DateTime _lastBannerUseTime = DateTime.MinValue;
        private const int BANNER_COOLDOWN_MS = 500;

        private DateTime _lastBuffSkillUseTime = DateTime.MinValue;
        private const int BUFF_SKILL_COOLDOWN_MS = 400;

        public SkillUsageService(IGameContext gameContext, ILogService log, ISkillService skillService,
                                 FollowerSettings settings, IInputService inputService,
                                 IMouseService mouseService, IPartyService partyService, FollowerCore core)
        {
            _gameContext = gameContext;
            _log = log;
            _skillService = skillService;
            _settings = settings;
            _inputService = inputService;
            _mouseService = mouseService;
            _partyService = partyService;
            _core = core;
        }

        private static bool IsKeyboardKey(Keys key)
        {
            return key != Keys.None &&
                   key != Keys.LButton &&
                   key != Keys.RButton &&
                   key != Keys.MButton;
        }

        public void Update()
        {
            if (_core.CurrentState == FollowerState.Stopped)
                return;

            var player = _gameContext.Player;
            if (player == null) return;

            if (_settings.ImGui.DebugSkills.Value)
            {
                if ((DateTime.Now - _lastDebugLog).TotalSeconds >= 1)
                {
                    _lastDebugLog = DateTime.Now;
                    try
                    {
                        var buffs = player.Buffs;
                        if (buffs != null && buffs.Count > 0)
                        {
                            _log.Info($"=== Active buffs ({buffs.Count}) ===");
                            foreach (var buff in buffs)
                                if (buff.Name != null)
                                    _log.Info($"  - {buff.Name}");
                        }
                        else
                            _log.Info("No active buffs found.");
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Error reading buffs: {ex.Message}");
                    }
                }
            }

            var skills = _skillService.GetSkills();
            if (skills.Count == 0) return;

            if (_settings.ImGui.DebugSkillBar.Value && (DateTime.Now - _lastSkillBarLog).TotalMilliseconds >= SKILLBAR_LOG_INTERVAL_MS)
            {
                _lastSkillBarLog = DateTime.Now;
                _skillService.LogCurrentSkills();
            }

            foreach (var skillInfo in skills)
            {
                if (!_settings.ImGui.SkillSettings.TryGetValue(skillInfo.SlotIndex, out var settings))
                    continue;

                if (!settings.Enabled)
                    continue;

                if (!IsKeyboardKey(skillInfo.Key))
                    continue;

                var actorSkill = skillInfo.ActorSkill;
                if (actorSkill == null) continue;

                if (!actorSkill.CanBeUsed)
                    continue;

                if (!CheckConditions(settings, skillInfo))
                    continue;

                if (IsBannerSkill(skillInfo.Name))
                {
                    if ((DateTime.Now - _lastBannerUseTime).TotalMilliseconds < BANNER_COOLDOWN_MS)
                        continue;
                }

                if (settings.Condition == UseCondition.BuffMissing)
                {
                    if ((DateTime.Now - _lastBuffSkillUseTime).TotalMilliseconds < BUFF_SKILL_COOLDOWN_MS)
                        continue;
                }

                var targetPos = GetTargetPosition(settings, skillInfo);
                if (!targetPos.HasValue && settings.Target != SkillTarget.Self)
                    continue;

                ApplySkill(skillInfo, targetPos ?? SharpDX.Vector2.Zero, settings.Target);

                if (IsBannerSkill(skillInfo.Name))
                    _lastBannerUseTime = DateTime.Now;

                if (settings.Condition == UseCondition.BuffMissing)
                    _lastBuffSkillUseTime = DateTime.Now;
            }
        }

        private bool CheckConditions(SkillSettings settings, SkillInfo skillInfo)
        {
            switch (settings.Condition)
            {
                case UseCondition.Always:
                    return true;

                case UseCondition.BuffMissing:
                    string buffName = skillInfo.ActorSkill?.InternalName ?? skillInfo.Name;
                    float timeLeft = GetBuffTimeLeft(_gameContext.Player, buffName);
                    return timeLeft <= 0.5f;

                case UseCondition.NearbyEnemies:
                    int enemyCount = GetNearbyEnemyCount(settings.EnemySearchRadius);
                    return enemyCount >= settings.NearbyEnemyThreshold;

                case UseCondition.ValourThreshold:
                    int currentValour = GetValourStacks();
                    if (currentValour < settings.ValourThresholdValue)
                        return false;

                    if (IsBannerSkill(skillInfo.Name))
                    {
                        bool hasBuff = HasBannerBuff(skillInfo.Name, skillInfo.InternalName);
                        if (hasBuff)
                            return false;
                    }
                    return true;

                default:
                    return false;
            }
        }

        private SharpDX.Vector2? GetTargetPosition(SkillSettings settings, SkillInfo skillInfo)
        {
            switch (settings.Target)
            {
                case SkillTarget.Self:
                    return SharpDX.Vector2.Zero;

                case SkillTarget.Enemy:
                    return GetNearestEnemyPosition(settings.EnemySearchRadius);

                case SkillTarget.Mouse:
                    var mousePos = _mouseService.GetCursorPosition();
                    return new SharpDX.Vector2(mousePos.X, mousePos.Y);

                case SkillTarget.Leader:
                    string leaderName = _settings.ImGui.LeaderName.Value;
                    if (string.IsNullOrEmpty(leaderName))
                        return null;
                    var pos = _partyService.GetPlayerGridPosition(leaderName);
                    if (pos.HasValue)
                        return new SharpDX.Vector2(pos.Value.X, pos.Value.Y);
                    return null;

                default:
                    return null;
            }
        }

        private void ApplySkill(SkillInfo skillInfo, SharpDX.Vector2 targetPos, SkillTarget target)
        {
            try
            {
                if (skillInfo.Key == Keys.None) return;

                if ((target == SkillTarget.Leader || target == SkillTarget.Enemy) && targetPos != SharpDX.Vector2.Zero)
                {
                    var worldPos = GridToWorld3D(targetPos);
                    var camera = _gameContext.GameController?.Game?.IngameState?.Camera;
                    if (camera != null)
                    {
                        var screenPos = camera.WorldToScreen(worldPos);
                        var windowRect = _gameContext.GameController.Window.GetWindowRectangle();
                        var finalPos = new SharpDX.Vector2(
                            screenPos.X + windowRect.Location.X,
                            screenPos.Y + windowRect.Location.Y
                        );
                        _mouseService.MoveCursorSmooth(new MouseVector2(finalPos.X, finalPos.Y), 10);
                        System.Threading.Thread.Sleep(50);
                    }
                }

                _inputService.KeyDown(skillInfo.Key);
                System.Threading.Thread.Sleep(50);
                _inputService.KeyUp(skillInfo.Key);
            }
            catch (Exception ex)
            {
                _log.Error($"Error using skill {skillInfo.Name}: {ex.Message}");
            }
        }

        private float GetBuffTimeLeft(Entity player, string buffName)
        {
            try
            {
                var buffs = player.Buffs;
                if (buffs == null) return 0f;
                string[] variants = { buffName, buffName.Replace("_", "") };
                foreach (var buff in buffs)
                {
                    if (buff.Name == null) continue;
                    foreach (var variant in variants)
                    {
                        if (buff.Name.Contains(variant, StringComparison.OrdinalIgnoreCase))
                        {
                            var time = 0f;
                            var propTimeLeft = buff.GetType().GetProperty("TimeLeft");
                            if (propTimeLeft != null && propTimeLeft.CanRead)
                                time = (float)propTimeLeft.GetValue(buff);
                            else
                            {
                                var propTimer = buff.GetType().GetProperty("Timer");
                                if (propTimer != null && propTimer.CanRead)
                                    time = (float)propTimer.GetValue(buff);
                            }
                            return time;
                        }
                    }
                }
            }
            catch { }
            return 0f;
        }

        private int GetNearbyEnemyCount(int radius)
        {
            try
            {
                var playerPos = _gameContext.Player?.GridPosNum;
                if (!playerPos.HasValue) return 0;
                var entities = _gameContext.GameController?.EntityListWrapper?.OnlyValidEntities;
                if (entities == null) return 0;
                int count = 0;
                foreach (var e in entities)
                {
                    if (e == null || !e.IsAlive) continue;
                    if (e.Type != EntityType.Monster) continue;
                    if (!e.IsHostile) continue;
                    var dist = SharpDX.Vector2.Distance(
                        new SharpDX.Vector2(e.GridPosNum.X, e.GridPosNum.Y),
                        new SharpDX.Vector2(playerPos.Value.X, playerPos.Value.Y)
                    );
                    if (dist <= radius)
                        count++;
                }
                return count;
            }
            catch { return 0; }
        }

        private SharpDX.Vector2? GetNearestEnemyPosition(int radius)
        {
            try
            {
                var playerPos = _gameContext.Player?.GridPosNum;
                if (!playerPos.HasValue) return null;
                var entities = _gameContext.GameController?.EntityListWrapper?.OnlyValidEntities;
                if (entities == null) return null;
                float minDist = float.MaxValue;
                SharpDX.Vector2? nearest = null;
                foreach (var e in entities)
                {
                    if (e == null || !e.IsAlive) continue;
                    if (e.Type != EntityType.Monster) continue;
                    if (!e.IsHostile) continue;
                    var ePos = new SharpDX.Vector2(e.GridPosNum.X, e.GridPosNum.Y);
                    var playerVec = new SharpDX.Vector2(playerPos.Value.X, playerPos.Value.Y);
                    var dist = SharpDX.Vector2.Distance(ePos, playerVec);
                    if (dist < minDist && dist <= radius)
                    {
                        minDist = dist;
                        nearest = ePos;
                    }
                }
                return nearest;
            }
            catch { return null; }
        }

        private bool IsBannerSkill(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return false;
            return skillName.Contains("Banner", StringComparison.OrdinalIgnoreCase);
        }

        private int GetValourStacks()
        {
            try
            {
                var player = _gameContext.Player;
                if (player == null) return 0;

                var buffsComponent = player.GetComponent<Buffs>();
                if (buffsComponent == null) return 0;

                var buffs = buffsComponent.ParseBuffs();
                if (buffs == null || buffs.Count == 0) return 0;

                foreach (var buff in buffs)
                {
                    if (buff == null) continue;

                    var buffName = buff.Name ?? "";
                    var displayName = buff.DisplayName ?? "";

                    if (buffName.Contains("Valour", StringComparison.OrdinalIgnoreCase) ||
                        displayName.Contains("Valour", StringComparison.OrdinalIgnoreCase))
                    {
                        int stacks = 0;

                        try
                        {
                            var chargeProp = buff.GetType().GetProperty("Charge");
                            if (chargeProp != null)
                            {
                                var chargeValue = chargeProp.GetValue(buff);
                                if (chargeValue != null)
                                    return Convert.ToInt32(chargeValue);
                            }
                        }
                        catch { }

                        try
                        {
                            var chargesProp = buff.GetType().GetProperty("Charges");
                            if (chargesProp != null)
                            {
                                var chargesValue = chargesProp.GetValue(buff);
                                if (chargesValue != null)
                                    return Convert.ToInt32(chargesValue);
                            }
                        }
                        catch { }

                        try
                        {
                            var stackProp = buff.GetType().GetProperty("StackCount");
                            if (stackProp != null)
                            {
                                var stackValue = stackProp.GetValue(buff);
                                if (stackValue != null)
                                    return Convert.ToInt32(stackValue);
                            }
                        }
                        catch { }

                        try
                        {
                            var valueProp = buff.GetType().GetProperty("Value");
                            if (valueProp != null)
                            {
                                var value = valueProp.GetValue(buff);
                                if (value != null)
                                    return Convert.ToInt32(value);
                            }
                        }
                        catch { }

                        try
                        {
                            var chargeField = buff.GetType().GetField("Charge");
                            if (chargeField != null)
                            {
                                var chargeValue = chargeField.GetValue(buff);
                                if (chargeValue != null)
                                    return Convert.ToInt32(chargeValue);
                            }
                        }
                        catch { }

                        return Math.Max(1, (int)buff.Timer);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"GetValourStacks error: {ex.Message}");
            }

            return 0;
        }

        private bool HasBannerBuff(string skillName, string internalName)
        {
            try
            {
                var player = _gameContext.Player;
                if (player == null) return false;

                var buffsComponent = player.GetComponent<Buffs>();
                if (buffsComponent == null) return false;

                var buffs = buffsComponent.ParseBuffs();
                if (buffs == null || buffs.Count == 0) return false;

                string[] searchPatterns = {
                    skillName,
                    internalName,
                    skillName.Replace("Banner", "").Trim(),
                    internalName.Replace("Banner", "").Trim()
                };
                searchPatterns = searchPatterns
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                foreach (var buff in buffs)
                {
                    if (buff == null) continue;
                    var buffName = buff.Name ?? "";
                    var displayName = buff.DisplayName ?? "";
                    foreach (var pattern in searchPatterns)
                    {
                        if (buffName.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                            displayName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _log.Error($"HasBannerBuff error: {ex.Message}");
                return false;
            }
        }

        private SharpDX.Vector3 GridToWorld3D(SharpDX.Vector2 gridPos)
        {
            var gc = _gameContext.GameController;
            if (gc == null) return SharpDX.Vector3.Zero;
            var numVec = new System.Numerics.Vector2(gridPos.X, gridPos.Y);
            var result = gc.IngameState.Data.ToWorldWithTerrainHeight(numVec);
            return new SharpDX.Vector3(result.X, result.Y, result.Z);
        }
    }
}