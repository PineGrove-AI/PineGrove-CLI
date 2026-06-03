using System.CommandLine;
using Pinegrove.Cli.Models;
using Pinegrove.Cli.Services;

namespace Pinegrove.Cli.Commands;

public static class InitCommand
{
    public static Command Create()
    {
        var configOption = new Option<string?>(
            "--config",
            "Path to write pinegrove-config.yaml (default: ./pinegrove-config.yaml)");

        var forceOption = new Option<bool>(
            "--force",
            "Overwrite existing file if present");

        var modeOption = new Option<string?>(
            "--mode",
            "Skip interactive prompt: 'quick-test' or 'optimized'");

        var cmd = new Command("init", "Create a pinegrove-config.yaml file tailored to your hardware")
        {
            configOption,
            forceOption,
            modeOption,
        };

        cmd.SetHandler((string? configPath, bool force, string? mode) =>
        {
            try
            {
                var path = configPath ?? "pinegrove-config.yaml";

                if (File.Exists(path) && !force)
                {
                    Console.Error.WriteLine($"Error: '{path}' already exists. Use --force to overwrite.");
                    Environment.ExitCode = 1;
                    return;
                }

                // Determine mode: from flag or interactive prompt
                var selectedMode = mode?.Trim().ToLowerInvariant();

                if (selectedMode == null)
                {
                    selectedMode = PromptForMode();
                    if (selectedMode == null)
                    {
                        Environment.ExitCode = 1;
                        return;
                    }
                }

                if (selectedMode != "quick-test" && selectedMode != "optimized")
                {
                    Console.Error.WriteLine("Error: --mode must be 'quick-test' or 'optimized'.");
                    Environment.ExitCode = 1;
                    return;
                }

                string yaml;
                if (selectedMode == "quick-test")
                {
                    yaml = GenerateQuickTestYaml();
                    File.WriteAllText(path, yaml);
                    Console.WriteLine($"Wrote quick-test config to '{path}':");
                    Console.WriteLine();
                }
                else
                {
                    var gpu = GpuChecker.DetectGpuDetails();
                    if (gpu == null)
                    {
                        Console.Error.WriteLine("Error: Could not detect NVIDIA GPU(s). Make sure nvidia-smi is installed and working.");
                        Console.Error.WriteLine("Tip: Use 'pinegrove-cli init --mode quick-test' to create a basic test config instead.");
                        Environment.ExitCode = 1;
                        return;
                    }

                    if (gpu.GpuCount == 1)
                        Console.WriteLine($"Detected 1 NVIDIA GPU with {gpu.VramPerGpuMiB[0]} MiB VRAM.");
                    else
                        Console.WriteLine($"Detected {gpu.GpuCount}x NVIDIA GPU(s) with {gpu.TotalVramMiB} MiB VRAM total.");
                    Console.WriteLine();

                    var (modelId, name, maxModelLen) = SelectQwen3Model(gpu);
                    yaml = GenerateOptimizedYaml(name, modelId, gpu.GpuCount, maxModelLen);

                    File.WriteAllText(path, yaml);
                    Console.WriteLine($"Wrote optimized config to '{path}':");
                    Console.WriteLine();
                }

                // Print the generated config so the user can see exactly what was written
                foreach (var line in yaml.TrimEnd().Split('\n'))
                    Console.WriteLine($"  {line}");
                Console.WriteLine();
                Console.WriteLine("Run 'pinegrove-cli start' to launch.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, configOption, forceOption, modeOption);

        return cmd;
    }

    private static string? PromptForMode()
    {
        Console.WriteLine("Select a setup mode:");
        Console.WriteLine();
        Console.WriteLine("  [1] Quick test  - Use a tiny model (facebook/opt-125m) to verify your setup works");
        Console.WriteLine("  [2] Optimized   - Detect your GPU(s) and configure a Qwen3 model for your hardware");
        Console.WriteLine();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Console.Write("Enter choice (1 or 2): ");
            var input = Console.ReadLine()?.Trim();

            if (input == null)
            {
                Console.Error.WriteLine("Error: No input received. Use --mode to skip interactive prompts.");
                return null;
            }

            if (input == "1") return "quick-test";
            if (input == "2") return "optimized";

            Console.WriteLine("Invalid choice. Please enter 1 or 2.");
        }

        Console.Error.WriteLine("Error: Too many invalid attempts.");
        return null;
    }

    private static string GenerateQuickTestYaml()
    {
        return @"models:
  - name: opt-125m
    model: facebook/opt-125m
    port: 8000
    args:
      max-model-len: 2048
";
    }

    private static (string modelId, string name, int maxModelLen) SelectQwen3Model(GpuInfo gpu)
    {
        var totalVram = gpu.TotalVramMiB;

        return totalVram switch
        {
            >= 81_920 => ("Qwen/Qwen3-32B", "qwen3-32b", 32768),
            >= 40_960 => ("Qwen/Qwen3-14B", "qwen3-14b", 32768),
            >= 20_480 => ("Qwen/Qwen3-8B", "qwen3-8b", 32768),
            >= 12_288 => ("Qwen/Qwen3-4B", "qwen3-4b", 16384),
            >= 5_120 => ("Qwen/Qwen3-1.7B", "qwen3-1.7b", 8192),
            _ => ("Qwen/Qwen3-0.6B", "qwen3-0.6b", 4096),
        };
    }

    private static string GenerateOptimizedYaml(string name, string modelId, int gpuCount, int maxModelLen)
    {
        return $@"models:
  - name: {name}
    model: {modelId}
    port: 8000
    args:
      tensor-parallel-size: {gpuCount}
      max-model-len: {maxModelLen}
";
    }
}
