using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace AillieoUtils.UnityEmbeddedServers.Tcp
{
    public class TcpServer: IDisposable
    {
        public event Action<IPEndPoint, byte[]> onReceive;

        public event Action<TcpClient> onClientConnected;

        private TcpListener tcpListener;
        private CancellationTokenSource cancelationTokenSource;
        private HashSet<TcpClient> clients = new HashSet<TcpClient>();

        public TcpServer(int port)
        {
            cancelationTokenSource = new CancellationTokenSource();

            this.tcpListener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            this.tcpListener.Start();
            Accept();
        }

        private async void Accept()
        {
            while (true)
            {
                try
                {
                    if (cancelationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    TcpClient tcpClient = await this.tcpListener.AcceptTcpClientAsync();
                    OnClientConnected(tcpClient);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        public void Send(byte[] bytes)
        {
            if (cancelationTokenSource.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            foreach (var c in clients)
            {
                SocketAsyncEventArgs socketAsyncEventArgs = new SocketAsyncEventArgs();
                socketAsyncEventArgs.SetBuffer(bytes, 0, bytes.Length);
                socketAsyncEventArgs.Completed += (o, e) => { };
                if (!c.Client.SendAsync(socketAsyncEventArgs))
                {
                }
            }
        }

        private void Receive(TcpClient tcpClient)
        {
            //while (true)
            {
                if (cancelationTokenSource.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }

                Socket socket = tcpClient.Client;
                SocketAsyncEventArgs socketAsyncEventArgs = new SocketAsyncEventArgs();
                socketAsyncEventArgs.SetBuffer(new byte[socket.ReceiveBufferSize], 0, socket.ReceiveBufferSize);
                socketAsyncEventArgs.Completed += (o, e) => OnReceive(tcpClient, e);
                if (!socket.ReceiveAsync(socketAsyncEventArgs))
                {
                    OnReceive(tcpClient, socketAsyncEventArgs);
                }
            }
        }

        private void OnReceive(TcpClient tcpClient, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (socketAsyncEventArgs.SocketError == SocketError.Success)
            {
                if (socketAsyncEventArgs.BytesTransferred != 0)
                {
                    IPEndPoint ipEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                    try
                    {
                        byte[] bytes = new byte[socketAsyncEventArgs.BytesTransferred];
                        Array.Copy(socketAsyncEventArgs.Buffer, bytes, socketAsyncEventArgs.BytesTransferred);
                        onReceive?.Invoke(ipEndPoint, bytes);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogException(e);
                    }

                    // 
                    Receive(tcpClient);
                }
                else
                {
                    if (tcpClient.Client.Connected)
                    {
                        // disconnected
                        clients.Remove(tcpClient);
                    }
                }
            }
        }

        private void OnClientConnected(TcpClient tcpClient)
        {
            clients.Add(tcpClient);

            try
            {
                onClientConnected?.Invoke(tcpClient);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }

            Receive(tcpClient);
        }

        public void Stop()
        {
            foreach (var c in clients)
            {
                if (!c.Connected)
                {
                    continue;
                }

                c.Client.Shutdown(SocketShutdown.Both);
                SocketAsyncEventArgs socketAsyncEventArgs = new SocketAsyncEventArgs();
                socketAsyncEventArgs.Completed += (o, e) => { };
                if (!c.Client.DisconnectAsync(socketAsyncEventArgs))
                {
                }
            }

            if (tcpListener != null)
            {
                tcpListener.Stop();
                tcpListener = null;
            }

            cancelationTokenSource.Cancel();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
