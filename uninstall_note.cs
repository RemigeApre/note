using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace NoteUninstaller
{
    public static class Program
    {
        const string AccentHex = "#7C3AED";
        const string DangerHex = "#C42B1C";

        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.Run(BuildUi());
        }

        static Window BuildUi()
        {
            var bg     = ColorBrush("#1A1A1A");
            var bgHov  = ColorBrush("#262626");
            var fg     = ColorBrush("#DDDDDD");
            var fgMute = ColorBrush("#888888");
            var border = ColorBrush("#2A2A2A");
            var danger = ColorBrush(DangerHex);

            var win = new Window
            {
                Title = "Note — Désinstallation",
                Width = 480,
                Height = 280,
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

            var head = new StackPanel();
            head.Children.Add(new TextBlock
            {
                Text = "Désinstaller Note",
                Foreground = fg,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            head.Children.Add(new TextBlock
            {
                Text = "Cette opération supprime l'application et ses raccourcis.",
                Foreground = fgMute,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 18)
            });
            Grid.SetRow(head, 0);
            grid.Children.Add(head);

            var installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Note");

            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = "À SUPPRIMER",
                Foreground = fgMute,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            body.Children.Add(MakeListItem("Application : " + installDir, fg));
            body.Children.Add(MakeListItem("Raccourci menu Démarrer", fg));
            body.Children.Add(MakeListItem("Entrée \"Lancer avec Windows\" (si présente)", fg));
            body.Children.Add(MakeListItem("Entrée Apps & fonctionnalités", fg));

            var chkRemoveData = new CheckBox
            {
                Content = "Supprimer aussi mes notes (" + dataDir + ")",
                Foreground = fg,
                FontSize = 12,
                Margin = new Thickness(0, 14, 0, 0),
                IsChecked = false,
                Cursor = Cursors.Hand
            };
            body.Children.Add(chkRemoveData);
            Grid.SetRow(body, 1);
            grid.Children.Add(body);

            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

            var btnCancel = new Button
            {
                Content = "Annuler",
                Background = Brushes.Transparent,
                Foreground = fg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 8, 18, 8),
                FontSize = 12,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(btnCancel, 1);
            btnRow.Children.Add(btnCancel);

            var btnUninstall = new Button
            {
                Content = "Désinstaller",
                Background = danger,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(22, 8, 22, 8),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            Grid.SetColumn(btnUninstall, 2);
            btnRow.Children.Add(btnUninstall);
            Grid.SetRow(btnRow, 2);
            grid.Children.Add(btnRow);

            rootBorder.Child = grid;
            win.Content = rootBorder;

            btnCancel.Click += (s, e) => win.Close();
            btnUninstall.Click += (s, e) =>
            {
                btnUninstall.IsEnabled = false;
                btnCancel.IsEnabled = false;
                status.Text = "Désinstallation en cours…";

                try
                {
                    DoUninstall(installDir, chkRemoveData.IsChecked == true);
                    status.Foreground = ColorBrush("#7CFFA8");
                    status.Text = "Désinstallation terminée.";
                    win.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Threading.Thread.Sleep(500);
                        win.Close();
                    }));
                }
                catch (Exception ex)
                {
                    status.Foreground = ColorBrush("#FF7C7C");
                    status.Text = "Erreur : " + ex.Message;
                    btnUninstall.IsEnabled = true;
                    btnCancel.IsEnabled = true;
                }
            };

            return win;
        }

        static SolidColorBrush ColorBrush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        static TextBlock MakeListItem(string text, Brush fg)
        {
            return new TextBlock
            {
                Text = "•  " + text,
                Foreground = fg,
                FontSize = 12,
                Margin = new Thickness(2, 2, 0, 2),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        static void DoUninstall(string installDir, bool removeData)
        {
            // 1. Stop notes.exe if running
            try
            {
                foreach (var p in Process.GetProcessesByName("notes"))
                {
                    try { p.CloseMainWindow(); p.WaitForExit(1500); } catch { }
                    if (!p.HasExited) { try { p.Kill(); } catch { } }
                }
            }
            catch { }

            // 2. Remove HKCU Run entry
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k != null && k.GetValue("Note") != null) k.DeleteValue("Note", false);
                }
            }
            catch { }

            // 3. Remove Start Menu shortcut
            try
            {
                var lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Note.lnk");
                if (File.Exists(lnk)) File.Delete(lnk);
            }
            catch { }

            // 4. Remove Programs and Features entry
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    if (k != null) try { k.DeleteSubKeyTree("Note", false); } catch { }
                }
            }
            catch { }

            // 5. Remove user data if requested
            if (removeData)
            {
                try
                {
                    var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Note");
                    if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
                }
                catch { }
            }

            // 6. Self-delete: spawn cmd to delete install dir after a short delay
            try
            {
                var script = "/C timeout /t 1 /nobreak > nul & rd /s /q \"" + installDir + "\"";
                var psi = new ProcessStartInfo("cmd.exe", script)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
            }
            catch { }
        }
    }
}
