using System.Diagnostics;

namespace Pinegrove.Cli.Services;

public static class LogService
{
    public static async Task TailAsync(string modelName, int lines, bool follow, CancellationToken ct)
    {
        var containerName = DockerLauncher.GetContainerName(modelName);

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("logs");
        psi.ArgumentList.Add("--tail"); psi.ArgumentList.Add(lines.ToString());
        if (follow)
            psi.ArgumentList.Add("--follow");
        psi.ArgumentList.Add(containerName);

        using var process = new Process { StartInfo = psi };
        ct.Register(() => { try { process.Kill(); } catch { } });
        process.Start();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Normal exit via Ctrl+C
        }
    }
}
