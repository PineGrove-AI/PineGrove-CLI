using System.CommandLine;
using System.Text;
using Pinegrove.Cli.Models;
using Pinegrove.Cli.Services;

namespace Pinegrove.Cli.Commands;

[Flags]
public enum GenerationType
{
  None = 0,
  Text = 1,
  Image = 2,
  SpeechToText = 4,
  TextToSpeech = 8,
}

public static class InitCommand
{
  public static Command Create()
  {
    var configOption = new Option<string?>(
        "--config",
        "Path to write pinegrove-config.yaml (default: ./pinegrove-config.yaml)");

    var forceOption = new Option<bool>(
        "--force",
        "Overwrite existing file if present");

    var modeOption = new Option<string?>(
        "--mode",
        "Skip interactive prompt: 'quick-test' or 'optimized'");

    var cmd = new Command("init", "Create a pinegrove-config.yaml file tailored to your hardware")
        {
            configOption,
            forceOption,
            modeOption,
        };

    cmd.SetHandler((string? configPath, bool force, string? mode) =>
    {
      try
      {
        var path = configPath ?? "pinegrove-config.yaml";

        if (File.Exists(path) && !force)
        {
          Console.Error.WriteLine($"Error: '{path}' already exists. Use --force to overwrite.");
          Environment.ExitCode = 1;
          return;
        }

        // Determine mode: from flag or interactive prompt
        var selectedMode = mode?.Trim().ToLowerInvariant();

        if (selectedMode == null)
        {
          selectedMode = PromptForMode();
          if (selectedMode == null)
          {
            Environment.ExitCode = 1;
            return;
          }
        }

        if (selectedMode != "quick-test" && selectedMode != "optimized")
        {
          Console.Error.WriteLine("Error: --mode must be 'quick-test' or 'optimized'.");
          Environment.ExitCode = 1;
          return;
        }

        string yaml;
        if (selectedMode == "quick-test")
        {
          yaml = GenerateQuickTestYaml();
          File.WriteAllText(path, yaml);
          Console.WriteLine($"Wrote quick-test config to '{path}':");
          Console.WriteLine();
        }
        else
        {
          var gpu = GpuChecker.DetectGpuDetails();
          if (gpu == null)
          {
            Console.Error.WriteLine("Error: Could not detect NVIDIA GPU(s). Make sure nvidia-smi is installed and working.");
            Console.Error.WriteLine("Tip: Use 'pinegrove-cli init --mode quick-test' to create a basic test config instead.");
            Environment.ExitCode = 1;
            return;
          }

          if (gpu.GpuCount == 1)
            Console.WriteLine($"Detected 1 NVIDIA GPU with {gpu.VramPerGpuMiB[0]} MiB VRAM.");
          else
            Console.WriteLine($"Detected {gpu.GpuCount}x NVIDIA GPU(s) with {gpu.TotalVramMiB} MiB VRAM total.");
          Console.WriteLine();

          var generationTypes = PromptForGenerationTypes();
          if (generationTypes == GenerationType.None)
          {
            Environment.ExitCode = 1;
            return;
          }

          var needsLlm = generationTypes.HasFlag(GenerationType.Text) ||
                         generationTypes.HasFlag(GenerationType.Image);

          string? family = null;
          if (needsLlm)
          {
            family = PromptForModelFamily();
            if (family == null)
            {
              Environment.ExitCode = 1;
              return;
            }
          }

          yaml = GenerateConfig(generationTypes, family, gpu);

          File.WriteAllText(path, yaml);
          Console.WriteLine($"Wrote optimized config to '{path}':");
          Console.WriteLine();
        }

        // Print the generated config so the user can see exactly what was written
        foreach (var line in yaml.TrimEnd().Split('\n'))
          Console.WriteLine($"  {line}");
        Console.WriteLine();
        Console.WriteLine("Run 'pinegrove-cli start' to launch.");
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.ExitCode = 1;
      }
    }, configOption, forceOption, modeOption);

    return cmd;
  }

  private static string? PromptForMode()
  {
    Console.WriteLine("Select a setup mode:");
    Console.WriteLine();
    Console.WriteLine("  [1] Quick test  - Use a tiny model (facebook/opt-125m) to verify your setup works");
    Console.WriteLine("  [2] Optimized   - Detect your GPU(s) and configure a model for your hardware");
    Console.WriteLine();

    for (var attempt = 0; attempt < 3; attempt++)
    {
      Console.Write("Enter choice (1 or 2): ");
      var input = Console.ReadLine()?.Trim();

      if (input == null)
      {
        Console.Error.WriteLine("Error: No input received. Use --mode to skip interactive prompts.");
        return null;
      }

      if (input == "1") return "quick-test";
      if (input == "2") return "optimized";

      Console.WriteLine("Invalid choice. Please enter 1 or 2.");
    }

    Console.Error.WriteLine("Error: Too many invalid attempts.");
    return null;
  }

  private static GenerationType PromptForGenerationTypes()
  {
    Console.WriteLine("What types of generation do you need? (select all that apply)");
    Console.WriteLine();
    Console.WriteLine("  [1] Text           - Text generation and chat via a large language model");
    Console.WriteLine("  [2] Image          - Image understanding (vision) via a multimodal model");
    Console.WriteLine("  [3] Speech-to-text - Transcribe audio to text via Whisper");
    Console.WriteLine("  [4] Text-to-speech - Generate speech from text via Kokoro");
    Console.WriteLine();

    for (var attempt = 0; attempt < 3; attempt++)
    {
      Console.Write("Enter choices (e.g. 1,3 or 1,2,3,4): ");
      var input = Console.ReadLine()?.Trim();

      if (input == null)
      {
        Console.Error.WriteLine("Error: No input received.");
        return GenerationType.None;
      }

      var result = GenerationType.None;
      var valid = true;

      foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
      {
        switch (part)
        {
          case "1": result |= GenerationType.Text; break;
          case "2": result |= GenerationType.Image; break;
          case "3": result |= GenerationType.SpeechToText; break;
          case "4": result |= GenerationType.TextToSpeech; break;
          default: valid = false; break;
        }
      }

      if (valid && result != GenerationType.None)
        return result;

      Console.WriteLine("Invalid choice. Enter comma-separated numbers from 1-4 (e.g. 1,3).");
    }

    Console.Error.WriteLine("Error: Too many invalid attempts.");
    return GenerationType.None;
  }

  private static string? PromptForModelFamily()
  {
    Console.WriteLine();
    Console.WriteLine("Which model family would you like to use?");
    Console.WriteLine();
    Console.WriteLine("  [1] Qwen  - More direct and reliable; strong reasoning and multilingual support (Qwen3.6)");
    Console.WriteLine("  [2] Gemma - More creative; Google's multimodal model with native image and audio (Gemma 4)");
    Console.WriteLine();

    for (var attempt = 0; attempt < 3; attempt++)
    {
      Console.Write("Enter choice (1 or 2): ");
      var input = Console.ReadLine()?.Trim();

      if (input == null)
      {
        Console.Error.WriteLine("Error: No input received.");
        return null;
      }

      if (input == "1") return "qwen";
      if (input == "2") return "gemma";

      Console.WriteLine("Invalid choice. Please enter 1 or 2.");
    }

    Console.Error.WriteLine("Error: Too many invalid attempts.");
    return null;
  }

  private static string GenerateQuickTestYaml()
  {
    return @"models:
  - name: opt-125m
    model: facebook/opt-125m
    port: 8000
    args:
      max-model-len: 2048
";
  }

  private static string GenerateConfig(GenerationType types, string? family, GpuInfo gpu)
  {
    var sb = new StringBuilder();
    sb.AppendLine("models:");
    var port = 8000;

    var needsLlm = types.HasFlag(GenerationType.Text) || types.HasFlag(GenerationType.Image);
    var needsVision = types.HasFlag(GenerationType.Image);

    if (needsLlm)
    {
      var (modelId, name, maxModelLen, extraArgs) = family switch
      {
        "qwen" => SelectQwenModel(gpu, needsVision),
        "gemma" => SelectGemmaModel(gpu, needsVision),
        _ => throw new ArgumentException($"Unknown model family: {family}"),
      };

      sb.AppendLine($"  - name: {name}");
      sb.AppendLine($"    model: {modelId}");
      sb.AppendLine($"    port: {port}");
      sb.AppendLine($"    args:");
      sb.AppendLine($"      tensor-parallel-size: {gpu.GpuCount}");
      sb.AppendLine($"      max-model-len: {maxModelLen}");

      foreach (var (key, value) in extraArgs)
        sb.AppendLine($"      {key}: {value}");

      port++;
    }

    if (types.HasFlag(GenerationType.SpeechToText))
    {
      var (modelId, name) = SelectWhisperModel(gpu);

      sb.AppendLine($"  - name: {name}");
      sb.AppendLine($"    model: {modelId}");
      sb.AppendLine($"    port: {port}");
      sb.AppendLine($"    args:");
      sb.AppendLine($"      tensor-parallel-size: {gpu.GpuCount}");

      port++;
    }

    if (types.HasFlag(GenerationType.TextToSpeech))
    {
      var (modelId, name) = SelectKokoroModel();

      sb.AppendLine($"  - name: {name}");
      sb.AppendLine($"    model: {modelId}");
      sb.AppendLine($"    port: {port}");
      sb.AppendLine($"    args:");
      sb.AppendLine($"      tensor-parallel-size: 1");
    }

    return sb.ToString();
  }

  private static (string modelId, string name, int maxModelLen, List<(string key, string value)> extraArgs) SelectQwenModel(GpuInfo gpu, bool needsVision)
  {
    var totalVram = gpu.TotalVramMiB;

    // Qwen3.6 models are natively multimodal (text + image + video).
    // When vision is not needed, use --language-model-only to skip the vision
    // encoder and free VRAM for more KV cache.
    var extraArgs = new List<(string key, string value)>();
    if (!needsVision)
      extraArgs.Add(("language-model-only", "true"));

    var (modelId, name, maxModelLen) = totalVram switch
    {
      >= 65_536 => ("Qwen/Qwen3.6-27B", "qwen3.6-27b", 32768),
      >= 20_480 => ("Qwen/Qwen3.6-35B-A3B", "qwen3.6-35b-a3b", 32768),
      >= 12_288 => ("Qwen/Qwen3-4B", "qwen3-4b", 16384),
      >= 5_120 => ("Qwen/Qwen3-1.7B", "qwen3-1.7b", 8192),
      _ => ("Qwen/Qwen3-0.6B", "qwen3-0.6b", 4096),
    };

    // Qwen3 (non-3.6) models don't have a vision encoder, so remove the flag
    if (name.StartsWith("qwen3-"))
    {
      extraArgs.RemoveAll(a => a.key == "language-model-only");
      if (needsVision)
      {
        Console.WriteLine($"Note: {name} does not support vision. Image understanding requires more VRAM for a Qwen3.6 model.");
        Console.WriteLine("      The configured model will handle text generation only.");
        Console.WriteLine();
      }
    }

    return (modelId, name, maxModelLen, extraArgs);
  }

  private static (string modelId, string name, int maxModelLen, List<(string key, string value)> extraArgs) SelectGemmaModel(GpuInfo gpu, bool needsVision)
  {
    var totalVram = gpu.TotalVramMiB;

    // Gemma 4 models are natively multimodal. When vision is not needed,
    // limit multimodal inputs to zero to skip encoder memory allocation.
    var extraArgs = new List<(string key, string value)>();
    if (!needsVision)
      extraArgs.Add(("limit-mm-per-prompt", "\"image=0\""));

    // gemma-4-26B-A4B-it: 26B total, 4B active (MoE) — needs ~20GB+
    // gemma-4-E4B-it: ~4B effective params — needs ~10GB+
    // gemma-4-E2B-it: ~2B effective params — fits on smaller GPUs
    var (modelId, name, maxModelLen) = totalVram switch
    {
      >= 40_960 => ("google/gemma-4-26B-A4B-it", "gemma4-26b-a4b", 32768),
      >= 12_288 => ("google/gemma-4-E4B-it", "gemma4-e4b", 16384),
      _ => ("google/gemma-4-E2B-it", "gemma4-e2b", 8192),
    };

    return (modelId, name, maxModelLen, extraArgs);
  }

  private static (string modelId, string name) SelectWhisperModel(GpuInfo gpu)
  {
    var totalVram = gpu.TotalVramMiB;

    // whisper-large-v3 ~10GB, large-v3-turbo ~6GB, medium ~5GB, small ~2GB
    return totalVram switch
    {
      >= 12_288 => ("openai/whisper-large-v3", "whisper-large-v3"),
      >= 8_192 => ("openai/whisper-large-v3-turbo", "whisper-large-v3-turbo"),
      >= 5_120 => ("openai/whisper-medium", "whisper-medium"),
      _ => ("openai/whisper-small", "whisper-small"),
    };
  }

  private static (string modelId, string name) SelectKokoroModel()
  {
    // Kokoro is a lightweight 82M-parameter TTS model that runs on any GPU
    return ("hexgrad/Kokoro-82M", "kokoro-tts");
  }
}
