namespace SandboxLauncher.Services;

public sealed record SandboxProfile(
    string Name,
    bool Networking,
    bool VGpu,
    int? MemoryInMB = null,
    bool AudioInput = false)
{
    public static readonly SandboxProfile Standard = new("Standard", true, true);
    public static readonly SandboxProfile Strict = new("Strict Isolation", false, false);
    public static readonly SandboxProfile Network = new("Network Enabled", true, false);

    public static IReadOnlyList<SandboxProfile> All { get; } = [Standard, Strict, Network];

    public SandboxProfile WithMemory(int? memoryInMB) => this with { MemoryInMB = memoryInMB };
    public SandboxProfile WithAudio(bool audioInput) => this with { AudioInput = audioInput };
}
