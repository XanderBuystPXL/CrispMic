using System;
using System.IO;
using System.Text.Json;

namespace CrispMic;

public class AppConfig
{
    public string? InputDeviceId { get; set; }
    public string? OutputDeviceId { get; set; }
    public string? MonitorDeviceId { get; set; }

    public float InputGainDb { get; set; } = 12.0f;    // -12 dB to +36 dB
    public float OutputGainDb { get; set; } = 0.0f;    // -12 dB to +12 dB

    public bool NoiseSuppressionEnabled { get; set; } = true;
    public bool HardReduceEnabled { get; set; } = true;
    public float HardReduceThreshold { get; set; } = 0.40f; // 0.10 to 0.95

    public float BassDb { get; set; } = 0.0f;    // -12 dB to +12 dB
    public float MidDb { get; set; } = 0.0f;     // -12 dB to +12 dB
    public float TrebleDb { get; set; } = 0.0f;  // -12 dB to +12 dB

    public bool MonitoringEnabled { get; set; } = false;
    public float MonitorVolume { get; set; } = 1.0f;

    public bool StartWithWindows { get; set; } = true;
    public bool Muted { get; set; } = false;

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrispMic",
        "config.json"
    );

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null) return config;
            }
        }
        catch { }

        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}
