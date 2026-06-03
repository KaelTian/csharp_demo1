namespace PLCProtocolTutorial
{
    using System;
    using System.Net.Sockets;
    /// <summary>
    /// 原生 S7 协议客户端（ISO-on-TCP / RFC1006）
    /// 支持：S7-1200 / S7-1500 / S7-300 / S7-400
    /// </summary>
    public class S7NativeClient : IDisposable
    {
        private Socket? _socket;
        private ushort _pduRef;
        private readonly int _rack;
        private readonly int _slot;

        /// <param name="rack">机架号，S7-1200/1500 通常为 0</param>
        /// <param name="slot">插槽号，S7-1200/1500 通常为 1；S7-300/400 通常为 2</param>
        public S7NativeClient(int rack = 0, int slot = 1)
        {
            _rack = rack;
            _slot = slot;
        }

        public bool IsConnected => _socket?.Connected ?? false;

        #region 连接与断开

        public void Connect(string ip)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(ip, 102);

            // 1. ISO-on-TCP (COTP) 连接建立
            SendCotpConnectionRequest();
            ReceiveCotpConnectionConfirm();

            // 2. S7 通信初始化（协商 PDU 大小）
            SendS7CommSetup();
            ReceiveS7CommSetupAck();
        }

        public void Disconnect()
        {
            _socket?.Close();
            _socket = null;
        }

        public void Dispose() => Disconnect();

        #endregion

        #region 公共读写接口

        /// <summary>读取 Real（4 字节），如 DB72.DBD2</summary>
        public float ReadReal(int db, int startByte)
        {
            byte[] raw = ReadBytes(db, startByte, 4);
            // S7 是大端序，PC 通常是小端序，需要翻转
            if (BitConverter.IsLittleEndian) Array.Reverse(raw);
            return BitConverter.ToSingle(raw, 0);
        }

        /// <summary>写入 Real（4 字节），如 DB72.DBD2</summary>
        public void WriteReal(int db, int startByte, float value)
        {
            byte[] raw = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(raw);
            WriteBytes(db, startByte, raw);
        }

        /// <summary>读取 Bool，如 DB72.DBX0.0</summary>
        public bool ReadBool(int db, int byteAddr, int bit)
        {
            byte[] raw = ReadBytes(db, byteAddr, 1);
            return (raw[0] & (1 << bit)) != 0;
        }

        /// <summary>写入 Bool，如 DB72.DBX0.0</summary>
        /// <remarks>采用"读字节→改位→写字节"策略，避免覆盖同字节其他位</remarks>
        public void WriteBool(int db, int byteAddr, int bit, bool value)
        {
            byte[] raw = ReadBytes(db, byteAddr, 1);
            if (value)
                raw[0] |= (byte)(1 << bit);
            else
                raw[0] &= (byte)~(1 << bit);
            WriteBytes(db, byteAddr, raw);
        }

        #endregion

        #region 核心协议：字节级读写

        /// <summary>从 DB 块读取原始字节</summary>
        public byte[] ReadBytes(int db, int startByte, int count)
        {
            if (count > 960) throw new ArgumentException("单次最多读取 960 字节");

            _pduRef++;
            byte[] item = BuildS7AnyItem(area: 0x84, db, startByte, bit: 0, transportSize: 0x02, length: count);

            // Parameter: Function=0x04(Read), ItemCount=1
            byte[] param = new byte[2 + item.Length];
            param[0] = 0x04;
            param[1] = 0x01;
            Array.Copy(item, 0, param, 2, item.Length);

            _socket?.Send(BuildS7Packet(param, null));
            byte[] resp = ReceiveTpkt();

            // ---- 解析 S7 响应 ----
            const int s7Offset = 7; // 跳过 TPKT(4) + COTP(3)
            ThrowIfS7Error(resp, s7Offset);

            int paramLen = (resp[s7Offset + 6] << 8) | resp[s7Offset + 7];
            int dataLen = (resp[s7Offset + 8] << 8) | resp[s7Offset + 9];
            int dataOff = s7Offset + 10 + paramLen;

            if (dataLen == 0 || resp.Length < dataOff + 4)
                throw new Exception("响应中无数据");

            byte returnCode = resp[dataOff];
            byte transSize = resp[dataOff + 1];
            int lengthBits = (resp[dataOff + 2] << 8) | resp[dataOff + 3];

            if (returnCode != 0xFF) // 0xFF = Success
                throw new Exception($"PLC 数据项错误: 0x{returnCode:X2}");

            // 0x04~0x08 长度按位计算；0x09(Octet String) 按字节计算
            int byteCount = (transSize == 0x09) ? lengthBits : lengthBits / 8;

            byte[] result = new byte[Math.Min(count, byteCount)];
            Array.Copy(resp, dataOff + 4, result, 0, result.Length);
            return result;
        }

        /// <summary>向 DB 块写入原始字节</summary>
        public void WriteBytes(int db, int startByte, byte[] data)
        {
            if (data.Length > 960) throw new ArgumentException("单次最多写入 960 字节");

            _pduRef++;
            byte[] item = BuildS7AnyItem(area: 0x84, db, startByte, bit: 0, transportSize: 0x02, length: data.Length);

            // Parameter: Function=0x05(Write), ItemCount=1
            byte[] param = new byte[2 + item.Length];
            param[0] = 0x05;
            param[1] = 0x01;
            Array.Copy(item, 0, param, 2, item.Length);

            // Data Section: ReturnCode(0x00) + TransportSize(0x02=BYTE) + Length(位) + Data
            byte[] s7Data = new byte[4 + data.Length];
            s7Data[0] = 0x00;
            s7Data[1] = 0x02;
            s7Data[2] = (byte)((data.Length * 8) >> 8);
            s7Data[3] = (byte)((data.Length * 8) & 0xFF);
            Array.Copy(data, 0, s7Data, 4, data.Length);

            _socket?.Send(BuildS7Packet(param, s7Data));
            byte[] resp = ReceiveTpkt();

            const int s7Offset = 7;
            ThrowIfS7Error(resp, s7Offset);

            // Write 响应的 Parameter 里包含每个 Item 的 ReturnCode
            int paramLen = (resp[s7Offset + 6] << 8) | resp[s7Offset + 7];
            if (paramLen >= 3 && resp[s7Offset + 10 + 2] != 0x00)
                throw new Exception($"写入被拒绝: 0x{resp[s7Offset + 10 + 2]:X2}");
        }

        #endregion

        #region 协议层构造

        /// <summary>发送 COTP Connection Request（ISO-on-TCP 握手）</summary>
        private void SendCotpConnectionRequest()
        {
            // Called TSAP (目标): 0x01 0x??  其中低字节 = (rack << 5) | slot
            byte calledTsapLow = (byte)((_rack << 5) | _slot);

            byte[] packet = new byte[]
            {
                // TPKT Header (4 bytes)
                0x03, 0x00, 0x00, 0x16,
                // COTP CR (18 bytes)
                0x11,                   // COTP Header Length
                0xe0,                   // PDU Type = CR (Connection Request)
                0x00, 0x00,             // Destination Reference
                0x00, 0x01,             // Source Reference
                0x00,                   // Class / Options
                // Calling TSAP (Source: PG/PC)
                0xc1, 0x02, 0x01, 0x00,
                // Called TSAP (Destination: PLC)
                0xc2, 0x02, 0x01, calledTsapLow,
                // TPDU Size (1024 bytes)
                0xc0, 0x01, 0x0a
            };
            _socket?.Send(packet);
        }

        /// <summary>接收 COTP Connection Confirm</summary>
        private void ReceiveCotpConnectionConfirm()
        {
            byte[] resp = ReceiveTpkt();
            // COTP PDU Type 位于 TPKT 后第 2 字节 (index 5)，应为 0x0d (CC)
            if (resp.Length < 7 || resp[5] != 0x0d)
                throw new Exception("COTP 连接确认失败");
        }

        /// <summary>发送 S7 Communication Setup（协商 PDU 大小）</summary>
        private void SendS7CommSetup()
        {
            _pduRef++;
            byte[] param = new byte[]
            {
                0xf0,       // Function: Setup Communication
                0x00,       // Reserved
                0x00, 0x01, // Max AMQ Caller
                0x00, 0x01, // Max AMQ Callee
                0x03, 0xc0  // PDU Size = 960
            };
            _socket?.Send(BuildS7Packet(param, null));
        }

        private void ReceiveS7CommSetupAck()
        {
            byte[] resp = ReceiveTpkt();
            const int s7Offset = 7;
            ThrowIfS7Error(resp, s7Offset);
        }

        /// <summary>构造 S7ANY 地址项（12 字节）</summary>
        /// <param name="area">0x84=DB, 0x81=Output, 0x82=Input, 0x83=Flag</param>
        /// <param name="transportSize">0x01=BIT, 0x02=BYTE</param>
        private static byte[] BuildS7AnyItem(byte area, int dbNumber, int startByte, int bit, byte transportSize, int length)
        {
            int address = startByte * 8 + bit; // S7 地址以位为单位
            return new byte[]
            {
                0x12, 0x0a, 0x10,               // Specification Type + Length + Syntax ID (S7ANY)
                transportSize,                    // Transport Size
                (byte)(length >> 8), (byte)(length & 0xFF), // Length
                (byte)(dbNumber >> 8), (byte)(dbNumber & 0xFF), // DB Number (Big Endian)
                area,                             // Area Code
                (byte)((address >> 16) & 0xFF),   // Address (Big Endian, 3 bytes)
                (byte)((address >> 8)  & 0xFF),
                (byte)(address & 0xFF)
            };
        }

        /// <summary>组装完整 S7 报文：TPKT + COTP + S7Header + Parameter + Data</summary>
        private byte[] BuildS7Packet(byte[] param, byte[]? data)
        {
            int paramLen = param?.Length ?? 0;
            int dataLen = data?.Length ?? 0;
            int s7Len = 10 + paramLen + dataLen;
            int totalLen = 4 + 3 + s7Len; // TPKT + COTP_DT + S7

            byte[] pkt = new byte[totalLen];
            int i = 0;

            // ---- TPKT (RFC 1006) ----
            pkt[i++] = 0x03; // Version
            pkt[i++] = 0x00; // Reserved
            pkt[i++] = (byte)(totalLen >> 8);
            pkt[i++] = (byte)(totalLen & 0xFF);

            // ---- COTP Data Transfer ----
            pkt[i++] = 0x02; // Length
            pkt[i++] = 0xf0; // PDU Type = DT (Data)
            pkt[i++] = 0x80; // Credit / Last Unit

            // ---- S7 Header (10 bytes) ----
            pkt[i++] = 0x32;                  // Protocol ID
            pkt[i++] = 0x01;                  // Message Type = Job Request
            pkt[i++] = 0x00; pkt[i++] = 0x00; // Reserved
            pkt[i++] = (byte)(_pduRef >> 8);  // PDU Reference
            pkt[i++] = (byte)(_pduRef & 0xFF);
            pkt[i++] = (byte)(paramLen >> 8); // Parameter Length
            pkt[i++] = (byte)(paramLen & 0xFF);
            pkt[i++] = (byte)(dataLen >> 8);  // Data Length
            pkt[i++] = (byte)(dataLen & 0xFF);

            // ---- Parameter & Data ----
            if (paramLen > 0) { Array.Copy(param!, 0, pkt, i, paramLen); i += paramLen; }
            if (dataLen > 0) { Array.Copy(data!, 0, pkt, i, dataLen); }

            return pkt;
        }

        #endregion

        #region 底层收发

        /// <summary>接收一个完整的 TPKT 包（先读 4 字节头，再读剩余长度）</summary>
        private byte[] ReceiveTpkt()
        {
            byte[] header = new byte[4];
            int read = 0;
            while (read < 4)
                read += _socket!.Receive(header, read, 4 - read, SocketFlags.None);

            int length = (header[2] << 8) | header[3];
            byte[] packet = new byte[length];
            Array.Copy(header, packet, 4);

            read = 4;
            while (read < length)
                read += _socket!.Receive(packet, read, length - read, SocketFlags.None);

            return packet;
        }

        /// <summary>检查 S7 响应头中的错误码</summary>
        private static void ThrowIfS7Error(byte[] resp, int s7Offset)
        {
            if (resp.Length < s7Offset + 10)
                throw new Exception("S7 响应过短");

            byte protoId = resp[s7Offset];
            byte msgType = resp[s7Offset + 1];
            if (protoId != 0x32 || msgType != 0x03) // 0x03 = Ack-Data
                throw new Exception("非法的 S7 响应");

            byte errClass = resp[s7Offset + 17]; // Error Class
            byte errCode = resp[s7Offset + 18]; // Error Code
            if (errClass != 0 || errCode != 0)
                throw new Exception($"PLC 报错: Class={errClass}, Code={errCode}");
        }

        #endregion
    }
}
