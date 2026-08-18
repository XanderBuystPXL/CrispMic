using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace CrispMic;

public static class IconHelper
{
    public static Icon CreateAppIcon()
    {
        try
        {
            // 1. Try extracting embedded executable icon
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon != null) return icon;
            }

            // 2. Try loading from app.ico in base directory
            string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(localIco))
            {
                return new Icon(localIco);
            }
        }
        catch { }

        // 3. Fallback: Draw default icon
        using var bmp = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var bgRect = new Rectangle(2, 2, 60, 60);
            using var brush = new SolidBrush(CrispTheme.AccentPrimary);
            g.FillEllipse(brush, bgRect);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
