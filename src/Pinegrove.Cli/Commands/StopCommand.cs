using System.CommandLine;
using Pinegrove.Cli.Services;

namespace Pinegrove.Cli.Commands;

public static class StopCommand
{
    public static Command Create()
    {
        var nameArg = new Argument<string?>(
            "model-name",
            () => null,
            "Optional: stop only this model. Omit to stop all.");

        var configOption = new Option<string?>(
            "--config",
            "Path to pinegrove-config.yaml");

        var cmd = new Command("stop", "Stop managed model servers (all, or a single named one without bouncing the rest)")
        {
            nameArg,
            configOption,
        };

        cmd.SetHandler((name, configPath) =>
        {
            try
            {
                var config = ConfigLoader.Load(configPath);
                var pm = new ProcessManager();

                if (string.IsNullOrWhiteSpace(name))
                {
                    Console.WriteLine("Stopping all models...");
                    pm.StopAll(config);
                }
                else
                {
                    var model = config.Models.Find(m =>
                        string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                    if (model is null)
                    {
                        Console.Error.WriteLine($"Model '{name}' not found in configuration.");
                        Environment.ExitCode = 1;
                        return;
                    }

                    Console.WriteLine($"Stopping '{name}'...");
                    pm.StopModel(name);
                }

                Console.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, nameArg, configOption);

        return cmd;
    }
}
