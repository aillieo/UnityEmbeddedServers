using System;

namespace AillieoUtils.UnityHttpServer
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ControllerAttribute : Attribute
    {
        public readonly string name;

        public ControllerAttribute()
        {
        }

        public ControllerAttribute(string name)
        {
            this.name = name;
        }
    }
}
