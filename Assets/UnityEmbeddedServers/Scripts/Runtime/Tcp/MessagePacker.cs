using System.Text;
using System;

namespace AillieoUtils.UnityEmbeddedServers.Tcp
{
    public static class MessagePacker
    {
        public const int headLength = sizeof(int);

        public static byte[] Pack(byte[] rawBytes)
        {
            byte[] headBytes = BitConverter.GetBytes((int)rawBytes.Length);
            byte[] bytes = new byte[headBytes.Length + rawBytes.Length];
            Array.Copy(headBytes, 0, bytes, 0, headBytes.Length);
            Array.Copy(rawBytes, 0, bytes, headBytes.Length, rawBytes.Length);
            return bytes;
        }

        public static bool Unpack(ref byte[] rawBytes, out byte[] data)
        {
            if (rawBytes.Length < headLength)
            {
                data = Array.Empty<byte>();
                return false;
            }

            int bodyLength = BitConverter.ToInt32(rawBytes, 0);
            if (headLength + bodyLength < rawBytes.Length)
            {
                data = Array.Empty<byte>();
                return false;
            }

            data = new byte[bodyLength];
            Array.Copy(rawBytes, headLength, data, 0, bodyLength);
            int rest = rawBytes.Length - headLength - bodyLength;
            if (rest > 0)
            {
                Array.Copy(rawBytes, headLength + bodyLength, rawBytes, 0, rest);
                Array.Resize(ref rawBytes, rest);
            }
            else
            {
                rawBytes = Array.Empty<byte>();
            }

            return true;
        }
    }
}
