using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CrispMic;

public static class CrispTheme
{
    public static readonly Color BgDark = Color.FromArgb(11, 13, 16);          // #0B0D10
    public static readonly Color CardBg = Color.FromArgb(18, 21, 27);          // #12151B
    public static readonly Color CardBorder = Color.FromArgb(30, 36, 48);      // #1E2430
    public static readonly Color CardInner = Color.FromArgb(24, 28, 38);       // #181C26

    public static readonly Color TextWhite = Color.FromArgb(245, 246, 250);
    public static readonly Color TextSecondary = Color.FromArgb(155, 165, 180);
    public static readonly Color TextMuted = Color.FromArgb(95, 105, 122);

    // Signature Cyber Emerald / Electric Mint Palette
    public static readonly Color AccentDark = Color.FromArgb(4, 120, 87);        // #047857 Deep Emerald
    public static readonly Color AccentPrimary = Color.FromArgb(16, 185, 129);   // #10B981 Vibrant Emerald
    public static readonly Color AccentLight = Color.FromArgb(52, 211, 153);     // #34D399 Bright Mint
    public static readonly Color AccentElectric = Color.FromArgb(110, 231, 183); // #6EE7B7 Luminous Cyan-Mint
    public static readonly Color AccentBadgeBg = Color.FromArgb(16, 42, 34);     // #102A22

    public static readonly Color AccentYellow = Color.FromArgb(245, 158, 11);
    public static readonly Color AccentRed = Color.FromArgb(239, 68, 68);

    public static readonly Color SliderTrack = Color.FromArgb(32, 38, 50);
}

public class ModernCardPanel : Panel
{
    public string Title { get; set; } = "";
    public string Badge { get; set; } = "";

    public ModernCardPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = CrispTheme.CardBg;
        Padding = new Padding(16, 32, 16, 14);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Parent?.BackColor ?? CrispTheme.BgDark);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = GetRoundedPath(rect, 10))
        {
            using var bgBrush = new SolidBrush(CrispTheme.CardBg);
            g.FillPath(bgBrush, path);
            using var borderPen = new Pen(CrispTheme.CardBorder, 1.2f);
            g.DrawPath(borderPen, path);
        }

        // Header Title
        if (!string.IsNullOrEmpty(Title))
        {
            using var font = new Font("Segoe UI", 8.2f, FontStyle.Bold);
            using var brush = new SolidBrush(CrispTheme.TextMuted);
            g.DrawString(Title.ToUpperInvariant(), font, brush, 16, 10);
        }

        // Header Badge
        if (!string.IsNullOrEmpty(Badge))
        {
            using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            var size = g.MeasureString(Badge, font);
            var badgeRect = new Rectangle(Width - (int)size.Width - 22, 8, (int)size.Width + 10, (int)size.Height + 4);
            using var badgePath = GetRoundedPath(badgeRect, 4);
            using var bgBrush = new SolidBrush(CrispTheme.AccentBadgeBg);
            using var textBrush = new SolidBrush(CrispTheme.AccentLight);
            g.FillPath(bgBrush, badgePath);
            g.DrawString(Badge, font, textBrush, badgeRect.X + 5, badgeRect.Y + 2);
        }
    }

    public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public class ModernComboBox : ComboBox
{
    private bool _isHovered = false;

    public ModernComboBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        BackColor = CrispTheme.CardInner;
        ForeColor = CrispTheme.TextWhite;
        Font = new Font("Segoe UI", 9f);
        ItemHeight = 26;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Parent?.BackColor ?? CrispTheme.CardBg);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = ModernCardPanel.GetRoundedPath(rect, 6))
        {
            using var bgBrush = new SolidBrush(CrispTheme.CardInner);
            g.FillPath(bgBrush, path);

            Color borderColor = _isHovered ? CrispTheme.AccentPrimary : CrispTheme.CardBorder;
            using var borderPen = new Pen(borderColor, 1.2f);
            g.DrawPath(borderPen, path);
        }

        // Text
        string text = SelectedItem?.ToString() ?? "";
        if (!string.IsNullOrEmpty(text))
        {
            using var font = new Font("Segoe UI", 8.8f, FontStyle.Bold);
            using var textBrush = new SolidBrush(CrispTheme.TextWhite);
            var textRect = new Rectangle(12, 0, Width - 38, Height);
            var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(text, font, textBrush, textRect, sf);
        }

        // Chevron Arrow
        int arrowX = Width - 20;
        int arrowY = (Height / 2) - 2;
        Color arrowColor = _isHovered ? CrispTheme.AccentLight : CrispTheme.TextSecondary;
        using (var arrowPen = new Pen(arrowColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(arrowPen, arrowX, arrowY, arrowX + 4, arrowY + 4);
            g.DrawLine(arrowPen, arrowX + 4, arrowY + 4, arrowX + 8, arrowY);
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color itemBg = isSelected ? Color.FromArgb(16, 42, 34) : CrispTheme.CardInner;
        Color itemText = isSelected ? CrispTheme.AccentLight : CrispTheme.TextWhite;

        using (var brush = new SolidBrush(itemBg))
        {
            g.FillRectangle(brush, e.Bounds);
        }

        string text = Items[e.Index]?.ToString() ?? "";
        using (var font = new Font("Segoe UI", 8.8f, isSelected ? FontStyle.Bold : FontStyle.Regular))
        using (var brush = new SolidBrush(itemText))
        {
            var textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);
            var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(text, font, brush, textRect, sf);
        }
    }
}

public class CrispSlider : Control
{
    private float _minimum = -12f;
    private float _maximum = 36f;
    private float _value = 0f;
    private bool _isDragging = false;
    private bool _isHovered = false;

    public float Minimum
    {
        get => _minimum;
        set { _minimum = value; Invalidate(); }
    }

    public float Maximum
    {
        get => _maximum;
        set { _maximum = value; Invalidate(); }
    }

    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, _minimum, _maximum);
            if (Math.Abs(_value - clamped) > 0.001f)
            {
                _value = clamped;
                ValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    public Color ActiveColor { get; set; } = CrispTheme.AccentPrimary;
    public event EventHandler? ValueChanged;

    public CrispSlider()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 26;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            UpdateValueFromMouse(e.X);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isDragging) UpdateValueFromMouse(e.X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _isDragging = false;
        base.OnMouseUp(e);
    }

    private void UpdateValueFromMouse(int mouseX)
    {
        int margin = 8;
        int trackWidth = Width - (margin * 2);
        if (trackWidth <= 0) return;

        float ratio = Math.Clamp((float)(mouseX - margin) / trackWidth, 0f, 1f);
        Value = _minimum + ratio * (_maximum - _minimum);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Parent?.BackColor ?? CrispTheme.CardBg);

        int margin = 8;
        int trackHeight = 6;
        int trackY = (Height - trackHeight) / 2;
        int trackWidth = Width - (margin * 2);

        // 1. Background Track
        var trackRect = new Rectangle(margin, trackY, trackWidth, trackHeight);
        using (var bgPath = ModernCardPanel.GetRoundedPath(trackRect, 3))
        {
            using var bgBrush = new SolidBrush(CrispTheme.SliderTrack);
            g.FillPath(bgBrush, bgPath);
        }

        // 2. Active Track (Dynamic Multi-Stop Luminous Gradient)
        float ratio = (_maximum > _minimum) ? (_value - _minimum) / (_maximum - _minimum) : 0f;
        int fillWidth = (int)(ratio * trackWidth);
        if (fillWidth > 3)
        {
            var activeRect = new Rectangle(margin, trackY, fillWidth, trackHeight);
            using var activePath = ModernCardPanel.GetRoundedPath(activeRect, 3);

            using var activeBrush = new LinearGradientBrush(
                new Point(margin, trackY),
                new Point(margin + fillWidth, trackY),
                CrispTheme.AccentDark,
                CrispTheme.AccentElectric
            );

            var blend = new ColorBlend(3)
            {
                Colors = new[] { CrispTheme.AccentDark, CrispTheme.AccentPrimary, CrispTheme.AccentElectric },
                Positions = new[] { 0.0f, 0.55f, 1.0f }
            };
            activeBrush.InterpolationColors = blend;

            g.FillPath(activeBrush, activePath);
        }

        // 3. Thumb with Glowing Halo
        int thumbRadius = _isDragging ? 8 : (_isHovered ? 7 : 6);
        int thumbX = margin + fillWidth;
        int thumbY = Height / 2;

        var thumbRect = new Rectangle(thumbX - thumbRadius, thumbY - thumbRadius, thumbRadius * 2, thumbRadius * 2);

        // Luminous Halo Glow
        using (var glowBrush = new SolidBrush(Color.FromArgb(_isHovered || _isDragging ? 90 : 50, CrispTheme.AccentPrimary)))
        {
            g.FillEllipse(glowBrush, thumbX - thumbRadius - 4, thumbY - thumbRadius - 4, (thumbRadius + 4) * 2, (thumbRadius + 4) * 2);
        }

        // White Thumb with Electric Mint Ring
        using (var thumbBrush = new SolidBrush(Color.White))
        {
            g.FillEllipse(thumbBrush, thumbRect);
        }
        using (var thumbPen = new Pen(CrispTheme.AccentElectric, 2f))
        {
            g.DrawEllipse(thumbPen, thumbRect);
        }
    }
}

public class CrispSwitch : Control
{
    private bool _checked = true;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                CheckedChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    public event EventHandler? CheckedChanged;

    public CrispSwitch()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Size = new Size(44, 24);
        Cursor = Cursors.Hand;
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Parent?.BackColor ?? CrispTheme.CardBg);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = ModernCardPanel.GetRoundedPath(rect, Height / 2))
        {
            if (_checked)
            {
                using var brush = new LinearGradientBrush(rect, CrispTheme.AccentPrimary, CrispTheme.AccentElectric, LinearGradientMode.Horizontal);
                g.FillPath(brush, path);
            }
            else
            {
                using var brush = new SolidBrush(Color.FromArgb(34, 40, 52));
                g.FillPath(brush, path);
            }
        }

        int knobSize = Height - 6;
        int pad = 3;
        int knobX = _checked ? (Width - knobSize - pad) : pad;
        int knobY = (Height - knobSize) / 2;

        // Subtle shadow
        using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
        {
            g.FillEllipse(shadowBrush, knobX, knobY + 1, knobSize, knobSize);
        }

        using (var knobBrush = new SolidBrush(Color.White))
        {
            g.FillEllipse(knobBrush, knobX, knobY, knobSize, knobSize);
        }
    }
}

public class LivePeakMeter : Control
{
    private float _peak = 0f;
    private float _peakHold = 0f;
    private int _holdTimer = 0;

    public void SetPeak(float value)
    {
        _peak = Math.Clamp(value, 0f, 1f);
        if (_peak > _peakHold)
        {
            _peakHold = _peak;
            _holdTimer = 25;
        }
        else if (_holdTimer > 0)
        {
            _holdTimer--;
        }
        else
        {
            _peakHold -= 0.03f;
            if (_peakHold < 0f) _peakHold = 0f;
        }

        if (Visible) Invalidate();
    }

    public LivePeakMeter()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 26;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Parent?.BackColor ?? CrispTheme.BgDark);

        int barHeight = 10;
        int barY = 2;
        var barRect = new Rectangle(0, barY, Width - 1, barHeight);

        // Background Bar
        using (var path = ModernCardPanel.GetRoundedPath(barRect, 5))
        {
            using var bgBrush = new SolidBrush(Color.FromArgb(20, 24, 32));
            g.FillPath(bgBrush, path);
            using var borderPen = new Pen(CrispTheme.CardBorder, 1f);
            g.DrawPath(borderPen, path);
        }

        // Active Level (Rich Multi-stop Gradient)
        int fillWidth = (int)(_peak * (Width - 2));
        if (fillWidth > 2)
        {
            var fillRect = new Rectangle(1, barY + 1, fillWidth, barHeight - 2);
            using var fillPath = ModernCardPanel.GetRoundedPath(fillRect, 4);

            using var gradient = new LinearGradientBrush(new Point(0, 0), new Point(Width, 0), CrispTheme.AccentPrimary, CrispTheme.AccentRed);
            var blend = new ColorBlend
            {
                Colors = new[] { CrispTheme.AccentPrimary, CrispTheme.AccentElectric, CrispTheme.AccentYellow, CrispTheme.AccentRed },
                Positions = new[] { 0.0f, 0.65f, 0.85f, 1.0f }
            };
            gradient.InterpolationColors = blend;

            g.FillPath(gradient, fillPath);
        }

        // Peak Hold Marker
        int holdX = (int)(_peakHold * (Width - 2));
        if (holdX > 2 && holdX < Width - 2)
        {
            using var holdPen = new Pen(Color.White, 2f);
            g.DrawLine(holdPen, holdX, barY, holdX, barY + barHeight);
        }

        // dB Scale
        using var font = new Font("Segoe UI", 7.2f);
        using var textBrush = new SolidBrush(CrispTheme.TextMuted);
        string[] labels = { "-60", "-36", "-24", "-12", "-6", "-3", "0 dB" };
        float[] positions = { 0.0f, 0.25f, 0.45f, 0.65f, 0.80f, 0.90f, 1.0f };

        for (int i = 0; i < labels.Length; i++)
        {
            int x = (int)(positions[i] * (Width - 24));
            if (i == labels.Length - 1) x = Width - (int)g.MeasureString(labels[i], font).Width - 2;
            g.DrawString(labels[i], font, textBrush, x, barY + barHeight + 2);
        }
    }
}
