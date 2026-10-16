using System.Text;
using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Tests;

public class CssInlinerTests
{
    private static readonly string ThemeDir = Path.Combine(Path.GetTempPath(), "cw-themes", "prueba");
    private static readonly byte[] Svg = Encoding.UTF8.GetBytes("<svg/>");
    private static readonly byte[] Font = [0, 1, 0, 0];

    private readonly Dictionary<string, byte[]> disk = new(StringComparer.OrdinalIgnoreCase)
    {
        [Path.Combine(ThemeDir, "fondo.svg")] = Svg,
        [Path.Combine(ThemeDir, "fuentes", "Cinzel.ttf")] = Font,
        [Path.Combine(ThemeDir, "fondo oscuro.svg")] = Svg,
        [Path.Combine(ThemeDir, "notas.txt")] = Encoding.UTF8.GetBytes("privado"),
        [Path.Combine(Path.GetTempPath(), "cw-themes", "otra", "fondo.svg")] = Svg,
        [Path.Combine(Path.GetTempPath(), "cw-themes", "secreto.png")] = [9, 9, 9],
    };

    private readonly List<string> reads = [];

    private InlinedCss Inline(string css) => CssInliner.Inline(css, ThemeDir, path =>
    {
        reads.Add(path);
        return disk.GetValueOrDefault(path);
    });

    private static string DataUri(string mime, byte[] bytes) =>
        $"url(\"data:{mime};base64,{Convert.ToBase64String(bytes)}\")";

    [Theory]
    [InlineData("""#main { background: url("fondo.svg"); }""")]
    [InlineData("""#main { background: url('fondo.svg'); }""")]
    [InlineData("""#main { background: url(fondo.svg); }""")]
    [InlineData("""#main { background: url(  "fondo.svg"  ); }""")]
    [InlineData("""#main { background: URL("./fondo.svg"); }""")]
    public void RelativeImagesBecomeDataUris(string css)
    {
        var result = Inline(css);

        Assert.Equal($"#main {{ background: {DataUri("image/svg+xml", Svg)}; }}", result.Css);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void FontsInSubfoldersAreInlinedWithTheirType()
    {
        var result = Inline("""@font-face { src: url("fuentes/Cinzel.ttf") format("truetype"); }""");

        Assert.Equal($"@font-face {{ src: {DataUri("font/ttf", Font)} format(\"truetype\"); }}", result.Css);
    }

    [Fact]
    public void QueryAndFragmentAreIgnoredAndEscapesDecoded()
    {
        var result = Inline("""a { background: url("fondo%20oscuro.svg#capa"), url("fondo.svg?v=2"); }""");

        Assert.Equal(
            $"a {{ background: {DataUri("image/svg+xml", Svg)}, {DataUri("image/svg+xml", Svg)}; }}",
            result.Css);
    }

    [Theory]
    [InlineData("""a { background: url("https://example.com/x.png"); }""")]
    [InlineData("""a { background: url("data:image/png;base64,AAAA"); }""")]
    [InlineData("""a { filter: url(#brillo); }""")]
    [InlineData("""a { background: url("/img/x.png"); }""")]
    [InlineData("""a { background: url("//cdn.example/x.png"); }""")]
    public void NonRelativeReferencesAreLeftAlone(string css)
    {
        var result = Inline(css);

        Assert.Equal(css, result.Css);
        Assert.Empty(result.Skipped);
        Assert.Empty(reads);
    }

    [Theory]
    [InlineData("../secreto.png")]
    [InlineData("../otra/fondo.svg")]
    [InlineData("fuentes/../../secreto.png")]
    [InlineData("..%2Fsecreto.png")]
    [InlineData(@"..\secreto.png")]
    public void FilesOutsideTheThemeAreNeverRead(string reference)
    {
        var css = $"a {{ background: url(\"{reference}\"); }}";

        var result = Inline(css);

        Assert.Equal(css, result.Css);
        Assert.Equal([reference], result.Skipped);
        Assert.Empty(reads);
    }

    [Theory]
    [InlineData("notas.txt")]
    [InlineData("skin.css")]
    [InlineData("script.js")]
    [InlineData("sin-extension")]
    public void OnlyImagesAndFontsAreRead(string reference)
    {
        var css = $"a {{ background: url(\"{reference}\"); }}";

        var result = Inline(css);

        Assert.Equal(css, result.Css);
        Assert.Equal([reference], result.Skipped);
        Assert.Empty(reads);
    }

    [Fact]
    public void MissingFilesAreReportedAndLeftAlone()
    {
        var css = """a { background: url("no-existe.png"); }""";

        var result = Inline(css);

        Assert.Equal(css, result.Css);
        Assert.Equal(["no-existe.png"], result.Skipped);
    }
}
