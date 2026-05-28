using System.CommandLine;
using Pinegrove.Cli.Services;

namespace Pinegrove.Cli.Commands;

public static class StopCommand
{
    public static Command Create()
    {
        var configOption = new Option<string?>(
            "--config",
            "Path to pinegrove-config.yaml");

        var cmd = new Command("stop", "Stop all managed vLLM model servers")
        {
            configOption,
        };

        cmd.SetHandler(configPath =>
        {
            try
            {
                var config = ConfigLoader.Load(configPath);
                var launcher = new PythonLauncher();
                var pm = new ProcessManager(launcher);

                Console.WriteLine("Stopping all models...");
                pm.StopAll(config);
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
