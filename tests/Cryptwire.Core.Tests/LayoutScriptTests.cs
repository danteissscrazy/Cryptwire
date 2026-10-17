using System.Text.Json;
using Cryptwire.Core.Layout;
using Jint;

namespace Cryptwire.Core.Tests;

/// <summary>
/// Ejecuta el script del divisor con Jint sobre un DOM de mentira: una ventana de 2000 px, el raíl de
/// 64 px y la lista hasta el píxel 664. «panel» es cualquier punto de la pantalla de chats; «encima»,
/// un menú o una ventana de la web que tapa la pantalla.
/// </summary>
public class LayoutScriptTests
{
    private const string FakeDom = """
        var listeners = {};
        var mounted = [];
        var classes = [];
        var window = {
          innerWidth: 2000, innerHeight: 1000,
          addEventListener: function (type, fn) { listeners[type] = fn; },
          removeEventListener: function (type, fn) { if (listeners[type] === fn) delete listeners[type]; },
          localStorage: {
            data: {},
            getItem: function (k) { return Object.prototype.hasOwnProperty.call(this.data, k) ? this.data[k] : null; },
            setItem: function (k, v) { this.data[k] = String(v); }
          }
        };
        window.top = window;
        var location = { hostname: "web.whatsapp.com" };
        var html = { classList: {
          toggle: function (c, on) { classes = classes.filter(function (x) { return x !== c; }); if (on) classes.push(c); },
          add: function (c) { if (classes.indexOf(c) < 0) classes.push(c); },
          remove: function () { var gone = Array.prototype.slice.call(arguments); classes = classes.filter(function (x) { return gone.indexOf(x) < 0; }); }
        } };
        var screen = { contains: function (t) { return t === "panel"; }, getBoundingClientRect: function () { return { left: 0, width: 2000 }; } };
        var column = { parentElement: screen, getBoundingClientRect: function () { return { left: 64, right: 664, top: 0, bottom: 1000 }; } };
        var document = {
          head: { appendChild: function (el) { if (mounted.indexOf(el) < 0) mounted.push(el); } },
          documentElement: html,
          getElementById: function (id) { return id === "side" ? { parentElement: column } : null; },
          createElement: function (tag) {
            var el = { tagName: tag, id: "", textContent: "" };
            el.remove = function () { mounted = mounted.filter(function (m) { return m !== el; }); };
            return el;
          }
        };
        function MutationObserver() { this.observe = function () {}; this.disconnect = function () {}; }
        function ev(x, target) {
          return { clientX: x, clientY: 500, target: target || "panel", button: 0, prevented: false,
            preventDefault: function () { this.prevented = true; }, stopPropagation: function () {} };
        }
        """;

    private static Engine Load(string? setup = null)
    {
        var engine = new Engine();
        engine.Execute(FakeDom);
        if (setup is not null)
        {
            engine.Execute(setup);
        }

        engine.Execute(LayoutScript.Install());
        return engine;
    }

    private static string Css(Engine engine) => engine.Evaluate("mounted[0].textContent").AsString();

    private static string Saved(Engine engine) =>
        engine.Evaluate($"window.localStorage.getItem({JsonSerializer.Serialize(LayoutScript.StorageKey)})").ToString();

    private static bool HasClass(Engine engine, string name) =>
        engine.Evaluate($"classes.indexOf({JsonSerializer.Serialize(name)}) >= 0").AsBoolean();

    private static void Drag(Engine engine, int from, int to)
    {
        engine.Execute($"listeners.pointerdown(ev({from})); listeners.pointermove(ev({to})); listeners.pointerup(ev({to}));");
    }

    [Fact]
    public void UntilYouDragItLeavesWhatsAppsWidthAlone()
    {
        var engine = Load();

        Assert.Equal(LayoutScript.StyleElementId, engine.Evaluate("mounted[0].id").AsString());
        Assert.DoesNotContain("flex:", Css(engine));
    }

    [Fact]
    public void NearTheLineTheCursorTurnsIntoAResizeHandle()
    {
        var engine = Load();

        engine.Execute("listeners.pointermove(ev(666));");
        Assert.True(HasClass(engine, "cw-panel-edge"));

        engine.Execute("listeners.pointermove(ev(700));");
        Assert.False(HasClass(engine, "cw-panel-edge"));
    }

    [Fact]
    public void DraggingTheLineSetsTheWidthAndRemembersIt()
    {
        var engine = Load();

        Drag(engine, 664, 800);

        Assert.Contains("""@media (orientation: landscape) { div:has(> #side), [data-testid="drawer-left"] { flex: 0 0 clamp(260px, 36.80%""", Css(engine));
        Assert.DoesNotContain("orientation: portrait", Css(engine));
        Assert.Equal("""{"landscape":0.368,"portrait":null}""", Saved(engine));
        Assert.False(HasClass(engine, "cw-panel-drag"));
    }

    [Fact]
    public void WithTheMonitorUprightOnlyTheUprightWidthChanges()
    {
        var engine = Load("""
            window.localStorage.data["cryptwire.panel"] = '{"landscape":0.3}';
            window.innerWidth = 1000; window.innerHeight = 1800;
            screen.getBoundingClientRect = function () { return { left: 0, width: 1000 }; };
            """);

        Drag(engine, 664, 564);

        Assert.Equal("""{"landscape":0.3,"portrait":0.5}""", Saved(engine));
        Assert.Contains("""@media (orientation: landscape) { div:has(> #side), [data-testid="drawer-left"] { flex: 0 0 clamp(260px, 30.00%""", Css(engine));
        Assert.Contains("""@media (orientation: portrait) { div:has(> #side), [data-testid="drawer-left"] { flex: 0 0 clamp(260px, 50.00%""", Css(engine));
    }

    [Theory]
    [InlineData(100, 0.13)]
    [InlineData(1990, 0.778)]
    public void BothPanelsStayUsable(int dropAt, double expected)
    {
        var engine = Load();

        Drag(engine, 664, dropAt);

        Assert.Equal(expected, engine.Evaluate("JSON.parse(window.localStorage.getItem('cryptwire.panel')).landscape").AsNumber(), 3);
    }

    [Fact]
    public void SavedWidthsAreUsedFromTheStart()
    {
        var engine = Load("""window.localStorage.data["cryptwire.panel"] = '{"landscape":0.25,"portrait":0.4}';""");

        Assert.Contains("clamp(260px, 25.00%", Css(engine));
        Assert.Contains("clamp(260px, 40.00%", Css(engine));
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("""{"landscape":5}""")]
    [InlineData("""{"landscape":"0.3"}""")]
    [InlineData("null")]
    public void BrokenSavedWidthsAreIgnored(string saved)
    {
        var engine = Load($"window.localStorage.data['cryptwire.panel'] = {JsonSerializer.Serialize(saved)};");

        Assert.DoesNotContain("flex:", Css(engine));
    }

    [Fact]
    public void DoubleClickOnTheLineGoesBackToWhatsAppsWidth()
    {
        var engine = Load("""window.localStorage.data["cryptwire.panel"] = '{"landscape":0.25,"portrait":0.4}';""");

        engine.Execute("listeners.dblclick(ev(665));");

        Assert.DoesNotContain("orientation: landscape", Css(engine));
        Assert.Contains("orientation: portrait", Css(engine));
        Assert.Equal("""{"landscape":null,"portrait":0.4}""", Saved(engine));
    }

    [Theory]
    [InlineData(300, "panel")]
    [InlineData(700, "panel")]
    [InlineData(665, "encima")]
    public void ClicksAwayFromTheLineOrOnTopOfItAreLeftToWhatsApp(int x, string target)
    {
        var engine = Load();

        engine.Execute($"var down = ev({x}, {JsonSerializer.Serialize(target)}); listeners.pointerdown(down);");

        Assert.False(engine.Evaluate("down.prevented").AsBoolean());
        Assert.False(HasClass(engine, "cw-panel-drag"));
        Assert.True(engine.Evaluate("window.localStorage.getItem('cryptwire.panel') === null").AsBoolean());
    }

    [Fact]
    public void InstallingTwiceKeepsASingleDivider()
    {
        var engine = Load();

        engine.Execute(LayoutScript.Install());

        Assert.Equal(1, (int)engine.Evaluate("mounted.length").AsNumber());
    }

    [Fact]
    public void RemoveGivesWhatsAppItsLayoutBack()
    {
        var engine = Load("""window.localStorage.data["cryptwire.panel"] = '{"landscape":0.25}';""");
        engine.Execute("listeners.pointermove(ev(665));");

        engine.Execute(LayoutScript.Remove());

        Assert.Equal(0, (int)engine.Evaluate("mounted.length").AsNumber());
        Assert.Equal(0, (int)engine.Evaluate("Object.keys(listeners).length").AsNumber());
        Assert.Equal(0, (int)engine.Evaluate("classes.length").AsNumber());

        engine.Execute(LayoutScript.Install());
        Assert.Equal(1, (int)engine.Evaluate("mounted.length").AsNumber());
    }

    [Theory]
    [InlineData("location.hostname = 'web.whatsapp.com.evil.example';")]
    [InlineData("window.top = {};")]
    public void DoesNothingOutsideTheMainWhatsAppDocument(string elsewhere)
    {
        var engine = Load(elsewhere);

        Assert.Equal(0, (int)engine.Evaluate("mounted.length").AsNumber());
        Assert.Equal(0, (int)engine.Evaluate("Object.keys(listeners).length").AsNumber());
    }
}
