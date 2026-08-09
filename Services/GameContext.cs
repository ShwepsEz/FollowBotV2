using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Components;

namespace FollowBotV2.Services
{
    public class GameContext : IGameContext
    {
        public GameController GameController { get; }

        public GameContext(GameController gameController)
        {
            GameController = gameController;
        }

        public Entity Player => GameController?.Player;
        public AreaInstance CurrentArea => GameController?.Area?.CurrentArea;
        public IngameState IngameState => GameController?.Game?.IngameState;

        public bool IsInTown
        {
            get
            {
                try
                {
                    var area = CurrentArea;
                    return area != null && area.IsTown;
                }
                catch { return false; }
            }
        }

        public bool IsInHideout
        {
            get
            {
                try
                {
                    var area = CurrentArea;
                    return area != null && area.IsHideout;
                }
                catch { return false; }
            }
        }
    }
}