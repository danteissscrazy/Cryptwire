using Cryptwire.Core.Settings;
using Cryptwire.Core.Wallpapers;

namespace Cryptwire.Core.Tests;

public sealed class WallpaperCssTests : IDisposable
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    private static readonly byte[] Webp = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8];

    private readonly TempDir dir = new();

    public void Dispose() => dir.Dispose();

    private string Folder => Path.Combine(dir.Path, "fondos");

    private string Put(string name, byte[] bytes)
    {
        var path = Path.Combine(Folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void TheImageGoesInlineBehindTheConversationAndDimmed()
    {
        var file = Put("fondo.jpg", Jpeg);

        var css = WallpaperCss.Build(new WallpaperSettings(file, Dim: 40), Folder)!;

        Assert.Contains("body #main {", css, StringComparison.Ordinal);
        Assert.Contains($"url(\"data:image/jpeg;base64,{Convert.ToBase64String(Jpeg)}\") center / cover no-repeat", css, StringComparison.Ordinal);
        Assert.Contains("#02040a) 40%, transparent", css, StringComparison.Ordinal);
        Assert.Contains("conversation-background", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("webp")]
    public void TheTypeComesFromTheContentNotTheName(string kind)
    {
        var file = Put("fondo.jpg", kind == "png" ? Png : Webp);

        Assert.Contains($"data:image/{kind};base64,", WallpaperCss.Build(new WallpaperSettings(file), Folder), StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutAWallpaperThereIsNoCss()
    {
        Assert.Null(WallpaperCss.Build(null, Folder));
    }

    [Fact]
    public void AFileThatIsNotAnImageIsNotInlined()
    {
        var file = Put("fondo.jpg", "esto no es una imagen"u8.ToArray());

        Assert.Null(WallpaperCss.Build(new WallpaperSettings(file), Folder));
    }

    [Fact]
    public void AFileOutsideTheWallpaperFolderIsNotInlined()
    {
        var outside = dir.Write("secreto.jpg", "x");
        File.WriteAllBytes(outside, Jpeg);

        Assert.Null(WallpaperCss.Build(new WallpaperSettings(outside), Folder));
        Assert.Null(WallpaperCss.Build(new WallpaperSettings(Path.Combine(Folder, "..", "secreto.jpg")), Folder));
    }

    [Fact]
    public void AMissingOrTooBigFileIsNotInlined()
    {
        var big = Put("grande.jpg", [.. Jpeg, .. new byte[WallpaperCss.MaxBytes]]);

        Assert.Null(WallpaperCss.Build(new WallpaperSettings(big), Folder));
        Assert.Null(WallpaperCss.Build(new WallpaperSettings(Path.Combine(Folder, "no-esta.jpg")), Folder));
    }

    [Theory]
    [InlineData(-5, "0%")]
    [InlineData(200, "90%")]
    public void TheDimIsClamped(int dim, string expected)
    {
        var file = Put("fondo.jpg", Jpeg);

        Assert.Contains($"#02040a) {expected}, transparent", WallpaperCss.Build(new WallpaperSettings(file, dim), Folder), StringComparison.Ordinal);
    }

    [Fact]
    public void ImageMimeNeedsTheWholeSignature()
    {
        Assert.Null(WallpaperCss.ImageMime([0xFF, 0xD8]));
        Assert.Null(WallpaperCss.ImageMime("RIFF\0\0\0\0WAVE"u8));
        Assert.Null(WallpaperCss.ImageMime([]));
    }
}
