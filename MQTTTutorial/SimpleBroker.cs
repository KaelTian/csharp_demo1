using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace MQTTTutorial
{
    // ==================== Broker 核心：订阅路由 + TCP 长连接 ====================
    public class SimpleBroker
    {
        private readonly TcpListener _listener;
        private readonly ConcurrentDictionary<string, ConcurrentBag<TcpClient>> _subscriptions = new();
        private readonly ConcurrentDictionary<TcpClient, string> _clients = new();
        private int _packetIdSeed = 0;

        public SimpleBroker(int port) => _listener = new TcpListener(IPAddress.Any, port);

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine("[Broker] 启动，等待客户端连接...");

            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            Console.WriteLine($"[Broker] 客户端接入: {endpoint}");
            _clients.TryAdd(client, endpoint);

            try
            {
                var stream = client.GetStream();

                while (client.Connected)
                {
                    var packet = await PacketSerializer.ReadPacketAsync(stream);
                    if (packet == null) break;

                    Console.WriteLine($"[Broker] 收到 {packet.Type} (Id={packet.PacketId}) from {endpoint}");

                    switch (packet.Type)
                    {
                        case PacketType.CONNECT:
                            PacketSerializer.WritePacket(stream, new MqttPacket
                            {
                                Type = PacketType.CONNACK,
                                PacketId = packet.PacketId
                            });
                            Console.WriteLine($"[Broker] {endpoint} 长连接已确认");
                            break;

                        case PacketType.SUBSCRIBE:
                            foreach (var topic in packet.Topics)
                            {
                                var bag = _subscriptions.GetOrAdd(topic, _ => new ConcurrentBag<TcpClient>());
                                bag.Add(client);
                                Console.WriteLine($"[Broker] {endpoint} 订阅 Topic: {topic}");
                            }
                            // 回 SUBACK，带上同样的 PacketId 让客户端匹配
                            PacketSerializer.WritePacket(stream, new MqttPacket
                            {
                                Type = PacketType.SUBACK,
                                PacketId = packet.PacketId
                            });
                            break;

                        case PacketType.PUBLISH:
                            var targets = MatchSubscribers(packet.Topic);
                            Console.WriteLine($"[Broker] Topic '{packet.Topic}' 匹配到 {targets.Count} 个订阅者");

                            foreach (var target in targets)
                            {
                                try
                                {
                                    if (target.Connected)
                                        PacketSerializer.WritePacket(target.GetStream(), new MqttPacket
                                        {
                                            Type = PacketType.PUBLISH,
                                            Topic = packet.Topic,
                                            Payload = packet.Payload
                                        });
                                }
                                catch { }
                            }
                            break;

                        case PacketType.PINGREQ:
                            PacketSerializer.WritePacket(stream, new MqttPacket
                            {
                                Type = PacketType.PINGRESP,
                                PacketId = packet.PacketId
                            });
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broker] {endpoint} 异常: {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(client, out _);
                client.Close();
                Console.WriteLine($"[Broker] {endpoint} 已断开");
            }
        }

        private List<TcpClient> MatchSubscribers(string publishTopic)
        {
            var result = new HashSet<TcpClient>();
            foreach (var (subTopic, clients) in _subscriptions)
            {
                if (TopicMatches(publishTopic, subTopic))
                    foreach (var c in clients) if (c.Connected) result.Add(c);
            }
            return result.ToList();
        }

        private bool TopicMatches(string pubTopic, string subTopic)
        {
            var pubParts = pubTopic.Split('/');
            var subParts = subTopic.Split('/');
            for (int i = 0; i < subParts.Length; i++)
            {
                if (subParts[i] == "#") return true;
                if (i >= pubParts.Length) return false;
                if (subParts[i] != "+" && subParts[i] != pubParts[i]) return false;
            }
            return pubParts.Length == subParts.Length;
        }
    }
}
