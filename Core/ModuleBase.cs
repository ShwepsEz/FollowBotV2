using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using FollowBotV2.Config;

namespace FollowBotV2.Core
{
    public abstract class ModuleBase
    {
        protected GameController GameController { get; private set; }
        protected FollowerCore Core { get; private set; }
        protected FollowerSettings Settings { get; private set; }

        public virtual void Initialize(GameController gameController, FollowerCore core, FollowerSettings settings)
        {
            GameController = gameController;
            Core = core;
            Settings = settings;
        }

        public virtual void Update() { }
        public virtual void OnAreaChange(AreaInstance area) { }
        public virtual void DrawOverlay(ref Vector2 pos, int lineHeight) { }
        public virtual void Reset() { }
    }
}