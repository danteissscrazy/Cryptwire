using Cryptwire.Core.Skins;
using Jint;

namespace Cryptwire.Core.Tests;

/// <summary>
/// Ejecuta el script de verdad (con Jint) sobre un DOM de mentira y mira qué deja en la página.
/// </summary>
public class SkinScriptTests
{
    private const string FakeDom = """
        var mounted = [];
        var listeners = {};
        var window = {};
        window.top = window;
        var location = { hostname: "web.whatsapp.com" };
        var document = {
          readyState: "loading",
          head: { appendChild: function (el) { mounted = mounted.filter(function (m) { return m !== el; }); mounted.push(el); } },
          documentElement: {},
          getElementById: function (id) { return mounted.find(function (m) { return m.id === id; }) || null; },
          createElement: function (tag) {
            var el = { tagName: tag, id: "", textContent: "" };
            el.remove = function () { mounted = mounted.filter(function (m) { return m !== el; }); };
            return el;
          },
          addEventListener: function (type, fn) { listeners[type] = fn; }
        };
        function MutationObserver() { this.observe = function () {}; this.disconnect = function () {}; }
        """;

    private static Engine Run(string css, string? setup = null)
    {
        var engine = new Engine();
        engine.Execute(FakeDom);
        if (setup is not null)
        {
            engine.Execute(setup);
        }

        engine.Execute(SkinScript.Apply(css));
        return engine;
    }

    private static int MountedCount(Engine engine) => (int)engine.Evaluate("mounted.length").AsNumber();

    private static string MountedCss(Engine engine) => engine.Evaluate("mounted[0].textContent").AsString();

    [Fact]
    public void MountsTheCssExactlyAsWritten()
    {
        const string css = "body { content: \"a\\\"b\" '`${x}`' '\u2028' '</script>' \\005C; }\n#app { color: red; }";

        var engine = Run(css);

        Assert.Equal(1, MountedCount(engine));
        Assert.Equal(SkinScript.StyleElementId, engine.Evaluate("mounted[0].id").AsString());
        Assert.Equal("style", engine.Evaluate("mounted[0].tagName").AsString());
        Assert.Equal(css, MountedCss(engine));
    }

    [Fact]
    public void DoesNothingOutsideWhatsAppWeb()
    {
        var engine = Run("body {}", "location.hostname = 'web.whatsapp.com.evil.example';");

        Assert.Equal(0, MountedCount(engine));
    }

    [Fact]
    public void DoesNothingInsideFrames()
    {
        var engine = Run("body {}", "window.top = {};");

        Assert.Equal(0, MountedCount(engine));
    }

    [Fact]
    public void KeepsASingleStyleWhenThePageFinishesLoading()
    {
        var engine = Run("body {}");

        engine.Execute("listeners.DOMContentLoaded();");

        Assert.Equal(1, MountedCount(engine));
    }

    [Fact]
    public void WaitsForTheDocumentWhenItDoesNotExistYet()
    {
        var engine = Run("body {}", "document.head = null; document.documentElement = null;");

        Assert.Equal(0, MountedCount(engine));
        engine.Execute("document.head = { appendChild: function (el) { mounted.push(el); } }; listeners.DOMContentLoaded();");
        Assert.Equal(1, MountedCount(engine));
    }

    [Fact]
    public void OnTheOpenPageItSwapsTheCssInPlace()
    {
        var engine = Run("#app { color: red; }");
        engine.Execute("document.readyState = 'complete'; listeners = {};");

        engine.Execute(SkinScript.Apply("#app { color: gold; }"));

        Assert.Equal(1, MountedCount(engine));
        Assert.Equal("#app { color: gold; }", MountedCss(engine));
        Assert.True(engine.Evaluate("listeners.DOMContentLoaded === undefined").AsBoolean());
    }

    [Fact]
    public void RemoveTakesTheSkinOffTheOpenPage()
    {
        var engine = Run("#app { color: red; }");

        engine.Execute(SkinScript.Remove());

        Assert.Equal(0, MountedCount(engine));
    }

    [Theory]
    [InlineData("location.hostname = 'example.com';")]
    [InlineData("window.top = {};")]
    public void RemoveDoesNothingOutsideTheMainWhatsAppDocument(string elsewhere)
    {
        var engine = Run("#app { color: red; }");
        engine.Execute(elsewhere);

        engine.Execute(SkinScript.Remove());

        Assert.Equal(1, MountedCount(engine));
    }

    [Fact]
    public void SafeModeDuringPageLoadSticks()
    {
        var engine = Run("#app { color: red; }");

        engine.Execute(SkinScript.Remove());
        engine.Execute("listeners.DOMContentLoaded();");

        Assert.Equal(0, MountedCount(engine));
    }

    [Fact]
    public void APendingMountFromAnOlderSkinUsesTheNewestCss()
    {
        var engine = Run("#app { color: red; }");
        var oldListener = engine.Evaluate("listeners.DOMContentLoaded");

        engine.Execute(SkinScript.Apply("#app { color: gold; }"));
        engine.SetValue("oldListener", oldListener);
        engine.Execute("oldListener();");

        Assert.Equal(1, MountedCount(engine));
        Assert.Equal("#app { color: gold; }", MountedCss(engine));
    }

    [Fact]
    public void RemoveWithoutASkinIsHarmless()
    {
        var engine = new Engine();
        engine.Execute(FakeDom);

        engine.Execute(SkinScript.Remove());

        Assert.Equal(0, MountedCount(engine));
    }
}
