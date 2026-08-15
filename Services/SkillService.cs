using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Components;
using FollowBotV2.Core;

namespace FollowBotV2.Services
{
    public class SkillService : ISkillService
    {
        private readonly IGameContext _gameContext;
        private readonly ILogService _log;
        private readonly Keys[] _slotKeys = new Keys[8];
        private bool _keybindsLoaded;

        public SkillService(IGameContext gameContext, ILogService log)
        {
            _gameContext = gameContext;
            _log = log;
            RefreshKeybindings();
        }

        public void RefreshKeybindings()
        {
            try
            {
                var shortcuts = _gameContext.IngameState?.ShortcutSettings?.Shortcuts;
                if (shortcuts == null || shortcuts.Count < 15)
                {
                    _keybindsLoaded = false;
                    SetDefaultKeys();
                    return;
                }

                for (int bar = 0; bar < 8; bar++)
                {
                    var shortcut = shortcuts[bar + 7];
                    var consoleKey = shortcut.MainKey;
                    _slotKeys[bar] = ConsoleKeyToKeys(consoleKey);
                }

                _keybindsLoaded = true;
                _log.Debug("Keybindings refreshed.");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to read keybindings: {ex.Message}");
                _keybindsLoaded = false;
                SetDefaultKeys();
            }
        }

        private void SetDefaultKeys()
        {
            for (int i = 0; i < 8; i++)
                _slotKeys[i] = DefaultKeyForSlot(i);
        }

        private static Keys DefaultKeyForSlot(int barPosition) => barPosition switch
        {
            0 => Keys.LButton,
            1 => Keys.RButton,
            2 => Keys.MButton,
            3 => Keys.Q,
            4 => Keys.W,
            5 => Keys.E,
            6 => Keys.R,
            7 => Keys.T,
            _ => Keys.None
        };

        private static Keys ConsoleKeyToKeys(ConsoleKey consoleKey)
        {
            var intVal = (int)consoleKey;

            if (intVal >= 48 && intVal <= 57) return (Keys)intVal;
            if (intVal >= 65 && intVal <= 90) return (Keys)intVal;
            if (intVal >= 112 && intVal <= 123) return (Keys)intVal;

            return consoleKey switch
            {
                ConsoleKey.Spacebar => Keys.Space,
                ConsoleKey.Tab => Keys.Tab,
                ConsoleKey.Enter => Keys.Enter,
                ConsoleKey.Escape => Keys.Escape,
                ConsoleKey.Insert => Keys.Insert,
                ConsoleKey.Delete => Keys.Delete,
                ConsoleKey.Home => Keys.Home,
                ConsoleKey.End => Keys.End,
                ConsoleKey.PageUp => Keys.PageUp,
                ConsoleKey.PageDown => Keys.PageDown,
                ConsoleKey.UpArrow => Keys.Up,
                ConsoleKey.DownArrow => Keys.Down,
                ConsoleKey.LeftArrow => Keys.Left,
                ConsoleKey.RightArrow => Keys.Right,
                ConsoleKey.Backspace => Keys.Back,
                ConsoleKey.OemComma => Keys.Oemcomma,
                ConsoleKey.OemPeriod => Keys.OemPeriod,
                ConsoleKey.OemMinus => Keys.OemMinus,
                ConsoleKey.OemPlus => Keys.Oemplus,
                _ => (Keys)intVal
            };
        }

        public Keys GetKeyForSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotKeys.Length)
                return Keys.None;

            if (_keybindsLoaded)
                return _slotKeys[slotIndex];
            return DefaultKeyForSlot(slotIndex);
        }

        public IReadOnlyList<SkillInfo> GetSkills()
        {
            var result = new List<SkillInfo>();

            try
            {
                var skillBar = _gameContext.IngameState?.IngameUi?.SkillBar;
                if (skillBar == null)
                    return result;

                var skills = skillBar.Skills;
                if (skills == null || skills.Count == 0)
                    return result;

                for (int i = 0; i < skills.Count; i++)
                {
                    var skillElement = skills[i];
                    if (skillElement == null)
                        continue;

                    var actorSkill = skillElement.Skill;
                    if (actorSkill == null)
                        continue;

                    string name = actorSkill.Name;
                    if (string.IsNullOrEmpty(name))
                        continue;

                    result.Add(new SkillInfo
                    {
                        Name = name,
                        InternalName = actorSkill.InternalName ?? name,
                        IconPath = skillElement.SkillIconPath,
                        SlotIndex = i,
                        ActorSkill = actorSkill,
                        Key = GetKeyForSlot(i)
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error getting skills: {ex.Message}");
            }

            return result;
        }

        public void LogCurrentSkills()
        {
            try
            {
                var skills = GetSkills();
                if (skills.Count == 0)
                {
                    _log.Info("[SkillBar] No skills on skill bar.");
                    return;
                }

                _log.Info($"[SkillBar] === {skills.Count} skills on skill bar ===");
                foreach (var skill in skills)
                {
                    string keyDisplay = skill.Key switch
                    {
                        Keys.LButton => "LMB",
                        Keys.RButton => "RMB",
                        Keys.MButton => "MMB",
                        Keys.None => "None",
                        _ => skill.Key.ToString()
                    };
                    _log.Info($"[SkillBar] Slot {skill.SlotIndex + 1}: {skill.Name} (Key: {keyDisplay}) Internal: {skill.InternalName}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error logging skill bar: {ex.Message}");
            }
        }
    }
}