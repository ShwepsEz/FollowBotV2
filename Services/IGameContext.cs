using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Components;

namespace FollowBotV2.Services
{
    public interface IGameContext
    {
        GameController GameController { get; }
        Entity Player { get; }
        AreaInstance CurrentArea { get; }
        IngameState IngameState { get; }
        bool IsInTown { get; }
        bool IsInHideout { get; }
    }
}