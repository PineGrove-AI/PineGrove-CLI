using System.Diagnostics;
using System.Text.RegularExpressions;
using Pinegrove.Cli.Models;

namespace Pinegrove.Cli.Services;

public sealed class DockerLauncher
{
    private const string VllmImage = "vllm/vllm-openai:latest";

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

        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add("--rm");
        psi.ArgumentList.Add("--name"); psi.ArgumentList.Add(containerName);
        psi.ArgumentList.Add("--gpus"); psi.ArgumentList.Add("all");
        psi.ArgumentList.Add("--ipc=host");
        psi.ArgumentList.Add("-p"); psi.ArgumentList.Add($"{model.Port}:{model.Port}");
        psi.ArgumentList.Add("-v"); psi.ArgumentList.Add($"{hfCacheDir}:/root/.cache/huggingface");

        var hfToken = Environment.GetEnvironmentVariable("HUGGING_FACE_HUB_TOKEN");
        if (!string.IsNullOrEmpty(hfToken))
        {
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add($"HUGGING_FACE_HUB_TOKEN={hfToken}");
        }

        psi.ArgumentList.Add(VllmImage);

        psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(model.Model);
        psi.ArgumentList.Add("--port"); psi.ArgumentList.Add(model.Port.ToString());

        if (model.Args is not null)
        {
            foreach (var (key, value) in model.Args)
            {
                psi.ArgumentList.Add($"--{key}");
                psi.ArgumentList.Add(value.ToString()!);
            }
        }

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new Exception($"docker run failed: {stderr.Trim()}");
    }

    public static string GetContainerName(string modelName) =>
        "pinegrove-" + Regex.Replace(modelName, @"[^a-zA-Z0-9_.-]", "-");
}
