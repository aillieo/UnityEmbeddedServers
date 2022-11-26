using System.Net.Sockets;
using System.Text;
using System.Net;
using System;
using System.Collections.Concurrent;
using AillieoUtils.UnityEmbeddedServers.Tcp;

namespace AillieoUtils.UnityEmbeddedServers.WebSocket
{
    public class WebSocketServer : IDisposable
    {
        public enum State
        {
            Negotiating = 0,
            HandShaking,
            Established,
        }

        private struct ClientInfo
        {
            public TcpClient tcpClient;
            public byte[] buffer;
            public State state;
        }

        private TcpServer tcpServer;

        private ConcurrentDictionary<IPEndPoint, ClientInfo> clients = new ConcurrentDictionary<IPEndPoint, ClientInfo>();

        public event Action<TcpClient> onClientConnected;
 
        public event Action<IPEndPoint, string> onReceive;

        public WebSocketServer(int port)
        {
            tcpServer = new TcpServer(port);
            tcpServer.onClientConnected += OnClientConnected;
            tcpServer.onReceive += OnReceive;
        }

        public void Start()
        {
            tcpServer.Start();
        }

        public void Stop()
        {
            tcpServer.Stop();
        }

        private void OnClientConnected(TcpClient tcpClient)
        {
            IPEndPoint ipEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
            ClientInfo clientInfo = new ClientInfo
            {
                tcpClient = tcpClient,
                state = State.Negotiating,
                buffer = Array.Empty<byte>(),
            };

            while (true)
            {
                if (clients.TryAdd(ipEndPoint, clientInfo))
                {
                    this.onClientConnected?.Invoke(clientInfo.tcpClient);
                    break;
                }
            }
        }

        private void OnReceive(IPEndPoint ipEndPoint, byte[] bytes)
        {
            if (clients.TryGetValue(ipEndPoint, out ClientInfo clientInfo))
            {
                byte[] buffer = clientInfo.buffer;
                Array.Resize(ref buffer, buffer.Length + bytes.Length);
                Array.Copy(bytes, 0, buffer, buffer.Length - bytes.Length, bytes.Length);
                clientInfo.buffer = buffer;
                clients[ipEndPoint] = clientInfo;

                if (clientInfo.state == State.Negotiating)
                {
                    clientInfo.state = State.HandShaking;
                    clients[ipEndPoint] = clientInfo;

                    bool succeed = WebSocketProtocal.HandShake(ref buffer, out byte[] response);

                    if (succeed)
                    {
                        clientInfo.tcpClient.GetStream().Write(response, 0, response.Length);

                        clientInfo = clients[ipEndPoint];
                        clientInfo.state = State.Established;
                        clientInfo.buffer = buffer;
                        clients[ipEndPoint] = clientInfo;

                        onClientConnected?.Invoke(clientInfo.tcpClient);
                    }
                }
                else if (clientInfo.state == State.Established)
                {
                    if (WebSocketProtocal.DecodeFrame(ref buffer, out byte[] payload, out bool fin, out OpCode opCode))
                    {
                        clientInfo.buffer = buffer;
                        clients[ipEndPoint] = clientInfo;

                        string message = Encoding.UTF8.GetString(payload);
                        onReceive?.Invoke(ipEndPoint, message);
                    }
                }
            }
        }

        public void Send(byte[] message)
        {
            byte[] buffer = WebSocketProtocal.EncodeFrame(message, true, OpCode.Text);
            tcpServer.Send(buffer);
        }

        public void Dispose()
        {
            this.Stop();
        }
    }
}
