using System.CommandLine;
using Pinegrove.Cli.Commands;

var rootCommand = new RootCommand("pinegrove-cli — manage vLLM model servers with a bundled Python runtime");

rootCommand.AddCommand(StartCommand.Create());
rootCommand.AddCommand(StopCommand.Create());
rootCommand.AddCommand(StatusCommand.Create());
rootCommand.AddCommand(LogsCommand.Create());
rootCommand.AddCommand(RestartCommand.Create());
rootCommand.AddCommand(CleanupCommand.Create());
rootCommand.AddCommand(InitCommand.Create());

return await rootCommand.InvokeAsync(args);
