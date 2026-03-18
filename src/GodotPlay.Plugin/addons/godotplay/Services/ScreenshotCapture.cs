using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class ScreenshotCapture
{
    private readonly SceneTree _sceneTree;
    private const int MaxWidth = 960;
    private const int MaxHeight = 540;

    public ScreenshotCapture(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
    }

    public ScreenshotResponse Capture(ScreenshotRequest request)
    {
        var viewport = _sceneTree.Root.GetViewport();
        var image = viewport.GetTexture().GetImage();

        // Resize if larger than max dimensions to stay under gRPC 4MB limit
        if (image.GetWidth() > MaxWidth || image.GetHeight() > MaxHeight)
        {
            var scale = Mathf.Min(
                (float)MaxWidth / image.GetWidth(),
                (float)MaxHeight / image.GetHeight());
            var newWidth = (int)(image.GetWidth() * scale);
            var newHeight = (int)(image.GetHeight() * scale);
            image.Resize(newWidth, newHeight, Image.Interpolation.Bilinear);
        }

        var jpegBytes = image.SaveJpgToBuffer(0.85f);

        return new ScreenshotResponse
        {
            PngData = Google.Protobuf.ByteString.CopyFrom(jpegBytes),
            Width = image.GetWidth(),
            Height = image.GetHeight()
        };
    }
}
