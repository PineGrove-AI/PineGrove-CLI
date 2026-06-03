namespace Pinegrove.Cli.Models;

public sealed class GpuInfo
{
    public int GpuCount { get; set; }
    public List<int> VramPerGpuMiB { get; set; } = new();
    public int TotalVramMiB => VramPerGpuMiB.Sum();
}
