using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Improbable.Stdlib.Platform
{
    public class UdpProxy : IDisposable
    {
        private System.Net.Sockets.UdpClient server;
        private UdpClient client;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public void Start(string remoteServerIp, ushort remoteServerPort, ushort localPort, CancellationToken cancellation = default, Action<string> debugLogReporter = null)
        {
            server = new System.Net.Sockets.UdpClient(AddressFamily.InterNetwork);
            var localIpAddress = IPAddress.Any;
            cancellation.Register(cts.Cancel);

            server.Client.Bind(new IPEndPoint(localIpAddress, localPort));

            debugLogReporter?.Invoke($"Proxy started UDP: {localIpAddress}:{localPort} -> {remoteServerIp}:{remoteServerPort}" );

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();

                    try
                    {
                        var message = await server.ReceiveAsync();
                        var endpoint = message.RemoteEndPoint;

                        if (client == null)
                        {
                            client = new UdpClient(server, endpoint, new IPEndPoint(IPAddress.Parse(remoteServerIp), remoteServerPort), cts.Token, debugLogReporter);
                            await client.Start();
                        }
                        
                        await client.SendToServer(message.Buffer);
                    }
                    catch (Exception ex)
                    {
                        debugLogReporter?.Invoke($"An exception occurred on receiving a client data-gram: {ex}");
                    }
                }
            }, cancellation);
        }

        public void Dispose()
        {
            cts?.Cancel();
            cts?.Dispose();

            server?.Dispose();
            client?.Dispose();
        }
    }

    internal class UdpClient : IDisposable
    {
        public UdpClient(System.Net.Sockets.UdpClient server, IPEndPoint clientEndpoint, IPEndPoint remoteServer, CancellationToken cancellation = default, Action<string> debugLogReporter = null)
        {
            this.server = server;

            this.remoteServer = remoteServer;
            this.cancellation = cancellation;
            this.debugLogReporter = debugLogReporter;
            this.clientEndpoint = clientEndpoint;

            debugLogReporter?.Invoke($"Established {clientEndpoint} => {remoteServer}");
        }

        private readonly TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        private readonly System.Net.Sockets.UdpClient server;
        private readonly System.Net.Sockets.UdpClient client = new System.Net.Sockets.UdpClient();
        private readonly IPEndPoint clientEndpoint;
        private readonly IPEndPoint remoteServer;
        private readonly CancellationToken cancellation;
        private readonly Action<string> debugLogReporter;

        public async Task SendToServer(byte[] message)
        {
            await tcs.Task;
            await client.SendAsync(message, message.Length, remoteServer);
        }

        public Task Start()
        {
            Task.Run(async () =>
            {
                try
                {
                    client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }

                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();

                    try
                    {
                        var result = await client.ReceiveAsync();
                        await server.SendAsync(result.Buffer, result.Buffer.Length, clientEndpoint);
                    }
                    catch (Exception ex)
                    {
                        debugLogReporter?.Invoke($"An exception occurred while receiving a server data-gram : {ex}");
                    }
                }
            }, cancellation);

            return tcs.Task;
        }

        public void Dispose()
        {
            debugLogReporter?.Invoke($"Closed {clientEndpoint} => {remoteServer}");

            server?.Dispose();
            client?.Dispose();
        }
    }

}
