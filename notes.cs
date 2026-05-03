using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace FloatingNotes
{
    public static class Native
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mod, uint key);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        public const uint MOD_ALT     = 0x1;
        public const uint MOD_CONTROL = 0x2;
        public const uint MOD_SHIFT   = 0x4;
        public const uint MOD_WIN     = 0x8;

        public const int WM_HOTKEY    = 0x0312;
        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_MAXIMIZE  = 0xF030;
    }

    public class Note
    {
        public string title { get; set; }
        public string content { get; set; }
    }

    public class WinCfg
    {
        public double? x { get; set; }
        public double? y { get; set; }
        public double w { get; set; }
        public double h { get; set; }
    }

    public class ShortcutDef
    {
        public string action { get; set; }
        public uint modifiers { get; set; }
        public uint vkey { get; set; }
        public bool enabled { get; set; }
    }

    public class TrashItem
    {
        public string title { get; set; }
        public string content { get; set; }
        public string deletedAt { get; set; }
    }

    public class AppState
    {
        public WinCfg window { get; set; }
        public List<Note> tabs { get; set; }
        public int active { get; set; }
        public string theme { get; set; }
        public string customColor { get; set; }
        public double fontSize { get; set; }
        public List<ShortcutDef> shortcuts { get; set; }
        public List<TrashItem> trash { get; set; }
        public bool launchOnStartup { get; set; }
    }

    class ShortcutRowRefs
    {
        public CheckBox Checkbox;
        public TextBlock Label;
        public Button BtnCombo;
    }

    public static class Program
    {
        // Paths
        static readonly string ScriptDir   = AppDomain.CurrentDomain.BaseDirectory;
        static readonly string IconPath    = Path.Combine(ScriptDir, "notes.ico");
        static readonly string XamlPath    = Path.Combine(ScriptDir, "notes.xaml");
        static readonly string DataDir     = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Note");
        static readonly string DataFile    = Path.Combine(DataDir, "notes.json");
        static readonly string DataFileTmp = Path.Combine(DataDir, "notes.tmp");
        static readonly string OldDataDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FloatingNotes");
        static readonly string OldDataFile = Path.Combine(OldDataDir, "notes.json");

        const string AumidId      = "LeGeai.Note";
        const string MutexName    = "LeGeai_Note_Singleton";
        const string ShowEvtName  = "LeGeai_Note_Show";
        const string StartupRegName = "Note";
        const int    MaxTrash     = 3;

        // Constants
        const double DefaultWidth     = 380;
        const double DefaultHeight    = 420;
        const double DefaultFontSize  = 14;
        const double MinFontSize      = 10;
        const double MaxFontSize      = 24;
        const int    MaxPages         = 20;
        // App accent color (Note variant = purple)
        const string AccentHex        = "#7C3AED";

        // Application + window
        static Application App;
        static Window Win;

        // Title bar
        static Grid NotesTitleBar, SettingsTitleBar;
        static StackPanel TabsHost;
        static ScrollViewer TabsScroll;
        static Button BtnAddTab, BtnSettings, BtnBackToNotes, BtnMin, BtnClose;
        static TextBlock SettingsTitle;
        static Border RootBorder;

        // Settings panel
        static Grid SettingsView;
        static StackPanel PaneGeneral, PaneShortcuts, PaneInformation, PaneTrash, ShortcutsHost;
        static Border SubTabGeneral, SubTabShortcuts, SubTabInformation, SubTabTrash;
        static TextBlock LblTabGeneral, LblTabShortcuts, LblTabInformation, LblTabTrash;
        static TextBlock LblTheme, LblFontSize, LblPosition, LblShortcutsHint;
        static TextBlock FontSizeLabel;
        static Button BtnReposition, BtnFontMinus, BtnFontPlus;
        static Border[] ThemeSwatches;
        static Grid CustomColorRow;
        static Border ColorSwatch;
        static TextBox HexInputBox;
        static Hyperlink LinkSite, LinkSupport;
        static string CurrentSubTab = "general";

        // Editor host
        static ContentControl EditorHost;

        // Search
        static Grid SearchPanel;
        static TextBox SearchInput;
        static TextBlock SearchCount;
        static Button BtnSearchClose;
        static StackPanel SearchResultsHost;
        static Border ScrollHintLeft, ScrollHintRight;
        static bool IsSearchOpen = false;

        // Drag-scroll state
        static bool DragPending = false;
        static bool DragActive  = false;
        static Point DragStart;
        static double DragStartOffset;
        static int DragPendingTab = -1;

        // Trash + startup
        static List<TrashItem> Trash = new List<TrashItem>();
        static StackPanel TrashHost;
        static Button BtnEmptyTrash, BtnQuitApp;
        static TextBlock LblTrash, LblStartup;
        static CheckBox ChkLaunchOnStartup;

        // Single-instance
        static Mutex _appMutex;
        static EventWaitHandle _showEvent;
        static RegisteredWaitHandle _showWaitHandle;

        // Pages (custom tab system)
        static List<Note> Pages = new List<Note>();
        static List<Border> TabButtons = new List<Border>();
        static int ActiveTabIndex = 0;
        static Dictionary<Note, TextBox> Editors = new Dictionary<Note, TextBox>();

        // State
        static DispatcherTimer SaveTimer;
        static string CurrentTheme       = "dark";
        static string CurrentCustomColor = "#1A1A1A";
        static double CurrentFontSize    = DefaultFontSize;
        static string Lang               = "en";
        static bool   IsSettingsOpen     = false;

        // Shortcuts
        static List<ShortcutDef> Shortcuts;
        static List<ShortcutRowRefs> ShortcutRowsRefs = new List<ShortcutRowRefs>();
        static int CapturingIndex = -1;

        // Themes (palette without alpha for Bg/BgHov; alpha applied via opacity slider)
        static readonly Dictionary<string, Dictionary<string, string>> Themes
            = new Dictionary<string, Dictionary<string, string>>
            {
                { "deep_black", new Dictionary<string, string> {
                    { "Bg", "#0A0A0A" }, { "BgHov", "#161616" }, { "Fg", "#E8E8E8" },
                    { "FgMute", "#666666" }, { "FgHov", "#999999" },
                    { "Border", "#1F1F1F" }, { "Selection", "#3A6FA5" }
                } },
                { "dark", new Dictionary<string, string> {
                    { "Bg", "#1A1A1A" }, { "BgHov", "#262626" }, { "Fg", "#DDDDDD" },
                    { "FgMute", "#777777" }, { "FgHov", "#AAAAAA" },
                    { "Border", "#2A2A2A" }, { "Selection", "#3A6FA5" }
                } },
                { "white", new Dictionary<string, string> {
                    { "Bg", "#FFFFFF" }, { "BgHov", "#F0F0F0" }, { "Fg", "#1A1A1A" },
                    { "FgMute", "#888888" }, { "FgHov", "#444444" },
                    { "Border", "#E0E0E0" }, { "Selection", "#A8C8E8" }
                } },
                { "cream", new Dictionary<string, string> {
                    { "Bg", "#FAF6EE" }, { "BgHov", "#F0EBDF" }, { "Fg", "#2A2520" },
                    { "FgMute", "#8A7F70" }, { "FgHov", "#5A4F40" },
                    { "Border", "#E8DFD0" }, { "Selection", "#D4B98A" }
                } },
                { "glass", new Dictionary<string, string> {
                    { "Bg", "#DCEEF5" }, { "BgHov", "#C8DCE8" }, { "Fg", "#1F2030" },
                    { "FgMute", "#6E7E8E" }, { "FgHov", "#3A4A5A" },
                    { "Border", "#A8C0D8" }, { "Selection", "#A8D0E8" }
                } }
            };

        // Internationalization
        static readonly Dictionary<string, Dictionary<string, string>> I18n
            = new Dictionary<string, Dictionary<string, string>>
            {
                { "fr", new Dictionary<string, string> {
                    { "settings", "Paramètres" }, { "back", "Retour" },
                    { "minimize", "Réduire" }, { "close", "Fermer" },
                    { "new_sheet", "Nouvelle feuille" },
                    { "theme_section", "THÈME" }, { "font_size_section", "TAILLE DU TEXTE" },
                    { "position_section", "POSITION" },
                    { "reposition_button", "Repositionner et redimensionner" },
                    { "rename", "Renommer" }, { "delete", "Supprimer la feuille" },
                    { "new_tab_default", "Nouvelle" }, { "untitled", "Sans titre" },
                    { "notes_default", "Notes" },
                    { "footer_text", "Application gratuite et open source réalisée par" },
                    { "tab_general", "Général" }, { "tab_shortcuts", "Raccourcis" }, { "tab_trash", "Corbeille" }, { "tab_information", "Information" },
                    { "shortcuts_hint", "RACCOURCIS GLOBAUX – CLIC POUR CAPTURER, ÉCHAP POUR ANNULER" },
                    { "press_keys", "Appuyez sur une touche…" },
                    { "sc_reposition", "Repositionner la fenêtre" },
                    { "sc_toggle_settings", "Ouvrir/fermer paramètres" },
                    { "sc_close_window", "Fermer la fenêtre" },
                    { "sc_minimize_window", "Réduire la fenêtre" },
                    { "sc_new_page", "Nouvelle page" },
                    { "sc_toggle_visibility", "Afficher / masquer la fenêtre" },
                    { "max_pages_title", "Limite atteinte" },
                    { "max_pages_msg", "Maximum de 20 feuilles atteint. Supprimez-en une pour en ajouter." },
                    { "trash_section", "CORBEILLE" },
                    { "trash_empty", "Vide" },
                    { "trash_empty_btn", "Vider la corbeille" },
                    { "trash_restore", "Restaurer" },
                    { "trash_delete_perm", "Supprimer définitivement" },
                    { "startup_section", "DÉMARRAGE" },
                    { "launch_on_startup", "Lancer avec Windows" },
                    { "quit_app", "Quitter Note" },
                    { "restored_suffix", " (restauré)" }
                } },
                { "en", new Dictionary<string, string> {
                    { "settings", "Settings" }, { "back", "Back" },
                    { "minimize", "Minimize" }, { "close", "Close" },
                    { "new_sheet", "New sheet" },
                    { "theme_section", "THEME" }, { "font_size_section", "TEXT SIZE" },
                    { "position_section", "POSITION" },
                    { "reposition_button", "Reset position and size" },
                    { "rename", "Rename" }, { "delete", "Delete sheet" },
                    { "new_tab_default", "New" }, { "untitled", "Untitled" },
                    { "notes_default", "Notes" },
                    { "footer_text", "Free and open-source app made by" },
                    { "tab_general", "General" }, { "tab_shortcuts", "Shortcuts" }, { "tab_trash", "Trash" }, { "tab_information", "Information" },
                    { "shortcuts_hint", "GLOBAL HOTKEYS – CLICK TO CAPTURE, ESC TO CANCEL" },
                    { "press_keys", "Press keys…" },
                    { "sc_reposition", "Reset window position" },
                    { "sc_toggle_settings", "Toggle settings" },
                    { "sc_close_window", "Close window" },
                    { "sc_minimize_window", "Minimize window" },
                    { "sc_new_page", "New page" },
                    { "sc_toggle_visibility", "Show / hide window" },
                    { "max_pages_title", "Limit reached" },
                    { "max_pages_msg", "Maximum of 20 sheets reached. Delete one to add another." },
                    { "trash_section", "TRASH" },
                    { "trash_empty", "Empty" },
                    { "trash_empty_btn", "Empty trash" },
                    { "trash_restore", "Restore" },
                    { "trash_delete_perm", "Delete permanently" },
                    { "startup_section", "STARTUP" },
                    { "launch_on_startup", "Launch with Windows" },
                    { "quit_app", "Quit Note" },
                    { "restored_suffix", " (restored)" }
                } },
                { "es", new Dictionary<string, string> {
                    { "settings", "Ajustes" }, { "back", "Atrás" },
                    { "minimize", "Minimizar" }, { "close", "Cerrar" },
                    { "new_sheet", "Nueva hoja" },
                    { "theme_section", "TEMA" }, { "font_size_section", "TAMAÑO DEL TEXTO" },
                    { "position_section", "POSICIÓN" },
                    { "reposition_button", "Restablecer posición y tamaño" },
                    { "rename", "Renombrar" }, { "delete", "Eliminar hoja" },
                    { "new_tab_default", "Nueva" }, { "untitled", "Sin título" },
                    { "notes_default", "Notas" },
                    { "footer_text", "Aplicación gratuita y de código abierto por" },
                    { "tab_general", "General" }, { "tab_shortcuts", "Atajos" }, { "tab_trash", "Papelera" }, { "tab_information", "Información" },
                    { "shortcuts_hint", "ATAJOS GLOBALES – CLIC PARA CAPTURAR, ESC PARA CANCELAR" },
                    { "press_keys", "Pulse una tecla…" },
                    { "sc_reposition", "Restablecer posición" },
                    { "sc_toggle_settings", "Abrir/cerrar ajustes" },
                    { "sc_close_window", "Cerrar ventana" },
                    { "sc_minimize_window", "Minimizar ventana" },
                    { "sc_new_page", "Nueva página" },
                    { "sc_toggle_visibility", "Mostrar / ocultar ventana" },
                    { "max_pages_title", "Límite alcanzado" },
                    { "max_pages_msg", "Máximo de 20 hojas alcanzado. Elimine una para añadir otra." },
                    { "trash_section", "PAPELERA" },
                    { "trash_empty", "Vacía" },
                    { "trash_empty_btn", "Vaciar la papelera" },
                    { "trash_restore", "Restaurar" },
                    { "trash_delete_perm", "Eliminar definitivamente" },
                    { "startup_section", "INICIO" },
                    { "launch_on_startup", "Iniciar con Windows" },
                    { "quit_app", "Salir de Note" },
                    { "restored_suffix", " (restaurada)" }
                } },
                { "de", new Dictionary<string, string> {
                    { "settings", "Einstellungen" }, { "back", "Zurück" },
                    { "minimize", "Minimieren" }, { "close", "Schließen" },
                    { "new_sheet", "Neues Blatt" },
                    { "theme_section", "DESIGN" }, { "font_size_section", "TEXTGRÖSSE" },
                    { "position_section", "POSITION" },
                    { "reposition_button", "Position und Größe zurücksetzen" },
                    { "rename", "Umbenennen" }, { "delete", "Blatt löschen" },
                    { "new_tab_default", "Neu" }, { "untitled", "Ohne Titel" },
                    { "notes_default", "Notizen" },
                    { "footer_text", "Kostenlose Open-Source-App von" },
                    { "tab_general", "Allgemein" }, { "tab_shortcuts", "Tastenkürzel" }, { "tab_trash", "Papierkorb" }, { "tab_information", "Information" },
                    { "shortcuts_hint", "GLOBALE TASTENKÜRZEL – KLICKEN ZUM ERFASSEN, ESC ZUM ABBRECHEN" },
                    { "press_keys", "Tasten drücken…" },
                    { "sc_reposition", "Fensterposition zurücksetzen" },
                    { "sc_toggle_settings", "Einstellungen ein/aus" },
                    { "sc_close_window", "Fenster schließen" },
                    { "sc_minimize_window", "Fenster minimieren" },
                    { "sc_new_page", "Neue Seite" },
                    { "sc_toggle_visibility", "Fenster anzeigen / ausblenden" },
                    { "max_pages_title", "Limit erreicht" },
                    { "max_pages_msg", "Maximal 20 Blätter erreicht. Löschen Sie eines, um ein neues hinzuzufügen." },
                    { "trash_section", "PAPIERKORB" },
                    { "trash_empty", "Leer" },
                    { "trash_empty_btn", "Papierkorb leeren" },
                    { "trash_restore", "Wiederherstellen" },
                    { "trash_delete_perm", "Endgültig löschen" },
                    { "startup_section", "AUTOSTART" },
                    { "launch_on_startup", "Mit Windows starten" },
                    { "quit_app", "Note beenden" },
                    { "restored_suffix", " (wiederhergestellt)" }
                } }
            };

        static string T(string key)
        {
            if (I18n[Lang].ContainsKey(key)) return I18n[Lang][key];
            if (I18n["en"].ContainsKey(key)) return I18n["en"][key];
            return key;
        }

        // ----- Main -----

        [STAThread]
        public static void Main()
        {
            // Single-instance: if already running, signal it to show and exit
            bool createdNew;
            _appMutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                try
                {
                    var existing = EventWaitHandle.OpenExisting(ShowEvtName);
                    existing.Set();
                }
                catch { }
                return;
            }

            try { Native.SetCurrentProcessExplicitAppUserModelID(AumidId); } catch { }
            MigrateOldData();
            Directory.CreateDirectory(DataDir);

            try
            {
                var l = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                Lang = I18n.ContainsKey(l) ? l : "en";
            }
            catch { Lang = "en"; }

            App = new Application();
            App.Resources["Accent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AccentHex));

            var state = LoadState();
            CurrentCustomColor = string.IsNullOrEmpty(state.customColor) ? "#1A1A1A" : state.customColor;
            CurrentFontSize    = (state.fontSize >= MinFontSize && state.fontSize <= MaxFontSize) ? state.fontSize : DefaultFontSize;
            Trash              = state.trash ?? new List<TrashItem>();
            App.Resources["EditorFontSize"] = CurrentFontSize;
            ApplyTheme(string.IsNullOrEmpty(state.theme) ? "dark" : state.theme);

            // Init shortcut definitions
            InitShortcuts();
            if (state.shortcuts != null)
            {
                foreach (var sav in state.shortcuts)
                {
                    var sc = Shortcuts.Find(s => s.action == sav.action);
                    if (sc != null)
                    {
                        sc.modifiers = sav.modifiers;
                        sc.vkey = sav.vkey;
                        sc.enabled = sav.enabled;
                    }
                }
            }

            try
            {
                using (var fs = File.OpenRead(XamlPath))
                {
                    Win = (Window)XamlReader.Load(fs);
                }

                FindElements();
                ApplyTranslations();

                if (File.Exists(IconPath))
                {
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.UriSource = new Uri(IconPath);
                        bi.EndInit();
                        bi.Freeze();
                        Win.Icon = bi;
                    }
                    catch { }
                }

                SaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                SaveTimer.Tick += (s, e) => { SaveTimer.Stop(); SaveState(); };

                ApplyWindowState(state.window);

                // Init pages
                Pages = state.tabs ?? new List<Note>();
                if (Pages.Count == 0)
                    Pages.Add(new Note { title = T("notes_default"), content = "" });
                ActiveTabIndex = state.active;
                if (ActiveTabIndex < 0 || ActiveTabIndex >= Pages.Count) ActiveTabIndex = 0;

                BuildTabButtons();
                EditorHost.Content = GetOrCreateEditor(Pages[ActiveTabIndex]);
                UpdateTabsVisual();

                // Wire chrome buttons
                BtnAddTab.Click   += (s, e) => AddNewPage();
                BtnSettings.Click += (s, e) => ToggleSettings();
                BtnBackToNotes.Click += (s, e) => HideSettings();
                BtnQuitApp.Click  += (s, e) => QuitApp();
                TabsScroll.PreviewMouseWheel += (s, e) =>
                {
                    TabsScroll.ScrollToHorizontalOffset(TabsScroll.HorizontalOffset - e.Delta);
                    e.Handled = true;
                };
                TabsScroll.ScrollChanged += (s, e) => UpdateScrollHints();
                TabsScroll.SizeChanged += (s, e) => UpdateScrollHints();
                TabsScroll.PreviewMouseLeftButtonDown += OnTabsPreviewDown;
                TabsScroll.PreviewMouseMove += OnTabsPreviewMove;
                TabsScroll.PreviewMouseLeftButtonUp += OnTabsPreviewUp;

                BtnSearchClose.Click += (s, e) => HideSearch();
                SearchInput.TextChanged += (s, e) => DoSearch();
                Win.PreviewKeyDown += OnGlobalKey;
                BtnMin.Click   += (s, e) => Win.WindowState = System.Windows.WindowState.Minimized;
                BtnClose.Click += (s, e) => HideWindow();

                BtnAddTab.ToolTip = T("new_sheet");
                BtnSettings.ToolTip = T("settings");
                BtnMin.ToolTip = T("minimize");
                BtnClose.ToolTip = T("close");
                BtnBackToNotes.ToolTip = T("back");

                WireSettingsPanel();
                BuildShortcutsUI();
                UpdateSubTabVisuals(true);  // start on General

                Win.StateChanged += (s, e) =>
                {
                    // Block any maximize attempt that slipped through
                    if (Win.WindowState == System.Windows.WindowState.Maximized)
                        Win.WindowState = System.Windows.WindowState.Normal;
                    ScheduleSave();
                };
                Win.Closing     += (s, e) =>
                {
                    UnregisterAllShortcuts();
                    SaveTimer.Stop();
                    SaveState();
                    if (_showWaitHandle != null) _showWaitHandle.Unregister(null);
                    if (_showEvent != null) _showEvent.Close();
                    if (_appMutex != null) { try { _appMutex.ReleaseMutex(); } catch { } _appMutex.Close(); }
                };

                Win.SourceInitialized += (s, e) =>
                {
                    HookWndProc();
                    RegisterAllShortcuts();
                    SetupSecondInstanceListener();
                };

                App.Run(Win);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Notes - Error");
            }
        }

        static void FindElements()
        {
            NotesTitleBar    = (Grid)Win.FindName("NotesTitleBar");
            SettingsTitleBar = (Grid)Win.FindName("SettingsTitleBar");
            TabsHost         = (StackPanel)Win.FindName("TabsHost");
            TabsScroll       = (ScrollViewer)Win.FindName("TabsScroll");
            BtnAddTab        = (Button)Win.FindName("BtnAddTab");
            BtnSettings      = (Button)Win.FindName("BtnSettings");
            BtnBackToNotes   = (Button)Win.FindName("BtnBackToNotes");
            BtnMin           = (Button)Win.FindName("BtnMin");
            BtnClose         = (Button)Win.FindName("BtnClose");
            SettingsTitle    = (TextBlock)Win.FindName("SettingsTitle");
            RootBorder       = (Border)Win.FindName("RootBorder");
            EditorHost       = (ContentControl)Win.FindName("EditorHost");

            SettingsView      = (Grid)Win.FindName("SettingsView");
            PaneGeneral       = (StackPanel)Win.FindName("PaneGeneral");
            PaneShortcuts     = (StackPanel)Win.FindName("PaneShortcuts");
            PaneTrash         = (StackPanel)Win.FindName("PaneTrash");
            PaneInformation   = (StackPanel)Win.FindName("PaneInformation");
            ShortcutsHost     = (StackPanel)Win.FindName("ShortcutsHost");
            SubTabGeneral     = (Border)Win.FindName("SubTabGeneral");
            SubTabShortcuts   = (Border)Win.FindName("SubTabShortcuts");
            SubTabTrash       = (Border)Win.FindName("SubTabTrash");
            SubTabInformation = (Border)Win.FindName("SubTabInformation");
            LblTabGeneral     = (TextBlock)Win.FindName("LblTabGeneral");
            LblTabShortcuts   = (TextBlock)Win.FindName("LblTabShortcuts");
            LblTabTrash       = (TextBlock)Win.FindName("LblTabTrash");
            LblTabInformation = (TextBlock)Win.FindName("LblTabInformation");
            LblTheme         = (TextBlock)Win.FindName("LblTheme");
            LblFontSize      = (TextBlock)Win.FindName("LblFontSize");
            LblPosition      = (TextBlock)Win.FindName("LblPosition");
            LblShortcutsHint = (TextBlock)Win.FindName("LblShortcutsHint");
            FontSizeLabel    = (TextBlock)Win.FindName("FontSizeLabel");
            BtnReposition    = (Button)Win.FindName("BtnReposition");
            BtnFontMinus     = (Button)Win.FindName("BtnFontMinus");
            BtnFontPlus      = (Button)Win.FindName("BtnFontPlus");
            LblTrash         = (TextBlock)Win.FindName("LblTrash");
            LblStartup       = (TextBlock)Win.FindName("LblStartup");
            TrashHost        = (StackPanel)Win.FindName("TrashHost");
            BtnEmptyTrash    = (Button)Win.FindName("BtnEmptyTrash");
            BtnQuitApp       = (Button)Win.FindName("BtnQuitApp");
            ChkLaunchOnStartup = (CheckBox)Win.FindName("ChkLaunchOnStartup");
            CustomColorRow   = (Grid)Win.FindName("CustomColorRow");
            ColorSwatch      = (Border)Win.FindName("ColorSwatch");
            HexInputBox      = (TextBox)Win.FindName("HexInputBox");
            LinkSite         = (Hyperlink)Win.FindName("LinkSite");
            LinkSupport      = (Hyperlink)Win.FindName("LinkSupport");

            SearchPanel       = (Grid)Win.FindName("SearchPanel");
            SearchInput       = (TextBox)Win.FindName("SearchInput");
            SearchCount       = (TextBlock)Win.FindName("SearchCount");
            BtnSearchClose    = (Button)Win.FindName("BtnSearchClose");
            SearchResultsHost = (StackPanel)Win.FindName("SearchResultsHost");
            ScrollHintLeft    = (Border)Win.FindName("ScrollHintLeft");
            ScrollHintRight   = (Border)Win.FindName("ScrollHintRight");

            ThemeSwatches = new Border[] {
                (Border)Win.FindName("SwDeepBlack"),
                (Border)Win.FindName("SwDark"),
                (Border)Win.FindName("SwWhite"),
                (Border)Win.FindName("SwCream"),
                (Border)Win.FindName("SwGlass"),
                (Border)Win.FindName("SwCustom"),
            };
        }

        static void ApplyTranslations()
        {
            Win.Title              = T("notes_default");
            SettingsTitle.Text     = T("settings");
            LblTabGeneral.Text     = T("tab_general");
            LblTabShortcuts.Text   = T("tab_shortcuts");
            LblTabTrash.Text       = T("tab_trash");
            LblTabInformation.Text = T("tab_information");
            LblTheme.Text          = T("theme_section");
            LblFontSize.Text       = T("font_size_section");
            LblPosition.Text       = T("position_section");
            BtnReposition.Content  = T("reposition_button");
            LblShortcutsHint.Text  = T("shortcuts_hint");
            LblTrash.Text          = T("trash_section");
            LblStartup.Text        = T("startup_section");
            ChkLaunchOnStartup.Content = T("launch_on_startup");
            BtnEmptyTrash.Content  = T("trash_empty_btn");
            BtnQuitApp.Content     = T("quit_app");
        }

        // ----- Tabs (custom) -----

        static void BuildTabButtons()
        {
            TabsHost.Children.Clear();
            TabButtons.Clear();
            for (int i = 0; i < Pages.Count; i++)
            {
                var btn = CreateTabButton(Pages[i]);
                TabsHost.Children.Add(btn);
                TabButtons.Add(btn);
            }
        }

        static Border CreateTabButton(Note page)
        {
            var border = new Border
            {
                Style = (Style)Win.FindResource("TabBtn"),
                Tag   = page
            };
            border.SetValue(System.Windows.Shell.WindowChrome.IsHitTestVisibleInChromeProperty, true);
            var label = new TextBlock
            {
                Style = (Style)Win.FindResource("TabLabel"),
                Text  = page.title
            };
            border.Child = label;

            border.MouseRightButtonUp += (s, e) =>
            {
                var b = (Border)s;
                var p = (Note)b.Tag;
                int idx = Pages.IndexOf(p);
                if (idx < 0) return;
                var menu = new ContextMenu();
                var miRename = new MenuItem { Header = T("rename") };
                miRename.Click += (s2, e2) => StartRenameTab(idx);
                menu.Items.Add(miRename);
                var miDelete = new MenuItem { Header = T("delete") };
                miDelete.Click += (s2, e2) => RemoveTab(idx);
                menu.Items.Add(miDelete);
                menu.PlacementTarget = b;
                menu.IsOpen = true;
                e.Handled = true;
            };

            return border;
        }

        static void SetActiveTab(int index)
        {
            if (index < 0 || index >= Pages.Count) return;
            ActiveTabIndex = index;
            EditorHost.Content = GetOrCreateEditor(Pages[index]);
            UpdateTabsVisual();
            ScheduleSave();
        }

        static TextBox GetOrCreateEditor(Note page)
        {
            if (Editors.ContainsKey(page)) return Editors[page];
            var editor = new TextBox
            {
                Style = (Style)Win.FindResource("Editor"),
                Text  = page.content
            };
            editor.TextChanged += (s, e) =>
            {
                page.content = editor.Text;
                ScheduleSave();
            };
            Editors[page] = editor;
            return editor;
        }

        static void UpdateTabsVisual()
        {
            var accent = (Brush)App.Resources["Accent"];
            var fg     = (Brush)App.Resources["Fg"];
            var fgMute = (Brush)App.Resources["FgMute"];
            for (int i = 0; i < TabButtons.Count; i++)
            {
                var btn = TabButtons[i];
                var label = btn.Child as TextBlock;
                if (label != null)
                {
                    label.Foreground = (i == ActiveTabIndex) ? fg : fgMute;
                }
                btn.BorderBrush = (i == ActiveTabIndex) ? accent : Brushes.Transparent;
            }
        }

        static void StartRenameTab(int index)
        {
            if (index < 0 || index >= Pages.Count) return;
            var btn = TabButtons[index];
            var page = Pages[index];

            var tbox = new TextBox
            {
                Text = page.title,
                MinWidth = 60, MaxWidth = 220,
                Padding = new Thickness(4, 1, 4, 1),
                BorderThickness = new Thickness(0),
                Background = (Brush)App.Resources["BgHov"],
                Foreground = (Brush)App.Resources["Fg"],
                CaretBrush = (Brush)App.Resources["Fg"],
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = btn
            };
            tbox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Return)
                {
                    e.Handled = true;
                    CommitRename((TextBox)s, false, index);
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    CommitRename((TextBox)s, true, index);
                }
            };
            tbox.LostFocus += (s, e) =>
            {
                var t = (TextBox)s;
                var b = (Border)t.Tag;
                if (!ReferenceEquals(b.Child, t)) return;
                CommitRename(t, false, index);
            };

            btn.Child = tbox;
            tbox.Focus();
            tbox.SelectAll();
        }

        static void CommitRename(TextBox tbox, bool cancel, int index)
        {
            if (index < 0 || index >= Pages.Count) return;
            var btn = TabButtons[index];
            var page = Pages[index];
            if (!cancel)
            {
                var nt = tbox.Text.Trim();
                if (string.IsNullOrEmpty(nt)) nt = T("untitled");
                page.title = nt;
            }
            var label = new TextBlock
            {
                Style = (Style)Win.FindResource("TabLabel"),
                Text  = page.title
            };
            btn.Child = label;
            UpdateTabsVisual();
            ScheduleSave();
        }

        static void AddNewPage()
        {
            if (Pages.Count >= MaxPages)
            {
                MessageBox.Show(T("max_pages_msg"), T("max_pages_title"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var page = new Note { title = T("new_tab_default"), content = "" };
            Pages.Add(page);
            BuildTabButtons();
            SetActiveTab(Pages.Count - 1);
            ScheduleSave();
            Win.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (TabsScroll != null) TabsScroll.ScrollToHorizontalOffset(double.MaxValue);
            }));
        }

        static void RemoveTab(int index)
        {
            if (Pages.Count <= 1) return;
            if (index < 0 || index >= Pages.Count) return;
            var page = Pages[index];
            SendToTrash(page);
            Editors.Remove(page);
            Pages.RemoveAt(index);
            if (ActiveTabIndex >= Pages.Count) ActiveTabIndex = Pages.Count - 1;
            BuildTabButtons();
            SetActiveTab(ActiveTabIndex);
            BuildTrashUI();
            ScheduleSave();
        }

        static void UpdateScrollHints()
        {
            if (TabsScroll == null) return;
            bool canRight = TabsScroll.HorizontalOffset + TabsScroll.ViewportWidth < TabsScroll.ExtentWidth - 0.5;
            bool canLeft  = TabsScroll.HorizontalOffset > 0.5;
            ScrollHintRight.Visibility = canRight ? Visibility.Visible : Visibility.Collapsed;
            ScrollHintLeft.Visibility  = canLeft  ? Visibility.Visible : Visibility.Collapsed;
        }

        static int HitTestTabFrom(object originalSource)
        {
            var src = originalSource as DependencyObject;
            while (src != null)
            {
                var b = src as Border;
                if (b != null && b.Tag is Note)
                {
                    return Pages.IndexOf((Note)b.Tag);
                }
                src = (src is Visual || src is System.Windows.Media.Media3D.Visual3D)
                    ? VisualTreeHelper.GetParent(src)
                    : LogicalTreeHelper.GetParent(src);
            }
            return -1;
        }

        static void OnTabsPreviewDown(object sender, MouseButtonEventArgs e)
        {
            DragStart = e.GetPosition(TabsScroll);
            DragStartOffset = TabsScroll.HorizontalOffset;
            DragPending = true;
            DragActive = false;
            DragPendingTab = HitTestTabFrom(e.OriginalSource);

            if (e.ClickCount == 2 && DragPendingTab >= 0)
            {
                DragPending = false;
                StartRenameTab(DragPendingTab);
                DragPendingTab = -1;
                e.Handled = true;
                return;
            }
            // Suppress default click handling on tab; we'll activate on Up if no drag.
            e.Handled = true;
        }

        static void OnTabsPreviewMove(object sender, MouseEventArgs e)
        {
            if (!DragPending || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(TabsScroll);
            if (!DragActive && Math.Abs(pos.X - DragStart.X) > 4)
            {
                DragActive = true;
                TabsScroll.CaptureMouse();
                TabsScroll.Cursor = System.Windows.Input.Cursors.SizeWE;
            }
            if (DragActive)
            {
                var delta = DragStart.X - pos.X;
                TabsScroll.ScrollToHorizontalOffset(DragStartOffset + delta);
                e.Handled = true;
            }
        }

        static void OnTabsPreviewUp(object sender, MouseButtonEventArgs e)
        {
            if (DragActive)
            {
                TabsScroll.ReleaseMouseCapture();
                TabsScroll.Cursor = null;
                DragActive = false;
                DragPending = false;
                DragPendingTab = -1;
                e.Handled = true;
                return;
            }
            if (DragPending)
            {
                DragPending = false;
                if (DragPendingTab >= 0)
                {
                    SetActiveTab(DragPendingTab);
                    e.Handled = true;
                }
                DragPendingTab = -1;
            }
        }

        // ----- Global keys & search -----

        static void OnGlobalKey(object sender, KeyEventArgs e)
        {
            if (CapturingIndex >= 0) return;  // shortcut capture has priority
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (IsSettingsOpen) HideSettings();
                if (IsSearchOpen) HideSearch();
                else ShowSearch();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && IsSearchOpen)
            {
                HideSearch();
                e.Handled = true;
            }
        }

        static void ShowSearch()
        {
            IsSearchOpen = true;
            EditorHost.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Visible;
            SearchInput.Text = "";
            SearchResultsHost.Children.Clear();
            SearchCount.Text = "";
            SearchInput.Focus();
        }

        static void HideSearch()
        {
            IsSearchOpen = false;
            SearchPanel.Visibility = Visibility.Collapsed;
            EditorHost.Visibility = Visibility.Visible;
        }

        static void DoSearch()
        {
            SearchResultsHost.Children.Clear();
            SearchCount.Text = "";
            var q = SearchInput.Text;
            if (string.IsNullOrEmpty(q)) return;

            // Sync current editor content
            foreach (var kv in Editors) kv.Key.content = kv.Value.Text;

            int total = 0;
            for (int pi = 0; pi < Pages.Count; pi++)
            {
                var page = Pages[pi];
                var content = page.content ?? "";
                int idx = 0;
                while ((idx = content.IndexOf(q, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    int snipStart = Math.Max(0, idx - 24);
                    int snipEnd   = Math.Min(content.Length, idx + q.Length + 36);
                    var snippet   = content.Substring(snipStart, snipEnd - snipStart)
                                           .Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
                    var prefix = (snipStart > 0) ? "…" : "";
                    var suffix = (snipEnd < content.Length) ? "…" : "";
                    int matchStart = idx;
                    int matchPage = pi;
                    int matchLen = q.Length;

                    SearchResultsHost.Children.Add(BuildSearchResult(page.title, prefix + snippet + suffix, matchPage, matchStart, matchLen));
                    total++;
                    idx += q.Length;
                    if (total >= 200) break;
                }
                if (total >= 200) break;
            }
            SearchCount.Text = total.ToString();
        }

        static Border BuildSearchResult(string pageTitle, string snippet, int pageIdx, int matchStart, int matchLen)
        {
            var b = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(10, 6, 10, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 1, 0, 1)
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = pageTitle,
                Foreground = (Brush)App.Resources["FgMute"],
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            });
            sp.Children.Add(new TextBlock
            {
                Text = snippet,
                Foreground = (Brush)App.Resources["Fg"],
                FontSize = 12,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            b.Child = sp;
            b.MouseEnter += (s, e) => b.Background = (Brush)App.Resources["BgHov"];
            b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonDown += (s, e) =>
            {
                HideSearch();
                SetActiveTab(pageIdx);
                Win.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    var ed = GetOrCreateEditor(Pages[pageIdx]);
                    if (matchStart + matchLen <= ed.Text.Length)
                    {
                        ed.Focus();
                        ed.Select(matchStart, matchLen);
                    }
                }));
            };
            return b;
        }

        // ----- Theme & opacity -----

        static void SetBrush(string key, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            App.Resources[key] = new SolidColorBrush(color);
        }

        static void ApplyTheme(string name)
        {
            if (name == "custom")
            {
                ApplyCustomTheme(CurrentCustomColor);
                return;
            }
            if (!Themes.ContainsKey(name)) name = "dark";
            var palette = Themes[name];
            foreach (var kvp in palette) SetBrush(kvp.Key, kvp.Value);
            CurrentTheme = name;
            if (Win != null && ThemeSwatches != null)
            {
                UpdateSwatchSelection();
                UpdateTabsVisual();
                UpdateSubTabVisuals(IsGeneralPaneActive());
            }
        }

        static void ApplyCustomTheme(string baseHex)
        {
            Color bg;
            try { bg = (Color)ColorConverter.ConvertFromString(baseHex); }
            catch { bg = (Color)ColorConverter.ConvertFromString("#1A1A1A"); }

            double luma = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
            bool isDark = luma < 128;

            SetBrush("Bg",        ToHex(bg));
            SetBrush("BgHov",     isDark ? ToHex(Lighten(bg, 0.06)) : ToHex(Darken(bg, 0.05)));
            SetBrush("Fg",        isDark ? "#E6E6E6" : "#1A1A1A");
            SetBrush("FgMute",    "#888888");
            SetBrush("FgHov",     isDark ? "#B0B0B0" : "#444444");
            SetBrush("Border",    isDark ? ToHex(Lighten(bg, 0.10)) : ToHex(Darken(bg, 0.10)));
            SetBrush("Selection", isDark ? "#3A6FA5" : "#A8C8E8");

            CurrentTheme = "custom";
            CurrentCustomColor = baseHex;
            if (Win != null && ThemeSwatches != null)
            {
                UpdateSwatchSelection();
                UpdateTabsVisual();
                UpdateSubTabVisuals(IsGeneralPaneActive());
            }
        }

        static Color Lighten(Color c, double f)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, c.R + 255 * f),
                (byte)Math.Min(255, c.G + 255 * f),
                (byte)Math.Min(255, c.B + 255 * f));
        }
        static Color Darken(Color c, double f)
        {
            return Color.FromRgb(
                (byte)Math.Max(0, c.R - 255 * f),
                (byte)Math.Max(0, c.G - 255 * f),
                (byte)Math.Max(0, c.B - 255 * f));
        }
        static string ToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        static void UpdateSwatchSelection()
        {
            var fg = (Brush)App.Resources["Fg"];
            foreach (var sw in ThemeSwatches)
            {
                sw.BorderBrush = (string)sw.Tag == CurrentTheme ? fg : Brushes.Transparent;
            }
        }

        static void UpdateCustomRowVisibility()
        {
            CustomColorRow.Visibility = CurrentTheme == "custom" ? Visibility.Visible : Visibility.Collapsed;
        }

        // ----- Settings panel -----

        static bool IsGeneralPaneActive()
        {
            return CurrentSubTab == "general";
        }

        static void ShowSettings()
        {
            IsSettingsOpen = true;
            EditorHost.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            NotesTitleBar.Visibility = Visibility.Collapsed;
            SettingsTitleBar.Visibility = Visibility.Visible;
            BtnAddTab.Visibility = Visibility.Collapsed;
        }

        static void HideSettings()
        {
            IsSettingsOpen = false;
            SettingsView.Visibility = Visibility.Collapsed;
            EditorHost.Visibility = Visibility.Visible;
            SettingsTitleBar.Visibility = Visibility.Collapsed;
            NotesTitleBar.Visibility = Visibility.Visible;
            BtnAddTab.Visibility = Visibility.Visible;
            // Cancel ongoing capture if any
            if (CapturingIndex >= 0) EndCapture(false);
        }

        static void ToggleSettings()
        {
            if (IsSettingsOpen) HideSettings();
            else ShowSettings();
        }

        static void SwitchSubTab(string tab)
        {
            CurrentSubTab = tab;
            PaneGeneral.Visibility     = tab == "general"     ? Visibility.Visible : Visibility.Collapsed;
            PaneShortcuts.Visibility   = tab == "shortcuts"   ? Visibility.Visible : Visibility.Collapsed;
            PaneTrash.Visibility       = tab == "trash"       ? Visibility.Visible : Visibility.Collapsed;
            PaneInformation.Visibility = tab == "information" ? Visibility.Visible : Visibility.Collapsed;
            UpdateSubTabVisuals();
        }

        static void UpdateSubTabVisuals(bool dummy = true)
        {
            if (SubTabGeneral == null) return;
            var accent = (Brush)App.Resources["Accent"];
            var fg     = (Brush)App.Resources["Fg"];
            var fgMute = (Brush)App.Resources["FgMute"];
            SubTabGeneral.BorderBrush     = CurrentSubTab == "general"     ? accent : Brushes.Transparent;
            SubTabShortcuts.BorderBrush   = CurrentSubTab == "shortcuts"   ? accent : Brushes.Transparent;
            SubTabTrash.BorderBrush       = CurrentSubTab == "trash"       ? accent : Brushes.Transparent;
            SubTabInformation.BorderBrush = CurrentSubTab == "information" ? accent : Brushes.Transparent;
            LblTabGeneral.Foreground      = CurrentSubTab == "general"     ? fg : fgMute;
            LblTabShortcuts.Foreground    = CurrentSubTab == "shortcuts"   ? fg : fgMute;
            LblTabTrash.Foreground        = CurrentSubTab == "trash"       ? fg : fgMute;
            LblTabInformation.Foreground  = CurrentSubTab == "information" ? fg : fgMute;
        }

        static void WireSettingsPanel()
        {
            // Sub-tab clicks
            SubTabGeneral.MouseLeftButtonDown     += (s, e) => SwitchSubTab("general");
            SubTabShortcuts.MouseLeftButtonDown   += (s, e) => SwitchSubTab("shortcuts");
            SubTabTrash.MouseLeftButtonDown       += (s, e) => SwitchSubTab("trash");
            SubTabInformation.MouseLeftButtonDown += (s, e) => SwitchSubTab("information");

            // Theme swatches
            foreach (var sw in ThemeSwatches)
                sw.MouseLeftButtonDown += OnSwatchClicked;

            // Custom hex input
            Action<string> updateColorSwatch = (hex) =>
            {
                try { ColorSwatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                catch { }
            };
            HexInputBox.Text = CurrentCustomColor;
            updateColorSwatch(CurrentCustomColor);

            HexInputBox.TextChanged += (s, e) =>
            {
                var txt = HexInputBox.Text.Trim();
                if (Regex.IsMatch(txt, "^#?[0-9A-Fa-f]{6}$"))
                {
                    if (!txt.StartsWith("#")) txt = "#" + txt;
                    updateColorSwatch(txt);
                    CurrentCustomColor = txt;
                    if (CurrentTheme == "custom")
                    {
                        ApplyCustomTheme(txt);
                        ScheduleSave();
                    }
                }
            };
            ColorSwatch.MouseLeftButtonDown += (s, e) =>
            {
                using (var dlg = new System.Windows.Forms.ColorDialog())
                {
                    dlg.FullOpen = true; dlg.AnyColor = true;
                    Color cur;
                    try { cur = (Color)ColorConverter.ConvertFromString(HexInputBox.Text); }
                    catch { cur = Colors.Black; }
                    dlg.Color = System.Drawing.Color.FromArgb(cur.R, cur.G, cur.B);
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var c = dlg.Color;
                        var hex = string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
                        HexInputBox.Text = hex;
                        if (CurrentTheme != "custom")
                        {
                            ApplyCustomTheme(hex);
                            UpdateCustomRowVisibility();
                            ScheduleSave();
                        }
                    }
                }
            };

            // Font size
            FontSizeLabel.Text = ((int)CurrentFontSize).ToString();
            BtnFontMinus.Click += (s, e) => SetFontSize(CurrentFontSize - 1);
            BtnFontPlus.Click  += (s, e) => SetFontSize(CurrentFontSize + 1);

            // Reposition
            BtnReposition.Click += (s, e) => { Reposition(); ScheduleSave(); };

            // Launch with Windows
            ChkLaunchOnStartup.IsChecked = IsAutoStartEnabled();
            ChkLaunchOnStartup.Checked   += (s, e) => { SetAutoStart(true);  ScheduleSave(); };
            ChkLaunchOnStartup.Unchecked += (s, e) => { SetAutoStart(false); ScheduleSave(); };

            // Trash
            BtnEmptyTrash.Click += (s, e) => EmptyTrash();
            BuildTrashUI();

            // Information links
            System.Windows.Navigation.RequestNavigateEventHandler navHandler = (s, e) =>
            {
                try { Process.Start(e.Uri.ToString()); } catch { }
                e.Handled = true;
            };
            LinkSite.RequestNavigate += navHandler;
            LinkSupport.RequestNavigate += navHandler;

            UpdateSwatchSelection();
            UpdateCustomRowVisibility();
        }

        static void OnSwatchClicked(object s, MouseButtonEventArgs e)
        {
            var border = (Border)s;
            var key = (string)border.Tag;
            if (key == CurrentTheme) return;
            if (key == "custom") ApplyCustomTheme(CurrentCustomColor);
            else                 ApplyTheme(key);
            UpdateCustomRowVisibility();
            ScheduleSave();
        }

        static void SetFontSize(double newSize)
        {
            if (newSize < MinFontSize) newSize = MinFontSize;
            if (newSize > MaxFontSize) newSize = MaxFontSize;
            if (Math.Abs(newSize - CurrentFontSize) < 0.01) return;
            CurrentFontSize = newSize;
            App.Resources["EditorFontSize"] = CurrentFontSize;
            FontSizeLabel.Text = ((int)CurrentFontSize).ToString();
            ScheduleSave();
        }

        static void Reposition()
        {
            Win.WindowState = System.Windows.WindowState.Normal;
            Win.Width  = DefaultWidth;
            Win.Height = DefaultHeight;
            var wa = SystemParameters.WorkArea;
            Win.Left = wa.Right  - Win.Width;
            Win.Top  = wa.Bottom - Win.Height;
        }

        static void ScheduleSave() { SaveTimer.Stop(); SaveTimer.Start(); }

        static void SetupSecondInstanceListener()
        {
            try
            {
                bool createdNew;
                _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvtName, out createdNew);
                _showWaitHandle = ThreadPool.RegisterWaitForSingleObject(_showEvent, (state, timedOut) =>
                {
                    Win.Dispatcher.BeginInvoke(new Action(ShowWindow));
                }, null, Timeout.Infinite, false);
            }
            catch { }
        }

        // ----- Hide / Show / Quit -----

        static void HideWindow()
        {
            SaveState();
            Win.Hide();
        }

        static void ShowWindow()
        {
            if (!Win.IsVisible) Win.Show();
            if (Win.WindowState == System.Windows.WindowState.Minimized)
                Win.WindowState = System.Windows.WindowState.Normal;
            Win.Activate();
        }

        static void ToggleVisibility()
        {
            if (Win.IsVisible) HideWindow();
            else ShowWindow();
        }

        static void QuitApp()
        {
            SaveState();
            Win.Show();  // ensure Closing fires properly
            Application.Current.Shutdown();
        }

        // ----- Migration -----

        static void MigrateOldData()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                if (File.Exists(OldDataFile) && !File.Exists(DataFile))
                {
                    File.Copy(OldDataFile, DataFile);
                }
            }
            catch { }
        }

        // ----- Startup / Auto-start -----

        static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key == null) return false;
                    return key.GetValue(StartupRegName) != null;
                }
            }
            catch { return false; }
        }

        static void SetAutoStart(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        var exePath = Path.Combine(ScriptDir, "notes.exe");
                        key.SetValue(StartupRegName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        if (key.GetValue(StartupRegName) != null)
                            key.DeleteValue(StartupRegName, false);
                    }
                }
            }
            catch { }
        }

        // ----- Trash -----

        static void SendToTrash(Note page)
        {
            Trash.Insert(0, new TrashItem
            {
                title = page.title,
                content = page.content,
                deletedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
            while (Trash.Count > MaxTrash) Trash.RemoveAt(Trash.Count - 1);
        }

        static void RestoreFromTrash(int trashIdx)
        {
            if (trashIdx < 0 || trashIdx >= Trash.Count) return;
            if (Pages.Count >= MaxPages)
            {
                MessageBox.Show(T("max_pages_msg"), T("max_pages_title"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var item = Trash[trashIdx];
            var title = item.title ?? T("untitled");
            var existing = new HashSet<string>();
            foreach (var p in Pages) existing.Add(p.title);
            if (existing.Contains(title))
            {
                var baseTitle = title + T("restored_suffix");
                title = baseTitle;
                int suffix = 2;
                while (existing.Contains(title)) { title = baseTitle + " (" + suffix + ")"; suffix++; }
            }
            var page = new Note { title = title, content = item.content ?? "" };
            Pages.Add(page);
            Trash.RemoveAt(trashIdx);
            BuildTabButtons();
            SetActiveTab(Pages.Count - 1);
            BuildTrashUI();
            ScheduleSave();
        }

        static void DeletePermFromTrash(int trashIdx)
        {
            if (trashIdx < 0 || trashIdx >= Trash.Count) return;
            Trash.RemoveAt(trashIdx);
            BuildTrashUI();
            ScheduleSave();
        }

        static void EmptyTrash()
        {
            if (Trash.Count == 0) return;
            Trash.Clear();
            BuildTrashUI();
            ScheduleSave();
        }

        static void BuildTrashUI()
        {
            if (TrashHost == null) return;
            TrashHost.Children.Clear();
            if (Trash.Count == 0)
            {
                TrashHost.Children.Add(new TextBlock
                {
                    Text = T("trash_empty"),
                    Foreground = (Brush)App.Resources["FgMute"],
                    FontSize = 11,
                    FontStyle = FontStyles.Italic
                });
                BtnEmptyTrash.Visibility = Visibility.Collapsed;
                return;
            }
            BtnEmptyTrash.Visibility = Visibility.Visible;
            for (int i = 0; i < Trash.Count; i++)
            {
                var item = Trash[i];
                int idx = i;
                var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var label = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                label.Inlines.Add(new Run { Text = item.title ?? "", Foreground = (Brush)App.Resources["Fg"] });
                label.Inlines.Add(new Run { Text = "  " + (item.deletedAt ?? ""), Foreground = (Brush)App.Resources["FgMute"], FontSize = 10 });
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                var btnRestore = new Button
                {
                    Style = (Style)Win.FindResource("StepperBtn"),
                    Content = "",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 10,
                    ToolTip = T("trash_restore"),
                    Margin = new Thickness(4, 0, 0, 0)
                };
                btnRestore.Click += (s, e) => RestoreFromTrash(idx);
                Grid.SetColumn(btnRestore, 1);
                grid.Children.Add(btnRestore);

                var btnDel = new Button
                {
                    Style = (Style)Win.FindResource("StepperBtn"),
                    Content = "",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 10,
                    ToolTip = T("trash_delete_perm"),
                    Margin = new Thickness(4, 0, 0, 0)
                };
                btnDel.Click += (s, e) => DeletePermFromTrash(idx);
                Grid.SetColumn(btnDel, 2);
                grid.Children.Add(btnDel);

                TrashHost.Children.Add(grid);
            }
        }

        // ----- Shortcuts -----

        static void InitShortcuts()
        {
            Shortcuts = new List<ShortcutDef> {
                new ShortcutDef {
                    action = "toggle_visibility",
                    modifiers = Native.MOD_CONTROL | Native.MOD_ALT,
                    vkey = (uint)KeyInterop.VirtualKeyFromKey(Key.N),
                    enabled = true
                },
                new ShortcutDef { action = "reposition" },
                new ShortcutDef { action = "toggle_settings" },
                new ShortcutDef { action = "close_window" },
                new ShortcutDef { action = "minimize_window" },
                new ShortcutDef { action = "new_page" }
            };
        }

        static void BuildShortcutsUI()
        {
            ShortcutsHost.Children.Clear();
            ShortcutRowsRefs.Clear();
            for (int i = 0; i < Shortcuts.Count; i++)
            {
                var row = BuildShortcutRow(i);
                ShortcutsHost.Children.Add(row);
            }
        }

        static Grid BuildShortcutRow(int index)
        {
            var sc = Shortcuts[index];
            var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var chk = new CheckBox
            {
                IsChecked = sc.enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            chk.Checked   += (s, e) => { sc.enabled = true;  RegisterAllShortcuts(); ScheduleSave(); };
            chk.Unchecked += (s, e) => { sc.enabled = false; RegisterAllShortcuts(); ScheduleSave(); };
            Grid.SetColumn(chk, 0);
            grid.Children.Add(chk);

            var label = new TextBlock
            {
                Text = T("sc_" + sc.action),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)App.Resources["Fg"],
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);

            var btnCombo = new Button
            {
                Style = (Style)Win.FindResource("ActionBtn"),
                Content = ComboDisplay(sc),
                FontSize = 11,
                Padding = new Thickness(8, 4, 8, 4),
                MinWidth = 110,
                Tag = index
            };
            btnCombo.Click += (s, e) => StartCapture(index);
            Grid.SetColumn(btnCombo, 2);
            grid.Children.Add(btnCombo);

            var btnReset = new Button
            {
                Style = (Style)Win.FindResource("StepperBtn"),
                Content = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Margin = new Thickness(6, 0, 0, 0),
                Width = 28, Height = 26
            };
            btnReset.Click += (s, e) => ResetShortcut(index);
            Grid.SetColumn(btnReset, 3);
            grid.Children.Add(btnReset);

            ShortcutRowsRefs.Add(new ShortcutRowRefs { Checkbox = chk, Label = label, BtnCombo = btnCombo });
            return grid;
        }

        static string ComboDisplay(ShortcutDef sc)
        {
            if (sc.vkey == 0) return "—";
            var parts = new List<string>();
            if ((sc.modifiers & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((sc.modifiers & Native.MOD_ALT) != 0)     parts.Add("Alt");
            if ((sc.modifiers & Native.MOD_SHIFT) != 0)   parts.Add("Shift");
            if ((sc.modifiers & Native.MOD_WIN) != 0)     parts.Add("Win");
            var key = KeyInterop.KeyFromVirtualKey((int)sc.vkey);
            parts.Add(key.ToString());
            return string.Join("+", parts.ToArray());
        }

        static void StartCapture(int index)
        {
            if (CapturingIndex >= 0) EndCapture(false);
            CapturingIndex = index;
            ShortcutRowsRefs[index].BtnCombo.Content = T("press_keys");
            Win.PreviewKeyDown += OnCaptureKey;
        }

        static void OnCaptureKey(object sender, KeyEventArgs e)
        {
            if (CapturingIndex < 0) return;
            e.Handled = true;

            var key = e.Key;
            if (key == Key.System) key = e.SystemKey;
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
                return;

            if (key == Key.Escape)
            {
                EndCapture(false);
                return;
            }

            uint mods = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= Native.MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)     mods |= Native.MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)   mods |= Native.MOD_SHIFT;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods |= Native.MOD_WIN;

            uint vkey = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (vkey == 0) { EndCapture(false); return; }

            var sc = Shortcuts[CapturingIndex];
            sc.modifiers = mods;
            sc.vkey = vkey;
            sc.enabled = true;

            EndCapture(true);
        }

        static void EndCapture(bool committed)
        {
            if (CapturingIndex < 0) return;
            int idx = CapturingIndex;
            CapturingIndex = -1;
            Win.PreviewKeyDown -= OnCaptureKey;

            var sc = Shortcuts[idx];
            var refs = ShortcutRowsRefs[idx];
            refs.BtnCombo.Content = ComboDisplay(sc);
            refs.Checkbox.IsChecked = sc.enabled;

            if (committed)
            {
                RegisterAllShortcuts();
                ScheduleSave();
            }
        }

        static void ResetShortcut(int index)
        {
            if (index < 0 || index >= Shortcuts.Count) return;
            var sc = Shortcuts[index];
            sc.modifiers = 0;
            sc.vkey = 0;
            sc.enabled = false;
            var refs = ShortcutRowsRefs[index];
            refs.BtnCombo.Content = "—";
            refs.Checkbox.IsChecked = false;
            RegisterAllShortcuts();
            ScheduleSave();
        }

        static void RegisterAllShortcuts()
        {
            if (Win == null) return;
            var hwnd = new WindowInteropHelper(Win).Handle;
            if (hwnd == IntPtr.Zero) return;
            for (int i = 0; i < Shortcuts.Count; i++)
            {
                Native.UnregisterHotKey(hwnd, i + 1);
                var sc = Shortcuts[i];
                if (sc.enabled && sc.vkey != 0)
                {
                    Native.RegisterHotKey(hwnd, i + 1, sc.modifiers, sc.vkey);
                }
            }
        }

        static void UnregisterAllShortcuts()
        {
            if (Win == null) return;
            var hwnd = new WindowInteropHelper(Win).Handle;
            if (hwnd == IntPtr.Zero) return;
            for (int i = 0; i < Shortcuts.Count; i++)
                Native.UnregisterHotKey(hwnd, i + 1);
        }

        // ----- WndProc hook -----

        static void HookWndProc()
        {
            var helper = new WindowInteropHelper(Win);
            var src = HwndSource.FromHwnd(helper.Handle);
            if (src != null) src.AddHook(WndProc);
        }

        static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                OnHotKey(id);
                handled = true;
            }
            else if (msg == Native.WM_SYSCOMMAND)
            {
                int cmd = wParam.ToInt32() & 0xFFF0;
                if (cmd == Native.SC_MAXIMIZE)
                    handled = true;
            }
            return IntPtr.Zero;
        }

        static void OnHotKey(int id)
        {
            if (id < 1 || id > Shortcuts.Count) return;
            InvokeAction(Shortcuts[id - 1].action);
        }

        static void InvokeAction(string action)
        {
            switch (action)
            {
                case "toggle_visibility": ToggleVisibility(); break;
                case "reposition":      ShowWindow(); Reposition(); ScheduleSave(); break;
                case "toggle_settings": ShowWindow(); ToggleSettings(); break;
                case "close_window":    QuitApp(); break;
                case "minimize_window": Win.WindowState = System.Windows.WindowState.Minimized; break;
                case "new_page":        ShowWindow(); AddNewPage(); break;
            }
        }

        // ----- State -----

        static AppState LoadState()
        {
            var def = new AppState
            {
                window      = new WinCfg { x = null, y = null, w = DefaultWidth, h = DefaultHeight },
                tabs        = new List<Note> { new Note { title = T("notes_default"), content = "" } },
                active      = 0,
                theme       = "dark",
                customColor = "#1A1A1A",
                fontSize    = DefaultFontSize,
                shortcuts   = null,
                trash       = new List<TrashItem>()
            };
            if (!File.Exists(DataFile)) return def;
            try
            {
                string raw = File.ReadAllText(DataFile, Encoding.UTF8);
                if (string.IsNullOrEmpty(raw)) return def;
                var s = new JavaScriptSerializer().Deserialize<AppState>(raw);
                if (s == null) return def;
                if (s.window == null) s.window = def.window;
                if (s.tabs == null || s.tabs.Count == 0) s.tabs = def.tabs;
                if (string.IsNullOrEmpty(s.theme)) s.theme = def.theme;
                if (string.IsNullOrEmpty(s.customColor)) s.customColor = def.customColor;
                if (s.fontSize < MinFontSize || s.fontSize > MaxFontSize) s.fontSize = def.fontSize;
                if (s.trash == null) s.trash = new List<TrashItem>();
                while (s.trash.Count > MaxTrash) s.trash.RemoveAt(s.trash.Count - 1);
                return s;
            }
            catch { return def; }
        }

        static void SaveState()
        {
            if (Win == null) return;

            // Sync editor content into pages
            foreach (var kv in Editors)
                kv.Key.content = kv.Value.Text;

            Rect bounds = new Rect(Win.Left, Win.Top, Win.Width, Win.Height);
            var state = new AppState
            {
                window      = new WinCfg { x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height },
                tabs        = Pages,
                active      = ActiveTabIndex,
                theme       = CurrentTheme,
                customColor = CurrentCustomColor,
                fontSize    = CurrentFontSize,
                shortcuts   = Shortcuts,
                trash       = Trash,
                launchOnStartup = IsAutoStartEnabled()
            };
            try
            {
                var json = new JavaScriptSerializer().Serialize(state);
                File.WriteAllText(DataFileTmp, json, Encoding.UTF8);
                if (File.Exists(DataFile))
                    File.Replace(DataFileTmp, DataFile, null);
                else
                    File.Move(DataFileTmp, DataFile);
            }
            catch
            {
                try { if (File.Exists(DataFileTmp)) File.Delete(DataFileTmp); } catch { }
            }
        }

        static void ApplyWindowState(WinCfg w)
        {
            if (w == null) return;
            if (w.w > 0) Win.Width  = w.w;
            if (w.h > 0) Win.Height = w.h;
            if (w.x.HasValue && w.y.HasValue)
            {
                Win.Left = w.x.Value;
                Win.Top  = w.y.Value;
            }
            else
            {
                var wa = SystemParameters.WorkArea;
                Win.Left = wa.Right  - Win.Width;
                Win.Top  = wa.Bottom - Win.Height;
            }
        }
    }
}
