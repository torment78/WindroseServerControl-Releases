namespace Elka_windrose_server_control.Models;

public sealed class AppSettings
{
    public int LaunchModeIndex { get; set; }
    public bool AutoRestart { get; set; }
    public bool AutoWorldBackup { get; set; }
    public bool DonationPopupDisabled { get; set; }
    public string? LockedWorldIslandId { get; set; }
    public string? SkippedAppVersion { get; set; }
    public string? LastActiveWorldPreset { get; set; }
}