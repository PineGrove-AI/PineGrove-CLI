using System.CommandLine;
using Pinegrove.Cli.Services;

namespace Pinegrove.Cli.Commands;

public static class CleanupCommand
{
    public static Command Create()
    {
        var cmd = new Command("cleanup", "Detect and kill orphan vLLM processes, remove stale PID files");

        cmd.SetHandler(() =>
        {
            try
            {
                var launcher = new PythonLauncher();
                var pm = new ProcessManager(launcher);

                Console.WriteLine("Cleaning up orphan processes...");
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
