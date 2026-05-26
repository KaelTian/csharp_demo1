namespace MQTTTutorial
{
    // ==================== 极简 MQTT-like 协议定义 ====================
    public enum PacketType : byte
    {
        CONNECT = 1,
        CONNACK = 2,
        PUBLISH = 3,
        SUBSCRIBE = 8,
        SUBACK = 9,
        PINGREQ = 12,
        PINGRESP = 13
    }
}
