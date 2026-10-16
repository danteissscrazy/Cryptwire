using Cryptwire.Core.Navigation;

namespace Cryptwire.Core.Tests;

public class NavigationPolicyTests
{
    private readonly NavigationPolicy policy = new();

    [Theory]
    [InlineData("https://web.whatsapp.com/")]
    [InlineData("https://web.whatsapp.com")]
    [InlineData("https://WEB.WhatsApp.com/send?phone=0")]
    [InlineData("https://web.whatsapp.com:443/")]
    public void WhatsAppWebOpensInside(string url)
    {
        Assert.Equal(NavigationDecision.Allow, policy.Decide(url, userInitiated: true));
        // Las redirecciones y los scripts de la propia web también pueden moverse dentro de ella.
        Assert.Equal(NavigationDecision.Allow, policy.Decide(url, userInitiated: false));
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("http://example.com/")]
    [InlineData("https://faq.whatsapp.com/")]
    [InlineData("https://www.whatsapp.com/")]
    [InlineData("http://web.whatsapp.com/")]
    [InlineData("https://web.whatsapp.com:8443/")]
    [InlineData("https://web.whatsapp.com.evil.example/")]
    [InlineData("https://evil.example/web.whatsapp.com")]
    [InlineData("https://web.whatsapp.com@evil.example/")]
    [InlineData("https://user@web.whatsapp.com/")]
    [InlineData("mailto:alguien@example.com")]
    public void OtherWebLinksOpenInTheBrowserOnlyAfterAClick(string url)
    {
        Assert.Equal(NavigationDecision.OpenExternal, policy.Decide(url, userInitiated: true));
        Assert.Equal(NavigationDecision.Block, policy.Decide(url, userInitiated: false));
    }

    [Theory]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<h1>hola</h1>")]
    [InlineData("blob:https://web.whatsapp.com/1234")]
    [InlineData("about:blank")]
    [InlineData("ms-settings:privacy")]
    [InlineData("search-ms:query=x")]
    [InlineData("steam://run/1")]
    [InlineData("\\\\servidor\\recurso")]
    [InlineData("/ruta/relativa")]
    [InlineData("no es una url")]
    [InlineData("")]
    [InlineData(null)]
    public void EverythingElseIsBlockedEvenAfterAClick(string? url)
    {
        Assert.Equal(NavigationDecision.Block, policy.Decide(url, userInitiated: true));
        Assert.Equal(NavigationDecision.Block, policy.DecideNewWindow(url, userInitiated: true));
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("mailto:alguien@example.com")]
    [InlineData("https://web.whatsapp.com/")]
    public void NewWindowsGoToTheBrowserOnlyAfterAClick(string url)
    {
        Assert.Equal(NavigationDecision.OpenExternal, policy.DecideNewWindow(url, userInitiated: true));
        Assert.Equal(NavigationDecision.Block, policy.DecideNewWindow(url, userInitiated: false));
    }

    [Theory]
    [InlineData("https://web.whatsapp.com/flows")]
    [InlineData("https://www.youtube.com/embed/x")]
    [InlineData("about:blank")]
    [InlineData("about:srcdoc")]
    [InlineData("blob:https://web.whatsapp.com/1234")]
    [InlineData("data:text/html,<p>x</p>")]
    public void FramesMayLoadSecureWebAndPageContent(string url) =>
        Assert.Equal(NavigationDecision.Allow, policy.DecideFrame(url));

    [Theory]
    [InlineData("http://example.com/")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:alguien@example.com")]
    [InlineData("ms-settings:privacy")]
    [InlineData("steam://run/1")]
    [InlineData(null)]
    public void FramesNeverLaunchAnythingElse(string? url) =>
        Assert.Equal(NavigationDecision.Block, policy.DecideFrame(url));

    [Theory]
    [InlineData("https://web.whatsapp.com", true)]
    [InlineData("https://web.whatsapp.com/", true)]
    [InlineData("https://web.whatsapp.com.evil.example", false)]
    [InlineData("http://web.whatsapp.com", false)]
    [InlineData("https://example.com", false)]
    [InlineData(null, false)]
    public void OnlyWhatsAppWebCountsAsWhatsApp(string? origin, bool expected) =>
        Assert.Equal(expected, NavigationPolicy.IsWhatsApp(origin));
}
