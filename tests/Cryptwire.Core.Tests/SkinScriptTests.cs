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
          head: { appendChild: function (el) { mounted = mounted.filter(function (m) { return m !== el; }); mounted.push(el); } },
          documentElement: {},
          getElementById: function (id) { return mounted.find(function (m) { return m.id === id; }) || null; },
          createElement: function (tag) { return { tagName: tag, id: "", textContent: "" }; },
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

    [Fact]
    public void MountsTheCssExactlyAsWritten()
    {
        const string css = "body { content: \"a\\\"b\" '`${x}`' '\u2028' '</script>' \\005C; }\n#app { color: red; }";

        var engine = Run(css);

        Assert.Equal(1, MountedCount(engine));
        Assert.Equal(SkinScript.StyleElementId, engine.Evaluate("mounted[0].id").AsString());
        Assert.Equal("style", engine.Evaluate("mounted[0].tagName").AsString());
        Assert.Equal(css, engine.Evaluate("mounted[0].textContent").AsString());
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
}
