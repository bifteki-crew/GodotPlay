using System.Collections.Concurrent;
using Godot;
using GodotPlay.Protocol;

namespace GodotPlay.Plugin.Services;

public class EventStreamer
{
    private readonly SceneTree _sceneTree;
    private readonly ConcurrentQueue<GameEvent> _eventQueue = new();
    private string _lastScenePath = "";

    public EventStreamer(SceneTree sceneTree)
    {
        _sceneTree = sceneTree;
        _lastScenePath = sceneTree.CurrentScene?.SceneFilePath ?? "";
    }

    public void Poll()
    {
        var currentScene = _sceneTree.CurrentScene?.SceneFilePath ?? "";
        if (currentScene != _lastScenePath)
        {
            _eventQueue.Enqueue(new GameEvent
            {
                Type = "scene_changed",
                Detail = $"{{\"from\":\"{_lastScenePath}\",\"to\":\"{currentScene}\"}}",
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            _lastScenePath = currentScene;
        }
    }

    public void PushEvent(string type, string detail)
    {
        _eventQueue.Enqueue(new GameEvent
        {
            Type = type,
            Detail = detail,
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    public bool TryDequeue(out GameEvent? evt)
    {
        return _eventQueue.TryDequeue(out evt);
    }
}
