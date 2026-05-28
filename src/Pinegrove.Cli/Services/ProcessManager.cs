using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using Pinegrove.Cli.Models;

namespace Pinegrove.Cli.Services;

public sealed class ProcessManager
{
    private static readonly string PidDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pinegrove", "pids");

    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pinegrove", "logs");

    private readonly PythonLauncher _launcher;

    public ProcessManager(PythonLauncher launcher)
    {
        _launcher = launcher;
        Directory.CreateDirectory(PidDir);
        Directory.CreateDirectory(LogDir);
    }

    public void StartModel(ModelConfig model)
    {
        // Check for stale PID
        var existingPid = ReadPid(model.Name);
        if (existingPid.HasValue && IsProcessRunning(existingPid.Value))
        {
            Console.WriteLine($"  [{model.Name}] Already running (PID {existingPid.Value}). Skipping.");
            return;
        }

        // Check port availability
        if (IsPortInUse(model.Port))
        {
            Console.Error.WriteLine($"  [{model.Name}] FAILED: Port {model.Port} is already in use.");
            return;
        }

        var logFile = Path.Combine(LogDir, $"{model.Name}.log");

        try
        {
            var process = _launcher.Launch(model, logFile);
            WritePid(model.Name, process.Id);
            Console.WriteLine($"  [{model.Name}] Started (PID {process.Id}, port {model.Port})");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [{model.Name}] FAILED to start: {ex.Message}");
        }
    }

    public void StopModel(string name)
    {
        var pid = ReadPid(name);
        if (!pid.HasValue)
        {
            Console.WriteLine($"  [{name}] No PID file found. Not running.");
            return;
        }

        if (!IsProcessRunning(pid.Value))
        {
            Console.WriteLine($"  [{name}] Process {pid.Value} is not running (stale PID). Cleaning up.");
            RemovePid(name);
            return;
        }

        try
        {
            var process = Process.GetProcessById(pid.Value);

            // Try graceful shutdown first (SIGTERM on Linux)
            process.Kill(entireProcessTree: true);
            process.WaitForExit(TimeSpan.FromSeconds(10));

            if (!process.HasExited)
            {
                Console.Error.WriteLine($"  [{name}] Force killing PID {pid.Value}...");
                process.Kill();
            }

            Console.WriteLine($"  [{name}] Stopped (PID {pid.Value}).");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [{name}] Error stopping PID {pid.Value}: {ex.Message}");
        }
        finally
        {
            RemovePid(name);
        }
    }

    public void StopAll(PinegroveConfig config)
    {
        foreach (var model in config.Models)
            StopModel(model.Name);
    }

    public async Task<List<ModelStatus>> GetStatusAsync(PinegroveConfig config)
    {
        var statuses = new List<ModelStatus>();

        foreach (var model in config.Models)
        {
            var status = new ModelStatus
            {
                Name = model.Name,
                Model = model.Model,
                Port = model.Port,
            };

            var pid = ReadPid(model.Name);
            status.Pid = pid;

            if (pid.HasValue && IsProcessRunning(pid.Value))
            {
                status.State = "running";
                status.Healthy = await CheckHealthAsync(model.Port);
            }
            else if (pid.HasValue)
            {
                status.State = "dead (stale PID)";
            }
            else
            {
                status.State = "stopped";
            }

            statuses.Add(status);
        }

        return statuses;
    }

    public void Cleanup()
    {
        if (!Directory.Exists(PidDir))
        {
            Console.WriteLine("No PID directory found. Nothing to clean up.");
            return;
        }

        var pidFiles = Directory.GetFiles(PidDir, "*.pid");
        if (pidFiles.Length == 0)
        {
            Console.WriteLine("No PID files found. Nothing to clean up.");
            return;
        }

        foreach (var pidFile in pidFiles)
        {
            var name = Path.GetFileNameWithoutExtension(pidFile);
            var pidText = File.ReadAllText(pidFile).Trim();

            if (int.TryParse(pidText, out var pid) && IsProcessRunning(pid))
            {
                try
                {
                    var proc = Process.GetProcessById(pid);
                    proc.Kill(entireProcessTree: true);
                    Console.WriteLine($"  [{name}] Killed orphan process {pid}.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  [{name}] Failed to kill PID {pid}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"  [{name}] Stale PID file (process {pidText} not running). Removing.");
            }

            File.Delete(pidFile);
        }

        Console.WriteLine("Cleanup complete.");
    }

    public static string GetLogPath(string modelName)
    {
        return Path.Combine(LogDir, $"{modelName}.log");
    }

    private static void WritePid(string name, int pid)
    {
        Directory.CreateDirectory(PidDir);
        File.WriteAllText(Path.Combine(PidDir, $"{name}.pid"), pid.ToString());
    }

    private static int? ReadPid(string name)
    {
        var path = Path.Combine(PidDir, $"{name}.pid");
        if (!File.Exists(path))
            return null;

        var text = File.ReadAllText(path).Trim();
        return int.TryParse(text, out var pid) ? pid : null;
    }

    private static void RemovePid(string name)
    {
        var path = Path.Combine(PidDir, $"{name}.pid");
        if (File.Exists(path))
            File.Delete(path);
    }

    private static bool IsProcessRunning(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            return !proc.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            using var listener = new TcpClient();
            listener.Connect("127.0.0.1", port);
            listener.Close();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task<bool> CheckHealthAsync(int port)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await http.GetAsync($"http://127.0.0.1:{port}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
