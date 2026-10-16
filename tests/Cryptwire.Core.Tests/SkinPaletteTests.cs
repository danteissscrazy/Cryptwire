using System.Text.Json;
using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Tests;

public sealed class SkinPaletteTests : IDisposable
{
    private readonly TempDir dir = new();

    public void Dispose() => dir.Dispose();

    private static SkinPalette From(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return SkinPalette.FromManifest(doc.RootElement);
    }

    [Fact]
    public void WithoutColorsTheWindowLooksLikeTheRelicario()
    {
        Assert.Equal(SkinPalette.Default, From("""{ "name": "Sin colores" }"""));
        Assert.Equal(SkinPalette.Default, From("""{ "ventana": "rojo" }"""));
        Assert.Equal(SkinPalette.Default, From("[]"));
    }

    [Fact]
    public void TheSkinColorsAreUsedAndTheMissingOnesComeFromTheDefault()
    {
        var palette = From("""{ "ventana": { "fondo": "#120408", "acento": "#ffd700", "alerta": "#80E0435A" } }""");

        Assert.Equal("#120408", palette.Background);
        Assert.Equal("#FFD700", palette.Accent);
        Assert.Equal("#80E0435A", palette.Alert);
        Assert.Equal(SkinPalette.Default.Text, palette.Text);
        Assert.Equal(SkinPalette.Default.Glow, palette.Glow);
    }

    [Theory]
    [InlineData("\"red\"")]
    [InlineData("\"#12345\"")]
    [InlineData("\"#1234567\"")]
    [InlineData("\"#GGGGGG\"")]
    [InlineData("\" #120408\"")]
    [InlineData("\"#120408;background:url(x)\"")]
    [InlineData("12")]
    [InlineData("null")]
    public void AnythingThatIsNotAColorIsIgnored(string value)
    {
        var palette = From($$"""{ "ventana": { "fondo": {{value}} } }""");

        Assert.Equal(SkinPalette.Default.Background, palette.Background);
    }

    [Fact]
    public void ThemeManagerReadsThePaletteWithTheName()
    {
        dir.Write(@"pieles\carmesi\skin.css", "body {}");
        dir.Write(@"pieles\carmesi\theme.json", """{ "name": "Carmesí", "ventana": { "acento": "#E0435A" } }""");
        dir.Write(@"pieles\llana\skin.css", "body {}");

        var themes = new ThemeManager([Path.Combine(dir.Path, "pieles")]).LoadAll();

        Assert.Equal("#E0435A", themes.Single(t => t.Id == "carmesi").Palette.Accent);
        Assert.Equal("Carmesí", themes.Single(t => t.Id == "carmesi").Name);
        Assert.Equal(SkinPalette.Default, themes.Single(t => t.Id == "llana").Palette);
    }

    [Fact]
    public void AManifestWithoutANameStillGivesItsColors()
    {
        dir.Write(@"pieles\carmesi\skin.css", "body {}");
        dir.Write(@"pieles\carmesi\theme.json", """{ "ventana": { "acento": "#E0435A" } }""");

        var theme = new ThemeManager([Path.Combine(dir.Path, "pieles")]).LoadAll().Single();

        Assert.Equal("carmesi", theme.Name);
        Assert.Equal("#E0435A", theme.Palette.Accent);
    }

    [Fact]
    public void ABrokenManifestGivesTheDefaultColors()
    {
        dir.Write(@"pieles\rota\skin.css", "body {}");
        dir.Write(@"pieles\rota\theme.json", "{ roto");

        Assert.Equal(SkinPalette.Default, new ThemeManager([Path.Combine(dir.Path, "pieles")]).LoadAll().Single().Palette);
    }

    [Fact]
    public void TheShippedSkinsDeclareValidColors()
    {
        var themes = new ThemeManager([RepoThemes()]).LoadAll();

        Assert.Contains(themes, t => t.Id == "relicario");
        foreach (var theme in themes)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(theme.Directory, ThemeManager.ManifestFile)));
            var declared = doc.RootElement.GetProperty(SkinPalette.ManifestKey);
            foreach (var color in declared.EnumerateObject())
            {
                Assert.True(SkinPalette.IsColor(color.Value.GetString()), $"{theme.Id}: {color.Name}");
            }
        }
    }

    private static string RepoThemes()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            var themes = Path.Combine(current.FullName, "themes");
            if (File.Exists(Path.Combine(current.FullName, "Cryptwire.slnx")) && Directory.Exists(themes))
            {
                return themes;
            }
        }

        throw new DirectoryNotFoundException("No encuentro la carpeta themes del repo.");
    }
}
