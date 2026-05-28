using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pinegrove.Cli.Services;

namespace Pinegrove.Cli.Commands;

public static class StatusCommand
{
    public static Command Create()
    {
        var configOption = new Option<string?>(
            "--config",
            "Path to pinegrove-config.yaml");

        var jsonOption = new Option<bool>(
            "--json",
            "Output status as JSON");

        var cmd = new Command("status", "Show status of all managed vLLM model servers")
        {
            configOption,
            jsonOption,
        };

        cmd.AddAlias("ps");

        cmd.SetHandler(async (configPath, asJson) =>
        {
            try
            {
                var config = ConfigLoader.Load(configPath);
                var launcher = new PythonLauncher();
                var pm = new ProcessManager(launcher);

                var statuses = await pm.GetStatusAsync(config);

                if (asJson)
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    };
                    Console.WriteLine(JsonSerializer.Serialize(statuses, options));
                }
                else
                {
                    Console.WriteLine($"{"NAME",-20} {"MODEL",-40} {"PORT",-8} {"PID",-10} {"STATE",-20} {"HEALTH",-8}");
                    Console.WriteLine(new string('-', 106));

                    foreach (var s in statuses)
                    {
                        var pidStr = s.Pid?.ToString() ?? "-";
                        var healthStr = s.State == "running" ? (s.Healthy ? "ok" : "fail") : "-";
                        Console.WriteLine($"{s.Name,-20} {s.Model,-40} {s.Port,-8} {pidStr,-10} {s.State,-20} {healthStr,-8}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, configOption, jsonOption);

        return cmd;
    }
}
