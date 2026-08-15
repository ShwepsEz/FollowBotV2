using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FollowBotV2.Services;

namespace FollowBotV2.Core
{
    public class TcpCommandServer : IDisposable
    {
        private readonly FollowerCore _core;
        private readonly ILogService _log;
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenTask;
        private bool _disposed;

        public int Port { get; private set; }
        public bool IsRunning => _listener != null && _cts != null && !_cts.IsCancellationRequested;

        public TcpCommandServer(FollowerCore core, ILogService log)
        {
            _core = core;
            _log = log;
        }

        public void Start(int port)
        {
            if (IsRunning)
                Stop();

            Port = port;
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _log.Info($"TCP Command Server started on port {port}");

            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _cts.Cancel();
            _listener?.Stop();
            try
            {
                _listenTask?.Wait(2000);
            }
            catch (AggregateException) { }
            _log.Info("TCP Command Server stopped");
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = HandleClientAsync(client, ct);
                }
            }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log.Error($"TCP Server error: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            try
            {
                using var stream = client.GetStream();
                var buffer = new byte[4096];
                var sb = new StringBuilder();

                while (!ct.IsCancellationRequested && client.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                    if (read == 0) break;

                    string chunk = Encoding.UTF8.GetString(buffer, 0, read);
                    sb.Append(chunk);

                    // Разделитель – новая строка
                    string[] lines = sb.ToString().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    sb.Clear();
                    foreach (var line in lines)
                    {
                        string response = ProcessCommand(line.Trim());
                        byte[] respBytes = Encoding.UTF8.GetBytes(response + "\n");
                        await stream.WriteAsync(respBytes, 0, respBytes.Length, ct).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"TCP client handling error: {ex.Message}");
            }
            finally
            {
                client?.Close();
            }
        }

        private string ProcessCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return "OK";

            var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLowerInvariant();

            switch (command)
            {
                case "start":
                    _core.ToggleFollow();
                    return "Following started (if was stopped)";
                case "stop":
                    _core.ToggleFollow();
                    return "Following stopped (if was running)";
                case "status":
                    return GetStatus();
                case "setleader":
                    if (parts.Length > 1)
                    {
                        string name = string.Join(" ", parts, 1, parts.Length - 1);
                        _core.Settings.ImGui.LeaderName.Value = name;
                        return $"Leader set to {name}";
                    }
                    return "Usage: setleader <name>";
                case "setmode":
                    if (parts.Length > 1)
                    {
                        string mode = parts[1];
                        if (mode == "follow" || mode == "ultimatumfarm")
                        {
                            _core.Settings.ImGui.BotMode.Value = mode;
                            return $"Mode set to {mode}";
                        }
                        return "Invalid mode. Use 'follow' or 'ultimatumfarm'";
                    }
                    return "Usage: setmode <follow|ultimatumfarm>";
                case "reload":
                    _core.ReloadWalkability();
                    return "Walkability reloaded";
                case "help":
                    return "Commands: start, stop, status, setleader <name>, setmode <follow|ultimatumfarm>, reload, help";
                default:
                    return $"Unknown command: {command}";
            }
        }

        private string GetStatus()
        {
            var state = _core.CurrentState;
            string leader = _core.Settings.ImGui.LeaderName.Value;
            string mode = _core.Settings.ImGui.BotMode.Value;
            bool inParty = _core.GetPartyService()?.IsLeaderInParty(leader) ?? false;
            string leaderPos = "unknown";
            if (inParty)
            {
                var pos = _core.GetPartyService()?.GetPlayerGridPosition(leader);
                if (pos.HasValue)
                    leaderPos = $"({pos.Value.X}, {pos.Value.Y})";
                else
                    leaderPos = "not found";
            }
            return $"State: {state} | Leader: {leader} ({leaderPos}) | Mode: {mode} | InParty: {inParty}";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _cts?.Dispose();
            _listener?.Stop();
        }
    }
}