using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.Assertions;

// https://datatracker.ietf.org/doc/html/rfc6455
namespace AillieoUtils.UnityEmbeddedServers.WebSocket
{
    public enum OpCode : byte
    {
        Continuation = 0x0,
        Text = 0x1,
        Binary = 0x2,
        ConnectionClose = 0x8,
        Ping = 0x9,
        Pong = 0x10,
    }

    public static class WebSocketProtocal
    {
        private static readonly Regex regWebSocketHeader = new Regex("^GET");
        private static readonly Regex regWebSocketKey = new Regex("Sec-WebSocket-Key: (.*)");
        private static readonly Regex regWebSocketVersion = new Regex("Sec-WebSocket-Version: (.*)");

        private const string eol = "\r\n";
        private const string webSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private const string handshakeResponseTemplate =
            "HTTP/1.1 101 Switching Protocols"
            + eol
            + "Connection: Upgrade"
            + eol
            + "Upgrade: websocket"
            + eol
            + "Sec-WebSocket-Accept: "
            + "{0}"
            + eol
            + eol
            ;

        public static bool HandShake(ref byte[] buffer, out byte[] response)
        {
            if (buffer.Length < 3)
            {
                response = default;
                return false;
            }

            string header = Encoding.UTF8.GetString(buffer);

            if (regWebSocketHeader.Match(header).Success)
            {
                int version = int.Parse(regWebSocketVersion.Match(header).Groups[1].Value.Trim(), CultureInfo.InvariantCulture);
                string key = regWebSocketKey.Match(header).Groups[1].Value.Trim();

                byte[] sha1Hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(key + webSocketGuid));
                string sha1HashBase64 = Convert.ToBase64String(sha1Hash);
                response = Encoding.UTF8.GetBytes(string.Format(CultureInfo.InvariantCulture, handshakeResponseTemplate, sha1HashBase64));
                buffer = Array.Empty<byte>();
                return true;
            }

            response = default;
            return false;
        }

        private const byte finBitFlag = 0x80;
        private const byte opCodeFlag = 0x0F;
        private const byte maskFlag = 0x80;

        public static byte[] EncodeFrame(byte[] payload, bool fin, OpCode opCode)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                byte finBitSet = fin ? finBitFlag : (byte)0;
                byte byte0 = (byte)(finBitSet | (byte)opCode);
                memoryStream.WriteByte(byte0);

                byte maskBitSet = 0;

                if (payload.Length < 126)
                {
                    byte byte1 = (byte)(maskBitSet | (byte)payload.Length);
                    memoryStream.WriteByte(byte1);
                }
                else if (payload.Length <= ushort.MaxValue)
                {
                    byte byte1 = (byte)(maskBitSet | 126);
                    memoryStream.WriteByte(byte1);
                    byte[] bytesForLen = BitConverter.GetBytes((ushort)payload.Length);
                    Array.Reverse(bytesForLen);
                    memoryStream.Write(bytesForLen, 0, bytesForLen.Length);
                }
                else
                {
                    byte byte1 = (byte)(maskBitSet | 127);
                    memoryStream.WriteByte(byte1);
                    byte[] bytesForLen = BitConverter.GetBytes((ulong)payload.Length);
                    Array.Reverse(bytesForLen);
                    memoryStream.Write(bytesForLen, 0, bytesForLen.Length);
                }

                memoryStream.Write(payload, 0, payload.Length);
                return memoryStream.ToArray();
            }
        }

        public static bool DecodeFrame(ref byte[] buffer, out byte[] payload, out bool fin, out OpCode opCode)
        {
            if (buffer.Length < 2)
            {
                payload = default;
                fin = default;
                opCode = default;
                return false;
            }

            byte byte0 = buffer[0];

            fin = (byte0 & finBitFlag) == finBitFlag;
            opCode = (OpCode)(byte0 & opCodeFlag);

            byte byte1 = buffer[1];
            bool maskBitSet = (byte1 & maskFlag) == maskFlag;

            int length = buffer[1] & 0b01111111;
            int offset = 2;
            if (length == 126)
            {
                byte[] bytesForLen = new byte[2];
                Array.Copy(buffer, 2, bytesForLen, 0, 2);
                Array.Reverse(bytesForLen);
                length = BitConverter.ToUInt16(bytesForLen, 0);
                offset += 2;
            }
            else if (length == 127)
            {
                byte[] bytesForLen = new byte[8];
                Array.Copy(buffer, 2, bytesForLen, 0, 8);
                Array.Reverse(bytesForLen);
                length = BitConverter.ToInt32(bytesForLen, 0);
                offset += 8;
            }

            if (length == 0)
            {
                payload = Array.Empty<byte>();
                Array.Copy(buffer, offset, buffer, 0L, buffer.Length - offset);
                Array.Resize(ref buffer, buffer.Length - offset);
                return true;
            }

            Assert.IsTrue(maskBitSet);

            payload = new byte[length];

            byte[] masks = new byte[4];
            Array.Copy(buffer, offset, masks, 0, 4);

            offset += 4;

            for (int i = 0; i < length; ++i)
            {
                payload[i] = (byte)(buffer[offset + i] ^ masks[i % 4]);
            }

            int consume = offset + length;
            Array.Copy(buffer, consume, buffer, 0, buffer.Length - consume);
            Array.Resize(ref buffer, buffer.Length - consume);
            return true;
        }
    }
}
