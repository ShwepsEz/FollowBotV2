using FollowBotV2.Helpers;

namespace FollowBotV2.Services
{
    public class MouseService : IMouseService
    {
        public void MoveCursorSmooth(MouseVector2 target, int steps = 10)
            => Mouse.MoveCursorSmooth(target, steps);

        public void LeftClick() => Mouse.LeftClick();
        public MouseVector2 GetCursorPosition() => Mouse.GetCursorPosition();
        public void SetCursorPosition(MouseVector2 pos) => Mouse.SetCursorPos(pos);
    }
}