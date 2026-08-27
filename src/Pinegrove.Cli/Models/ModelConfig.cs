namespace Pinegrove.Cli.Models;

public sealed class ModelConfig
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Port { get; set; }
    public Dictionary<string, object>? Args { get; set; }

    // --- Backend abstraction (multi-backend support) ---
    // "vllm"      (default): docker <image> <model> --port <port> <args>   (image: vllm/vllm-openai:latest)
    // "infinity"          : docker <image> v2 --model-id <model> --port <port> <args>  (image: michaelf34/infinity:latest-cpu)
    // "container"         : run the image's own ENTRYPOINT/CMD (+ optional `command`); the model is selected via `env`.
    public string Backend { get; set; } = "vllm";
    public string? Image { get; set; }                 // overrides the per-backend default image
    // GPU selection. Accepts a bool or a docker device spec, so a config can pin
    // a model to one card on a multi-GPU box:
    //   (unset) / true / "all"  => --gpus all        (default, back-compatible)
    //   false / "none"          => no --gpus flag    (CPU workload)
    //   "device=0" / "device=0,1" / "1" => --gpus <value>
    public string? Gpus { get; set; }
    public string Restart { get; set; } = "no";        // docker --restart policy (e.g. unless-stopped)
    public Dictionary<string, string>? Env { get; set; } // -e KEY=VALUE
    public List<string>? Volumes { get; set; }         // extra -v mounts ("host:container", leading ~ expanded)
    public List<string>? Command { get; set; }         // explicit container command (container backend)
    public string HealthPath { get; set; } = "/health"; // path used by `status` health check

    // infinity backend: serve multiple models in one container (else falls back to single `Model`).
    public List<string>? ModelIds { get; set; }          // repeated --model-id
    public List<string>? ServedModelNames { get; set; }  // repeated --served-model-name (paired with ModelIds)
}
