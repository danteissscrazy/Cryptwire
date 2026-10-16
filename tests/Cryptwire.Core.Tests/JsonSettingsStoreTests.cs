using Cryptwire.Core.Settings;

namespace Cryptwire.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly TempDir dir = new();

    public void Dispose() => dir.Dispose();

    private string SettingsPath => Path.Combine(dir.Path, "Cryptwire", "settings.json");

    [Fact]
    public void WithoutAFileYouGetTheDefaults()
    {
        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equivalent(new AppSettings(), settings);
        Assert.Null(settings.ThemeId);
        Assert.False(settings.SafeMode);
    }

    [Fact]
    public void WhatYouSaveIsWhatYouLoad()
    {
        var store = new JsonSettingsStore(SettingsPath);

        store.Save(new AppSettings { ThemeId = "relicario", SafeMode = true });

        Assert.Equivalent(new AppSettings { ThemeId = "relicario", SafeMode = true }, store.Load());
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void TheWindowSurvivesASaveAndALoad()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var settings = new AppSettings { ThemeId = "relicario", Window = new WindowBounds(-1080, 0, 0, 1920, Maximized: true) };

        store.Save(settings);

        Assert.Equivalent(settings, store.Load(), strict: true);
        Assert.DoesNotContain("isPlausible", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void SavingAgainReplacesTheFile()
    {
        var store = new JsonSettingsStore(SettingsPath);
        store.Save(new AppSettings { ThemeId = "prueba", SafeMode = true });

        store.Save(new AppSettings { ThemeId = "relicario" });

        Assert.Equivalent(new AppSettings { ThemeId = "relicario" }, store.Load());
    }

    [Fact]
    public void TheFileIsReadableJson()
    {
        new JsonSettingsStore(SettingsPath).Save(new AppSettings { ThemeId = "relicario" });

        var json = File.ReadAllText(SettingsPath);

        Assert.Contains("\"themeId\": \"relicario\"", json);
        Assert.Contains("\"safeMode\": false", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ roto")]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "themeId": 7 }""")]
    public void ABrokenFileNeverStopsTheApp(string content)
    {
        dir.Write(@"Cryptwire\settings.json", content);

        Assert.Equivalent(new AppSettings(), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void TonesWallpaperAndTraySurviveASaveAndALoad()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var settings = new AppSettings
        {
            MessageTone = ToneChoice.FromFile(@"C:\Cryptwire\tonos\acero.wav"),
            CallTone = ToneChoice.Silent,
            Wallpaper = new WallpaperSettings(@"C:\Cryptwire\fondos\fondo.jpg", Dim: 30),
            ShowToasts = false,
            CloseToTray = false,
            TrayHintShown = true,
        }.WithToneRule("Trish", @"C:\Cryptwire\tonos\trish.wav", null);

        store.Save(settings);

        Assert.Equivalent(settings, store.Load(), strict: true);
        var json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"source\": \"File\"", json);
        Assert.Contains("\"source\": \"None\"", json);
        Assert.Contains("\"chat\": \"Trish\"", json);
    }

    [Fact]
    public void AFileFromPhaseTwoGetsTheNewDefaults()
    {
        dir.Write(@"Cryptwire\settings.json", """{ "themeId": "relicario", "safeMode": false }""");

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal("relicario", settings.ThemeId);
        Assert.Equal(ToneChoice.FromSkin, settings.MessageTone);
        Assert.Empty(settings.ToneRules);
        Assert.True(settings.CloseToTray);
        Assert.True(settings.ShowToasts);
    }

    [Fact]
    public void NullsInAHandEditedFileGetTheirDefaults()
    {
        dir.Write(@"Cryptwire\settings.json", """{ "themeId": "relicario", "messageTone": null, "toneRules": [null, { "chat": "Trish", "messageTone": "t.wav" }] }""");

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal("relicario", settings.ThemeId);
        Assert.Equal(ToneChoice.FromSkin, settings.MessageTone);
        Assert.Equal([new ToneRule("Trish", "t.wav")], settings.ToneRules);
    }

    [Fact]
    public void AnUnknownToneSourceIsABrokenFile()
    {
        dir.Write(@"Cryptwire\settings.json", """{ "themeId": "relicario", "messageTone": { "source": "Telepatia" } }""");

        Assert.Equivalent(new AppSettings(), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void UnknownFieldsFromANewerVersionAreIgnored()
    {
        dir.Write(@"Cryptwire\settings.json", """{ "themeId": "relicario", "toneRules": [] }""");

        Assert.Equal("relicario", new JsonSettingsStore(SettingsPath).Load().ThemeId);
    }
}
