using GodotPlay;

namespace GodotPlay.Tests;

[TestFixture]
public class LauncherTests
{
    [Test]
    public void BuildArgs_Headless_IncludesFlag()
    {
        var options = new LaunchOptions
        {
            ProjectPath = "/tmp/project",
            Headless = true,
            Scene = "res://scenes/main.tscn",
            Port = 50051
        };

        var args = GodotPlayLauncher.BuildGodotArgs(options);

        Assert.That(args, Does.Contain("--headless"));
        Assert.That(args, Does.Contain("--path"));
        Assert.That(args, Does.Contain("/tmp/project"));
        Assert.That(args, Does.Contain("res://scenes/main.tscn"));
    }

    [Test]
    public void BuildArgs_NotHeadless_OmitsFlag()
    {
        var options = new LaunchOptions
        {
            ProjectPath = "/tmp/project",
            Headless = false
        };

        var args = GodotPlayLauncher.BuildGodotArgs(options);

        Assert.That(args, Does.Not.Contain("--headless"));
    }

    [Test]
    public void BuildArgs_NoScene_OmitsScenePath()
    {
        var options = new LaunchOptions
        {
            ProjectPath = "/tmp/project",
            Headless = true
        };

        var args = GodotPlayLauncher.BuildGodotArgs(options);

        Assert.That(args, Does.Not.Contain("res://"));
    }
}
