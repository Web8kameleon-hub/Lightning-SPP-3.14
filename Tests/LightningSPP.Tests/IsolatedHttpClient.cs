using System.Net;
using System.Net.Sockets;

namespace LightningSPP.Tests;

/// <summary>Real TCP client with a fixture-specific loopback source, without changing server limits.</summary>
internal static class IsolatedHttpClient
{
    internal static HttpClient Create(int port, byte fixture, CookieContainer? cookies = null)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            CookieContainer = cookies ?? new CookieContainer(),
            ConnectCallback = async (connection, token) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0." + fixture), 0));
                    await socket.ConnectAsync(IPAddress.Loopback, connection.DnsEndPoint.Port, token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
        return new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:" + port) };
    }
}
