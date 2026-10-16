using Cryptwire.Core.Tones;

namespace Cryptwire.Core.Tests;

public sealed class ToneLibraryTests : IDisposable
{
    private readonly TempDir dir = new();

    public void Dispose() => dir.Dispose();

    private ToneLibrary Library => new(Path.Combine(dir.Path, "Cryptwire", "tonos"));

    [Fact]
    public void AToneIsCopiedIntoTheToneFolder()
    {
        var source = dir.Write(@"Descargas\acero.wav", "RIFF tono");

        var copy = Library.Import(source);

        Assert.Equal(Path.Combine(Library.Directory, "acero.wav"), copy);
        Assert.Equal("RIFF tono", File.ReadAllText(copy));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void TheSameToneTwiceIsOneCopy()
    {
        var source = dir.Write(@"Descargas\acero.mp3", "ID3 tono");

        Assert.Equal(Library.Import(source), Library.Import(dir.Write(@"Otra\acero.mp3", "ID3 tono")));
        Assert.Single(Directory.GetFiles(Library.Directory));
    }

    [Fact]
    public void ADifferentToneWithTheSameNameIsNumbered()
    {
        var first = Library.Import(dir.Write(@"A\campana.wav", "uno"));
        var second = Library.Import(dir.Write(@"B\campana.wav", "dos"));

        Assert.Equal("campana.wav", Path.GetFileName(first));
        Assert.Equal("campana-2.wav", Path.GetFileName(second));
        Assert.Equal("uno", File.ReadAllText(first));
    }

    [Theory]
    [InlineData("tono.exe")]
    [InlineData("tono.wav.lnk")]
    [InlineData("tono")]
    [InlineData("tono.ogg")]
    public void OnlySoundFormatsAreAccepted(string name)
    {
        var source = dir.Write(name, "x");

        Assert.Throws<ToneImportException>(() => Library.Import(source));
        Assert.False(Directory.Exists(Library.Directory));
    }

    [Fact]
    public void ATooBigToneIsRefused()
    {
        var source = Path.Combine(dir.Path, "largo.wav");
        using (var file = File.Create(source))
        {
            file.SetLength(ToneLibrary.MaxBytes + 1);
        }

        var error = Assert.Throws<ToneImportException>(() => Library.Import(source));
        Assert.Contains("MB", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingToneIsRefused()
    {
        Assert.Throws<ToneImportException>(() => Library.Import(Path.Combine(dir.Path, "no-esta.wav")));
    }

    [Theory]
    [InlineData("acero", "acero")]
    [InlineData("CON", "tono-CON")]
    [InlineData("com1", "tono-com1")]
    [InlineData("lpt9.final", "tono-lpt9.final")]
    [InlineData("COM10", "COM10")]
    [InlineData("acero. . ", "acero")]
    [InlineData("a<b>c:d|e?f*g", "a_b_c_d_e_f_g")]
    [InlineData("  ", "tono")]
    public void TheCopyGetsAValidWindowsName(string raw, string expected)
    {
        Assert.Equal(expected, ToneLibrary.SafeBaseName(raw));
    }

    [Fact]
    public void AVeryLongNameIsShortened()
    {
        Assert.True(ToneLibrary.SafeBaseName(new string('a', 300)).Length <= 60);
    }
}
