using System.Text.Json;

namespace DesktopPeek.Models;

internal sealed class AppConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DesktopPeek");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>Layered alpha 0-200 (default 50; 0 = fully transparent).</summary>
    public byte Opacity { get; set; } = 50;

    /// <summary>Continuous desktop hover required before peek (0–3000 ms, default 500).</summary>
    public int HoverDelayMs { get; set; } = 500;

    /// <summary>Launch at Windows logon (default on).</summary>
    public bool AutoStart { get; set; } = true;

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (cfg is not null)
                {
                    cfg.Opacity = ClampOpacity(cfg.Opacity);
                    cfg.HoverDelayMs = ClampHoverDelay(cfg.HoverDelayMs);
                    // Older configs omitted AutoStart; JSON bool defaulted to false — keep default on.
                    if (json.IndexOf("AutoStart", StringComparison.OrdinalIgnoreCase) < 0)
                        cfg.AutoStart = true;
                    return cfg;
                }
            }
        }
        catch
        {
            // fall through to defaults
        }

        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            Opacity = ClampOpacity(Opacity);
            HoverDelayMs = ClampHoverDelay(HoverDelayMs);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // ignore persistence errors
        }
    }

    public static byte ClampOpacity(int value) =>
        (byte)Math.Clamp(value, 0, 200);

    public static int ClampHoverDelay(int value) =>
        Math.Clamp(value, 0, 3000);
}
