using GodotPlay;

namespace GodotPlay.Tests;

[TestFixture]
public class VisualRegressionTests
{
    private string _tempDir = null!;

    [SetUp]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"godotplay-vr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public void ComputeHash_SameData_SameHash()
    {
        var data = new byte[1000];
        Random.Shared.NextBytes(data);
        var hash1 = VisualRegression.ComputeHash(data);
        var hash2 = VisualRegression.ComputeHash(data);
        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public void ComputeHash_DifferentData_DifferentHash()
    {
        var data1 = new byte[1000];
        var data2 = new byte[1000];
        Random.Shared.NextBytes(data1);
        Random.Shared.NextBytes(data2);
        var hash1 = VisualRegression.ComputeHash(data1);
        var hash2 = VisualRegression.ComputeHash(data2);
        // Different random data should (very likely) produce different hashes
        Assert.That(hash1, Is.Not.EqualTo(hash2));
    }

    [Test]
    public void Compare_IdenticalHashes_Returns1()
    {
        var similarity = VisualRegression.Compare(0xABCDEF0123456789, 0xABCDEF0123456789);
        Assert.That(similarity, Is.EqualTo(1.0));
    }

    [Test]
    public void Compare_CompletelyDifferent_ReturnsLow()
    {
        var similarity = VisualRegression.Compare(0x0000000000000000, 0xFFFFFFFFFFFFFFFF);
        Assert.That(similarity, Is.EqualTo(0.0));
    }

    [Test]
    public async Task CompareWithBaseline_NewBaseline_SavesAndReturnsPass()
    {
        var data = new byte[1000];
        Random.Shared.NextBytes(data);
        var result = await VisualRegression.CompareWithBaseline("test-screen", data, _tempDir);
        Assert.That(result.IsNewBaseline, Is.True);
        Assert.That(result.Passed, Is.True);
        Assert.That(File.Exists(Path.Combine(_tempDir, "test-screen.hash")), Is.True);
    }

    [Test]
    public async Task CompareWithBaseline_SameImage_ReturnsPass()
    {
        var data = new byte[1000];
        Random.Shared.NextBytes(data);
        await VisualRegression.SaveBaseline("test-screen", data, _tempDir);
        var result = await VisualRegression.CompareWithBaseline("test-screen", data, _tempDir);
        Assert.That(result.IsNewBaseline, Is.False);
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Similarity, Is.EqualTo(1.0));
    }

    [Test]
    public async Task CompareWithBaseline_DifferentImage_DetectsRegression()
    {
        var data1 = new byte[1000];
        var data2 = new byte[1000];
        // Fill with structured data that produces different hashes
        // data1: ascending pattern (0,1,2,...) — mix of above/below average
        for (int i = 0; i < data1.Length; i++) data1[i] = (byte)(i % 256);
        // data2: descending pattern (255,254,...) — inverted relationship to average
        for (int i = 0; i < data2.Length; i++) data2[i] = (byte)(255 - (i % 256));
        await VisualRegression.SaveBaseline("test-screen", data1, _tempDir);
        var result = await VisualRegression.CompareWithBaseline("test-screen", data2, _tempDir);
        Assert.That(result.Passed, Is.False);
        Assert.That(result.Similarity, Is.LessThan(0.5));
    }
}
