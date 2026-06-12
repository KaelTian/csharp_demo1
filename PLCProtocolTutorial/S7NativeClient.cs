namespace PLCProtocolTutorial
{
    using System;
    using System.Net.Sockets;
    public class S7NativeClient : IDisposable
    {
        private Socket _socket;
        private ushort _pduRef;
        private readonly int _rack;
        private readonly int _slot;

        public S7NativeClient(int rack = 0, int slot = 1)
        {
            _rack = rack;
            _slot = slot;
        }

        public bool IsConnected => _socket?.Connected ?? false;

        public void Connect(string ip)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.ReceiveTimeout = 5000;
            _socket.SendTimeout = 5000;
            _socket.Connect(ip, 102);

            // 1. COTP 握手
            SendCotpConnectionRequest();
            ReceiveCotpConnectionConfirm();

            // 2. S7 通信初始化
            SendS7CommSetup();
            ReceiveS7CommSetupAck();
        }

        public void Disconnect()
        {
            _socket?.Close();
            _socket = null;
        }

        public void Dispose() => Disconnect();

        #region 公共读写接口

        public float ReadReal(int db, int startByte)
        {
            byte[] raw = ReadBytes(db, startByte, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(raw);
            return BitConverter.ToSingle(raw, 0);
        }

        public void WriteReal(int db, int startByte, float value)
        {
            byte[] raw = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(raw);
            WriteBytes(db, startByte, raw);
        }

        public bool ReadBool(int db, int byteAddr, int bit)
        {
            byte[] raw = ReadBytes(db, byteAddr, 1);
            return (raw[0] & (1 << bit)) != 0;
        }

        public void WriteBool(int db, int byteAddr, int bit, bool value)
        {
            byte[] raw = ReadBytes(db, byteAddr, 1);
            if (value) raw[0] |= (byte)(1 << bit);
            else raw[0] &= (byte)~(1 << bit);
            WriteBytes(db, byteAddr, raw);
        }

        #endregion

        #region 核心协议实现

        public byte[] ReadBytes(int db, int startByte, int count)
        {
            if (count > 960) throw new ArgumentException("单次最多读取 960 字节");

            _pduRef++;
            byte[] item = BuildS7AnyItem(area: 0x84, db, startByte, bit: 0, transportSize: 0x02, length: count);

            byte[] param = new byte[2 + item.Length];
            param[0] = 0x04; // Read
            param[1] = 0x01; // ItemCount
            Array.Copy(item, 0, param, 2, item.Length);

            _socket.Send(BuildS7Packet(param, null));
            byte[] resp = ReceiveTpkt();

            const int s7Off = 7; // TPKT(4) + COTP(3)
            ThrowIfS7Error(resp, s7Off);

            int paramLen = (resp[s7Off + 6] << 8) | resp[s7Off + 7];
            int dataLen = (resp[s7Off + 8] << 8) | resp[s7Off + 9];
            int dataOff = s7Off + 10 + paramLen;

            if (dataLen == 0 || resp.Length < dataOff + 4)
                throw new Exception("响应中无数据");

            byte returnCode = resp[dataOff];
            byte transSize = resp[dataOff + 1];
            int lengthBits = (resp[dataOff + 2] << 8) | resp[dataOff + 3];

            if (returnCode != 0xFF)
                throw new Exception($"PLC 数据项错误: 0x{returnCode:X2}");

            int byteCount = (transSize == 0x09) ? lengthBits : lengthBits / 8;
            byte[] result = new byte[Math.Min(count, byteCount)];
            Array.Copy(resp, dataOff + 4, result, 0, result.Length);
            return result;
        }

        public void WriteBytes(int db, int startByte, byte[] data)
        {
            if (data.Length > 960) throw new ArgumentException("单次最多写入 960 字节");

            _pduRef++;
            byte[] item = BuildS7AnyItem(area: 0x84, db, startByte, bit: 0, transportSize: 0x02, length: data.Length);

            byte[] param = new byte[2 + item.Length];
            param[0] = 0x05; // Write
            param[1] = 0x01;
            Array.Copy(item, 0, param, 2, item.Length);

            byte[] s7Data = new byte[4 + data.Length];
            s7Data[0] = 0x00;
            s7Data[1] = 0x02; // BYTE
            s7Data[2] = (byte)((data.Length * 8) >> 8);
            s7Data[3] = (byte)((data.Length * 8) & 0xFF);
            Array.Copy(data, 0, s7Data, 4, data.Length);

            _socket.Send(BuildS7Packet(param, s7Data));
            byte[] resp = ReceiveTpkt();

            const int s7Off = 7;
            ThrowIfS7Error(resp, s7Off);

            int paramLen = (resp[s7Off + 6] << 8) | resp[s7Off + 7];
            if (paramLen >= 3 && resp[s7Off + 10 + 2] != 0x00)
                throw new Exception($"写入被拒绝: 0x{resp[s7Off + 10 + 2]:X2}");
        }

        #endregion

        #region 协议层构造

        private void SendCotpConnectionRequest()
        {
            byte calledTsapLow = (byte)((_rack << 5) | _slot);

            byte[] packet = new byte[]
            {
                // TPKT (4 bytes)
                0x03, 0x00, 0x00, 0x16,
                // COTP CR (18 bytes)
                0x11,                   // COTP Header Length
                0xe0,                   // PDU Type = CR
                0x00, 0x00,             // Destination Reference
                0x00, 0x01,             // Source Reference
                0x00,                   // Class / Options
                // Calling TSAP (Source)
                0xc1, 0x02, 0x01, 0x00,
                // Called TSAP (Destination: PLC)
                0xc2, 0x02, 0x01, calledTsapLow,
                // TPDU Size (1024)
                0xc0, 0x01, 0x0a
            };
            _socket.Send(packet);
        }

        private void ReceiveCotpConnectionConfirm()
        {
            byte[] resp = ReceiveTpkt();
            Console.WriteLine($"[COTP CC] 原始报文 ({resp.Length} bytes): {HexDump(resp)}");

            if (resp.Length < 7)
                throw new Exception($"COTP 响应过短: {resp.Length} bytes");

            // TPKT(4) + COTP Length(1) + COTP Type(1) = index 5
            byte cotpType = resp[5];

            // 0x0d = CC (Connection Confirm), 0x06 = DR (Disconnect Request/拒绝)
            if (cotpType == 0x06)
                throw new Exception($"COTP 连接被 PLC 拒绝 (DR)。请检查: 1) Rack/Slot 是否正确 2) PLC 是否允许 PUT/GET 3) IP/端口是否可达。当前 TSAP=0x01{(byte)((_rack << 5) | _slot):X2}");

            if (cotpType != 0x0d)
                throw new Exception($"COTP 响应类型异常: 0x{cotpType:X2} (期望 0x0d)");
        }

        private void SendS7CommSetup()
        {
            _pduRef++;
            byte[] param = new byte[]
            {
                0xf0,       // Setup Communication
                0x00,
                0x00, 0x01, // Max AMQ Caller
                0x00, 0x01, // Max AMQ Callee
                0x03, 0xc0  // PDU Size = 960
            };
            _socket.Send(BuildS7Packet(param, null));
        }

        private void ReceiveS7CommSetupAck()
        {
            byte[] resp = ReceiveTpkt();
            const int s7Off = 7;
            ThrowIfS7Error(resp, s7Off);
            Console.WriteLine($"[S7 CommSetup] 成功，协商 PDU 大小完成");
        }

        private static byte[] BuildS7AnyItem(byte area, int dbNumber, int startByte, int bit, byte transportSize, int length)
        {
            int address = startByte * 8 + bit;
            return new byte[]
            {
                0x12, 0x0a, 0x10,
                transportSize,
                (byte)(length >> 8), (byte)(length & 0xFF),
                (byte)(dbNumber >> 8), (byte)(dbNumber & 0xFF),
                area,
                (byte)((address >> 16) & 0xFF),
                (byte)((address >> 8)  & 0xFF),
                (byte)(address & 0xFF)
            };
        }

        private byte[] BuildS7Packet(byte[] param, byte[] data)
        {
            int pLen = param?.Length ?? 0;
            int dLen = data?.Length ?? 0;
            int s7Len = 10 + pLen + dLen;
            int total = 4 + 3 + s7Len;

            byte[] pkt = new byte[total];
            int i = 0;

            // TPKT
            pkt[i++] = 0x03; pkt[i++] = 0x00;
            pkt[i++] = (byte)(total >> 8); pkt[i++] = (byte)(total & 0xFF);

            // COTP DT
            pkt[i++] = 0x02; pkt[i++] = 0xf0; pkt[i++] = 0x80;

            // S7 Header
            pkt[i++] = 0x32; pkt[i++] = 0x01; // Job
            pkt[i++] = 0x00; pkt[i++] = 0x00;
            pkt[i++] = (byte)(_pduRef >> 8); pkt[i++] = (byte)(_pduRef & 0xFF);
            pkt[i++] = (byte)(pLen >> 8); pkt[i++] = (byte)(pLen & 0xFF);
            pkt[i++] = (byte)(dLen >> 8); pkt[i++] = (byte)(dLen & 0xFF);

            if (pLen > 0) { Array.Copy(param, 0, pkt, i, pLen); i += pLen; }
            if (dLen > 0) { Array.Copy(data, 0, pkt, i, dLen); }

            return pkt;
        }

        #endregion

        #region 底层收发 & 工具

        private byte[] ReceiveTpkt()
        {
            byte[] header = new byte[4];
            int read = 0;
            while (read < 4)
                read += _socket.Receive(header, read, 4 - read, SocketFlags.None);

            int length = (header[2] << 8) | header[3];
            byte[] packet = new byte[length];
            Array.Copy(header, packet, 4);

            read = 4;
            while (read < length)
                read += _socket.Receive(packet, read, length - read, SocketFlags.None);

            return packet;
        }

        private static void ThrowIfS7Error(byte[] resp, int s7Off)
        {
            if (resp.Length < s7Off + 10)
                throw new Exception("S7 响应过短");

            if (resp[s7Off] != 0x32 || resp[s7Off + 1] != 0x03)
                throw new Exception("非法 S7 响应");

            byte errClass = resp[s7Off + 17];
            byte errCode = resp[s7Off + 18];
            if (errClass != 0 || errCode != 0)
                throw new Exception($"PLC 报错: Class={errClass}, Code={errCode}");
        }

        private static string HexDump(byte[] data)
        {
            return string.Join(" ", data.Select(b => b.ToString("X2")));
        }

        #endregion
    }
}
