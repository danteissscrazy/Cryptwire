using System.Text.Json;
using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Tests;

public class SkinScriptTests
{
    [Fact]
    public void EmbedsTheCssAsAJsonStringLiteral()
    {
        const string css = "body { font-family: \"Cinzel\"; content: '\\2014'; }\n</style><script>alert(1)</script>";

        var script = SkinScript.Apply(css);

        Assert.Contains($"const css = {JsonSerializer.Serialize(css)};", script);
        Assert.DoesNotContain("</style>", script);
        Assert.DoesNotContain("<script>", script);
    }

    [Fact]
    public void OnlyRunsInTheTopWhatsAppDocument()
    {
        var script = SkinScript.Apply("body {}");

        Assert.Contains("window.top !== window", script);
        Assert.Contains("location.hostname !== \"web.whatsapp.com\"", script);
    }

    [Fact]
    public void ReusesASingleStyleElement()
    {
        var script = SkinScript.Apply("body {}");

        Assert.Contains($"document.getElementById(\"{SkinScript.StyleElementId}\")", script);
    }
}
