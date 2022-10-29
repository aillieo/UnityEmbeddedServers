using UnityEngine;

namespace AillieoUtils.UnityHttpServer
{
    public class HttpServerMonoBehaviour : MonoBehaviour
    {
        private HttpServer httpListener;

        [SerializeField]
        private bool autoStart = true;

        [SerializeField]
        private int port = 10250;

        private void Start()
        {
            if (autoStart)
            {
                httpListener = new HttpServer(port);
                httpListener.Start();
            }
        }

        private void OnDestroy()
        {
            if (httpListener != null)
            {
                this.httpListener.Stop();
            }
        }
    }
}
