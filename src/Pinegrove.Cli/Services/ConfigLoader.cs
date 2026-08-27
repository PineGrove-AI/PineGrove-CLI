using Pinegrove.Cli.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pinegrove.Cli.Services;

public static class ConfigLoader
{
    private const string DefaultConfigFileName = "pinegrove-config.yaml";

    public static PinegroveConfig Load(string? configPath = null)
    {
        var path = configPath ?? FindConfigFile();

        if (!File.Exists(path))
            throw new FileNotFoundException($"Configuration file not found: {path}");

        var yaml = File.ReadAllText(path);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        PinegroveConfig config;
        try
        {
            config = deserializer.Deserialize<PinegroveConfig>(yaml)
                ?? throw new InvalidOperationException("Configuration file is empty.");
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new InvalidOperationException(
                $"YAML parse error at line {ex.Start.Line}, column {ex.Start.Column}: {ex.Message}", ex);
        }

        Validate(config);
        return config;
    }

    private static string FindConfigFile()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName),
            Path.Combine(AppContext.BaseDirectory, DefaultConfigFileName),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            $"Cannot find {DefaultConfigFileName}. " +
            "Place it in the current directory or next to the pinegrove-cli binary.");
    }

    private static void Validate(PinegroveConfig config)
    {
        if (config.Models is null || config.Models.Count == 0)
            throw new InvalidOperationException("Configuration must define at least one model.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ports = new HashSet<int>();

        foreach (var model in config.Models)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                throw new InvalidOperationException("Each model must have a 'name'.");

            var backend = (model.Backend ?? "vllm").Trim().ToLowerInvariant();

            var hasModelIds = model.ModelIds is { Count: > 0 };
            if (string.IsNullOrWhiteSpace(model.Model) && backend != "container" && !(backend == "infinity" && hasModelIds))
                throw new InvalidOperationException(
                    $"Model '{model.Name}' is missing the 'model' field (or 'model-ids' for an infinity backend).");

            if (backend == "container" && string.IsNullOrWhiteSpace(model.Image))
                throw new InvalidOperationException($"Model '{model.Name}' (backend 'container') requires an 'image'.");

            if (model.Port is < 1 or > 65535)
                throw new InvalidOperationException(
                    $"Model '{model.Name}' has invalid port {model.Port}. Must be 1-65535.");

            if (!names.Add(model.Name))
                throw new InvalidOperationException($"Duplicate model name: '{model.Name}'.");

            if (!ports.Add(model.Port))
                throw new InvalidOperationException(
                    $"Duplicate port {model.Port} on model '{model.Name}'. Each model must use a unique port.");
        }
    }
}
