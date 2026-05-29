using System.Diagnostics;
using Pinegrove.Cli.Models;

namespace Pinegrove.Cli.Services;

public sealed class PythonLauncher
{
    private readonly string _pythonPath;

    public PythonLauncher()
    {
        _pythonPath = ResolvePythonPath();
    }

    public Process Launch(ModelConfig model, string logFilePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var arg in BuildArguments(model))
            psi.ArgumentList.Add(arg);

        // Prevent vLLM / torch from picking up any system Python
        psi.Environment["PYTHONNOUSERSITE"] = "1";

        var process = new Process { StartInfo = psi };

        var logDir = Path.GetDirectoryName(logFilePath)!;
        Directory.CreateDirectory(logDir);

        process.Start();

        _ = Task.Run(async () =>
        {
            using var log = new StreamWriter(logFilePath, append: true) { AutoFlush = true };
            var gate = new SemaphoreSlim(1, 1);
            await Task.WhenAll(
                PipeAsync(process.StandardOutput, log, "", gate),
                PipeAsync(process.StandardError, log, "[stderr] ", gate));
            await process.WaitForExitAsync();
            await log.WriteLineAsync($"[pinegrove] Process exited with code {process.ExitCode}");
        });

        return process;
    }

    private static List<string> BuildArguments(ModelConfig model)
    {
        var parts = new List<string>
        {
            "-m", "vllm.entrypoints.openai.api_server",
            "--model", model.Model,
            "--port", model.Port.ToString(),
        };

        if (model.Args is not null)
        {
            foreach (var (key, value) in model.Args)
            {
                parts.Add($"--{key}");
                parts.Add(value.ToString()!);
            }
        }

        return parts;
    }

    private static async Task PipeAsync(TextReader reader, StreamWriter writer, string prefix, SemaphoreSlim gate)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            await gate.WaitAsync();
            try { await writer.WriteLineAsync(prefix + line); }
            finally { gate.Release(); }
        }
    }

    private static string ResolvePythonPath()
    {
        // Look for the bundled runtime relative to the application base
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "runtime", "python", "bin", "python"),
            Path.Combine(Directory.GetCurrentDirectory(), "runtime", "python", "bin", "python"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        throw new FileNotFoundException(
            "Python runtime not found. Reinstall pinegrove-cli.\n" +
            $"Expected at: {candidates[0]}");
    }
}
