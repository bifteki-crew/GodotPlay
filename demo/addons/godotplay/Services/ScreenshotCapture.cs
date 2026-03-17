using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class ScreenshotCapture
{
    private readonly SceneTree _sceneTree;

    public ScreenshotCapture(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ScreenshotResponse Capture(ScreenshotRequest request)
    {
        var viewport = _sceneTree.Root.GetViewport();
        var image = viewport.GetTexture().GetImage();
        var pngBytes = image.SavePngToBuffer();

        return new ScreenshotResponse
        {
            PngData = Google.Protobuf.ByteString.CopyFrom(pngBytes),
            Width = image.GetWidth(),
            Height = image.GetHeight()
        };
    }
}
