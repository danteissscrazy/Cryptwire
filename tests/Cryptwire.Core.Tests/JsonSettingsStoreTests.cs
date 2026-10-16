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

        Assert.Equal(new AppSettings(), settings);
        Assert.Null(settings.ThemeId);
        Assert.False(settings.SafeMode);
    }

    [Fact]
    public void WhatYouSaveIsWhatYouLoad()
    {
        var store = new JsonSettingsStore(SettingsPath);

        store.Save(new AppSettings { ThemeId = "relicario", SafeMode = true });

        Assert.Equal(new AppSettings { ThemeId = "relicario", SafeMode = true }, store.Load());
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void TheWindowSurvivesASaveAndALoad()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var settings = new AppSettings { ThemeId = "relicario", Window = new WindowBounds(-1080, 0, 0, 1920, Maximized: true) };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
        Assert.DoesNotContain("isPlausible", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void SavingAgainReplacesTheFile()
    {
        var store = new JsonSettingsStore(SettingsPath);
        store.Save(new AppSettings { ThemeId = "prueba", SafeMode = true });

        store.Save(new AppSettings { ThemeId = "relicario" });

        Assert.Equal(new AppSettings { ThemeId = "relicario" }, store.Load());
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

        Assert.Equal(new AppSettings(), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void UnknownFieldsFromANewerVersionAreIgnored()
    {
        dir.Write(@"Cryptwire\settings.json", """{ "themeId": "relicario", "toneRules": [] }""");

        Assert.Equal("relicario", new JsonSettingsStore(SettingsPath).Load().ThemeId);
    }
}
