using Cryptwire.Core.Settings;
using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Tests;

public class SkinSessionTests
{
    private static readonly AppSettings Saved = new() { ThemeId = "relicario" };

    [Fact]
    public void StartsWithWhatWasSaved()
    {
        var session = new SkinSession(Saved with { SafeMode = true }, safeModeThisSession: false);

        Assert.True(session.SafeMode);
        Assert.Equal("relicario", session.Settings.ThemeId);
    }

    [Fact]
    public void SafeModeFromTheCommandLineIsNeverSaved()
    {
        var session = new SkinSession(Saved, safeModeThisSession: true);

        Assert.True(session.SafeMode);
        Assert.False(session.Settings.SafeMode);
    }

    [Fact]
    public void F8LeavesSafeModeFromTheCommandLineAndSavesItOff()
    {
        var session = new SkinSession(Saved, safeModeThisSession: true);

        var toSave = session.ToggleSafeMode();

        Assert.False(session.SafeMode);
        Assert.False(toSave.SafeMode);
    }

    [Fact]
    public void F8TwiceComesBackToTheSkin()
    {
        var session = new SkinSession(Saved, safeModeThisSession: false);

        Assert.True(session.ToggleSafeMode().SafeMode);
        Assert.True(session.SafeMode);
        Assert.False(session.ToggleSafeMode().SafeMode);
        Assert.False(session.SafeMode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ChoosingASkinAlwaysShowsIt(bool savedSafeMode, bool safeModeThisSession)
    {
        var session = new SkinSession(Saved with { SafeMode = savedSafeMode }, safeModeThisSession);

        var toSave = session.Select("prueba");

        Assert.False(session.SafeMode);
        Assert.Equivalent(new AppSettings { ThemeId = "prueba", SafeMode = false }, toSave);
    }

    [Fact]
    public void TheWindowOpensWhereYouLeftIt()
    {
        var left = new WindowBounds(100, 50, 1180, 1870, Maximized: true);
        var session = new SkinSession(Saved with { Window = left }, safeModeThisSession: false);

        Assert.Equal(left, session.StartWindow);
    }

    [Fact]
    public void WithoutASavedWindowItOpensCentred()
    {
        Assert.Null(new SkinSession(Saved, safeModeThisSession: false).StartWindow);
    }

    [Theory]
    [InlineData(0, 0, 50, 50)]
    [InlineData(0, 0, 100000, 900)]
    [InlineData(-90000, 0, -89000, 900)]
    [InlineData(500, 500, 100, 100)]
    public void AnImpossibleSavedWindowIsIgnored(int left, int top, int right, int bottom)
    {
        var session = new SkinSession(Saved with { Window = new WindowBounds(left, top, right, bottom, false) }, safeModeThisSession: false);

        Assert.Null(session.StartWindow);
    }

    [Fact]
    public void ClosingRemembersTheWindowAndKeepsEverythingElse()
    {
        var session = new SkinSession(Saved with { SafeMode = true }, safeModeThisSession: false);
        var closed = new WindowBounds(-1080, 0, 0, 1920, Maximized: true);

        var toSave = session.RememberWindow(closed);

        Assert.Equal(Saved with { SafeMode = true, Window = closed }, toSave);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnImpossibleWindowOnCloseKeepsTheLastGoodOne(bool hadOne)
    {
        var good = new WindowBounds(0, 0, 1280, 820, false);
        var session = new SkinSession(Saved with { Window = hadOne ? good : null }, safeModeThisSession: false);

        var toSave = session.RememberWindow(new WindowBounds(0, 0, 10, 10, false));

        Assert.Equal(hadOne ? good : null, toSave.Window);
        Assert.Equal(hadOne ? good : null, session.RememberWindow(null).Window);
    }

    [Fact]
    public void ChangingSkinOrSafeModeKeepsTheWindow()
    {
        var window = new WindowBounds(0, 0, 1280, 820, false);
        var session = new SkinSession(Saved with { Window = window }, safeModeThisSession: false);

        Assert.Equal(window, session.Select("prueba").Window);
        Assert.Equal(window, session.ToggleSafeMode().Window);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ChoosingNoSkinIsRejected(string themeId)
    {
        var session = new SkinSession(Saved, safeModeThisSession: false);

        Assert.ThrowsAny<ArgumentException>(() => session.Select(themeId));
        Assert.Equal(Saved, session.Settings);
    }
}
