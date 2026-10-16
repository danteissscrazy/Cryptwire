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
}
