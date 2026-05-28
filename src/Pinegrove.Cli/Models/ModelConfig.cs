namespace Pinegrove.Cli.Models;

public sealed class ModelConfig
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Port { get; set; }
    public Dictionary<string, object>? Args { get; set; }
}
