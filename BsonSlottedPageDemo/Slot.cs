namespace BsonSlottedPageDemo
{
    // ==================== 2. Slotted Page 层 ====================
    /// <summary>
    /// 
    /// </summary>
    public class Slot
    {
        public ushort Offset;   // 数据在页内偏移
        public ushort Length;   // 数据长度
        public bool IsDeleted;  // 逻辑删除标记
    }

    public class SlottedPage
    {
        public const int PageSize = 4096;
        public const int HeaderSize = 64; // 模拟页头
        public const int SlotSize = 4;    // Offset(2) + Length(2)

        private readonly byte[] _data = new byte[PageSize];
        private ushort _freeOffset;     // Data Area 当前可用位置（从 HeaderSize 开始向上）
        private ushort _slotOffset;     // Slot Array 当前可用位置（从 PageSize 开始向下）

        public List<Slot> Slots { get; } = new();

        public SlottedPage()
        {
            _freeOffset = HeaderSize;
            _slotOffset = (ushort)PageSize;
        }

        // Insert：返回 Slot Index
        public int Insert(BsonDocument doc)
        {
            var bson = BsonCodec.Encode(doc);
            if (bson.Length > RemainingSpace())
                throw new InvalidOperationException("页空间不足");

            // 1. 写数据到 Data Area
            Buffer.BlockCopy(bson, 0, _data, _freeOffset, bson.Length);

            // 2. 在 Slot Array 尾部追加 Slot
            _slotOffset -= SlotSize;
            var slot = new Slot { Offset = _freeOffset, Length = (ushort)bson.Length, IsDeleted = false };
            Slots.Add(slot);

            // 写入 Slot 到页尾（注意：这里简化处理，实际应序列化到 _data）
            // 为了演示，Slot 只存内存列表，不真的写到 _data 末尾，避免解析复杂度
            _freeOffset += (ushort)bson.Length;

            return Slots.Count - 1; // 返回 Slot Index
        }

        // Select：通过 Slot Index 读取
        public BsonDocument? Select(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Slots.Count) return null;
            var slot = Slots[slotIndex];
            if (slot.IsDeleted) return null;

            var slice = new byte[slot.Length];
            Buffer.BlockCopy(_data, slot.Offset, slice, 0, slot.Length);
            return BsonCodec.Decode(slice);
        }

        // Delete：逻辑删除
        public void Delete(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < Slots.Count)
                Slots[slotIndex].IsDeleted = true;
        }

        public int RemainingSpace() => _slotOffset - _freeOffset;

        public void DumpHex()
        {
            Console.WriteLine("=== Page Hex Dump (前 256 字节) ===");
            for (int i = 0; i < Math.Min(256, _data.Length); i += 16)
            {
                Console.Write($"{i:X4}: ");
                for (int j = 0; j < 16; j++)
                    if (i + j < _data.Length) Console.Write($"{_data[i + j]:X2} ");
                Console.WriteLine();
            }
        }
    }
}
