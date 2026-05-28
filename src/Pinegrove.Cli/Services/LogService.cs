namespace Pinegrove.Cli.Services;

public static class LogService
{
    public static async Task TailAsync(string modelName, int lines, bool follow, CancellationToken ct)
    {
        var logPath = ProcessManager.GetLogPath(modelName);

        if (!File.Exists(logPath))
        {
            Console.Error.WriteLine($"No log file found for '{modelName}' at: {logPath}");
            return;
        }

        // Print last N lines
        var allLines = await File.ReadAllLinesAsync(logPath, ct);
        var startIndex = Math.Max(0, allLines.Length - lines);
        for (var i = startIndex; i < allLines.Length; i++)
            Console.WriteLine(allLines[i]);

        if (!follow)
            return;

        // Follow mode: watch for new content
        using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(0, SeekOrigin.End);
        using var reader = new StreamReader(fs);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is not null)
            {
                Console.WriteLine(line);
            }
            else
            {
                try
                {
                    await Task.Delay(250, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
