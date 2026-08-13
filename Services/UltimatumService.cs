using System;
using System.Collections.Generic;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory;
using SharpDX;
using FollowBotV2.Config;
using FollowBotV2.Helpers;

namespace FollowBotV2.Services
{
    public class UltimatumService : IUltimatumService
    {
        private readonly IGameContext _gameContext;
        private readonly ILogService _log;
        private readonly FollowerSettings _settings;
        private readonly IMouseService _mouseService;

        private bool _choiceMadeThisRound = false;
        private int _lastChoicesCount = -1;
        private bool _wasPanelVisible = false;

        public bool ChoiceMadeThisRound => _choiceMadeThisRound;

        public UltimatumService(IGameContext gameContext, ILogService log, FollowerSettings settings, IMouseService mouseService)
        {
            _gameContext = gameContext;
            _log = log;
            _settings = settings;
            _mouseService = mouseService;
        }

        public bool IsPanelOpen
        {
            get
            {
                if (!_settings.ImGui.EnableUltimatum.Value)
                    return false;

                try
                {
                    var panel = GetUltimatumPanel();
                    if (panel == null) return false;
                    bool visible = panel.IsVisible;
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Debug($"Ultimatum panel visible: {visible}");
                    return visible;
                }
                catch (Exception ex)
                {
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Error($"Ultimatum IsPanelOpen error: {ex.Message}");
                    return false;
                }
            }
        }

        public void CheckAndHandle()
        {
            if (!_settings.ImGui.EnableUltimatum.Value)
                return;

            try
            {
                var panel = GetUltimatumPanel();
                if (panel == null)
                {
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Debug("Ultimatum: panel is null");
                    return;
                }

                bool isVisible = panel.IsVisible;

                // Отслеживание открытия панели (переход из невидимого в видимое)
                if (isVisible && !_wasPanelVisible)
                {
                    _choiceMadeThisRound = false;
                    _lastChoicesCount = -1;
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Debug("Ultimatum: panel just opened, resetting state");
                }

                _wasPanelVisible = isVisible;

                if (!isVisible)
                {
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Debug("Ultimatum: panel closed");
                    return;
                }

                var choices = panel.ChoicesElements;
                int currentChoicesCount = choices?.Count ?? 0;

                // Изменение количества выборов = новая волна
                if (currentChoicesCount != _lastChoicesCount && currentChoicesCount > 0)
                {
                    _choiceMadeThisRound = false;
                    _lastChoicesCount = currentChoicesCount;
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Debug($"Ultimatum: choices count changed to {currentChoicesCount}, resetting choiceMadeThisRound");
                }

                // Проверка: если все LockedVotes == 0, значит новая волна (лидер ещё не выбрал)
                if (_choiceMadeThisRound && choices != null && choices.Count > 0)
                {
                    bool allLockedZero = true;
                    foreach (var choice in choices)
                    {
                        if (choice.LockedVotes > 0)
                        {
                            allLockedZero = false;
                            break;
                        }
                    }
                    if (allLockedZero)
                    {
                        _choiceMadeThisRound = false;
                        if (_settings.ImGui.DebugUltimatum.Value)
                            _log.Debug("Ultimatum: all LockedVotes are 0, resetting choiceMadeThisRound (new wave)");
                    }
                }

                if (_settings.ImGui.DebugUltimatum.Value)
                    _log.Debug($"Ultimatum: panel visible, choiceMadeThisRound={_choiceMadeThisRound}, choices={currentChoicesCount}");

                if (_choiceMadeThisRound)
                    return;

                if (choices == null || choices.Count == 0)
                {
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Debug("Ultimatum: no choices found");
                    return;
                }

                UltimatumChoiceElement selectedChoice = null;
                int choiceIndex = 0;

                foreach (var choice in choices)
                {
                    try
                    {
                        int lockedVotes = choice.LockedVotes;
                        if (_settings.ImGui.DebugUltimatum.Value)
                            _log.Debug($"Choice {choiceIndex}: LockedVotes={lockedVotes}");

                        if (lockedVotes > 0)
                        {
                            selectedChoice = choice;
                            if (_settings.ImGui.DebugUltimatum.Value)
                                _log.Debug($"Found locked choice at index {choiceIndex}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_settings.ImGui.DebugUltimatum.Value)
                            _log.Error($"Error reading choice {choiceIndex}: {ex.Message}");
                    }
                    choiceIndex++;
                }

                if (selectedChoice == null)
                {
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Debug("Ultimatum: No choice with LockedVotes > 0 found, waiting...");
                    return;
                }

                // Проверяем, выбран ли уже этот вариант (чтобы не кликать дважды)
                bool isSelected = selectedChoice.IsSelectedChoice;

                if (!isSelected)
                {
                    ClickElement(selectedChoice);
                    System.Threading.Thread.Sleep(200);
                }
                else
                {
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Debug("Choice already selected, skipping click");
                }

                // Нажимаем Confirm
                var confirmButton = panel.ConfirmButton;
                if (confirmButton != null && confirmButton.IsVisible)
                {
                    ClickElement(confirmButton);
                    _choiceMadeThisRound = true;
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Info("Ultimatum: confirmed choice");
                }
                else
                {
                    if (_settings.ImGui.DebugUltimatum.Value)
                        _log.Warn("Ultimatum: Confirm button not visible");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Ultimatum error: {ex.Message}");
                if (_settings.ImGui.DebugUltimatum.Value)
                    _log.Debug($"Ultimatum exception stack: {ex.StackTrace}");
            }
        }

        private UltimatumPanel GetUltimatumPanel()
        {
            var ingameUi = _gameContext.IngameState?.IngameUi;
            return ingameUi?.UltimatumPanel;
        }

        private void ClickElement(Element element)
        {
            try
            {
                if (element == null) return;

                var rect = element.GetClientRect();
                if (rect == null) return;

                var centerX = rect.X + rect.Width / 2;
                var centerY = rect.Y + rect.Height / 2;

                var windowRect = _gameContext.GameController.Window.GetWindowRectangle();
                var targetScreen = new SharpDX.Vector2(centerX + windowRect.Location.X, centerY + windowRect.Location.Y);

                _mouseService.MoveCursorSmooth(new MouseVector2(targetScreen.X, targetScreen.Y), 5);
                System.Threading.Thread.Sleep(50);
                _mouseService.LeftClick();
            }
            catch (Exception ex)
            {
                _log.Error($"Error clicking UI element: {ex.Message}");
            }
        }
    }
}