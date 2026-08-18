using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CrispMic;

public partial class MainForm : Form
{
    private readonly AppConfig _config;
    private readonly AudioEngine _engine;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _uiTimer;
    private readonly Icon _appIcon;

    // UI Controls
    private ModernComboBox _cboInput = null!;
    private ModernComboBox _cboOutput = null!;
    private Button _btnMonitor = null!;
    private Button _btnMute = null!;

    private CrispSlider _sliderInputGain = null!;
    private Label _lblInputGainVal = null!;
    private CrispSlider _sliderOutputGain = null!;
    private Label _lblOutputGainVal = null!;

    private CrispSwitch _swDenoise = null!;
    private CrispSwitch _swHardReduce = null!;
    private CrispSlider _sliderHardReduce = null!;
    private Label _lblHardReduceVal = null!;
    private Label _lblVadStatus = null!;

    private CrispSlider _sliderBass = null!;
    private Label _lblBassVal = null!;
    private CrispSlider _sliderMid = null!;
    private Label _lblMidVal = null!;
    private CrispSlider _sliderTreble = null!;
    private Label _lblTrebleVal = null!;

    private LivePeakMeter _peakMeter = null!;
    private CrispSwitch _swStartup = null!;
    private Label _lblStatusDot = null!;
    private Label _lblStatusText = null!;

    public MainForm(bool startMinimized)
    {
        _config = AppConfig.Load();
        _engine = new AudioEngine(_config);
        _engine.OnError += OnEngineError;

        _appIcon = IconHelper.CreateAppIcon();
        Icon = _appIcon;

        InitializeComponent();

        _trayIcon = CreateTrayIcon();

        _uiTimer = new System.Windows.Forms.Timer { Interval = 33 }; // 30 FPS
        _uiTimer.Tick += OnUiTimerTick;

        LoadDevicesAndSettings();

        _engine.Start();
        UpdateStatusDisplay();

        if (startMinimized)
        {
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            Hide();
        }
        else
        {
            _uiTimer.Start();
        }
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "CrispMic";
        ClientSize = new Size(482, 734);
        MinimumSize = new Size(498, 773);
        MaximumSize = new Size(498, 773);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = CrispTheme.BgDark;
        ForeColor = CrispTheme.TextWhite;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScroll = false;

        // 1. Top Header Bar
        var pnlHeader = new Panel
        {
            Location = new Point(18, 14),
            Size = new Size(446, 38),
            BackColor = Color.Transparent
        };

        // Logo
        var pnlLogo = new Panel { Location = new Point(0, 2), Size = new Size(170, 32), BackColor = Color.Transparent };
        pnlLogo.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var wavePen = new Pen(CrispTheme.AccentPrimary, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(wavePen, 4, 12, 4, 20);
            g.DrawLine(wavePen, 10, 6, 10, 26);
            g.DrawLine(wavePen, 16, 2, 16, 30);
            g.DrawLine(wavePen, 22, 10, 22, 22);

            using var font = new Font("Segoe UI", 12.5f, FontStyle.Bold);
            using var brush = new SolidBrush(CrispTheme.TextWhite);
            g.DrawString("CRISP", font, brush, 30, 3);

            using var micBrush = new SolidBrush(CrispTheme.AccentLight);
            g.DrawString("MIC", font, micBrush, 88, 3);
        };

        _btnMonitor = new Button
        {
            Text = _config.MonitoringEnabled ? "MONITOR: ON" : "MONITOR: OFF",
            Size = new Size(115, 30),
            Location = new Point(230, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = _config.MonitoringEnabled ? CrispTheme.AccentBadgeBg : CrispTheme.CardInner,
            ForeColor = _config.MonitoringEnabled ? CrispTheme.AccentLight : CrispTheme.TextSecondary,
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnMonitor.FlatAppearance.BorderColor = _config.MonitoringEnabled ? CrispTheme.AccentPrimary : CrispTheme.CardBorder;
        _btnMonitor.Click += (s, e) => ToggleMonitor();

        _btnMute = new Button
        {
            Text = _config.Muted ? "UNMUTE" : "MUTE",
            Size = new Size(85, 30),
            Location = new Point(355, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = _config.Muted ? CrispTheme.AccentRed : CrispTheme.CardInner,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnMute.FlatAppearance.BorderColor = CrispTheme.CardBorder;
        _btnMute.Click += (s, e) => ToggleMute();

        pnlHeader.Controls.AddRange(new Control[] { pnlLogo, _btnMonitor, _btnMute });
        Controls.Add(pnlHeader);

        // 2. Output Peak Meter
        var pnlMeterCard = new ModernCardPanel
        {
            Location = new Point(18, 58),
            Size = new Size(446, 62),
            Title = "Live Output Signal Level",
            Badge = "Soft-Limiter Protected"
        };
        _peakMeter = new LivePeakMeter { Location = new Point(16, 26), Size = new Size(414, 24) };
        pnlMeterCard.Controls.Add(_peakMeter);
        Controls.Add(pnlMeterCard);

        // 3. Audio Routing Card
        var pnlRouting = new ModernCardPanel
        {
            Location = new Point(18, 126),
            Size = new Size(446, 126),
            Title = "Audio Routing",
            Badge = "WASAPI 10ms"
        };

        var lblIn = new Label { Text = "Input Device:", Location = new Point(16, 32), AutoSize = true, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), ForeColor = CrispTheme.TextSecondary };
        _cboInput = CreateStyledComboBox(120, 28, 310);
        _cboInput.SelectedIndexChanged += (s, e) => OnDeviceSelectionChanged();

        var lblOut = new Label { Text = "Output Target:", Location = new Point(16, 68), AutoSize = true, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), ForeColor = CrispTheme.TextSecondary };
        _cboOutput = CreateStyledComboBox(120, 64, 310);
        _cboOutput.SelectedIndexChanged += (s, e) => OnDeviceSelectionChanged();

        pnlRouting.Controls.AddRange(new Control[] { lblIn, _cboInput, lblOut, _cboOutput });
        Controls.Add(pnlRouting);

        // 4. Gain & Preamp Card
        var pnlGain = new ModernCardPanel
        {
            Location = new Point(18, 258),
            Size = new Size(446, 142),
            Title = "Gain & Preamplification",
            Badge = "-12 dB to +36 dB"
        };

        // Input Gain
        var lblInGain = new Label { Text = "Input Preamp:", Location = new Point(16, 32), AutoSize = true, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), ForeColor = CrispTheme.TextSecondary };
        _lblInputGainVal = new Label { Text = $"{_config.InputGainDb:+0.0;-0.0;0.0} dB", Location = new Point(340, 30), Size = new Size(90, 20), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = CrispTheme.AccentLight };
        _sliderInputGain = new CrispSlider
        {
            Location = new Point(16, 54),
            Size = new Size(414, 24),
            Minimum = -12f,
            Maximum = 36f,
            Value = _config.InputGainDb,
            ActiveColor = CrispTheme.AccentPrimary
        };
        _sliderInputGain.ValueChanged += (s, e) =>
        {
            _config.InputGainDb = _sliderInputGain.Value;
            _lblInputGainVal.Text = $"{_config.InputGainDb:+0.0;-0.0;0.0} dB";
            _config.Save();
        };

        // Output Gain
        var lblOutGain = new Label { Text = "Master Output:", Location = new Point(16, 84), AutoSize = true, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), ForeColor = CrispTheme.TextSecondary };
        _lblOutputGainVal = new Label { Text = $"{_config.OutputGainDb:+0.0;-0.0;0.0} dB", Location = new Point(340, 82), Size = new Size(90, 20), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = CrispTheme.AccentLight };
        _sliderOutputGain = new CrispSlider
        {
            Location = new Point(16, 106),
            Size = new Size(414, 24),
            Minimum = -12f,
            Maximum = 12f,
            Value = _config.OutputGainDb,
            ActiveColor = CrispTheme.AccentPrimary
        };
        _sliderOutputGain.ValueChanged += (s, e) =>
        {
            _config.OutputGainDb = _sliderOutputGain.Value;
            _lblOutputGainVal.Text = $"{_config.OutputGainDb:+0.0;-0.0;0.0} dB";
            _config.Save();
        };

        pnlGain.Controls.AddRange(new Control[] { lblInGain, _lblInputGainVal, _sliderInputGain, lblOutGain, _lblOutputGainVal, _sliderOutputGain });
        Controls.Add(pnlGain);

        // 5. Noise Cancellation Card
        var pnlNoise = new ModernCardPanel
        {
            Location = new Point(18, 406),
            Size = new Size(446, 150),
            Title = "Noise Cancellation & Isolation",
            Badge = "Xiph RNNoise"
        };

        // RNNoise AI Toggle
        var lblAiTitle = new Label { Text = "AI Noise Suppression", Location = new Point(16, 30), AutoSize = true, Font = new Font("Segoe UI", 9.2f, FontStyle.Bold), ForeColor = CrispTheme.TextWhite };
        var lblAiSub = new Label { Text = "Neural model removes fan, typing & background hum", Location = new Point(16, 48), AutoSize = true, Font = new Font("Segoe UI", 7.8f), ForeColor = CrispTheme.TextMuted };
        _swDenoise = new CrispSwitch
        {
            Location = new Point(386, 32),
            Checked = _config.NoiseSuppressionEnabled
        };
        _swDenoise.CheckedChanged += (s, e) =>
        {
            _config.NoiseSuppressionEnabled = _swDenoise.Checked;
            _config.Save();
        };

        // Hard-Reduce Gate
        var lblHrTitle = new Label { Text = "Noise Gate (Hard Squelch)", Location = new Point(16, 76), AutoSize = true, Font = new Font("Segoe UI", 9.2f, FontStyle.Bold), ForeColor = CrispTheme.TextWhite };
        var lblHrSub = new Label { Text = "Completely silences mic between spoken words", Location = new Point(16, 94), AutoSize = true, Font = new Font("Segoe UI", 7.8f), ForeColor = CrispTheme.TextMuted };
        _lblVadStatus = new Label { Text = "• VOICE", Location = new Point(270, 76), AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = CrispTheme.AccentLight };

        _swHardReduce = new CrispSwitch
        {
            Location = new Point(386, 78),
            Checked = _config.HardReduceEnabled
        };
        _swHardReduce.CheckedChanged += (s, e) =>
        {
            _config.HardReduceEnabled = _swHardReduce.Checked;
            _config.Save();
        };

        var lblThresh = new Label { Text = "Threshold:", Location = new Point(16, 118), AutoSize = true, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = CrispTheme.TextMuted };
        _lblHardReduceVal = new Label { Text = $"{(int)(_config.HardReduceThreshold * 100)}%", Location = new Point(80, 118), AutoSize = true, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = CrispTheme.AccentLight };
        _sliderHardReduce = new CrispSlider
        {
            Location = new Point(120, 116),
            Size = new Size(310, 20),
            Minimum = 10f,
            Maximum = 95f,
            Value = _config.HardReduceThreshold * 100f,
            ActiveColor = CrispTheme.AccentPrimary
        };
        _sliderHardReduce.ValueChanged += (s, e) =>
        {
            _config.HardReduceThreshold = _sliderHardReduce.Value / 100f;
            _lblHardReduceVal.Text = $"{(int)_sliderHardReduce.Value}%";
            _config.Save();
        };

        pnlNoise.Controls.AddRange(new Control[] { lblAiTitle, lblAiSub, _swDenoise, lblHrTitle, lblHrSub, _lblVadStatus, _swHardReduce, lblThresh, _lblHardReduceVal, _sliderHardReduce });
        Controls.Add(pnlNoise);

        // 6. 3-Band EQ Card
        var pnlEq = new ModernCardPanel
        {
            Location = new Point(18, 562),
            Size = new Size(446, 116),
            Title = "3-Band Equalizer",
            Badge = "Biquad Filter"
        };

        // Bass
        var lblBass = new Label { Text = "Bass (120Hz)", Location = new Point(16, 28), AutoSize = true, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = CrispTheme.TextSecondary };
        _lblBassVal = new Label { Text = $"{_config.BassDb:+0;-0;0} dB", Location = new Point(90, 28), Size = new Size(46, 16), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = CrispTheme.AccentLight };
        _sliderBass = new CrispSlider { Location = new Point(16, 48), Size = new Size(120, 22), Minimum = -12f, Maximum = 12f, Value = _config.BassDb, ActiveColor = CrispTheme.AccentPrimary };
        _sliderBass.ValueChanged += (s, e) => { _config.BassDb = _sliderBass.Value; _lblBassVal.Text = $"{_config.BassDb:+0;-0;0} dB"; _engine.UpdateFilters(); _config.Save(); };

        // Mid
        var lblMid = new Label { Text = "Mid (1.2kHz)", Location = new Point(156, 28), AutoSize = true, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = CrispTheme.TextSecondary };
        _lblMidVal = new Label { Text = $"{_config.MidDb:+0;-0;0} dB", Location = new Point(230, 28), Size = new Size(46, 16), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = CrispTheme.AccentLight };
        _sliderMid = new CrispSlider { Location = new Point(156, 48), Size = new Size(120, 22), Minimum = -12f, Maximum = 12f, Value = _config.MidDb, ActiveColor = CrispTheme.AccentPrimary };
        _sliderMid.ValueChanged += (s, e) => { _config.MidDb = _sliderMid.Value; _lblMidVal.Text = $"{_config.MidDb:+0;-0;0} dB"; _engine.UpdateFilters(); _config.Save(); };

        // Treble
        var lblTreble = new Label { Text = "Treble (5.5kHz)", Location = new Point(296, 28), AutoSize = true, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = CrispTheme.TextSecondary };
        _lblTrebleVal = new Label { Text = $"{_config.TrebleDb:+0;-0;0} dB", Location = new Point(384, 28), Size = new Size(46, 16), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = CrispTheme.AccentLight };
        _sliderTreble = new CrispSlider { Location = new Point(296, 48), Size = new Size(134, 22), Minimum = -12f, Maximum = 12f, Value = _config.TrebleDb, ActiveColor = CrispTheme.AccentPrimary };
        _sliderTreble.ValueChanged += (s, e) => { _config.TrebleDb = _sliderTreble.Value; _lblTrebleVal.Text = $"{_config.TrebleDb:+0;-0;0} dB"; _engine.UpdateFilters(); _config.Save(); };

        var btnResetEq = new Button
        {
            Text = "Reset EQ to Flat (0 dB)",
            Location = new Point(16, 78),
            Size = new Size(414, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = CrispTheme.CardInner,
            ForeColor = CrispTheme.TextSecondary,
            Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnResetEq.FlatAppearance.BorderColor = CrispTheme.CardBorder;
        btnResetEq.Click += (s, e) => { _sliderBass.Value = 0; _sliderMid.Value = 0; _sliderTreble.Value = 0; };

        pnlEq.Controls.AddRange(new Control[] { lblBass, _lblBassVal, _sliderBass, lblMid, _lblMidVal, _sliderMid, lblTreble, _lblTrebleVal, _sliderTreble, btnResetEq });
        Controls.Add(pnlEq);

        // 7. Clean Status & Startup Footer Bar
        var pnlFooter = new Panel
        {
            Location = new Point(18, 686),
            Size = new Size(446, 32),
            BackColor = Color.Transparent
        };

        _lblStatusDot = new Label
        {
            Text = "•",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = CrispTheme.AccentPrimary,
            Location = new Point(2, 2),
            AutoSize = true
        };

        _lblStatusText = new Label
        {
            Text = "WASAPI ACTIVE (10ms)",
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
            ForeColor = CrispTheme.TextSecondary,
            Location = new Point(18, 6),
            AutoSize = true
        };

        // Themed Windows Startup Switch on the right
        var lblStartup = new Label
        {
            Text = "Start on boot",
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
            ForeColor = CrispTheme.TextSecondary,
            Location = new Point(310, 6),
            AutoSize = true
        };

        _swStartup = new CrispSwitch
        {
            Location = new Point(398, 4),
            Size = new Size(44, 22),
            Checked = StartupHelper.IsSetToRunAtStartup()
        };
        _swStartup.CheckedChanged += (s, e) =>
        {
            StartupHelper.SetRunAtStartup(_swStartup.Checked);
            _config.StartWithWindows = _swStartup.Checked;
            _config.Save();
        };

        pnlFooter.Controls.AddRange(new Control[] { _lblStatusDot, _lblStatusText, lblStartup, _swStartup });
        Controls.Add(pnlFooter);

        ResumeLayout(false);
    }

    private ModernComboBox CreateStyledComboBox(int x, int y, int width)
    {
        return new ModernComboBox
        {
            Location = new Point(x, y),
            Size = new Size(width, 30)
        };
    }

    private void LoadDevicesAndSettings()
    {
        var inputs = AudioEngine.GetInputDevices();
        _cboInput.Items.Clear();
        foreach (var dev in inputs) _cboInput.Items.Add(dev);
        if (_cboInput.Items.Count > 0)
        {
            int idx = inputs.FindIndex(d => d.Id == _config.InputDeviceId);
            _cboInput.SelectedIndex = idx >= 0 ? idx : 0;
        }

        var outputs = AudioEngine.GetOutputDevices();
        _cboOutput.Items.Clear();
        foreach (var dev in outputs) _cboOutput.Items.Add(dev);
        if (_cboOutput.Items.Count > 0)
        {
            int idx = outputs.FindIndex(d => d.Id == _config.OutputDeviceId);
            if (idx < 0) idx = outputs.FindIndex(d => d.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase));
            _cboOutput.SelectedIndex = idx >= 0 ? idx : 0;
        }
    }

    private void OnDeviceSelectionChanged()
    {
        if (_cboInput.SelectedItem is AudioDeviceInfo inDev) _config.InputDeviceId = inDev.Id;
        if (_cboOutput.SelectedItem is AudioDeviceInfo outDev) _config.OutputDeviceId = outDev.Id;

        _config.Save();
        _engine.Start();
        UpdateStatusDisplay();
    }

    private void ToggleMute()
    {
        bool newMute = !_config.Muted;
        _engine.SetMuted(newMute);
        _btnMute.Text = newMute ? "UNMUTE" : "MUTE";
        _btnMute.BackColor = newMute ? CrispTheme.AccentRed : CrispTheme.CardInner;
        _config.Save();
        UpdateStatusDisplay();
    }

    private void ToggleMonitor()
    {
        _config.MonitoringEnabled = !_config.MonitoringEnabled;
        _btnMonitor.Text = _config.MonitoringEnabled ? "MONITOR: ON" : "MONITOR: OFF";
        _btnMonitor.BackColor = _config.MonitoringEnabled ? CrispTheme.AccentBadgeBg : CrispTheme.CardInner;
        _btnMonitor.ForeColor = _config.MonitoringEnabled ? CrispTheme.AccentLight : CrispTheme.TextSecondary;
        _btnMonitor.FlatAppearance.BorderColor = _config.MonitoringEnabled ? CrispTheme.AccentPrimary : CrispTheme.CardBorder;
        _config.Save();
        _engine.Start();
    }

    private void UpdateStatusDisplay()
    {
        _btnMute.Text = _config.Muted ? "UNMUTE" : "MUTE";
        _btnMute.BackColor = _config.Muted ? CrispTheme.AccentRed : CrispTheme.CardInner;
    }

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        if (!Visible || WindowState == FormWindowState.Minimized) return;

        _peakMeter.SetPeak(_engine.CurrentPeak);

        if (_swHardReduce.Checked)
        {
            bool isSpeech = _engine.CurrentVad >= _config.HardReduceThreshold;
            _lblVadStatus.Text = isSpeech ? "• SPEECH DETECTED" : "• GATE CLOSED";
            _lblVadStatus.ForeColor = isSpeech ? CrispTheme.AccentLight : CrispTheme.TextMuted;
        }
        else
        {
            _lblVadStatus.Text = "• GATE OFF";
            _lblVadStatus.ForeColor = CrispTheme.TextMuted;
        }
    }

    private void OnEngineError(string err)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnEngineError(err));
            return;
        }
    }

    private NotifyIcon CreateTrayIcon()
    {
        var icon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "CrispMic (Active)",
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open CrispMic", null, (s, e) => RestoreFromTray());
        menu.Items.Add("Mute / Unmute", null, (s, e) => ToggleMute());
        menu.Items.Add("-");
        menu.Items.Add("Exit", null, (s, e) => Application.Exit());

        icon.ContextMenuStrip = menu;
        icon.DoubleClick += (s, e) => RestoreFromTray();
        return icon;
    }

    private void HideToTray()
    {
        _uiTimer.Stop(); // HALT UI TIMER FOR 0.0% CPU & GPU IN TRAY
        Hide();
        _trayIcon.ShowBalloonTip(1000, "CrispMic", "Minimized to tray (0.0% GPU).", ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        _uiTimer.Start();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            HideToTray();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
        else
        {
            _uiTimer.Stop();
            _engine.Dispose();
            _trayIcon.Dispose();
            base.OnFormClosing(e);
        }
    }
}
