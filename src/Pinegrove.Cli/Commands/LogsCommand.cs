using System.CommandLine;
using Pinegrove.Cli.Services;

namespace Pinegrove.Cli.Commands;

public static class LogsCommand
{
    public static Command Create()
    {
        var nameArg = new Argument<string>(
            "model-name",
            "Name of the model to show logs for");

        var linesOption = new Option<int>(
            "--lines",
            getDefaultValue: () => 50,
            "Number of lines to show");
        linesOption.AddAlias("-n");

        var followOption = new Option<bool>(
            "--follow",
            "Follow log output (stream new lines)");
        followOption.AddAlias("-f");

        var cmd = new Command("logs", "Show logs for a specific model server")
        {
            nameArg,
            linesOption,
            followOption,
        };

        cmd.SetHandler(async (name, lines, follow) =>
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await LogService.TailAsync(name, lines, follow, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Normal exit via Ctrl+C
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, nameArg, linesOption, followOption);

        return cmd;
    }
}
