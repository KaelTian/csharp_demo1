namespace BsonSlottedPageDemo
{
    /// <summary>
    /// BsonValue 是一个极简的 BSON 值类，代表 BSON 中的各种数据类型。它可以是一个简单的值（如数字、字符串、布尔值等），也可以是一个复杂的文档或数组。
    /// </summary>
    public class BsonValue
    {
        public BsonType Type { get; set; }

        public object? Value { get; set; }

        public static BsonValue Int32(int v)=>new () { Type = BsonType.Int32, Value = v };
        public static BsonValue Int64(long v) => new() { Type = BsonType.Int64, Value = v };
        public static BsonValue String(string v) => new() { Type = BsonType.String, Value = v };
        public static BsonValue Bool(bool v) => new() { Type = BsonType.Boolean, Value = v };
        public static BsonValue Date(DateTime v) => new() { Type = BsonType.DateTime, Value = v };
    }

    public class BsonDocument : Dictionary<string, BsonValue> { }
}
