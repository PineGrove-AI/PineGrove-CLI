using System.CommandLine;
using Pinegrove.Cli.Services;

namespace Pinegrove.Cli.Commands;

public static class StartCommand
{
    public static Command Create()
    {
        var configOption = new Option<string?>(
            "--config",
            "Path to pinegrove-config.yaml");

        var cmd = new Command("start", "Start all configured vLLM model servers")
        {
            configOption,
        };

        cmd.SetHandler(configPath =>
        {
            try
            {
                var config = ConfigLoader.Load(configPath);
                var pm = new ProcessManager();

                Console.WriteLine($"Starting {config.Models.Count} model(s)...");

                foreach (var model in config.Models)
                    pm.StartModel(model);

                Console.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, configOption);

        return cmd;
    }
}
