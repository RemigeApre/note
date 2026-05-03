using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NoteInstaller
{
    public static class Program
    {
        static readonly string[] Files = { "notes.exe", "notes.xaml", "notes.ico", "README.txt", "uninstall_note.exe" };
        const string AppVersion   = "1.0.0";
        const string AppPublisher = "Le Geai Informatique";
        const string AppUrl       = "https://legeai-informatique.fr";
        const string AccentHex = "#7C3AED";

        [STAThread]
        public static void Main()
        {
            var app = new Application();
            var win = BuildUi();
            app.Run(win);
        }

        static Window BuildUi()
        {
            var bg     = ColorBrush("#1A1A1A");
            var bgHov  = ColorBrush("#262626");
            var fg     = ColorBrush("#DDDDDD");
            var fgMute = ColorBrush("#888888");
            var border = ColorBrush("#2A2A2A");
            var accent = ColorBrush(AccentHex);

            var win = new Window
            {
                Title = "Note — Installation",
                Width = 480,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = bg,
                Foreground = fg,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13
            };

            var rootBorder = new Border { BorderBrush = border, BorderThickness = new Thickness(1) };
            var grid = new Grid { Margin = new Thickness(28) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "Installation de Note",
                Foreground = fg,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var subtitle = new TextBlock
            {
                Text = "Une note locale, légère et toujours à portée de main.",
                Foreground = fgMute,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 18)
            };
            var head = new StackPanel();
            head.Children.Add(title);
            head.Children.Add(subtitle);
            Grid.SetRow(head, 0);
            grid.Children.Add(head);

            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = "DOSSIER D'INSTALLATION",
                Foreground = fgMute,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Note");
            var pathBox = new TextBox
            {
                Text = defaultPath,
                Background = bgHov,
                Foreground = fg,
                CaretBrush = fg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 18)
            };
            body.Children.Add(pathBox);

            var chkStartMenu = MakeCheckBox("Créer un raccourci dans le menu Démarrer", fg, true);
            var chkAutoStart = MakeCheckBox("Lancer Note avec Windows", fg, false);
            var chkLaunch    = MakeCheckBox("Lancer Note après l'installation", fg, true);
            body.Children.Add(chkStartMenu);
            body.Children.Add(chkAutoStart);
            body.Children.Add(chkLaunch);
            Grid.SetRow(body, 1);
            grid.Children.Add(body);

            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var status = new TextBlock
            {
                Foreground = fgMute,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(status, 0);
            btnRow.Children.Add(status);

            var btnInstall = MakePrimaryButton("Installer", accent);
            Grid.SetColumn(btnInstall, 1);
            btnRow.Children.Add(btnInstall);
            Grid.SetRow(btnRow, 2);
            grid.Children.Add(btnRow);

            rootBorder.Child = grid;
            win.Content = rootBorder;

            btnInstall.Click += (s, e) =>
            {
                btnInstall.IsEnabled = false;
                status.Foreground = fgMute;
                status.Text = "Installation en cours…";

                try
                {
                    var path = pathBox.Text.Trim();
                    if (string.IsNullOrEmpty(path)) throw new Exception("Chemin invalide.");
                    Directory.CreateDirectory(path);
                    foreach (var f in Files)
                        ExtractResource(f, Path.Combine(path, f));

                    if (chkStartMenu.IsChecked == true)
                        CreateShortcut(path);
                    if (chkAutoStart.IsChecked == true)
                        SetAutoStart(Path.Combine(path, "notes.exe"));
                    RegisterUninstall(path);

                    status.Foreground = ColorBrush("#7CFFA8");
                    status.Text = "Installation terminée.";

                    if (chkLaunch.IsChecked == true)
                        try { System.Diagnostics.Process.Start(Path.Combine(path, "notes.exe")); } catch { }

                    win.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Threading.Thread.Sleep(700);
                        win.Close();
                    }));
                }
                catch (Exception ex)
                {
                    status.Foreground = ColorBrush("#FF7C7C");
                    status.Text = "Erreur : " + ex.Message;
                    btnInstall.IsEnabled = true;
                }
            };

            return win;
        }

        static SolidColorBrush ColorBrush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        static CheckBox MakeCheckBox(string text, Brush fg, bool isChecked)
        {
            return new CheckBox
            {
                Content = text,
                Foreground = fg,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand,
                IsChecked = isChecked
            };
        }

        static Button MakePrimaryButton(string text, Brush bg)
        {
            var btn = new Button
            {
                Content = text,
                Background = bg,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(22, 8, 22, 8),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            return btn;
        }

        static void ExtractResource(string name, string targetPath)
        {
            var asm = Assembly.GetExecutingAssembly();
            string resName = null;
            foreach (var n in asm.GetManifestResourceNames())
            {
                if (n.Equals(name, StringComparison.OrdinalIgnoreCase)
                    || n.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
                {
                    resName = n;
                    break;
                }
            }
            if (resName == null) throw new FileNotFoundException("Ressource introuvable : " + name);
            using (var stream = asm.GetManifestResourceStream(resName))
            using (var fs = File.Create(targetPath))
            {
                stream.CopyTo(fs);
            }
        }

        static void CreateShortcut(string installPath)
        {
            var startMenuFolder = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var lnkPath = Path.Combine(startMenuFolder, "Note.lnk");
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            var scType = sc.GetType();
            scType.InvokeMember("TargetPath",       BindingFlags.SetProperty, null, sc, new object[] { Path.Combine(installPath, "notes.exe") });
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { installPath });
            scType.InvokeMember("IconLocation",     BindingFlags.SetProperty, null, sc, new object[] { Path.Combine(installPath, "notes.ico") + ",0" });
            scType.InvokeMember("Description",      BindingFlags.SetProperty, null, sc, new object[] { "Note" });
            scType.InvokeMember("Save",             BindingFlags.InvokeMethod, null, sc, null);
            Marshal.ReleaseComObject(sc);
            Marshal.ReleaseComObject(shell);
        }

        static void SetAutoStart(string exePath)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null)
                    key.SetValue("Note", "\"" + exePath + "\"");
            }
        }

        static void RegisterUninstall(string installPath)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Note"))
                {
                    if (k == null) return;
                    k.SetValue("DisplayName", "Note");
                    k.SetValue("DisplayVersion", AppVersion);
                    k.SetValue("Publisher", AppPublisher);
                    k.SetValue("URLInfoAbout", AppUrl);
                    k.SetValue("InstallLocation", installPath);
                    k.SetValue("DisplayIcon", Path.Combine(installPath, "notes.ico"));
                    k.SetValue("UninstallString", "\"" + Path.Combine(installPath, "uninstall_note.exe") + "\"");
                    k.SetValue("NoModify", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch { }
        }
    }
}
