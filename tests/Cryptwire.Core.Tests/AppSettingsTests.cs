using Cryptwire.Core.Settings;
using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void TheDefaultsSoundWithTheSkinAndCloseToTheTray()
    {
        var settings = new AppSettings();

        Assert.Equal(ToneChoice.FromSkin, settings.MessageTone);
        Assert.Equal(ToneChoice.FromSkin, settings.CallTone);
        Assert.Empty(settings.ToneRules);
        Assert.Null(settings.Wallpaper);
        Assert.True(settings.ShowToasts);
        Assert.True(settings.CloseToTray);
        Assert.False(settings.TrayHintShown);
    }

    [Fact]
    public void AToneRuleIsAddedThenChangedInItsPlace()
    {
        var settings = new AppSettings()
            .WithToneRule("Trish", "trish.wav", null)
            .WithToneRule("Lady", "lady.wav", null)
            .WithToneRule("trish", "otro.wav", "llama.wav");

        Assert.Equal([new ToneRule("trish", "otro.wav", "llama.wav"), new ToneRule("Lady", "lady.wav")], settings.ToneRules);
    }

    [Fact]
    public void ARuleWithoutTonesIsRemoved()
    {
        var settings = new AppSettings().WithToneRule("Trish", "trish.wav", null);

        Assert.Empty(settings.WithToneRule("Trish", " ", null).ToneRules);
        Assert.Empty(settings.WithoutToneRule("TRISH").ToneRules);
        Assert.Empty(new AppSettings().WithToneRule("Lady", null, null).ToneRules);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u202A")]
    public void ARuleNeedsAChatName(string chat)
    {
        Assert.Empty(new AppSettings().WithToneRule(chat, "x.wav", null).ToneRules);
    }

    [Fact]
    public void NormalizedFillsTheGapsOfAHandEditedFile()
    {
        var broken = new AppSettings
        {
            MessageTone = null!,
            CallTone = null!,
            ToneRules = [null!, new ToneRule(" ", "x.wav"), new ToneRule(" Trish ", "a.wav", ""), new ToneRule("trish", "b.wav"), new ToneRule("Lady")],
            Wallpaper = new WallpaperSettings(File: "", Dim: 50),
        };

        var fixedUp = broken.Normalized();

        Assert.Equal(ToneChoice.FromSkin, fixedUp.MessageTone);
        Assert.Equal(ToneChoice.FromSkin, fixedUp.CallTone);
        Assert.Equal([new ToneRule("Trish", "a.wav"), new ToneRule("Lady")], fixedUp.ToneRules);
        Assert.Null(fixedUp.Wallpaper);
    }

    [Fact]
    public void NormalizedWithoutRulesAtAllGivesAnEmptyList()
    {
        Assert.Empty((new AppSettings { ToneRules = null! }).Normalized().ToneRules);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(40, 40)]
    [InlineData(150, WallpaperSettings.MaxDim)]
    public void TheWallpaperDimStaysInRange(int dim, int expected)
    {
        var settings = new AppSettings { Wallpaper = new WallpaperSettings(@"C:\fondos\fondo.jpg", dim) }.Normalized();

        Assert.Equal(expected, settings.Wallpaper!.Dim);
    }

    [Fact]
    public void UpdatingOtherSettingsLeavesSkinSafeModeAndWindowAlone()
    {
        var window = new WindowBounds(0, 0, 1400, 900, Maximized: false);
        var session = new SkinSession(new AppSettings { ThemeId = "relicario", SafeMode = true, Window = window }, safeModeThisSession: false);

        var saved = session.Update(s => s with
        {
            ThemeId = "otra",
            SafeMode = false,
            Window = null,
            CloseToTray = false,
            MessageTone = ToneChoice.Silent,
        });

        Assert.Equal("relicario", saved.ThemeId);
        Assert.True(saved.SafeMode);
        Assert.Equal(window, saved.Window);
        Assert.False(saved.CloseToTray);
        Assert.Equal(ToneChoice.Silent, saved.MessageTone);
        Assert.Same(saved, session.Settings);
    }

    [Fact]
    public void UpdatesAreAppliedOverTheLatestSettings()
    {
        var session = new SkinSession(new AppSettings(), safeModeThisSession: false);

        session.Update(s => s.WithToneRule("Trish", "trish.wav", null));
        session.Select("prueba");
        var saved = session.Update(s => s with { ShowToasts = false });

        Assert.Equal("prueba", saved.ThemeId);
        Assert.False(saved.ShowToasts);
        Assert.Equal([new ToneRule("Trish", "trish.wav")], saved.ToneRules);
    }
}
