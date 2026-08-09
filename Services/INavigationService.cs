using GameOffsets.Native;

namespace FollowBotV2.Services
{
    public interface INavigationService
    {
        void LoadWalkabilityData();
        void MoveTo(Vector2i target);
        void Stop();
        void Update();
        void DrawPath(ExileCore.Graphics graphics);
        Vector2i? FindNearestWalkable(Vector2i center, int maxRadius = 10);
        bool HasPath { get; }
        bool IsCloseToTarget { get; }
        bool IsPathfinding { get; }
    }
}