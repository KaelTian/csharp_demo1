namespace BsonSlottedPageDemo
{
    // ==================== 1. 极简 BSON 层 ====================
    public enum BsonType : byte
    {
        Double = 0x01,
        String = 0x02,
        Document = 0x03,
        Array = 0x04,
        Int32 = 0x10,
        Int64 = 0x12,
        Boolean = 0x08,
        DateTime = 0x09,
        Null = 0x0A
    }

}
