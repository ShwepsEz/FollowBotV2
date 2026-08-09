using FollowBotV2.Helpers;

namespace FollowBotV2.Services
{
    public interface IMouseService
    {
        void MoveCursorSmooth(MouseVector2 target, int steps = 10);
        void LeftClick();
        MouseVector2 GetCursorPosition();
        void SetCursorPosition(MouseVector2 pos);
    }
}