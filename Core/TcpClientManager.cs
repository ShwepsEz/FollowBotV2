using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FollowBotV2.Services;

namespace FollowBotV2.Core
{
    public class TcpClientManager : IDisposable
    {
        private readonly ILogService _log;
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly object _lock = new object();
        private bool _disposed;

        public string LastResponse { get; private set; } = "";
        public bool IsConnected => _client != null && _client.Connected;

        public TcpClientManager(ILogService log)
        {
            _log = log;
        }

        public async Task<bool> ConnectAsync(string host, int port, int timeoutMs = 3000)
        {
            if (IsConnected)
                Disconnect();

            try
            {
                _client = new TcpClient();
                var connectTask = _client.ConnectAsync(host, port);
                if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask)
                {
                    _log.Error($"Connection to {host}:{port} timed out.");
                    _client?.Close();
                    _client = null;
                    return false;
                }
                await connectTask;
                _stream = _client.GetStream();
                _log.Info($"Connected to {host}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Connection failed: {ex.Message}");
                _client = null;
                return false;
            }
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                _stream?.Close();
                _stream = null;
                _client?.Close();
                _client = null;
                _log.Info("Disconnected from server");
            }
        }

        public async Task<string> SendCommandAsync(string command)
        {
            if (!IsConnected)
                return "Not connected";
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(command + "\n");
                await _stream.WriteAsync(data, 0, data.Length);
                await _stream.FlushAsync();
                var buffer = new byte[4096];
                int read = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    LastResponse = "No response";
                    return LastResponse;
                }
                LastResponse = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                return LastResponse;
            }
            catch (Exception ex)
            {
                LastResponse = $"Error: {ex.Message}";
                Disconnect();
                return LastResponse;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
        }
    }
}