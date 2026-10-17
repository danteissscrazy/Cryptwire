using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Tests;

public sealed class ThemeManagerTests : IDisposable
{
    private readonly TempDir app = new();
    private readonly TempDir user = new();

    public void Dispose()
    {
        app.Dispose();
        user.Dispose();
    }

    private ThemeManager Manager() => new([app.Path, user.Path]);

    private static Theme ThemeAt(string id) => new(id, id, Path.Combine(Path.GetTempPath(), id));

    private static void WriteBytes(string path, long length)
    {
        using var file = File.Create(path);
        file.SetLength(length);
    }

    /// <summary>Una unión de directorios: en Windows no necesita permisos de administrador.</summary>
    private static void MakeJunction(string link, string target)
    {
        using var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target])
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        mklink.WaitForExit();
        Assert.True(Directory.Exists(link), "no se ha podido crear la unión de prueba");
    }

    [Fact]
    public void FindsEveryFolderWithASkinCss()
    {
        app.Write(@"relicario\skin.css", "a {}");
        app.Write(@"prueba\skin.css", "a {}");
        app.Write(@"a-medias\fondo.svg", "<svg/>");
        app.Write("suelto.css", "a {}");

        var themes = Manager().LoadAll();

        Assert.Equal(["prueba", "relicario"], themes.Select(t => t.Id));
        Assert.Equal(Path.Combine(app.Path, "relicario"), themes[1].Directory);
        Assert.Equal(Path.Combine(app.Path, "relicario", "skin.css"), themes[1].CssPath);
        Assert.Equal(Path.Combine(app.Path, "relicario", "tono.wav"), themes[1].TonePath);
    }

    [Fact]
    public void TheNameComesFromThemeJsonAndOrdersTheList()
    {
        app.Write(@"relicario\skin.css", "a {}");
        app.Write(@"relicario\theme.json", """{ "name": "Relicario" }""");
        app.Write(@"zeta\skin.css", "a {}");
        app.Write(@"zeta\theme.json", """{ "name": "Alba" }""");

        var themes = Manager().LoadAll();

        Assert.Equal(["Alba", "Relicario"], themes.Select(t => t.Name));
        Assert.Equal("zeta", themes[0].Id);
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("""["Relicario"]""")]
    [InlineData("""{ "name": 7 }""")]
    [InlineData("""{ "name": "   " }""")]
    [InlineData("""{ "otro": "Relicario" }""")]
    public void ABrokenThemeJsonFallsBackToTheFolderName(string manifest)
    {
        app.Write(@"relicario\skin.css", "a {}");
        app.Write(@"relicario\theme.json", manifest);

        var theme = Assert.Single(Manager().LoadAll());

        Assert.Equal("relicario", theme.Name);
    }

    [Fact]
    public void TheNameIsCleanedBecauseItIsShownInTheTitleBar()
    {
        app.Write(@"relicario\skin.css", "a {}");
        app.Write(@"relicario\theme.json", """{ "name": "  Reli\ncario\u0007 de un nombre larguísimo que no cabe en ningún menú  " }""");

        var theme = Assert.Single(Manager().LoadAll());

        Assert.Equal("Relicario de un nombre larguísimo que no", theme.Name);
        Assert.DoesNotContain(theme.Name, char.IsControl);
    }

    [Fact]
    public void InvisibleAndLineBreakingCharactersAreDropped()
    {
        // Separador de línea, inversión de sentido (RLO), espacio de anchura cero y separador de párrafo.
        var name = $"Rock & Roll{(char)0x2028}x{(char)0x202E}mal{(char)0x200B}o{(char)0x2029}";
        app.Write(@"relicario\skin.css", "a {}");
        app.Write(@"relicario\theme.json", System.Text.Json.JsonSerializer.Serialize(new { name }));

        Assert.Equal("Rock & Rollxmalo", Assert.Single(Manager().LoadAll()).Name);
    }

    [Fact]
    public void TheNameIsCutWithoutSplittingAnEmoji()
    {
        var name = new string('a', 39) + char.ConvertFromUtf32(0x1F480) + "fin";
        app.Write(@"relicario\skin.css", "a {}");
        app.Write(@"relicario\theme.json", System.Text.Json.JsonSerializer.Serialize(new { name }));

        Assert.Equal(new string('a', 39) + char.ConvertFromUtf32(0x1F480), Assert.Single(Manager().LoadAll()).Name);
    }

    [Fact]
    public void AHugeThemeJsonIsNotRead()
    {
        app.Write(@"relicario\skin.css", "a {}");
        app.Write(@"relicario\theme.json", $$"""{ "name": "Gigante", "relleno": "{{new string('x', 20_000)}}" }""");

        Assert.Equal("relicario", Assert.Single(Manager().LoadAll()).Name);
    }

    /// <summary>
    /// Un enlace simbólico a un fichero. Sin el modo de desarrollador de Windows (ni administrador) no se
    /// puede crear: entonces devuelve false y la prueba que lo usa no tiene nada que comprobar.
    /// </summary>
    private static bool TryMakeFileLink(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [Fact]
    public void AThemeJsonThatIsALinkIsNotRead()
    {
        app.Write(@"relicario\skin.css", "a {}");
        user.Write("otro.json", """{ "name": "Otro sitio" }""");

        if (TryMakeFileLink(Path.Combine(app.Path, "relicario", "theme.json"), Path.Combine(user.Path, "otro.json")))
        {
            Assert.Equal("relicario", Assert.Single(new ThemeManager([app.Path]).LoadAll()).Name);
        }
    }

    [Fact]
    public void ASkinToneHasToBeARealWaveFile()
    {
        var wave = Path.Combine(app.Path, "tono.wav");
        File.WriteAllBytes(wave, [.. "RIFF"u8, 4, 0, 0, 0, .. "WAVE"u8, .. "fmt "u8]);
        var mp3 = Path.Combine(app.Path, "falso.wav");
        File.WriteAllBytes(mp3, [0x49, 0x44, 0x33, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        var cut = Path.Combine(app.Path, "corto.wav");
        File.WriteAllBytes(cut, [.. "RIFF"u8]);
        var avi = Path.Combine(app.Path, "video.wav");
        File.WriteAllBytes(avi, [.. "RIFF"u8, 4, 0, 0, 0, .. "AVI "u8]);

        Assert.True(ThemeManager.IsWaveFile(wave));
        Assert.False(ThemeManager.IsWaveFile(mp3));
        Assert.False(ThemeManager.IsWaveFile(avi));
        Assert.False(ThemeManager.IsWaveFile(cut));
        Assert.False(ThemeManager.IsWaveFile(Path.Combine(app.Path, "no-esta.wav")));
    }

    [Fact]
    public void ASkinToneThatIsALinkIsNeverOpened()
    {
        var target = Path.Combine(user.Path, "de-verdad.wav");
        File.WriteAllBytes(target, [.. "RIFF"u8, 4, 0, 0, 0, .. "WAVE"u8]);

        if (TryMakeFileLink(Path.Combine(app.Path, "tono.wav"), target))
        {
            Assert.False(ThemeManager.IsWaveFile(Path.Combine(app.Path, "tono.wav")));
        }
    }

    [Fact]
    public void YourOwnCopyOfAThemeReplacesTheOneThatShipsWithTheApp()
    {
        app.Write(@"relicario\skin.css", "a {}");
        user.Write(@"Relicario\skin.css", "a { color: gold; }");

        var theme = Assert.Single(Manager().LoadAll());

        Assert.Equal(Path.Combine(user.Path, "Relicario"), theme.Directory);
    }

    [Fact]
    public void MissingFoldersAreJustEmpty()
    {
        var manager = new ThemeManager([Path.Combine(app.Path, "no-existe"), user.Path]);

        Assert.Empty(manager.LoadAll());
    }

    [Theory]
    [InlineData("")]
    [InlineData("themes")]
    [InlineData(@"..\themes")]
    public void RelativeFoldersAreRejected(string root)
    {
        Assert.Throws<ArgumentException>(() => new ThemeManager([root]));
    }

    [Fact]
    public void ResolveKeepsYourChoice()
    {
        IReadOnlyList<Theme> themes = [ThemeAt("prueba"), ThemeAt("relicario")];

        Assert.Equal("prueba", ThemeManager.Resolve(themes, "PRUEBA")?.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("borrada")]
    public void ResolveFallsBackToTheDefaultTheme(string? chosen)
    {
        IReadOnlyList<Theme> themes = [ThemeAt("prueba"), ThemeAt("relicario")];

        Assert.Equal(ThemeManager.DefaultThemeId, ThemeManager.Resolve(themes, chosen)?.Id);
    }

    [Fact]
    public void ResolveFallsBackToTheFirstWhenEvenTheDefaultIsGone()
    {
        IReadOnlyList<Theme> themes = [ThemeAt("alba"), ThemeAt("prueba")];

        Assert.Equal("alba", ThemeManager.Resolve(themes, "borrada")?.Id);
        Assert.Null(ThemeManager.Resolve([], "relicario"));
    }

    [Theory]
    [InlineData("alba", "prueba")]
    [InlineData("prueba", "relicario")]
    [InlineData("relicario", "alba")]
    [InlineData("borrada", "alba")]
    [InlineData(null, "alba")]
    public void NextCyclesThroughTheList(string? current, string expected)
    {
        IReadOnlyList<Theme> themes = [ThemeAt("alba"), ThemeAt("prueba"), ThemeAt("relicario")];

        Assert.Equal(expected, ThemeManager.Next(themes, current)?.Id);
    }

    [Fact]
    public void NextWithNoThemesIsNull()
    {
        Assert.Null(ThemeManager.Next([], "relicario"));
    }

    [Fact]
    public void BuildCssInlinesTheThemeFiles()
    {
        app.Write(@"relicario\skin.css", """#app { background: url("fondo.svg"), url("../fuera.svg"); }""");
        app.Write(@"relicario\fondo.svg", "<svg/>");
        app.Write("fuera.svg", "<svg/>");
        var theme = Assert.Single(Manager().LoadAll());

        var built = ThemeManager.BuildCss(theme);

        Assert.Contains($"url(\"data:image/svg+xml;base64,{Convert.ToBase64String("<svg/>"u8.ToArray())}\")", built.Css);
        Assert.Equal(["../fuera.svg"], built.Skipped);
    }

    [Fact]
    public void AnAssetBiggerThanTheLimitIsSkipped()
    {
        app.Write(@"relicario\skin.css", """#app { background: url("enorme.png"), url("fondo.svg"); }""");
        app.Write(@"relicario\fondo.svg", "<svg/>");
        WriteBytes(Path.Combine(app.Path, "relicario", "enorme.png"), ThemeManager.MaxAssetBytes + 1);

        var built = ThemeManager.BuildCss(Assert.Single(Manager().LoadAll()));

        Assert.Equal(["enorme.png"], built.Skipped);
        Assert.Contains("data:image/svg+xml", built.Css);
    }

    [Fact]
    public void ThereIsACapOnEverythingAThemeInlines()
    {
        // Un fondo de 6 MB usado seis veces son 36 MB incrustados: los que no caben se saltan.
        var uses = string.Join(", ", Enumerable.Repeat("""url("fondo.png")""", 6));
        app.Write(@"relicario\skin.css", $"#app {{ background: {uses}; }}");
        WriteBytes(Path.Combine(app.Path, "relicario", "fondo.png"), 6 * 1024 * 1024);

        var built = ThemeManager.BuildCss(Assert.Single(Manager().LoadAll()));

        Assert.Equal("fondo.png", Assert.Single(built.Skipped));
        Assert.True(built.Css.Length < 48 * 1024 * 1024);
    }

    [Fact]
    public void AHugeSkinCssIsRefused()
    {
        var theme = Path.Combine(app.Path, "relicario");
        Directory.CreateDirectory(theme);
        WriteBytes(Path.Combine(theme, "skin.css"), ThemeManager.MaxCssBytes + 1);

        Assert.Throws<IOException>(() => ThemeManager.BuildCss(Assert.Single(Manager().LoadAll())));
    }

    [Fact]
    public void FilesReachedThroughALinkAreNeverRead()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Fuera de la piel, un «.png» que en realidad es otro fichero cualquiera del disco.
        app.Write(@"fuera\fondo.png", "secreto");
        app.Write(@"relicario\skin.css", """#app { background: url("img/fondo.png"); }""");
        var link = Path.Combine(app.Path, "relicario", "img");
        MakeJunction(link, Path.Combine(app.Path, "fuera"));
        try
        {
            var built = ThemeManager.BuildCss(Assert.Single(Manager().LoadAll()));

            Assert.Equal(["img/fondo.png"], built.Skipped);
            Assert.DoesNotContain(Convert.ToBase64String("secreto"u8.ToArray()), built.Css);
        }
        finally
        {
            // Sin recursión: se borra la unión, no lo que hay al otro lado.
            Directory.Delete(link);
        }
    }

    [Fact]
    public void BuildCssOfAThemeThatVanishedThrows()
    {
        var theme = ThemeAt("borrada") with { Directory = Path.Combine(app.Path, "borrada") };

        Assert.ThrowsAny<IOException>(() => ThemeManager.BuildCss(theme));
    }
}
