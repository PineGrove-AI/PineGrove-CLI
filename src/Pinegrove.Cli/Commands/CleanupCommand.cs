using System.CommandLine;
using Pinegrove.Cli.Services;

namespace Pinegrove.Cli.Commands;

public static class CleanupCommand
{
    public static Command Create()
    {
        var cmd = new Command("cleanup", "Stop all running pinegrove containers");

        cmd.SetHandler(() =>
        {
            try
            {
                var pm = new ProcessManager();

                Console.WriteLine("Cleaning up pinegrove containers...");
                pm.Cleanup();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }
}
