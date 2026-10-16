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
        Assert.Equal(new AppSettings { ThemeId = "prueba", SafeMode = false }, toSave);
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
