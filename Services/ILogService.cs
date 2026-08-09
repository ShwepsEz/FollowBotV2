namespace FollowBotV2.Services
{
    public interface ILogService
    {
        void Info(string message);
        void Debug(string message);
        void Error(string message);
        void Warn(string message);
    }
}