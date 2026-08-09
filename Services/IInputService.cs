using System.Windows.Forms;

namespace FollowBotV2.Services
{
    public interface IInputService
    {
        void KeyDown(Keys key);
        void KeyUp(Keys key);
        void RegisterKey(Keys key);
        bool IsKeyDown(Keys key);
        bool PressedOnce(Keys key);
    }
}