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
        var logDir = Path.GetDirectoryName(logFilePath)!;
        Directory.CreateDirectory(logDir);

        // Use bash to redirect output directly to the log file.
        // "$@" expands each argument as a separate word, so no shell escaping is needed.
        // The log path is passed via environment variable for the same reason.
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            UseShellExecute = false,
        };
        psi.Environment["PYTHONNOUSERSITE"] = "1";
        psi.Environment["LOG"] = logFilePath;
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("\"$@\" >> \"$LOG\" 2>&1; echo \"[pinegrove] Process exited with code $?\" >> \"$LOG\"");
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(_pythonPath);
        foreach (var arg in BuildArguments(model))
            psi.ArgumentList.Add(arg);

        var process = new Process { StartInfo = psi };
        process.Start();
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
