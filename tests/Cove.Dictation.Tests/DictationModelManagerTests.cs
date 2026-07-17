using Cove.Dictation;
using Xunit;

namespace Cove.Dictation.Tests;

public sealed class DictationModelManagerTests
{
    [Fact]
    public void ModelDirAbsent_TryGetReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-dictate-" + Guid.NewGuid().ToString("N")[..8]);
        var mgr = new DictationModelManager(root);
        Assert.Null(mgr.TryGetModelDir());
    }

    [Fact]
    public void CompleteModelDir_TryGetReturnsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-dictate-" + Guid.NewGuid().ToString("N")[..8]);
        var dir = Path.Combine(root, DictationModelManager.ModelDirName);
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var f in DictationModelManager.RequiredFiles)
                File.WriteAllText(Path.Combine(dir, f), "x");
            File.WriteAllText(Path.Combine(root, DictationModelManager.VadFileName), "x");
            var mgr = new DictationModelManager(root);
            Assert.Equal(dir, mgr.TryGetModelDir());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IncompleteModelDir_TryGetReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-dictate-" + Guid.NewGuid().ToString("N")[..8]);
        var dir = Path.Combine(root, DictationModelManager.ModelDirName);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "tokens.txt"), "x");
            var mgr = new DictationModelManager(root);
            Assert.Null(mgr.TryGetModelDir());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChecksumMismatch_DeletesArchiveAndThrows()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-dictate-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "bogus.tar.bz2");
            await File.WriteAllTextAsync(archive, "not a real archive");
            await Assert.ThrowsAsync<DictationException>(
                () => DictationModelManager.VerifyChecksumAsync(archive, new string('0', 64), CancellationToken.None));
            Assert.False(File.Exists(archive));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
