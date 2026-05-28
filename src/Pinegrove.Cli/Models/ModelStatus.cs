namespace Pinegrove.Cli.Models;

public sealed class ModelStatus
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Port { get; set; }
    public int? Pid { get; set; }
    public string State { get; set; } = "unknown";
    public bool Healthy { get; set; }
}
