using System.Diagnostics;

namespace Cryptwire.Core.Tests;

public sealed class ContainedFilesTests : IDisposable
{
    private readonly TempDir dir = new();

    public void Dispose() => dir.Dispose();

    private string Root => Path.Combine(dir.Path, "fondos");

    [Fact]
    public void AFileInsideIsRead()
    {
        var file = dir.Write(@"fondos\sub\fondo.jpg", "dentro");

        Assert.Equal("dentro"u8.ToArray(), ContainedFiles.TryRead(Root, file, 100));
    }

    [Fact]
    public void AFileOutsideIsNotRead()
    {
        dir.Write(@"fondos\fondo.jpg", "dentro");
        var outside = dir.Write("fuera.txt", "secreto");

        Assert.Null(ContainedFiles.TryRead(Root, outside, 100));
        Assert.Null(ContainedFiles.TryRead(Root, Path.Combine(Root, "..", "fuera.txt"), 100));
        Assert.Null(ContainedFiles.TryRead(Root, Path.Combine(dir.Path, "fondos-otro", "x.jpg"), 100));
        Assert.Null(ContainedFiles.TryRead(Root, Root, 100));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"fondos\fondo.jpg")]
    public void ARelativeOrEmptyPathIsNotRead(string? path)
    {
        dir.Write(@"fondos\fondo.jpg", "dentro");

        Assert.Null(ContainedFiles.TryRead(Root, path, 100));
    }

    [Fact]
    public void ATooBigOrMissingFileIsNotRead()
    {
        var file = dir.Write(@"fondos\fondo.jpg", "0123456789");

        Assert.Null(ContainedFiles.TryRead(Root, file, 9));
        Assert.NotNull(ContainedFiles.TryRead(Root, file, 10));
        Assert.Null(ContainedFiles.TryRead(Root, Path.Combine(Root, "no-esta.jpg"), 100));
    }

    [Fact]
    public void AJunctionInsideThatPointsOutsideIsNotFollowed()
    {
        dir.Write(@"fondos\fondo.jpg", "dentro");
        dir.Write(@"secretos\clave.jpg", "secreto");
        var junction = Path.Combine(Root, "enlace");
        using (var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{Path.Combine(dir.Path, "secretos")}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!)
        {
            mklink.WaitForExit();
        }

        Assert.True(Directory.Exists(junction));
        Assert.Null(ContainedFiles.TryRead(Root, Path.Combine(junction, "clave.jpg"), 100));
        Directory.Delete(junction);
    }
}
