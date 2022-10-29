using System;
using System.Net.Http;

namespace AillieoUtils.UnityHttpServer
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HttpMethodAttribute : Attribute
    {
        public readonly string name;
        public readonly HttpMethod httpMethod;

        public HttpMethodAttribute(string httpMethod)
        {
            this.httpMethod = new HttpMethod(httpMethod);
        }

        public HttpMethodAttribute(HttpMethod httpMethod, string name)
        {
            this.httpMethod = httpMethod;
            this.name = name;
        }
    }
}
