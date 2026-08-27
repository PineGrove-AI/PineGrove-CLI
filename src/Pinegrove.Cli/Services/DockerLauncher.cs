using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Pinegrove.Cli.Models;

namespace Pinegrove.Cli.Services;

public sealed class DockerLauncher
{
    private const string DefaultVllmImage = "vllm/vllm-openai:latest";
    private const string DefaultInfinityImage = "michaelf34/infinity:latest-cpu";

    public void Launch(ModelConfig model)
    {
        var containerName = GetContainerName(model.Name);
        var hfCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "huggingface");

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            RedirectStandardError = true,
        };
        var a = psi.ArgumentList;

        a.Add("run");
        a.Add("-d");
        a.Add("--name"); a.Add(containerName);

        if (model.Gpus)
        {
            a.Add("--gpus"); a.Add("all");
        }

        a.Add("--ipc=host");

        if (!string.IsNullOrWhiteSpace(model.Restart) &&
            !string.Equals(model.Restart, "no", StringComparison.OrdinalIgnoreCase))
        {
            a.Add("--restart"); a.Add(model.Restart);
        }

        a.Add("-p"); a.Add($"{model.Port}:{model.Port}");
        a.Add("-v"); a.Add($"{hfCacheDir}:/root/.cache/huggingface");

        if (model.Volumes is not null)
        {
            foreach (var vol in model.Volumes)
            {
                a.Add("-v"); a.Add(ExpandHome(vol));
            }
        }

        var hfToken = Environment.GetEnvironmentVariable("HUGGING_FACE_HUB_TOKEN");
        if (!string.IsNullOrEmpty(hfToken))
        {
            a.Add("-e"); a.Add($"HUGGING_FACE_HUB_TOKEN={hfToken}");
        }

        if (model.Env is not null)
        {
            foreach (var (key, value) in model.Env)
            {
                a.Add("-e"); a.Add($"{key}={value}");
            }
        }

        var backend = (model.Backend ?? "vllm").Trim().ToLowerInvariant();
        var image = model.Image ?? backend switch
        {
            "vllm" => DefaultVllmImage,
            "infinity" => DefaultInfinityImage,
            _ => throw new Exception($"Model '{model.Name}' (backend '{backend}') requires an 'image'."),
        };
        a.Add(image);

        // Command after the image differs per backend.
        switch (backend)
        {
            case "vllm":
                a.Add(model.Model);
                a.Add("--port"); a.Add(model.Port.ToString());
                AppendFlagArgs(a, model.Args);
                break;

            case "infinity":
                a.Add("v2");
                if (model.ModelIds is { Count: > 0 })
                {
                    foreach (var id in model.ModelIds) { a.Add("--model-id"); a.Add(id); }
                    if (model.ServedModelNames is not null)
                        foreach (var n in model.ServedModelNames) { a.Add("--served-model-name"); a.Add(n); }
                }
                else
                {
                    a.Add("--model-id"); a.Add(model.Model);
                }
                a.Add("--port"); a.Add(model.Port.ToString());
                AppendFlagArgs(a, model.Args);
                break;

            case "container":
                // Use the image's own ENTRYPOINT/CMD; the model is selected via env.
                // Optional explicit command + flag args for full control.
                if (model.Command is not null)
                    foreach (var c in model.Command) a.Add(c);
                AppendFlagArgs(a, model.Args);
                break;

            default:
                throw new Exception(
                    $"Unknown backend '{backend}' for model '{model.Name}'. Use: vllm | infinity | container.");
        }

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new Exception($"docker run failed: {stderr.Trim()}");
    }

    // YamlDotNet deserializes scalars in Dictionary<string,object> as strings, so `true`
    // arrives as "true" not bool. A true bool => bare flag; otherwise => --key value.
    private static void AppendFlagArgs(ICollection<string> a, Dictionary<string, object>? args)
    {
        if (args is null) return;
        foreach (var (key, value) in args)
        {
            if (value is bool flag || (value is string sv && bool.TryParse(sv, out flag)))
            {
                if (flag) a.Add($"--{key}");
            }
            else
            {
                a.Add($"--{key}");
                a.Add(value.ToString()!);
            }
        }
    }

    private static string ExpandHome(string p) =>
        p.StartsWith("~/")
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), p[2..])
            : p;

    public static string GetContainerName(string modelName) =>
        "pinegrove-" + Regex.Replace(modelName, @"[^a-zA-Z0-9_.-]", "-");
}
