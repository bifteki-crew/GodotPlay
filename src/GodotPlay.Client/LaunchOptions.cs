namespace GodotPlay;

public class LaunchOptions
{
    public required string ProjectPath { get; init; }
    public bool Headless { get; init; } = true;
    public string? Scene { get; init; }
    public int Port { get; init; } = 50051;
    public string GodotPath { get; init; } = "godot";
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
