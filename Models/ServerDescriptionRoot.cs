namespace Elka_windrose_server_control.Models;

public sealed class ServerDescriptionRoot
{
    public int Version { get; set; }
    public string? DeploymentId { get; set; }
    public ServerDescriptionPersistent? ServerDescription_Persistent { get; set; }
}

public sealed class ServerDescriptionPersistent
{
    public string? PersistentServerId { get; set; }
    public string? InviteCode { get; set; }
    public bool IsPasswordProtected { get; set; }
    public string? Password { get; set; }
    public string? ServerName { get; set; }
    public string? WorldIslandId { get; set; }
    public int MaxPlayerCount { get; set; }
    public string? UserSelectedRegion { get; set; }
    public string? P2pProxyAddress { get; set; }
    public bool UseDirectConnection { get; set; }
    public string? DirectConnectionServerAddress { get; set; }
    public int DirectConnectionServerPort { get; set; }
    public string? DirectConnectionProxyAddress { get; set; }
    public bool AutoLoadLatestBackupIfHasBroken { get; set; }
}