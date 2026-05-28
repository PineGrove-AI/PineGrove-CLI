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
        var args = BuildArguments(model);

        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // Prevent vLLM / torch from picking up any system Python
        psi.Environment["PYTHONNOUSERSITE"] = "1";

        var process = new Process { StartInfo = psi };

        var logDir = Path.GetDirectoryName(logFilePath)!;
        Directory.CreateDirectory(logDir);

        var logStream = new StreamWriter(logFilePath, append: true) { AutoFlush = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                logStream.WriteLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                logStream.WriteLine($"[stderr] {e.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private static string BuildArguments(ModelConfig model)
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

        return string.Join(' ', parts);
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
