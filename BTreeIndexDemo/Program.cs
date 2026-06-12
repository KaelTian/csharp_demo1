using System.Buffers.Binary;
using System.Text;

namespace BTreeIndexDemo
{
    // ==================== 0. 基础类型 ====================
    /// <summary>数据页中的坐标：哪一页、哪个槽位</summary>
    public readonly record struct RowId(int PageId, int SlotIndex);

    // ==================== 1. 极简 BSON（复用之前的） ====================
    public static class BsonCodec
    {
        public static byte[] Encode(Dictionary<string, object> doc)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8);
            w.Write(0); // 长度占位
            foreach (var kv in doc)
            {
                if (kv.Value is int iv)
                {
                    w.Write((byte)0x10);
                    WriteCString(w, kv.Key);
                    w.Write(iv);
                }
                else if (kv.Value is string sv)
                {
                    w.Write((byte)0x02);
                    WriteCString(w, kv.Key);
                    var bytes = Encoding.UTF8.GetBytes(sv);
                    w.Write(bytes.Length + 1);
                    w.Write(bytes);
                    w.Write((byte)0);
                }
            }
            w.Write((byte)0); // 文档结束
            var data = ms.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0, 4), data.Length);
            return data;
        }

        public static Dictionary<string, object> Decode(byte[] data)
        {
            var doc = new Dictionary<string, object>();
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms, Encoding.UTF8);
            r.ReadInt32(); // 长度
            while (ms.Position < data.Length - 1)
            {
                byte type = r.ReadByte();
                if (type == 0) break;
                string key = ReadCString(r);
                switch (type)
                {
                    case 0x10: doc[key] = r.ReadInt32(); break;
                    case 0x02:
                        int len = r.ReadInt32();
                        doc[key] = Encoding.UTF8.GetString(r.ReadBytes(len - 1));
                        r.ReadByte(); break;
                    default: throw new NotSupportedException($"Type 0x{type:X2}");
                }
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
            var bytes = new List<byte>();
            while (true)
            {
                byte b = r.ReadByte();
                if (b == 0) break;
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }

    // ==================== 2. Slotted Data Page ====================
    /// <summary>数据页：存 BSON 字节流，通过 Slot Directory 寻址</summary>
    public class DataPage
    {
        public int PageId;
        // 简化版：直接存 BSON 块，真实 LiteDB 会有 Slot Array 和 FreeSpace 管理
        public List<byte[]> Slots = new();
        public List<bool> Deleted = new();

        public int Insert(byte[] bson)
        {
            Slots.Add(bson);
            Deleted.Add(false);
            return Slots.Count - 1;
        }

        public byte[]? Select(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Slots.Count || Deleted[slotIndex]) return null;
            return Slots[slotIndex];
        }
    }

    // ==================== 3. B+Tree 索引页 ====================
    public class BTreeIndexPage
    {
        public int PageId;
        public bool IsLeaf;        // true=叶子, false=内部节点
        public bool IsRoot;
        public int ParentPageId = -1;

        // 叶子节点：Keys[i] 对应 RowIds[i]
        public List<int> Keys = new();
        public List<RowId> RowIds = new ();

        // 内部节点：Children 比 Keys 多一个
        public List<int> Children = new();

        // 叶子链表：用于范围扫描快速横向跳转
        public int? NextPageId;
    }

    // ==================== 4. B+Tree 索引引擎 ====================
    public class BTreeIndex
    {
        private int _nextPageId = 1;
        private readonly int _maxKeys; // 节点最多容纳的键数（>= 则分裂）
        private readonly Dictionary<int, BTreeIndexPage> _pages = new();

        public BTreeIndexPage Root { get; private set; }

        public BTreeIndex(int maxKeys = 3)
        {
            _maxKeys = maxKeys;
            Root = AllocPage(isLeaf: true, isRoot: true);
        }

        private BTreeIndexPage AllocPage(bool isLeaf, bool isRoot, int parentId = -1)
        {
            var p = new BTreeIndexPage
            {
                PageId = _nextPageId++,
                IsLeaf = isLeaf,
                IsRoot = isRoot,
                ParentPageId = parentId
            };
            _pages[p.PageId] = p;
            return p;
        }

        // ---------- 查找 ----------
        public RowId? Search(int key)
        {
            var page = Root;
            while (!page.IsLeaf)
            {
                // 找到第一个大于 key 的位置，落入该位置左侧子树
                int i = 0;
                while (i < page.Keys.Count && key >= page.Keys[i]) i++;
                page = _pages[page.Children[i]];
            }

            int idx = page.Keys.BinarySearch(key);
            return idx >= 0 ? page.RowIds[idx] : null;
        }

        // 打印查找路径（用于演示）
        public RowId? SearchWithTrace(int key)
        {
            var page = Root;
            Console.Write($"  查找路径: Root(Page{page.PageId})");
            while (!page.IsLeaf)
            {
                int i = 0;
                while (i < page.Keys.Count && key >= page.Keys[i]) i++;
                Console.Write($" -> Child{i}(Page{page.Children[i]})");
                page = _pages[page.Children[i]];
            }
            Console.WriteLine($" -> Leaf(Page{page.PageId})");

            int idx = page.Keys.BinarySearch(key);
            if (idx >= 0) return page.RowIds[idx];
            Console.WriteLine($"  键 {key} 不在叶子中");
            return null;
        }

        // ---------- 范围扫描（利用叶子链表）----------
        public List<RowId> RangeScan(int startKey, int endKey)
        {
            var result = new List< RowId > ();
            var leaf = FindLeafPage(startKey);
            while (leaf != null)
            {
                for (int i = 0; i < leaf.Keys.Count; i++)
                {
                    if (leaf.Keys[i] > endKey) return result;
                    if (leaf.Keys[i] >= startKey) result.Add(leaf.RowIds[i]);
                }
                leaf = leaf.NextPageId.HasValue ? _pages[leaf.NextPageId.Value] : null;
            }
            return result;
        }

        private BTreeIndexPage FindLeafPage(int key)
        {
            var page = Root;
            while (!page.IsLeaf)
            {
                int i = 0;
                while (i < page.Keys.Count && key >= page.Keys[i]) i++;
                page = _pages[page.Children[i]];
            }
            return page;
        }

        // ---------- 插入 ----------
        public void Insert(int key, RowId rowId)
        {
            var leaf = FindLeafPage(key);
            InsertIntoLeaf(leaf, key, rowId);
            if (leaf.Keys.Count > _maxKeys)
                SplitLeaf(leaf);
        }

        private void InsertIntoLeaf(BTreeIndexPage leaf, int key, RowId rowId)
        {
            int idx = leaf.Keys.BinarySearch(key);
            if (idx >= 0) throw new InvalidOperationException($"重复键: {key}");
            idx = ~idx;
            leaf.Keys.Insert(idx, key);
            leaf.RowIds.Insert(idx, rowId);
        }

        // 叶子分裂：中间键复制到父节点（B+Tree 特性）
        private void SplitLeaf(BTreeIndexPage leaf)
        {
            int mid = leaf.Keys.Count / 2;
            int midKey = leaf.Keys[mid];

            var newLeaf = AllocPage(isLeaf: true, isRoot: false, parentId: leaf.ParentPageId);
            newLeaf.Keys.AddRange(leaf.Keys.Skip(mid));
            newLeaf.RowIds.AddRange(leaf.RowIds.Skip(mid));

            leaf.Keys.RemoveRange(mid, leaf.Keys.Count - mid);
            leaf.RowIds.RemoveRange(mid, leaf.RowIds.Count - mid);

            // 维护叶子链表
            newLeaf.NextPageId = leaf.NextPageId;
            leaf.NextPageId = newLeaf.PageId;

            InsertIntoParent(leaf, midKey, newLeaf.PageId);
        }

        // 内部节点分裂：中间键提升到父节点
        private void SplitInternal(BTreeIndexPage page)
        {
            int mid = page.Keys.Count / 2;
            int midKey = page.Keys[mid];

            var newPage = AllocPage(isLeaf: false, isRoot: false, parentId: page.ParentPageId);
            newPage.Keys.AddRange(page.Keys.Skip(mid + 1));
            newPage.Children.AddRange(page.Children.Skip(mid + 1));

            // 更新迁移子节点的父指针
            foreach (var cid in newPage.Children)
                _pages[cid].ParentPageId = newPage.PageId;

            page.Keys.RemoveRange(mid, page.Keys.Count - mid);
            page.Children.RemoveRange(mid + 1, page.Children.Count - (mid + 1));

            if (page.IsRoot)
            {
                // 根分裂：新建根
                var newRoot = AllocPage(isLeaf: false, isRoot: true);
                newRoot.Keys.Add(midKey);
                newRoot.Children.Add(page.PageId);
                newRoot.Children.Add(newPage.PageId);

                page.IsRoot = false;
                page.ParentPageId = newRoot.PageId;
                newPage.ParentPageId = newRoot.PageId;

                Root = newRoot;
            }
            else
            {
                InsertIntoParent(page, midKey, newPage.PageId);
            }
        }

        private void InsertIntoParent(BTreeIndexPage leftPage, int key, int rightPageId)
        {
            if (leftPage.IsRoot)
            {
                // 根叶子分裂
                var newRoot = AllocPage(isLeaf: false, isRoot: true);
                newRoot.Keys.Add(key);
                newRoot.Children.Add(leftPage.PageId);
                newRoot.Children.Add(rightPageId);

                leftPage.IsRoot = false;
                leftPage.ParentPageId = newRoot.PageId;
                _pages[rightPageId].ParentPageId = newRoot.PageId;

                Root = newRoot;
            }
            else
            {
                var parent = _pages[leftPage.ParentPageId];
                int idx = parent.Keys.BinarySearch(key);
                if (idx < 0) idx = ~idx;

                parent.Keys.Insert(idx, key);
                parent.Children.Insert(idx + 1, rightPageId);
                _pages[rightPageId].ParentPageId = parent.PageId;

                if (parent.Keys.Count > _maxKeys)
                    SplitInternal(parent);
            }
        }

        // ---------- 可视化 ----------
        public void PrintTree()
        {
            Console.WriteLine("=== B+Tree 索引结构 ===");
            PrintNode(Root, 0);
            Console.WriteLine();
        }

        private void PrintNode(BTreeIndexPage page, int indent)
        {
            string pad = new string(' ', indent * 2);
            if (page.IsLeaf)
            {
                string links = page.NextPageId.HasValue ? $" → NextLeaf(Page{page.NextPageId})" : "";
                Console.WriteLine($"{pad}Leaf[Page{page.PageId}] Keys=[{string.Join(",", page.Keys)}]{links}");
            }
            else
            {
                Console.WriteLine($"{pad}Internal[Page{page.PageId}] Keys=[{string.Join(",", page.Keys)}]");
                foreach (var cid in page.Children)
                    PrintNode(_pages[cid], indent + 1);
            }
        }
    }

    // ==================== 5. 表：数据页 + 索引页 ====================
    public class LiteTable
    {
        private readonly BTreeIndex _index;
        private readonly List<DataPage> _dataPages = new();
        private int _nextDataPageId = 0;
        private DataPage _currentPage;

        public LiteTable()
        {
            _index = new BTreeIndex(maxKeys: 3); // 设小一点，方便看分裂
            _currentPage = new DataPage { PageId = _nextDataPageId++ };
            _dataPages.Add(_currentPage);
        }

        public void Insert(int id, string name, int age)
        {
            // 1. BSON 编码
            var doc = new Dictionary<string, object> { ["_id"] = id, ["name"] = name, ["age"] = age };
            var bson = BsonCodec.Encode(doc);

            // 2. 写入数据页（模拟页满切换）
            if (_currentPage.Slots.Count >= 3)
            {
                _currentPage = new DataPage { PageId = _nextDataPageId++ };
                _dataPages.Add(_currentPage);
            }
            int slot = _currentPage.Insert(bson);
            var rowId = new RowId(_currentPage.PageId, slot);

            // 3. 插入索引
            _index.Insert(id, rowId);
        }

        public Dictionary<string, object>? SelectById(int id)
        {
            var rowId = _index.SearchWithTrace(id);
            if (!rowId.HasValue) return null;
            var page = _dataPages[rowId.Value.PageId];
            var bson = page.Select(rowId.Value.SlotIndex);
            return bson == null ? null : BsonCodec.Decode(bson);
        }

        public List<Dictionary<string, object>> RangeScan(int startId, int endId)
        {
            var rows = _index.RangeScan(startId, endId);
            var result = new List< Dictionary<string, object> > ();
            foreach (var rid in rows)
            {
                var bson = _dataPages[rid.PageId].Select(rid.SlotIndex);
                if (bson != null) result.Add(BsonCodec.Decode(bson));
            }
            return result;
        }

        public void PrintIndex() => _index.PrintTree();
    }

    // ==================== 6. 演示 ====================
    class Program
    {
        static void Main()
        {
            var table = new LiteTable();

            Console.WriteLine("=== 插入 1~7（maxKeys=3，会触发多层分裂）===\n");
            for (int i = 1; i <= 7; i++)
            {
                table.Insert(i, $"User{i}", 20 + i);
                Console.WriteLine($"已插入 _id={i}");
            }

            Console.WriteLine("\n=== 当前 B+Tree 结构 ===");
            table.PrintIndex();

            Console.WriteLine("=== 点查 _id=5 ===");
            var doc = table.SelectById(5);
            if (doc != null)
                Console.WriteLine($"结果: _id={doc["_id"]}, name={doc["name"]}, age={doc["age"]}\n");

            Console.WriteLine("=== 范围扫描 _id 2~6 ===");
            var range = table.RangeScan(2, 6);
            foreach (var d in range)
                Console.WriteLine($"  _id={d["_id"]}, name={d["name"]}, age={d["age"]}");
        }
    }
}