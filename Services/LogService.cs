using ExileCore;
using FollowBotV2.Core;

namespace FollowBotV2.Services
{
    public class LogService : ILogService
    {
        private readonly FollowerCore _plugin;

        public LogService(FollowerCore plugin)
        {
            _plugin = plugin;
        }

        public void Info(string message) => _plugin.LogMessage($"[INFO] {message}");
        public void Debug(string message) => _plugin.LogMessage($"[DEBUG] {message}");
        public void Error(string message) => _plugin.LogMessage($"[ERROR] {message}");
        public void Warn(string message) => _plugin.LogMessage($"[WARN] {message}");
    }
}