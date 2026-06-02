using System.CommandLine;
using Pinegrove.Cli.Services;

namespace Pinegrove.Cli.Commands;

public static class RestartCommand
{
    public static Command Create()
    {
        var nameArg = new Argument<string>(
            "model-name",
            "Name of the model to restart");

        var configOption = new Option<string?>(
            "--config",
            "Path to pinegrove-config.yaml");

        var cmd = new Command("restart", "Restart a specific model server")
        {
            nameArg,
            configOption,
        };

        cmd.SetHandler((name, configPath) =>
        {
            try
            {
                var config = ConfigLoader.Load(configPath);
                var model = config.Models.Find(m =>
                    string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                if (model is null)
                {
                    Console.Error.WriteLine($"Model '{name}' not found in configuration.");
                    Environment.ExitCode = 1;
                    return;
                }

                var pm = new ProcessManager();

                Console.WriteLine($"Restarting '{name}'...");
                pm.StopModel(name);

                // Brief pause to let the port release
                Thread.Sleep(1000);

                pm.StartModel(model);
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
