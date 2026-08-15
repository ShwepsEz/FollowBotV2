using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FollowBotV2.Core
{
    public static class ServerDiscovery
    {
        public static async Task<List<string>> ScanLocalNetworkAsync(int port, int timeoutMs = 200)
        {
            var result = new List<string>();
            var ips = new HashSet<string>();

            var localIPs = GetLocalIPAddresses();
            foreach (var ip in localIPs)
            {
                // Основная подсеть
                string baseIP = ip.Substring(0, ip.LastIndexOf('.') + 1);
                for (int i = 1; i < 255; i++)
                    ips.Add(baseIP + i);

                // Соседние подсети (±1 в третьем октете)
                var parts = ip.Split('.');
                if (parts.Length == 4 && int.TryParse(parts[2], out int thirdOctet))
                {
                    for (int offset = -1; offset <= 1; offset += 2)
                    {
                        int newThird = thirdOctet + offset;
                        if (newThird >= 1 && newThird <= 254)
                        {
                            string neighborPrefix = $"{parts[0]}.{parts[1]}.{newThird}.";
                            for (int i = 1; i < 255; i++)
                                ips.Add(neighborPrefix + i);
                        }
                    }
                }
            }

            if (ips.Count == 0)
                return result;

            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(200); // больше параллелизма

            foreach (var ip in ips)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        using (var client = new TcpClient())
                        {
                            var connectTask = client.ConnectAsync(ip, port);
                            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) == connectTask)
                            {
                                await connectTask;
                                lock (result)
                                    result.Add(ip);
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return result;
        }

        private static List<string> GetLocalIPAddresses()
        {
            var result = new List<string>();
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        result.Add(ip.ToString());
                }
            }
            catch { }
            return result;
        }
    }
}