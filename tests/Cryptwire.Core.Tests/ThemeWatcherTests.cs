using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Tests;

/// <summary>Con ficheros de verdad: los avisos del sistema llegan con algo de retraso, de ahí las esperas.</summary>
public sealed class ThemeWatcherTests : IDisposable
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly TempDir dir = new();
    private readonly TempDir outside = new();
    private int changes;

    public void Dispose()
    {
        dir.Dispose();
        outside.Dispose();
    }

    private ThemeWatcher Watch(TimeSpan? quiet = null)
    {
        var watcher = new ThemeWatcher(dir.Path, quiet ?? Quiet);
        watcher.Changed += (_, _) => Interlocked.Increment(ref changes);
        return watcher;
    }

    private static async Task WaitForAsync(Func<int> count, int expected)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (count() < expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    private Task WaitForChangesAsync(int expected) => WaitForAsync(() => Volatile.Read(ref changes), expected);

    [Fact]
    public async Task AnEditedCssRaisesChanged()
    {
        dir.Write("skin.css", "a {}");
        using var watcher = Watch();

        dir.Write("skin.css", "a { color: gold; }");
        await WaitForChangesAsync(1);

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task FilesInSubfoldersCountToo()
    {
        dir.Write(@"fuentes\Cinzel.ttf", "x");
        using var watcher = Watch();

        dir.Write(@"fuentes\Cinzel.ttf", "y");
        await WaitForChangesAsync(1);

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task ABurstOfWritesReloadsOnlyOnce()
    {
        // La espera es larga a propósito: las escrituras van mucho más rápido, aunque la máquina vaya cargada.
        var quiet = TimeSpan.FromMilliseconds(800);
        using var watcher = Watch(quiet);

        for (var i = 0; i < 6; i++)
        {
            dir.Write("skin.css", $"a {{ order: {i}; }}");
            await Task.Delay(30);
        }

        await WaitForChangesAsync(1);
        await Task.Delay(quiet * 2);

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task AnEditorThatSavesThroughATemporaryFileIsCaught()
    {
        dir.Write("skin.css", "a {}");
        var temp = outside.Write("skin.css.tmp", "a { color: gold; }");
        using var watcher = Watch();

        // Solo el cambio de nombre toca la carpeta vigilada.
        File.Move(temp, Path.Combine(dir.Path, "skin.css"), overwrite: true);
        await WaitForChangesAsync(1);

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task NothingArrivesAfterDispose()
    {
        var control = 0;
        using var alive = new ThemeWatcher(dir.Path, Quiet);
        alive.Changed += (_, _) => Interlocked.Increment(ref control);
        var closed = Watch();
        closed.Dispose();

        dir.Write("skin.css", "a {}");
        await WaitForAsync(() => Volatile.Read(ref control), 1);
        await Task.Delay(Quiet * 2);

        Assert.Equal(1, control);
        Assert.Equal(0, changes);
    }
}
