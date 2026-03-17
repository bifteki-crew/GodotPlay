using System.Diagnostics;

namespace GodotPlay;

public static class GodotPlayLauncher
{
    public static async Task<GodotPlaySession> LaunchAsync(
        LaunchOptions options,
        CancellationToken ct = default)
    {
        var args = BuildGodotArgs(options);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = options.GodotPath,
                Arguments = string.Join(" ", args),
                UseShellExecute = false
            }
        };

        process.Start();

        try
        {
            var session = await GodotPlaySession.ConnectAsync(
                $"http://localhost:{options.Port}",
                options.StartupTimeout,
                ct);
            session.AttachProcess(process);
            return session;
        }
        catch
        {
            if (!process.HasExited)
                process.Kill();
            throw;
        }
    }

    public static List<string> BuildGodotArgs(LaunchOptions options)
    {
        var args = new List<string>();

        args.Add("--path");
        args.Add(options.ProjectPath);

        if (options.Headless)
            args.Add("--headless");

        if (!string.IsNullOrEmpty(options.Scene))
            args.Add(options.Scene);

        return args;
    }
}
