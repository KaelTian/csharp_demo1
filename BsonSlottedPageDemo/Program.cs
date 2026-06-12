namespace BsonSlottedPageDemo
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== BSON + Slotted Page 演示 ===\n");

            // 构造文档
            var doc1 = new BsonDocument
            {
                ["_id"] = BsonValue.Int32(1),
                ["name"] = BsonValue.String("张三"),
                ["age"] = BsonValue.Int32(28)
            };

            var doc2 = new BsonDocument
            {
                ["_id"] = BsonValue.Int32(2),
                ["name"] = BsonValue.String("李四"),
                ["active"] = BsonValue.Bool(true),
                ["created"] = BsonValue.Date(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc))
            };

            // 先看 BSON 二进制
            Console.WriteLine("--- 单条 BSON 十六进制 ---");
            var bsonBytes = BsonCodec.Encode(doc1);
            Console.WriteLine(BitConverter.ToString(bsonBytes).Replace("-", " "));
            Console.WriteLine();

            // Slotted Page 操作
            var page = new SlottedPage();

            Console.WriteLine("--- Insert ---");
            int idx1 = page.Insert(doc1);
            int idx2 = page.Insert(doc2);
            Console.WriteLine($"插入 doc1 -> Slot[{idx1}]");
            Console.WriteLine($"插入 doc2 -> Slot[{idx2}]");
            Console.WriteLine($"页剩余空间: {page.RemainingSpace()} bytes\n");

            Console.WriteLine("--- Select ---");
            var d1 = page.Select(idx1);
            var d2 = page.Select(idx2);
            PrintDoc(d1, "Slot[0]");
            PrintDoc(d2, "Slot[1]");

            Console.WriteLine("--- Delete Slot[0] ---");
            page.Delete(0);
            var d1After = page.Select(0);
            Console.WriteLine(d1After == null ? "Slot[0] 已删除，Select 返回 null" : "仍可读");

            Console.WriteLine("\n--- 页内十六进制快照 ---");
            page.DumpHex();
        }

        static void PrintDoc(BsonDocument? doc, string label)
        {
            if (doc == null) { Console.WriteLine($"{label}: null"); return; }
            Console.WriteLine($"{label}:");
            foreach (var kv in doc)
            {
                string valStr = kv.Value.Value?.ToString() ?? "null";
                Console.WriteLine($"  {kv.Key} ({kv.Value.Type}) = {valStr}");
            }
        }
    }
}
