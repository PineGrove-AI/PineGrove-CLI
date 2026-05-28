using System.CommandLine;
using System.IO;

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

        var cmd = new Command("init", "Create a template pinegrove-config.yaml file")
        {
            configOption,
            forceOption,
        };

        cmd.SetHandler((string? configPath, bool force) =>
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

                var template = @"models:
  - name: llama3
    model: meta-llama/Llama-3-8B-Instruct
    port: 8000
    args:
      tensor-parallel-size: 1
      max-model-len: 8192

  - name: mistral
    model: mistralai/Mistral-7B-Instruct
    port: 8001
    args:
      max-model-len: 4096
";

                File.WriteAllText(path, template);
                Console.WriteLine($"Wrote template config to '{path}'.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, configOption, forceOption);

        return cmd;
    }
}

