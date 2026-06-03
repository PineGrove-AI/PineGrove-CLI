using System.Diagnostics;
using Pinegrove.Cli.Models;

namespace Pinegrove.Cli.Services;

public static class GpuChecker
{
    private const string CudaTestImage = "nvidia/cuda:12.4.1-base-ubuntu22.04";

    public static bool Check()
    {
        return CheckNvidiaSmi() && CheckDockerGpu();
    }

    /// <summary>
    /// Parses nvidia-smi to detect GPU count and per-GPU VRAM.
    /// Returns null if nvidia-smi is unavailable or output cannot be parsed.
    /// </summary>
    public static GpuInfo? DetectGpuDetails()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "nvidia-smi",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("--query-gpu=memory.total");
        psi.ArgumentList.Add("--format=csv,noheader,nounits");

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                return null;

            var info = new GpuInfo();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(line.Trim(), out var mib))
                    info.VramPerGpuMiB.Add(mib);
            }

            info.GpuCount = info.VramPerGpuMiB.Count;
            return info.GpuCount > 0 ? info : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool CheckNvidiaSmi()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "nvidia-smi",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            process.WaitForExit();
            if (process.ExitCode == 0)
                return true;
        }
        catch { }

        Console.Error.WriteLine("error: nvidia-smi not found or failed. Is an NVIDIA GPU and driver installed?");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  Install NVIDIA drivers:  https://www.nvidia.com/Download/index.aspx");
        Console.Error.WriteLine("  Ubuntu/Debian shortcut:  sudo apt install nvidia-driver-535");
        return false;
    }

    private static bool CheckDockerGpu()
    {
        Console.WriteLine($"Verifying Docker GPU access (may pull {CudaTestImage} on first run)...");

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--rm");
        psi.ArgumentList.Add("--gpus"); psi.ArgumentList.Add("all");
        psi.ArgumentList.Add(CudaTestImage);
        psi.ArgumentList.Add("nvidia-smi");

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            process.WaitForExit();
            if (process.ExitCode == 0)
                return true;
        }
        catch { }

        Console.Error.WriteLine();
        Console.Error.WriteLine("error: Docker cannot access the GPU. Is the NVIDIA Container Toolkit installed?");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  Install guide: https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  Ubuntu/Debian quick install:");
        Console.Error.WriteLine("    curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey \\");
        Console.Error.WriteLine("      | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg");
        Console.Error.WriteLine("    curl -sL https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list \\");
        Console.Error.WriteLine("      | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list");
        Console.Error.WriteLine("    sudo apt-get update && sudo apt-get install -y nvidia-container-toolkit");
        Console.Error.WriteLine("    sudo nvidia-ctk runtime configure --runtime=docker && sudo systemctl restart docker");
        return false;
    }
}
