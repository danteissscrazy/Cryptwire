using Cryptwire.Core.Notifications;

namespace Cryptwire.Core.Tests;

public class NotificationIconTests
{
    [Theory]
    [InlineData("https://pps.whatsapp.net/v/t61.24694-24/foto.jpg?oh=abc&oe=def", IconSource.WhatsAppServer)]
    [InlineData("https://media-mad1-1.cdn.whatsapp.net/v/foto.jpg", IconSource.WhatsAppServer)]
    [InlineData("https://static.whatsapp.net/rsrc.php/v4/icono.png", IconSource.WhatsAppServer)]
    [InlineData("https://web.whatsapp.com/img/icono.png", IconSource.WhatsAppServer)]
    [InlineData("data:image/png;base64,iVBORw0KGgo=", IconSource.Inline)]
    [InlineData("DATA:IMAGE/JPEG;BASE64,/9j/4AAQ", IconSource.Inline)]
    public void PhotosFromWhatsAppOrInlineAreUsed(string uri, IconSource source)
    {
        Assert.Equal(source, NotificationIcon.Classify(uri));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://pps.whatsapp.net/foto.jpg")]
    [InlineData("https://pps.whatsapp.net:8443/foto.jpg")]
    [InlineData("https://evilwhatsapp.net/foto.jpg")]
    [InlineData("https://whatsapp.net.evil.example/foto.jpg")]
    [InlineData("https://usuario@pps.whatsapp.net/foto.jpg")]
    [InlineData("https://192.168.1.1/foto.jpg")]
    [InlineData("file:///C:/Users/x/foto.jpg")]
    [InlineData(@"\\servidor\fotos\foto.jpg")]
    [InlineData("blob:https://web.whatsapp.com/1234")]
    [InlineData("data:text/html;base64,PGgxPg==")]
    [InlineData("data:image/svg+xml,<svg/>")]
    [InlineData("javascript:alert(1)")]
    public void AnythingElseFallsBackToTheInitial(string? uri)
    {
        Assert.Equal(IconSource.None, NotificationIcon.Classify(uri));
    }

    [Fact]
    public void AnInlinePhotoIsDecoded()
    {
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, NotificationIcon.DecodeInline("data:image/png;base64,iVBORw=="));
    }

    [Theory]
    [InlineData("data:image/png;base64,***")]
    [InlineData("https://pps.whatsapp.net/foto.jpg")]
    public void ABrokenOrRemoteUriDecodesToNothing(string uri)
    {
        Assert.Null(NotificationIcon.DecodeInline(uri));
    }

    [Fact]
    public void AnInlinePhotoPastTheCapIsNotDecoded()
    {
        var huge = "data:image/png;base64," + new string('A', (NotificationIcon.MaxBytes * 4 / 3) + 8);

        Assert.Null(NotificationIcon.DecodeInline(huge));
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    public void OnlyJpegPngAndWebpAreDecoded(byte[] header, string mime)
    {
        Assert.Equal(mime, NotificationIcon.ImageMime(header));
    }

    [Theory]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })] // GIF
    [InlineData(new byte[] { 0x42, 0x4D, 0, 0, 0, 0 })] // BMP
    [InlineData(new byte[] { 0x49, 0x49, 0x2A, 0x00 })] // TIFF
    [InlineData(new byte[] { 0x00, 0x00, 0x01, 0x00 })] // ICO
    [InlineData(new byte[] { 0x49, 0x49, 0xBC, 0x01 })] // JPEG XR
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 })] // <svg
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 })] // PNG cortado
    public void AnyOtherFormatIsRefusedBeforeDecoding(byte[] header)
    {
        Assert.Null(NotificationIcon.ImageMime(header));
    }

    [Theory]
    [InlineData(96, 96, true)]
    [InlineData(1024, 1024, true)]
    [InlineData(640, 1, true)]
    [InlineData(1, 200_000, false)]
    [InlineData(1025, 10, false)]
    [InlineData(10, 1025, false)]
    [InlineData(0, 96, false)]
    [InlineData(96, -1, false)]
    public void HugeOrEmptyPhotosAreNotDecoded(int width, int height, bool fits)
    {
        Assert.Equal(fits, NotificationIcon.FitsSize(width, height));
    }
}
