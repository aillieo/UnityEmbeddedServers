using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;

namespace AillieoUtils.UnityEmbeddedServers.Http
{
    public class HttpServer : IDisposable
    {
        private readonly int port;

        private HttpListener httpListener;

        private bool disposed;

        public HttpServer(int port)
        {
            this.port = port;
        }

        public void Start()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(HttpServer));
            }

            if (httpListener != null)
            {
                return;
            }

            httpListener = new HttpListener();
            httpListener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            httpListener.Prefixes.Add($"http://*:{port}/");

            httpListener.Start();

            UnityEngine.Debug.Log($"{nameof(HttpServer)} Start {GetLocalIPAddress()}:{port}");

            Listen();
        }

        public void Stop()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(HttpServer));
            }

            if (httpListener != null)
            {
                httpListener.Stop();
                httpListener = null;
            }
        }

        private async void Listen()
        {
            if (disposed || httpListener == null)
            {
                return;
            }

            try
            {
                HttpListenerContext httpListenerContext = await httpListener.GetContextAsync();

                try
                {
                    await OnHttpRequest(httpListenerContext);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
            catch (ObjectDisposedException)
            {
                // closed
                return;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }

            Listen();
        }

        private async Task OnHttpRequest(HttpListenerContext httpListenerContext)
        {
            var request = httpListenerContext.Request;
            var response = httpListenerContext.Response;

            string controllerName = Router.GetControllerName(request.Url.Segments);
            string actionName = Router.GetActionName(request.Url.Segments);

            MethodInfo methodInfo = Router.GetHandler(controllerName, new HttpMethod(request.HttpMethod), actionName);

            if (methodInfo == null)
            {
                response.StatusCode = 404;
                using (StreamWriter writer = new StreamWriter(response.OutputStream))
                {
                    writer.Write("Page not found.");
                }

                return;
            }

            try
            {
                string result = await Router.Invoke(controllerName, methodInfo, request.QueryString);

                UnityEngine.Debug.Log($"{result}");

                response.StatusCode = 200;
                using (StreamWriter writer = new StreamWriter(response.OutputStream))
                {
                    writer.Write(result);
                }
            }
            catch (Exception e)
            {
                response.StatusCode = 500;
                using (StreamWriter writer = new StreamWriter(response.OutputStream))
                {
                    writer.Write(e.Message);
                }

                UnityEngine.Debug.LogException(e);
            }
        }

        public static IPAddress GetLocalIPAddress()
        {
            try
            {
                NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (NetworkInterface network in networkInterfaces)
                {
                    IPInterfaceProperties properties = network.GetIPProperties();

                    foreach (IPAddressInformation address in properties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                        {
                            continue;
                        }

                        if (IPAddress.IsLoopback(address.Address))
                        {
                            continue;
                        }

                        return address.Address;
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e);
            }

            try
            {
                IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());
                foreach (IPAddress address in addresses)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return address;
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e);
            }

            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint.Address;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e);
            }

            return null;
        }

        public void Dispose()
        {
            Stop();
            disposed = true;
        }
    }
}
