namespace MQTTTutorial
{
    public class MqttPacket
    {
        public PacketType Type { get; set; }
        public int PacketId { get; set; }  // 新增：用于匹配 SUBSCRIBE <-> SUBACK
        public string Topic { get; set; } = "";
        public string Payload { get; set; } = "";
        public List<string> Topics { get; set; } = new(); // 用于 SUBSCRIBE
    }
}
