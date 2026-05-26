using System.Collections.Concurrent;
using System.Net.Sockets;

namespace MQTTTutorial
{
    // ==================== 客户端 ====================
    public class SimpleClient
    {
        private TcpClient _client = null!;
        private NetworkStream _stream = null!;
        private readonly string _name;
        private int _packetId = 0;

        // 核心修复：等待 ACK 的字典。Key: PacketId, Value: 对应的 TaskCompletionSource
        private readonly ConcurrentDictionary<int, TaskCompletionSource<MqttPacket>> _pendingAcks = new();

        // 消息到达事件（用于接收 PUBLISH）
        public event Action<string, string>? OnMessageReceived;

        public SimpleClient(string name) => _name = name;

        public async Task ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();

            int pid = Interlocked.Increment(ref _packetId);
            var tcs = new TaskCompletionSource<MqttPacket>();
            _pendingAcks[pid] = tcs;

            // 发送 CONNECT
            PacketSerializer.WritePacket(_stream, new MqttPacket
            {
                Type = PacketType.CONNECT,
                PacketId = pid
            });

            // 启动唯一的读取循环（必须在发送第一个请求前启动，防止 ACK 被漏掉）
            _ = ReceiveLoopAsync();

            // 等待 CONNACK
            var ack = await tcs.Task;
            Console.WriteLine($"[{_name}] 连接确认: {ack.Type}");
        }

        public async Task SubscribeAsync(params string[] topics)
        {
            int pid = Interlocked.Increment(ref _packetId);
            var tcs = new TaskCompletionSource<MqttPacket>();
            _pendingAcks[pid] = tcs;

            PacketSerializer.WritePacket(_stream, new MqttPacket
            {
                Type = PacketType.SUBSCRIBE,
                PacketId = pid,
                Topics = topics.ToList()
            });

            // 等待 SUBACK，而不是自己读流！
            var ack = await tcs.Task;
            Console.WriteLine($"[{_name}] 订阅确认: {ack.Type} (Id={ack.PacketId})");
        }

        public async Task PublishAsync(string topic, string payload)
        {
            int pid = Interlocked.Increment(ref _packetId);
            // PUBLISH 在 QoS0 下不需要等待 ACK，直接发
            PacketSerializer.WritePacket(_stream, new MqttPacket
            {
                Type = PacketType.PUBLISH,
                PacketId = pid,
                Topic = topic,
                Payload = payload
            });
            Console.WriteLine($"[{_name}] 发布 -> {topic}: {payload}");
        }

        // 唯一的读取入口
        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (_client.Connected)
                {
                    var packet = await PacketSerializer.ReadPacketAsync(_stream);
                    if (packet == null) break;

                    // 如果是 ACK 包（CONNACK/SUBACK/PINGRESP），唤醒等待的 Task
                    if (packet.Type is PacketType.CONNACK or PacketType.SUBACK or PacketType.PINGRESP)
                    {
                        if (_pendingAcks.TryRemove(packet.PacketId, out var tcs))
                        {
                            tcs.TrySetResult(packet);
                        }
                    }
                    // 如果是 PUBLISH，走事件分发
                    else if (packet.Type == PacketType.PUBLISH)
                    {
                        OnMessageReceived?.Invoke(packet.Topic, packet.Payload);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_name}] 接收异常: {ex.Message}");
            }
            Console.WriteLine($"[{_name}] 连接已关闭");
        }
    }
}