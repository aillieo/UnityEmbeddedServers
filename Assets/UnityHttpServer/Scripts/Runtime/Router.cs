using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AillieoUtils.CSReflectionUtils;
using UnityEngine;

namespace AillieoUtils.UnityHttpServer
{
    internal static class Router
    {
        private static Dictionary<string, Type> cachedControllerToType;
        private static ConcurrentDictionary<string, object> cachedControllerToInstance = new ConcurrentDictionary<string, object>();

        static Router()
        {
            cachedControllerToType = ReflectionUtils.GetAllTypesWithAttribute<ControllerAttribute>()
                .ToDictionary(
                pair => string.IsNullOrWhiteSpace(pair.Key.name) ? pair.Value.Name : pair.Key.name,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        public static MethodInfo GetHandler(string controllerName, HttpMethod method, string actionName)
        {
            if (!cachedControllerToType.TryGetValue(controllerName, out Type type))
            {
                return null;
            }

            MethodInfo methodInfo = type.GetMethod(actionName, ReflectionUtils.flagAllAccessible | BindingFlags.IgnoreCase);
            if (methodInfo == null)
            {
                methodInfo = type.GetMethods()
                    .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>(), (m, a) => (m, a))
                    .Where(o => o.a.httpMethod == method && (!string.IsNullOrWhiteSpace(o.a.name) && string.Equals(o.a.name, actionName, StringComparison.OrdinalIgnoreCase)))
                    .Select(o => o.m)
                    .FirstOrDefault();
            }

            return methodInfo;
        }

        public static Task<string> Invoke(string controllerName, MethodInfo methodInfo, NameValueCollection queryString)
        {
            object controller = null;
            if (!methodInfo.IsStatic)
            {
                controller = cachedControllerToInstance.GetOrAdd(controllerName, CreateController);
            }

            object[] args;
            var parameters = methodInfo.GetParameters();
            if (parameters == null || parameters.Length == 0)
            {
                args = Array.Empty<object>();
            }
            else
            {
                args = new object[parameters.Length];
            }

            for (int i = 0; i < parameters.Length; ++i)
            {
                string paramName = parameters[i].Name;
                object paramValue = queryString.Get(paramName);
                args[i] = paramValue;
            }

            bool hasReturnType = methodInfo.ReturnType != typeof(void);
            if (hasReturnType)
            {
                if (methodInfo.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null)
                {
                }

                object result = methodInfo.Invoke(controller, args);
                if (result is string resultStr)
                {
                    return Task.FromResult(resultStr);
                }
                else
                {
                    return Task.FromResult(JsonUtility.ToJson(result));
                }
            }
            else
            {
                methodInfo.Invoke(controller, args);
                return Task.FromResult(string.Empty);
            }
        }

        private static object CreateController(string controllerName)
        {
            if (cachedControllerToType.TryGetValue(controllerName, out Type type))
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }

        public static string GetControllerName(string[] urlSegments)
        {
            if (urlSegments.Length < 2)
            {
                return string.Empty;
            }

            return urlSegments[1].Replace("/", string.Empty);
        }

        public static string GetActionName(string[] urlSegments)
        {
            if (urlSegments.Length < 3)
            {
                return string.Empty;
            }

            return urlSegments[2].Replace("/", string.Empty);
        }
    }
}
