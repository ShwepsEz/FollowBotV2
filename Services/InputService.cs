using System.Collections.Generic;
using System.Windows.Forms;
using ExileCore;

namespace FollowBotV2.Services
{
    public class InputService : IInputService
    {
        private readonly Dictionary<Keys, bool> _lastKeyStates = new Dictionary<Keys, bool>();

        public void KeyDown(Keys key) => Input.KeyDown(key);
        public void KeyUp(Keys key) => Input.KeyUp(key);
        public void RegisterKey(Keys key) => Input.RegisterKey(key);
        public bool IsKeyDown(Keys key) => Input.IsKeyDown(key);

        public bool PressedOnce(Keys key)
        {
            bool currentState = Input.IsKeyDown(key);
            bool previousState = _lastKeyStates.ContainsKey(key) && _lastKeyStates[key];
            _lastKeyStates[key] = currentState;
            return currentState && !previousState;
        }
    }
}