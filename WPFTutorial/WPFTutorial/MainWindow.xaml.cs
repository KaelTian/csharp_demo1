using System.Windows;
using System.Windows.Media;
using S7.Net;
using WPFTutorial.Controls;
using WPFTutorial.Models;
using WPFTutorial.Services;

namespace WPFTutorial;

public partial class MainWindow : Window
{
    // Simulator for demo mode
    private readonly DataSimulator _simulator = new();
    private readonly System.Timers.Timer _clockTimer;
    private int _logCount;

    // PLC service
    private readonly PlcDataService _plcService = new();
    private readonly PlcConnectionConfig _plcConfig = new();
    private bool _plcConnected;

    public MainWindow()
    {
        InitializeComponent();

        // Set up PLC tag definitions for DB5
        SetupPlcTags();

        // Wire up PLC events
        _plcService.DataUpdated += OnPlcDataUpdated;
        _plcService.ConnectionChanged += OnPlcConnectionChanged;

        // Simulator for demo / fallback
        _simulator.DataUpdated += OnSimulatorUpdated;

        _clockTimer = new System.Timers.Timer(1000);
        _clockTimer.Elapsed += (_, _) => Dispatcher.Invoke(() =>
        {
            HeaderTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        });
        _clockTimer.Start();

        AddLog("系统启动完成 — 仿真模式运行中，点击「连接 PLC」切换实盘模式");
    }

    private void SetupPlcTags()
    {
        var tags = new List<PlcTagDefinition>
        {
            new() { Name = "主机编码步数", Offset = "0.0",  Type = "REAL", DbNumber = 5 },
            new() { Name = "进料台编码步数", Offset = "4.0",  Type = "REAL", DbNumber = 5 },
            new() { Name = "进料台安全步数", Offset = "12.0", Type = "REAL", DbNumber = 5 },
            new() { Name = "进料预行步数", Offset = "20.0", Type = "REAL", DbNumber = 5 },
            new() { Name = "进料步数", Offset = "24.0",  Type = "REAL", DbNumber = 5 },
            new() { Name = "总步数", Offset = "28.0",  Type = "REAL", DbNumber = 5 },
            new() { Name = "出料步数", Offset = "44.0",  Type = "REAL", DbNumber = 5 },
            new() { Name = "上毛刷启动步数", Offset = "48.0", Type = "REAL", DbNumber = 5 },
            new() { Name = "上毛刷停止步数", Offset = "52.0", Type = "REAL", DbNumber = 5 },
            new() { Name = "下毛刷停止步数", Offset = "60.0", Type = "REAL", DbNumber = 5 },
        };

        _plcService.Configure(tags, _plcConfig);
        plcTagList.ItemsSource = _plcService.TagValues;
    }

    // ===== PLC Connection =====

    private async void PlcConnect_Click(object sender, RoutedEventArgs e)
    {
        if (_plcConnected)
        {
            _plcService.Disconnect();
            return;
        }

        // Read config from UI
        _plcConfig.IpAddress = plcIpBox.Text.Trim();
        _plcConfig.Rack = int.TryParse(plcRackBox.Text.Trim(), out var rack) ? rack : 0;
        _plcConfig.Slot = int.TryParse(plcSlotBox.Text.Trim(), out var slot) ? slot : 1;

        plcConnectBtn.IsEnabled = false;
        plcConnectBtn.Content = "连接中...";
        plcStatusText.Text = "正在连接...";
        plcStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 200, 0));

        await _plcService.ConnectAsync();

        plcConnectBtn.IsEnabled = true;

        if (_plcService.IsConnected)
        {
            _plcConnected = true;
            plcConnectBtn.Content = "断开 PLC";
            plcConnectBtn.Background = new SolidColorBrush(Color.FromRgb(40, 30, 30));
            plcConnectBtn.Foreground = new SolidColorBrush(Color.FromRgb(255, 120, 120));
            plcConnectBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(80, 40, 40));
            AddLog($"PLC 已连接 — {_plcConfig.IpAddress}:{_plcConfig.Rack}/{_plcConfig.Slot}");
        }
        else
        {
            _plcConnected = false;
            plcConnectBtn.Content = "连接 PLC";
            plcConnectBtn.Background = new SolidColorBrush(Color.FromRgb(26, 58, 42));
            plcConnectBtn.Foreground = new SolidColorBrush(Color.FromRgb(128, 208, 160));
            plcConnectBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(42, 90, 58));
            plcStatusText.Text = $"失败: {_plcService.LastError}";
            AddLog($"⚠ PLC 连接失败: {_plcService.LastError}");
        }
    }

    private void OnPlcConnectionChanged(bool connected)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnPlcConnectionChanged(connected));
            return;
        }

        var green = Color.FromRgb(80, 220, 120);
        var red = Color.FromRgb(220, 80, 80);
        var gray = Color.FromRgb(96, 112, 128);

        plcStatusDot.Fill = connected
            ? new SolidColorBrush(green)
            : new SolidColorBrush(gray);

        plcStatusText.Text = connected ? "已连接" : "未连接";
        plcStatusText.Foreground = connected
            ? new SolidColorBrush(green)
            : new SolidColorBrush(gray);

        ledComm.IsOn = connected;

        if (!connected)
        {
            _plcConnected = false;
            plcConnectBtn.Content = "连接 PLC";
            plcConnectBtn.Background = new SolidColorBrush(Color.FromRgb(26, 58, 42));
            plcConnectBtn.Foreground = new SolidColorBrush(Color.FromRgb(128, 208, 160));
            plcConnectBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(42, 90, 58));
        }
    }

    private void OnPlcDataUpdated()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(OnPlcDataUpdated);
            return;
        }

        // Force ListView refresh
        plcTagList.Items.Refresh();

        // Map PLC values to controls
        var totalSteps = _plcService.GetDoubleValue("总步数");
        var mainEncoder = _plcService.GetDoubleValue("主机编码步数");
        var feedSteps = _plcService.GetDoubleValue("进料步数");
        var brushStart = _plcService.GetDoubleValue("上毛刷启动步数");
        var brushStop = _plcService.GetDoubleValue("上毛刷停止步数");

        // Map to existing controls
        progressRing.Percentage = totalSteps > 0 ? Math.Min(totalSteps * 0.01, 100) : 0;
        motor.Speed = mainEncoder * 0.1;
        flowMeter.FlowRate = feedSteps * 0.5;
        tank.Level = totalSteps > 0 ? Math.Min(totalSteps * 0.005, 100) : 0;

        // Update status bar with PLC mode
        statusBarRight.Text = $"PLC: {_plcConfig.IpAddress} | DB5 | 刷新率: {_plcConfig.PollIntervalMs}ms";

        // Update LED based on data quality
        var allGood = _plcService.TagValues.All(t => t.IsConnected);
        ledRun.IsOn = allGood;
    }

    // ===== Simulator (fallback) =====

    private void OnSimulatorUpdated()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(OnSimulatorUpdated);
            return;
        }

        // Only update from simulator when PLC is not connected
        if (_plcService.IsConnected) return;

        tempGauge.Temperature = _simulator.Temperature;
        pressGauge.Pressure = _simulator.Pressure;
        flowMeter.FlowRate = _simulator.FlowRate;

        tank.Level = _simulator.TankLevel;
        pump.Speed = _simulator.PumpSpeed;
        motor.Speed = _simulator.MotorSpeed;
        valve.IsOpen = _simulator.ValveOpen;
        pipe.HasFlow = _simulator.ValveOpen;

        trendChart.NewValue = _simulator.TrendValue;
        progressRing.Percentage = _simulator.TankLevel;

        ledPower.IsOn = _simulator.LedOn;
        ledRun.IsOn = _simulator.MotorRunning;

        alarmLight.IsAlarm = _simulator.AlarmActive;

        statusSystem.StatusText = _simulator.StatusText;
        statusSystem.Status = _simulator.AlarmActive ? StatusType.Alarm : StatusType.Normal;

        statusProcess.StatusText = _simulator.MotorRunning ? "运行中" : "已停止";
        statusProcess.Status = _simulator.MotorRunning ? StatusType.Normal : StatusType.Offline;

        statusAlarmCtrl.StatusText = _simulator.AlarmActive ? "有报警!" : "无报警";
        statusAlarmCtrl.Status = _simulator.AlarmActive ? StatusType.Alarm : StatusType.Normal;

        statusBarText.Text = _simulator.AlarmActive ? "⚠ 系统报警中" : "● 系统运行中";
        statusBullet.Fill = _simulator.AlarmActive ? Brushes.Red : Brushes.LimeGreen;

        tempValueText.Text = $"{_simulator.Temperature:F1} °C";
        pressValueText.Text = $"{_simulator.Pressure:F2} MPa";
        flowValueText.Text = $"{_simulator.FlowRate:F1} L/min";

        if (_simulator.AlarmActive && _logCount % 5 == 0)
        {
            var msg = _simulator.Temperature > 38
                ? $"温度过高: {_simulator.Temperature:F1}°C"
                : $"压力过高: {_simulator.Pressure:F2}MPa";
            AddLog($"⚠ 报警: {msg}");
        }

        _logCount++;
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var lines = eventLog.Text.Split('\n');
        var newText = $"[{timestamp}] {message}\n" + string.Join("\n", lines.Take(5));
        eventLog.Text = newText;
    }

    protected override void OnClosed(EventArgs e)
    {
        _simulator.Dispose();
        _plcService.Dispose();
        _clockTimer.Dispose();
        base.OnClosed(e);
    }
}
