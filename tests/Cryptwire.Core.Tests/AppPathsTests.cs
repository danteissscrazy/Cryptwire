namespace Cryptwire.Core.Tests;

public class AppPathsTests
{
    private static readonly string LocalAppData = Path.Combine(Path.GetTempPath(), "cw-local");
    private static readonly string AppDir = Path.Combine(Path.GetTempPath(), "cw-app");

    [Fact]
    public void WebViewSessionLivesInLocalAppData()
    {
        var paths = new AppPaths(LocalAppData, AppDir);

        Assert.Equal(Path.Combine(LocalAppData, "Cryptwire", "WebView2"), paths.WebViewDataDir);
    }

    [Fact]
    public void ThemesShipNextToTheExecutable()
    {
        var paths = new AppPaths(LocalAppData, AppDir);

        Assert.Equal(Path.Combine(AppDir, "themes"), paths.ThemesDir);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relativa")]
    [InlineData(@"relativa\Cryptwire")]
    public void RelativeOrEmptyRootsAreRejected(string root)
    {
        Assert.Throws<InvalidOperationException>(() => new AppPaths(root, AppDir));
        Assert.Throws<InvalidOperationException>(() => new AppPaths(LocalAppData, root));
    }

    [Fact]
    public void FromEnvironmentNeverPointsAtTheWorkingDirectory()
    {
        var paths = AppPaths.FromEnvironment();

        Assert.True(Path.IsPathFullyQualified(paths.WebViewDataDir));
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            paths.WebViewDataDir,
            StringComparison.OrdinalIgnoreCase);
    }
}
