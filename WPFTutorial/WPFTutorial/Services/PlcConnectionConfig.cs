using S7.Net;

namespace WPFTutorial.Services;

public class PlcConnectionConfig
{
    public string IpAddress { get; set; } = "192.168.0.1";
    public int Rack { get; set; } = 0;
    public int Slot { get; set; } = 1;
    public CpuType CpuType { get; set; } = CpuType.S71500;
    public int TimeoutMs { get; set; } = 3000;
    public int PollIntervalMs { get; set; } = 500;
}
