using System.Diagnostics;
using System.Net.Sockets;
using Pinegrove.Cli.Models;

namespace Pinegrove.Cli.Services;

public sealed class ProcessManager
{
    private readonly DockerLauncher _launcher = new();

    public void StartModel(ModelConfig model)
    {
        var containerName = DockerLauncher.GetContainerName(model.Name);

        if (GetContainerStatus(containerName) == "running")
        {
            Console.WriteLine($"  [{model.Name}] Already running (container {containerName}). Skipping.");
            return;
        }

        if (IsPortInUse(model.Port))
        {
            Console.Error.WriteLine($"  [{model.Name}] FAILED: Port {model.Port} is already in use.");
            return;
        }

        try
        {
            _launcher.Launch(model);

            if (CrashedImmediately(containerName, out var crashLogs))
            {
                try { RunDocker("rm", containerName); } catch { }
                Console.Error.WriteLine($"  [{model.Name}] FAILED: vllm exited immediately.");
                if (!string.IsNullOrWhiteSpace(crashLogs))
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(crashLogs);
                }
                return;
            }

            Console.WriteLine($"  [{model.Name}] Started (container {containerName}, port {model.Port})");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [{model.Name}] FAILED to start: {ex.Message}");
        }
    }

    public void StopModel(string name)
    {
        var containerName = DockerLauncher.GetContainerName(name);
        var status = GetContainerStatus(containerName);

        if (status is null)
        {
            Console.WriteLine($"  [{name}] No container found. Nothing to stop.");
            return;
        }

        try
        {
            if (status == "running")
                RunDocker("stop", containerName);
            RunDocker("rm", containerName);
            Console.WriteLine($"  [{name}] Stopped.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [{name}] Error stopping: {ex.Message}");
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
            var containerName = DockerLauncher.GetContainerName(model.Name);
            var running = GetContainerStatus(containerName) == "running";

            var status = new ModelStatus
            {
                Name = model.Name,
                Model = model.Model,
                Port = model.Port,
                ContainerName = containerName,
                State = running ? "running" : "stopped",
                Healthy = running && await CheckHealthAsync(model.Port),
            };

            statuses.Add(status);
        }

        return statuses;
    }

    public void Cleanup()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        psi.ArgumentList.Add("ps");
        psi.ArgumentList.Add("-aq");
        psi.ArgumentList.Add("--filter"); psi.ArgumentList.Add("name=pinegrove-");

        using var listProcess = new Process { StartInfo = psi };
        listProcess.Start();
        var output = listProcess.StandardOutput.ReadToEnd().Trim();
        listProcess.WaitForExit();

        if (string.IsNullOrEmpty(output))
        {
            Console.WriteLine("No pinegrove containers found. Nothing to clean up.");
            return;
        }

        foreach (var id in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var containerId = id.Trim();
            try
            {
                RunDocker("rm", "-f", containerId);
                Console.WriteLine($"  Removed container {containerId}.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Failed to remove container {containerId}: {ex.Message}");
            }
        }

        Console.WriteLine("Cleanup complete.");
    }

    // Polls for up to 3 seconds to detect an immediate crash (e.g. bad vllm args).
    private static bool CrashedImmediately(string containerName, out string logs)
    {
        logs = string.Empty;
        var deadline = DateTime.UtcNow.AddSeconds(3);

        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
            var status = GetContainerStatus(containerName);
            if (status is "exited" or "dead")
            {
                logs = GetContainerLogs(containerName);
                return true;
            }
            if (status == "running")
                return false;
        }

        return false;
    }

    private static string? GetContainerStatus(string containerName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("inspect");
        psi.ArgumentList.Add("--format"); psi.ArgumentList.Add("{{.State.Status}}");
        psi.ArgumentList.Add(containerName);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        return process.ExitCode == 0 ? output : null;
    }

    private static string GetContainerLogs(string containerName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("logs");
        psi.ArgumentList.Add(containerName);

        using var process = new Process { StartInfo = psi };
        process.Start();
        // Read both streams concurrently to avoid deadlocks
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return (stdout.Result + stderr.Result).Trim();
    }

    private static void RunDocker(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new Exception(stderr.Trim());
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            using var client = new TcpClient();
            client.Connect("127.0.0.1", port);
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
