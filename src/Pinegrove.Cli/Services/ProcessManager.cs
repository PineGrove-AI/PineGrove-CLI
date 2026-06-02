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

        if (IsContainerRunning(containerName))
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

        if (!IsContainerRunning(containerName))
        {
            Console.WriteLine($"  [{name}] Not running. Nothing to stop.");
            return;
        }

        try
        {
            RunDocker("stop", containerName);
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
            var running = IsContainerRunning(containerName);

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
        psi.ArgumentList.Add("-q");
        psi.ArgumentList.Add("--filter"); psi.ArgumentList.Add("name=pinegrove-");

        using var listProcess = new Process { StartInfo = psi };
        listProcess.Start();
        var output = listProcess.StandardOutput.ReadToEnd().Trim();
        listProcess.WaitForExit();

        if (string.IsNullOrEmpty(output))
        {
            Console.WriteLine("No pinegrove containers running. Nothing to clean up.");
            return;
        }

        foreach (var id in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var containerId = id.Trim();
            try
            {
                RunDocker("stop", containerId);
                Console.WriteLine($"  Stopped container {containerId}.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Failed to stop container {containerId}: {ex.Message}");
            }
        }

        Console.WriteLine("Cleanup complete.");
    }

    private static bool IsContainerRunning(string containerName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("inspect");
        psi.ArgumentList.Add("--format"); psi.ArgumentList.Add("{{.State.Running}}");
        psi.ArgumentList.Add(containerName);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        return output == "true";
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
