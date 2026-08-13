namespace FollowBotV2.Services
{
    public interface IUltimatumService
    {
        bool IsPanelOpen { get; }
        bool ChoiceMadeThisRound { get; }
        void CheckAndHandle();
    }
}