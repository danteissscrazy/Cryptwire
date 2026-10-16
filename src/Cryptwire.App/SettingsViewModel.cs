using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cryptwire.Core;
using Cryptwire.Core.Notifications;
using Cryptwire.Core.Settings;
using Cryptwire.Core.Tones;
using Microsoft.Win32;

namespace Cryptwire.App;

/// <summary>
/// La lógica de la pantalla de ajustes (MVVM): la ventana solo se enlaza a estas propiedades y comandos.
/// Cada cambio se guarda en el momento a través de <see cref="SkinController"/>, que es el dueño de los
/// ajustes; las reglas (qué es un tono válido, cómo se guarda una regla) viven en Core.
/// </summary>
internal sealed class SettingsViewModel : INotifyPropertyChanged
{
    private const string WhatsAppOrigin = "https://web.whatsapp.com";

    private readonly SkinController skins;
    private readonly RecentChats recent;
    private readonly AppPaths paths;
    private readonly ToneLibrary library;
    private readonly SoundPlayer preview = new();
    private readonly DispatcherTimer dimDelay = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private int wallpaperDim = WallpaperSettings.DefaultDim;
    private string newRuleChat = string.Empty;
    private string? status;

    public SettingsViewModel(SkinController skins, RecentChats recent, AppPaths paths)
    {
        this.skins = skins;
        this.recent = recent;
        this.paths = paths;
        library = new ToneLibrary(paths.TonesDir);
        dimDelay.Tick += (_, _) => SaveDim();

        SelectSkinCommand = new RelayCommand(p => _ = p is SkinOption skin ? skins.SelectAsync(skin.Id) : Task.CompletedTask);
        SetMessageToneCommand = new RelayCommand(p => SetGeneralTone(p as string, call: false));
        SetCallToneCommand = new RelayCommand(p => SetGeneralTone(p as string, call: true));
        PlayMessageToneCommand = new RelayCommand(() => PlayGeneral(call: false));
        PlayCallToneCommand = new RelayCommand(() => PlayGeneral(call: true));
        UseRecentChatCommand = new RelayCommand(p => NewRuleChat = p as string ?? string.Empty);
        AddRuleCommand = new RelayCommand(AddRule);
        ChooseRuleMessageCommand = new RelayCommand(p => ChooseRuleTone(p as ToneRuleRow, call: false));
        ChooseRuleCallCommand = new RelayCommand(p => ChooseRuleTone(p as ToneRuleRow, call: true));
        ClearRuleCallCommand = new RelayCommand(p => ClearRuleCall(p as ToneRuleRow));
        RemoveRuleCommand = new RelayCommand(p => RemoveRule(p as ToneRuleRow));
        PlayFileCommand = new RelayCommand(p => Play(p as string));
        ChooseWallpaperCommand = new RelayCommand(ChooseWallpaper);
        RemoveWallpaperCommand = new RelayCommand(RemoveWallpaper);

        skins.StateChanged += OnStateChanged;
        Reload();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Piel ───────────────────────────────────────────────────────────────────────────────────────

    public ObservableCollection<SkinOption> Skins { get; } = [];

    public ICommand SelectSkinCommand { get; }

    public bool SafeMode
    {
        get => skins.SafeMode;
        set
        {
            if (value != skins.SafeMode)
            {
                _ = skins.ToggleSafeModeAsync();
            }
        }
    }

    // ── Avisos ─────────────────────────────────────────────────────────────────────────────────────

    public bool ShowToasts
    {
        get => skins.Settings.ShowToasts;
        set => Update(s => s with { ShowToasts = value }, refresh: false);
    }

    public string MessageToneSource => SourceKey(skins.Settings.MessageTone);

    public string CallToneSource => SourceKey(skins.Settings.CallTone);

    public string MessageToneLabel => Describe(skins.Settings.MessageTone, "tono.wav de la piel");

    public string CallToneLabel => Describe(skins.Settings.CallTone, "llamada.wav de la piel");

    /// <summary>Parámetro: «piel», «ninguno» o «archivo» (este abre el selector de ficheros).</summary>
    public ICommand SetMessageToneCommand { get; }

    public ICommand SetCallToneCommand { get; }

    public ICommand PlayMessageToneCommand { get; }

    public ICommand PlayCallToneCommand { get; }

    // ── Tonos por chat ─────────────────────────────────────────────────────────────────────────────

    public ObservableCollection<ToneRuleRow> ToneRules { get; } = [];

    public bool HasToneRules => ToneRules.Count > 0;

    /// <summary>Los chats que te han avisado desde que abriste Cryptwire, para no tener que escribir el nombre.</summary>
    public IReadOnlyList<string> RecentChats => [.. recent.Names];

    public bool HasRecentChats => recent.Names.Count > 0;

    public string NewRuleChat
    {
        get => newRuleChat;
        set
        {
            newRuleChat = value;
            Notify();
        }
    }

    public ICommand UseRecentChatCommand { get; }

    public ICommand AddRuleCommand { get; }

    public ICommand ChooseRuleMessageCommand { get; }

    public ICommand ChooseRuleCallCommand { get; }

    public ICommand ClearRuleCallCommand { get; }

    public ICommand RemoveRuleCommand { get; }

    public ICommand PlayFileCommand { get; }

    // ── Fondo ──────────────────────────────────────────────────────────────────────────────────────

    public bool HasWallpaper => skins.Settings.Wallpaper is not null;

    public ImageSource? WallpaperPreview { get; private set; }

    public int WallpaperDim
    {
        get => wallpaperDim;
        set
        {
            wallpaperDim = Math.Clamp(value, 0, WallpaperSettings.MaxDim);
            Notify();
            dimDelay.Stop();
            dimDelay.Start();
        }
    }

    public int MaxDim => WallpaperSettings.MaxDim;

    public ICommand ChooseWallpaperCommand { get; }

    public ICommand RemoveWallpaperCommand { get; }

    // ── Sistema ────────────────────────────────────────────────────────────────────────────────────

    public bool StartWithWindows
    {
        get
        {
            try
            {
                return StartupManager.IsEnabled();
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                return false;
            }
        }

        set
        {
            try
            {
                if (value)
                {
                    StartupManager.Enable();
                }
                else
                {
                    StartupManager.Disable();
                }

                Status = value ? "Cryptwire arrancará en la bandeja al iniciar sesión." : "Cryptwire ya no arranca con Windows.";
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException)
            {
                Status = "Windows no ha dejado cambiar el inicio automático.";
            }

            Notify();
        }
    }

    public bool CloseToTray
    {
        get => skins.Settings.CloseToTray;
        set => Update(s => s with { CloseToTray = value }, refresh: false);
    }

    /// <summary>Lo último que ha pasado: un error al elegir un fichero o una confirmación.</summary>
    public string? Status
    {
        get => status;
        private set
        {
            status = value;
            Notify();
        }
    }

    /// <summary>La ventana se cierra: deja de escuchar a la piel y calla el tono de prueba.</summary>
    public void Detach()
    {
        skins.StateChanged -= OnStateChanged;
        if (dimDelay.IsEnabled)
        {
            // Lo último que moviste en el deslizador también cuenta, aunque cierres enseguida.
            SaveDim();
        }

        preview.StopRinging("prueba");
    }

    /// <summary>Quién es el dueño de los selectores de ficheros: con dueño, no se puede abrir otro encima.</summary>
    public System.Windows.Window? DialogOwner { get; set; }

    private static string SourceKey(ToneChoice choice) => choice.Source switch
    {
        ToneSource.None => "ninguno",
        ToneSource.File when !string.IsNullOrWhiteSpace(choice.File) => "archivo",
        _ => "piel",
    };

    private static string Describe(ToneChoice choice, string skinTone) => choice.Source switch
    {
        ToneSource.None => "sin tono",
        ToneSource.File when !string.IsNullOrWhiteSpace(choice.File) => Path.GetFileName(choice.File),
        _ => skinTone,
    };

    private static ImageSource? LoadPreview(string? file)
    {
        if (file is null || !File.Exists(file))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(file, UriKind.Absolute);
            image.DecodePixelWidth = 360;
            image.CacheOption = BitmapCacheOption.OnLoad; // sin dejar el fichero abierto
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or FileFormatException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void OnStateChanged(object? sender, EventArgs e) => Reload();

    private void SaveDim()
    {
        dimDelay.Stop();
        var dim = wallpaperDim;
        Update(s => s.Wallpaper is null ? s : s with { Wallpaper = s.Wallpaper with { Dim = dim } });
    }

    private void Reload()
    {
        Skins.Clear();
        foreach (var theme in skins.Available)
        {
            Skins.Add(new SkinOption(theme.Id, theme.Name, theme.Id == skins.Active?.Id && !skins.SafeMode));
        }

        ToneRules.Clear();
        foreach (var rule in skins.Settings.ToneRules)
        {
            ToneRules.Add(new ToneRuleRow(rule.Chat, rule.MessageTone, rule.CallTone));
        }

        if (!dimDelay.IsEnabled)
        {
            wallpaperDim = skins.Settings.Wallpaper?.Dim ?? WallpaperSettings.DefaultDim;
        }

        WallpaperPreview = LoadPreview(skins.Settings.Wallpaper?.File);
        Notify(string.Empty);
    }

    private void Update(Func<AppSettings, AppSettings> change, bool refresh = true) => _ = skins.UpdateAsync(change, refresh);

    private void SetGeneralTone(string? source, bool call)
    {
        ToneChoice? choice = source switch
        {
            "piel" => ToneChoice.FromSkin,
            "ninguno" => ToneChoice.Silent,
            "archivo" => PickTone(call) is { } file ? ToneChoice.FromFile(file) : null,
            _ => null,
        };

        if (choice is null)
        {
            Notify(string.Empty); // vuelve a enseñar lo que había si se cancela
            return;
        }

        Update(s => call ? s with { CallTone = choice } : s with { MessageTone = choice }, refresh: false);
    }

    private void PlayGeneral(bool call)
    {
        var sample = new IncomingNotification(WhatsAppOrigin, string.Empty, string.Empty, call ? NotificationBridgeScript.CallTagPrefix : null);
        var settings = skins.Settings with { ToneRules = [] };
        Play(new ToneResolver(() => settings, () => skins.Active, paths.TonesDir).Resolve(sample));
    }

    private void Play(string? file)
    {
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            Status = "No hay tono que probar.";
            return;
        }

        // Una vuelta del tono, por el canal de las llamadas: así pulsar otro corta el anterior.
        preview.Ring("prueba", file, TimeSpan.FromSeconds(8));
    }

    private void AddRule()
    {
        if (ChatName.Normalize(NewRuleChat) is not { } chat)
        {
            Status = "Escribe el nombre del chat tal como sale en WhatsApp, o elige uno de los recientes.";
            return;
        }

        if (PickTone(call: false) is not { } file)
        {
            return;
        }

        var existing = skins.Settings.ToneRules.FirstOrDefault(r => ChatName.Same(r.Chat, chat));
        Update(s => s.WithToneRule(chat, file, existing?.CallTone), refresh: false);
        NewRuleChat = string.Empty;
        Status = $"«{chat}» ya tiene su tono.";
    }

    private void ChooseRuleTone(ToneRuleRow? row, bool call)
    {
        if (row is null || PickTone(call) is not { } file)
        {
            return;
        }

        Update(s => s.WithToneRule(row.Chat, call ? row.MessageTone : file, call ? file : row.CallTone), refresh: false);
    }

    private void ClearRuleCall(ToneRuleRow? row)
    {
        if (row is not null)
        {
            Update(s => s.WithToneRule(row.Chat, row.MessageTone, null), refresh: false);
        }
    }

    private void RemoveRule(ToneRuleRow? row)
    {
        if (row is not null)
        {
            Update(s => s.WithoutToneRule(row.Chat), refresh: false);
        }
    }

    /// <summary>Elige un fichero de sonido y lo copia a la carpeta de tonos. Null si se cancela o no vale.</summary>
    private string? PickTone(bool call)
    {
        var dialog = new OpenFileDialog
        {
            Title = call ? "Tono de llamada" : "Tono de mensaje",
            Filter = "Sonidos|" + string.Join(';', ToneLibrary.Extensions.Select(e => "*" + e)),
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(DialogOwner) != true)
        {
            return null;
        }

        try
        {
            var copy = library.Import(dialog.FileName);
            Status = $"Tono guardado: {Path.GetFileName(copy)}.";
            return copy;
        }
        catch (ToneImportException e)
        {
            Status = e.Message;
            return null;
        }
    }

    private void ChooseWallpaper()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Fondo de la conversación",
            Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif;*.tif;*.tiff",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(DialogOwner) != true)
        {
            return;
        }

        try
        {
            var copy = WallpaperImporter.Import(dialog.FileName, paths.WallpapersDir);
            var dim = wallpaperDim;
            Update(s => s with { Wallpaper = new WallpaperSettings(copy, dim) });
            WallpaperImporter.Prune(paths.WallpapersDir, copy);
            Status = "Fondo puesto.";
        }
        catch (WallpaperImportException e)
        {
            Status = e.Message;
        }
    }

    private void RemoveWallpaper()
    {
        Update(s => s with { Wallpaper = null });
        WallpaperImporter.Prune(paths.WallpapersDir, keep: null);
        Status = "Vuelve el fondo de la piel.";
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Una piel en la lista de los ajustes.</summary>
internal sealed record SkinOption(string Id, string Name, bool IsActive);

/// <summary>Una fila de «tonos por chat».</summary>
internal sealed record ToneRuleRow(string Chat, string? MessageTone, string? CallTone)
{
    public string MessageLabel => MessageTone is null ? "el general" : Path.GetFileName(MessageTone);

    public string CallLabel => CallTone is null ? "el general" : Path.GetFileName(CallTone);

    public bool HasCallTone => CallTone is not null;
}
