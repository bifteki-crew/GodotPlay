using GodotPlay;
using GodotPlay.Protocol;

namespace GodotPlay.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class EndToEndTests
{
    private GodotPlaySession? _session;

    [SetUp]
    public async Task Setup()
    {
        _session = await GodotPlayLauncher.LaunchAsync(new LaunchOptions
        {
            ProjectPath = Path.GetFullPath("../../../../demo"),
            Headless = false,
            Scene = "res://scenes/main_menu.tscn",
            Port = 50051,
            GodotPath = "godot",
            StartupTimeout = TimeSpan.FromSeconds(30)
        });
    }

    [TearDown]
    public async Task Teardown()
    {
        if (_session != null)
            await _session.DisposeAsync();
    }

    [Test]
    public async Task Ping_ReturnsReady()
    {
        var ping = await _session!.PingAsync();
        Assert.That(ping.Ready, Is.True);
        Assert.That(ping.Version, Is.EqualTo("0.1.0"));
    }

    [Test]
    public async Task GetSceneTree_ReturnsMainMenu()
    {
        var tree = await _session!.GetSceneTreeAsync();
        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.CurrentScenePath, Does.Contain("main_menu"));
    }

    [Test]
    public async Task FindNodes_FindsStartButton()
    {
        var locator = _session!.Locator(className: "Button", namePattern: "StartButton");
        await Expect.That(locator).ToExistAsync();
    }

    [Test]
    public async Task ClickStartButton_NavigatesToGameScene()
    {
        var startButton = _session!.Locator(path: "/root/MainMenu/VBoxContainer/StartButton");
        await startButton.ClickAsync();

        // Wait for scene change
        await Task.Delay(500);
        var tree = await _session!.GetSceneTreeAsync();
        Assert.That(tree.CurrentScenePath, Does.Contain("game"));
    }

    [Test]
    public async Task TakeScreenshot_ReturnsPngData()
    {
        var screenshot = await _session!.ScreenshotAsync();
        Assert.That(screenshot.PngData.Length, Is.GreaterThan(0));
        Assert.That(screenshot.Width, Is.GreaterThan(0));
        Assert.That(screenshot.Height, Is.GreaterThan(0));

        // Save for visual inspection
        var path = Path.Combine(Path.GetTempPath(), "godotplay_screenshot.png");
        await File.WriteAllBytesAsync(path, screenshot.PngData.ToByteArray());
        TestContext.WriteLine($"Screenshot saved to: {path}");
    }
}
