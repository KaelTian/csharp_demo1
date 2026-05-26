using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MQTTTutorial
{
    public static class PacketSerializer
    {
        // 写：先写 4 字节长度（大端），再写 JSON 负载
        public static void WritePacket(NetworkStream stream, MqttPacket packet)
        {
            var json = JsonSerializer.Serialize(packet);
            var payload = Encoding.UTF8.GetBytes(json);
            var lengthBytes = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lengthBytes, payload.Length);

            stream.Write(lengthBytes, 0, 4);   // 长度头
            stream.Write(payload, 0, payload.Length); // 负载
        }

        // 读：先读 4 字节长度，再精确读 N 字节（解决粘包/拆包）
        public static async Task<MqttPacket?> ReadPacketAsync(NetworkStream stream)
        {
            var lengthBuf = new byte[4];
            if (!await ReadExactAsync(stream, lengthBuf, 4)) return null;

            int length = BinaryPrimitives.ReadInt32BigEndian(lengthBuf);
            if (length <= 0 || length > 100_000) return null; // 防护

            var payloadBuf = new byte[length];
            if (!await ReadExactAsync(stream, payloadBuf, length)) return null;

            var json = Encoding.UTF8.GetString(payloadBuf);
            return JsonSerializer.Deserialize<MqttPacket>(json);
        }

        // 辅助：确保从流中精确读取指定字节数（TCP 可能一次读不全）
        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = await stream.ReadAsync(buffer, read, count - read);
                if (n == 0) return false; // 连接断开
                read += n;
            }
            return true;
        }
    }
}
