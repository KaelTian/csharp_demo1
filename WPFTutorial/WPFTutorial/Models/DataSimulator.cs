using System.Timers;
using Timer = System.Timers.Timer;

namespace WPFTutorial.Models;

public class DataSimulator : IDisposable
{
    private readonly Timer _timer;
    private readonly Random _random = new();

    public event Action? DataUpdated;

    public double Temperature { get; private set; } = 25.0;
    public double Pressure { get; private set; } = 0.5;
    public double FlowRate { get; private set; } = 30.0;
    public double TankLevel { get; private set; } = 65.0;
    public bool ValveOpen { get; private set; } = true;
    public bool MotorRunning { get; private set; } = true;
    public double MotorSpeed { get; private set; } = 1450.0;
    public double PumpSpeed { get; private set; } = 1200.0;
    public bool LedOn { get; private set; } = true;
    public bool AlarmActive { get; private set; }
    public string StatusText { get; private set; } = "正常运行";
    public double TrendValue { get; private set; } = 50.0;

    private int _tick;

    public DataSimulator()
    {
        _timer = new Timer(200);
        _timer.Elapsed += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        _tick++;

        Temperature = 25.0 + Math.Sin(_tick * 0.05) * 8 + _random.NextDouble() * 2;
        Pressure = 0.5 + Math.Sin(_tick * 0.03) * 0.3 + _random.NextDouble() * 0.1;
        FlowRate = 30.0 + Math.Sin(_tick * 0.04) * 10 + _random.NextDouble() * 3;
        TankLevel = 65.0 + Math.Sin(_tick * 0.02) * 15 + _random.NextDouble() * 2;
        MotorSpeed = 1450.0 + Math.Sin(_tick * 0.01) * 30;
        PumpSpeed = 1200.0 + Math.Sin(_tick * 0.015) * 50;
        LedOn = (_tick / 10) % 2 == 0;
        AlarmActive = Temperature > 38.0 || Pressure > 1.0;
        StatusText = AlarmActive ? "警报：超出范围" : "正常运行";
        TrendValue = 50.0 + Math.Sin(_tick * 0.08) * 20 + Math.Sin(_tick * 0.02) * 10;

        DataUpdated?.Invoke();
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
