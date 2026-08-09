using GameOffsets.Native; // ← добавлено
using ExileCore;          // опционально, для Graphics

namespace FollowBotV2.Services
{
    public interface ITransitionService
    {
        // --- Для порталов (другая зона) ---
        Vector2i? GetPortalTarget(string leaderName);
        bool ShouldClickPortal(Vector2i currentPosition, Vector2i portalPosition);
        void ClickPortal(Vector2i portalPosition);
        void Reset();
        void RefreshTransitions();
        void DrawTransitions(Graphics graphics); // можно оставить ExileCore.Graphics, но using ExileCore позволяет писать Graphics

        // --- Для переходов (та же зона, но за дверью) ---
        Vector2i? GetNearestTransitionTarget();
        bool ShouldClickTransition(Vector2i currentPosition, Vector2i transitionPosition);
        void ClickTransition(Vector2i transitionPosition);
    }
}