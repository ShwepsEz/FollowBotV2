using GameOffsets.Native;
using ExileCore;

namespace FollowBotV2.Services
{
    public interface ITransitionService
    {
        Vector2i? GetPortalTarget(string leaderName);
        bool ShouldClickPortal(Vector2i currentPosition, Vector2i portalPosition);
        void ClickPortal(Vector2i portalPosition);
        void Reset();
        void RefreshTransitions();
        void DrawTransitions(Graphics graphics);

        Vector2i? GetNearestTransitionTarget();
        bool ShouldClickTransition(Vector2i currentPosition, Vector2i transitionPosition);
        void ClickTransition(Vector2i transitionPosition);
    }
}