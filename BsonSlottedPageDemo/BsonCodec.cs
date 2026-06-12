using System.Buffers.Binary;
using System.Text;

namespace BsonSlottedPageDemo
{
    public static class BsonCodec
    {
        /// <summary>
        /// 编码文档 -> byte[]
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static byte[] Encode(BsonDocument doc)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            // 占位 4 字节长度
            writer.Write(0);

            foreach (var kv in doc)
            {
                writer.Write((byte)kv.Value.Type);
                WriteCString(writer, kv.Key);

                switch (kv.Value.Type)
                {
                    case BsonType.Int32:
                        writer.Write((int)kv.Value.Value!);
                        break;
                    case BsonType.Int64:
                        writer.Write((long)kv.Value.Value!);
                        break;
                    case BsonType.Boolean:
                        writer.Write((bool)kv.Value.Value! ? (byte)1 : (byte)0);
                        break;
                    case BsonType.DateTime:
                        // BSON DateTime 是 UTC 毫秒数 Int64
                        long msVal = new
                            DateTimeOffset((DateTime)kv.Value.Value!).ToUnixTimeMilliseconds();
                        writer.Write(msVal);
                        break;
                    case BsonType.String:
                        var strBytes = Encoding.UTF8.GetBytes((string)kv.Value.Value!);
                        writer.Write(strBytes.Length + 1); // 含 \0
                        writer.Write(strBytes);
                        writer.Write((byte)0);
                        break;
                }
            }

            writer.Write((byte)0); // 文档结尾

            var bytes = ms.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), bytes.Length);
            return bytes;
        }
        /// <summary>
        /// 解码 byte[] -> 文档
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static BsonDocument Decode(byte[] data)
        {
            var doc = new BsonDocument();
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms, Encoding.UTF8);

            int docLen = reader.ReadInt32(); // 读取文档长度
            if (docLen != data.Length) throw new InvalidDataException("长度不匹配");

            while (ms.Position < data.Length - 1)
            {
                byte type = reader.ReadByte();
                if (type == 0) break; // 文档结尾
                string key = ReadCString(reader);

                BsonValue val = new() { Type = (BsonType)type };
                switch ((BsonType)type)
                {
                    case BsonType.Int32:
                        val.Value = reader.ReadInt32();
                        break;
                    case BsonType.Int64:
                        val.Value = reader.ReadInt64();
                        break;
                    case BsonType.Boolean:
                        val.Value = reader.ReadByte() != 0;
                        break;
                    case BsonType.DateTime:
                        long msVal = reader.ReadInt64();
                        // BSON DateTime 是 UTC 毫秒数 Int64
                        val.Value = DateTimeOffset.FromUnixTimeMilliseconds(msVal).UtcDateTime;
                        break;
                    case BsonType.String:
                        int strLen = reader.ReadInt32();
                        var strBytes = reader.ReadBytes(strLen - 1); // 不含 \0
                        reader.ReadByte(); // 读取 \0
                        val.Value = Encoding.UTF8.GetString(strBytes);
                        break;
                }
                doc[key] = val;
            }

            return doc;
        }

        private static void WriteCString(BinaryWriter w, string s)
        {
            w.Write(Encoding.UTF8.GetBytes(s));
            w.Write((byte)0);
        }

        private static string ReadCString(BinaryReader r)
        {
            var ms = new MemoryStream();
            while (true)
            {
                byte b = r.ReadByte();
                if (b == 0) break;
                ms.WriteByte(b);
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
