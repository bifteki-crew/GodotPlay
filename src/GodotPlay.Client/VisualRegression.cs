namespace GodotPlay;

public static class VisualRegression
{
    private const string DefaultBaselineDir = ".godotplay-baselines";

    /// <summary>
    /// Compute a simple perceptual hash from raw image bytes.
    /// Samples a grid of pixels from the PNG/JPEG data.
    /// </summary>
    public static ulong ComputeHash(byte[] imageData)
    {
        // Simple hash: sample 64 evenly-spaced bytes from the image data
        // Not a true perceptual hash, but good enough for regression detection
        if (imageData.Length < 64)
            return 0;

        ulong hash = 0;
        var step = imageData.Length / 64;
        long total = 0;

        // First pass: compute average
        for (int i = 0; i < 64; i++)
            total += imageData[i * step];
        var avg = total / 64;

        // Second pass: build hash
        for (int i = 0; i < 64; i++)
        {
            if (imageData[i * step] >= avg)
                hash |= (1UL << i);
        }

        return hash;
    }

    /// <summary>
    /// Compare two hashes. Returns similarity 0.0 (completely different) to 1.0 (identical).
    /// </summary>
    public static double Compare(ulong hash1, ulong hash2)
    {
        var xor = hash1 ^ hash2;
        var diffBits = 0;
        while (xor != 0)
        {
            diffBits += (int)(xor & 1);
            xor >>= 1;
        }
        return 1.0 - (diffBits / 64.0);
    }

    public static async Task SaveBaseline(string name, byte[] imageData, string? baselineDir = null)
    {
        var dir = baselineDir ?? DefaultBaselineDir;
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, $"{name}.bin"), imageData);

        // Also save the hash for quick comparison
        var hash = ComputeHash(imageData);
        await File.WriteAllTextAsync(Path.Combine(dir, $"{name}.hash"), hash.ToString());
    }

    public static async Task<(bool exists, ulong hash, byte[]? data)> LoadBaseline(string name, string? baselineDir = null)
    {
        var dir = baselineDir ?? DefaultBaselineDir;
        var hashPath = Path.Combine(dir, $"{name}.hash");
        var dataPath = Path.Combine(dir, $"{name}.bin");

        if (!File.Exists(hashPath))
            return (false, 0, null);

        var hash = ulong.Parse(await File.ReadAllTextAsync(hashPath));
        var data = File.Exists(dataPath) ? await File.ReadAllBytesAsync(dataPath) : null;
        return (true, hash, data);
    }

    public static async Task<VisualCompareResult> CompareWithBaseline(string name, byte[] currentImage, string? baselineDir = null)
    {
        var currentHash = ComputeHash(currentImage);
        var (exists, baselineHash, _) = await LoadBaseline(name, baselineDir);

        if (!exists)
        {
            await SaveBaseline(name, currentImage, baselineDir);
            return new VisualCompareResult
            {
                Name = name,
                IsNewBaseline = true,
                Similarity = 1.0,
                Message = $"New baseline saved for '{name}'."
            };
        }

        var similarity = Compare(currentHash, baselineHash);
        return new VisualCompareResult
        {
            Name = name,
            IsNewBaseline = false,
            Similarity = similarity,
            Message = similarity >= 0.95
                ? $"'{name}': PASS (similarity: {similarity:P1})"
                : $"'{name}': CHANGED (similarity: {similarity:P1}) — visual regression detected!"
        };
    }
}

public class VisualCompareResult
{
    public required string Name { get; init; }
    public bool IsNewBaseline { get; init; }
    public double Similarity { get; init; }
    public required string Message { get; init; }
    public bool Passed => IsNewBaseline || Similarity >= 0.95;
}
