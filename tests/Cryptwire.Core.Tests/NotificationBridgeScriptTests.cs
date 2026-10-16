using Cryptwire.Core.Notifications;
using Jint;

namespace Cryptwire.Core.Tests;

/// <summary>
/// Ejecuta el puente de avisos con Jint sobre una página de mentira: un <c>Notification</c> que apunta lo
/// que se crea y un <c>ServiceWorkerRegistration</c> cuyo <c>showNotification</c> original apunta lo que
/// le llega (en WebView2, eso sería el aviso de Windows que no pasa por Cryptwire).
/// </summary>
public class NotificationBridgeScriptTests
{
    private const string FakePage = """
        var created = [];
        var native = [];
        var result = null;
        function Notification(title, options) {
          if (Notification.fail) throw new Error("sin permiso");
          this.title = title;
          this.body = options.body;
          this.tag = options.tag;
          this.options = options;
          this.listeners = {};
          this.closed = false;
          created.push(this);
        }
        Notification.prototype.addEventListener = function (type, fn) { this.listeners[type] = fn; };
        Notification.prototype.close = function () { this.closed = true; if (this.listeners.close) this.listeners.close(); };
        function ServiceWorkerRegistration() {}
        ServiceWorkerRegistration.prototype.showNotification = function (title, options) {
          native.push({ title: title, options: options });
          return Promise.resolve("original");
        };
        ServiceWorkerRegistration.prototype.getNotifications = function (filter) {
          return Promise.resolve([{ tag: "de-windows", data: {}, close: function () {} }].filter(function (n) { return !filter || filter.tag == null || n.tag === filter.tag; }));
        };
        var window = { Notification: Notification, ServiceWorkerRegistration: ServiceWorkerRegistration };
        window.top = window;
        var location = { hostname: "web.whatsapp.com" };
        var registration = new ServiceWorkerRegistration();
        var callOptions = {
          body: "Llamada entrante", tag: "3EB0", requireInteraction: true, silent: true,
          data: { callId: "3EB0", notificationBannerKey: "swbanner:a:1" },
          actions: [{ action: "accept", title: "Aceptar" }, { action: "decline", title: "Rechazar" }]
        };
        """;

    private static Engine Load(string? setup = null)
    {
        var engine = new Engine();
        engine.Execute(FakePage);
        if (setup is not null)
        {
            engine.Execute(setup);
        }

        engine.Execute(NotificationBridgeScript.Install());
        return engine;
    }

    private static int Count(Engine engine, string array) => (int)engine.Evaluate($"{array}.length").AsNumber();

    private static string Text(Engine engine, string expression) => engine.Evaluate(expression).AsString();

    [Fact]
    public void ACallFromTheServiceWorkerBecomesANoticeCryptwireCanCatch()
    {
        var engine = Load();

        engine.Execute("registration.showNotification('Vergil', callOptions).then(function (r) { result = r; });");

        Assert.Equal(0, Count(engine, "native"));
        Assert.Equal(1, Count(engine, "created"));
        Assert.Equal("Vergil", Text(engine, "created[0].title"));
        Assert.Equal("Llamada entrante", Text(engine, "created[0].body"));
        Assert.Equal(NotificationBridgeScript.CallTagPrefix + "3EB0", Text(engine, "created[0].tag"));
        Assert.True(engine.Evaluate("created[0].options.silent === true").AsBoolean());
        Assert.True(engine.Evaluate("created[0].options.requireInteraction === true").AsBoolean());
        Assert.True(engine.Evaluate("created[0].options.actions === undefined").AsBoolean());
        Assert.True(engine.Evaluate("result === undefined").AsBoolean());
    }

    [Fact]
    public void OtherServiceWorkerNoticesAreMarkedAsSuch()
    {
        var engine = Load();

        engine.Execute("registration.showNotification('WhatsApp', { body: 'Prueba', tag: 'verify-notification', actions: [{ action: 'x', title: 'Confirmar' }] });");

        Assert.Equal(0, Count(engine, "native"));
        Assert.Equal(NotificationBridgeScript.WorkerTagPrefix + "verify-notification", Text(engine, "created[0].tag"));
        Assert.True(engine.Evaluate("created[0].options.requireInteraction === false").AsBoolean());
    }

    [Theory]
    [InlineData("undefined")]
    [InlineData("{}")]
    [InlineData("{ data: 'texto' }")]
    [InlineData("{ data: { callId: null } }")]
    public void WithoutACallIdItIsNotACall(string options)
    {
        var engine = Load();

        engine.Execute($"registration.showNotification('Aviso', {options});");

        Assert.StartsWith(NotificationBridgeScript.WorkerTagPrefix, Text(engine, "created[0].tag"), StringComparison.Ordinal);
        Assert.Equal(string.Empty, Text(engine, "created[0].body"));
    }

    [Fact]
    public void WhatsAppCanStillFindAndCloseItsNotice()
    {
        var engine = Load();
        engine.Execute("registration.showNotification('Vergil', callOptions);");

        engine.Execute("""
            registration.getNotifications({ tag: '3EB0' }).then(function (list) {
              result = list;
              list.filter(function (n) { return n.data && n.data.notificationBannerKey === 'swbanner:a:1'; })
                  .forEach(function (n) { n.close(); });
            });
            """);

        Assert.Equal(1, Count(engine, "result"));
        Assert.Equal("3EB0", Text(engine, "result[0].tag"));
        Assert.True(engine.Evaluate("created[0].closed").AsBoolean());
    }

    [Fact]
    public void AClosedNoticeIsNoLongerListed()
    {
        var engine = Load();
        engine.Execute("registration.showNotification('Vergil', callOptions); created[0].close();");

        engine.Execute("registration.getNotifications({ tag: '3EB0' }).then(function (list) { result = list; });");

        Assert.Equal(0, Count(engine, "result"));
    }

    [Fact]
    public void WhenThePageClosesItItIsForgottenEvenWithoutACloseEvent()
    {
        // WebView2 no devuelve a la página el evento «close» de un aviso que ella misma cierra.
        var engine = Load("Notification.prototype.close = function () { this.closed = true; };");
        engine.Execute("registration.showNotification('Vergil', callOptions);");

        engine.Execute("""
            registration.getNotifications({ tag: '3EB0' })
              .then(function (list) { list.forEach(function (n) { n.close(); }); })
              .then(function () { return registration.getNotifications({ tag: '3EB0' }); })
              .then(function (list) { result = list; });
            """);

        Assert.True(engine.Evaluate("created[0].closed").AsBoolean());
        Assert.Equal(0, Count(engine, "result"));
    }

    [Fact]
    public void WithoutAFilterItListsTheirsAndOurs()
    {
        var engine = Load();
        engine.Execute("registration.showNotification('A', { tag: 'uno' }); registration.showNotification('B', { tag: 'dos' });");

        engine.Execute("registration.getNotifications().then(function (list) { result = list.map(function (n) { return n.tag; }); });");

        Assert.Equal("de-windows,uno,dos", Text(engine, "result.join(',')"));
    }

    [Fact]
    public void IfTheNoticeCannotBeCreatedTheOriginalPathStillWorks()
    {
        var engine = Load("Notification.fail = true;");

        engine.Execute("registration.showNotification('Vergil', callOptions).then(function (r) { result = r; });");

        Assert.Equal(1, Count(engine, "native"));
        Assert.Equal("Vergil", Text(engine, "native[0].title"));
        Assert.Equal("original", Text(engine, "result"));
    }

    [Fact]
    public void IfTheirListFailsOursIsStillReturned()
    {
        var engine = Load("ServiceWorkerRegistration.prototype.getNotifications = function () { return Promise.reject(new Error('x')); };");
        engine.Execute("registration.showNotification('A', { tag: 'uno' });");

        engine.Execute("registration.getNotifications().then(function (list) { result = list.length; });");

        Assert.Equal(1, (int)engine.Evaluate("result").AsNumber());
    }

    [Theory]
    [InlineData("location.hostname = 'web.whatsapp.com.evil.example';")]
    [InlineData("window.top = {};")]
    public void DoesNothingOutsideTheMainWhatsAppDocument(string elsewhere)
    {
        var engine = Load(elsewhere);

        engine.Execute("registration.showNotification('Vergil', callOptions);");

        Assert.Equal(1, Count(engine, "native"));
        Assert.Equal(0, Count(engine, "created"));
    }

    [Fact]
    public void InstallingTwiceWrapsOnlyOnce()
    {
        var engine = Load();

        engine.Execute(NotificationBridgeScript.Install());
        engine.Execute("registration.showNotification('A', { tag: 'uno' });");
        engine.Execute("registration.getNotifications().then(function (list) { result = list.length; });");

        Assert.Equal(1, Count(engine, "created"));
        Assert.Equal(2, (int)engine.Evaluate("result").AsNumber());
    }

    [Fact]
    public void InstallingTwiceThenRemovingLeavesNoBridge()
    {
        // Se instala al crearse el documento y otra vez sobre la página abierta: quitarlo una vez basta.
        var engine = Load();
        engine.Execute(NotificationBridgeScript.Install());

        engine.Execute(NotificationBridgeScript.Remove());
        engine.Execute("registration.showNotification('Vergil', callOptions);");

        Assert.Equal(1, Count(engine, "native"));
        Assert.Equal(0, Count(engine, "created"));
    }

    [Fact]
    public void RemoveBringsBackTheOriginals()
    {
        var engine = Load();

        engine.Execute(NotificationBridgeScript.Remove());
        engine.Execute("registration.showNotification('Vergil', callOptions);");

        Assert.Equal(1, Count(engine, "native"));
        Assert.Equal(0, Count(engine, "created"));

        engine.Execute(NotificationBridgeScript.Install());
        engine.Execute("registration.showNotification('Vergil', callOptions);");
        Assert.Equal(1, Count(engine, "created"));
    }

    [Fact]
    public void RemoveClosesTheNoticesStillShowing()
    {
        var engine = Load();
        engine.Execute("registration.showNotification('Vergil', callOptions);");

        engine.Execute(NotificationBridgeScript.Remove());

        Assert.True(engine.Evaluate("created[0].closed").AsBoolean());
    }

    [Fact]
    public void WithoutServiceWorkersOrNotificationsItIsHarmless()
    {
        var engine = new Engine();
        engine.Execute("var window = {}; window.top = window; var location = { hostname: 'web.whatsapp.com' };");

        engine.Execute(NotificationBridgeScript.Install());
        engine.Execute(NotificationBridgeScript.Remove());
    }

    [Theory]
    [InlineData(NotificationBridgeScript.CallTagPrefix + "3EB0", NotificationKind.Call)]
    [InlineData(NotificationBridgeScript.WorkerTagPrefix + "verify-notification", NotificationKind.Message)]
    [InlineData("3EB0", NotificationKind.Message)]
    [InlineData("", NotificationKind.Message)]
    [InlineData(null, NotificationKind.Message)]
    public void TheTagSaysWhatKindOfNoticeItIs(string? tag, NotificationKind kind)
    {
        Assert.Equal(kind, new IncomingNotification("https://web.whatsapp.com", "Vergil", "Llamada entrante", tag).Kind);
    }
}
