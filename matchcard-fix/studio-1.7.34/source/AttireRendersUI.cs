using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GorillaStudio.NameFolders
{
    public static partial class FolderBridge
    {
        /// <summary>Finds CharProfileTable.jsfb in the remembered Discovery data folder.</summary>
        private static string AttireRosterTable(Window owner)
        {
            string root = RememberedRoot(owner);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;
            foreach (string candidate in new[] { Path.Combine(root, "Roster", "CharProfileTable.jsfb"), Path.Combine(root, "CharProfileTable.jsfb") })
                if (File.Exists(candidate)) return candidate;
            try
            {
                foreach (string child in Directory.GetDirectories(root))
                {
                    string candidate = Path.Combine(child, "Roster", "CharProfileTable.jsfb");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            return null;
        }

        private static bool WweClosed() { return Process.GetProcessesByName("WWE2K26_x64").Length == 0; }

        /// <summary>Lets the user install one completed render as one alternate attire's own moving render.</summary>
        private static void OpenAttireRenderInstall(Window parent, Window owner, CompletedRenderEntry entry, Action<string> report)
        {
            if (entry == null || !entry.IsValid) { report("Select one available completed render first."); return; }
            string game = owner.GetType().GetField("Game", InstanceMembers)?.GetValue(owner) as string;
            if (string.IsNullOrWhiteSpace(game) || !Directory.Exists(game)) { report("Choose your installed WWE 2K26 game folder in Studio first."); return; }
            string table = AttireRosterTable(owner);
            if (table == null) { report("Set Discovery data… first: attire pictures are read from Roster\\CharProfileTable.jsfb."); return; }
            Dictionary<int, List<AttirePicture>> roster;
            try { roster = DiscoveryRenderLinks.ReadAttires(table); }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException)
            { report("Could not read attire pictures: " + error.Message); return; }
            if (!roster.ContainsKey(entry.WrestlerId)) { report("Wrestler " + entry.WrestlerId.ToString(CultureInfo.InvariantCulture) + " is not in the current Discovery roster."); return; }

            var dialog = new Window { Owner = parent, Title = "Attire render — " + (entry.Name ?? "Wrestler") + " (" + entry.WrestlerId.ToString(CultureInfo.InvariantCulture) + ")",
                Width = 760, Height = 560, MinWidth = 600, MinHeight = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 27)), Foreground = Brushes.White, FontFamily = owner.FontFamily, FontSize = 13 };
            var grid = new Grid { Margin = new Thickness(16) };
            for (int index = 0; index < 4; index++) grid.RowDefinitions.Add(new RowDefinition { Height = index == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            dialog.Content = grid;
            var intro = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
                Text = "Give one alternate attire its own moving render. Pick the attire this render shows.\n"
                    + "Render: " + Path.GetFileName(entry.PublishedPath ?? entry.Path) + (string.IsNullOrWhiteSpace(entry.AttireName) ? "" : "   (made with attire: " + entry.AttireName + ")") + "\n"
                    + "Works when the attire has its own roster picture in the game. The render is installed as an extra slot; the Moving Renders plugin is not changed." };
            grid.Children.Add(intro);
            var list = new ListBox { Background = new SolidColorBrush(Color.FromRgb(29, 29, 35)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(65, 65, 76)) };
            Grid.SetRow(list, 1); grid.Children.Add(list);
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10), MinHeight = 36 };
            var statusScroll = new ScrollViewer { Content = status, MaxHeight = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(statusScroll, 2); grid.Children.Add(statusScroll);
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            Button install = CompletedRenderButton("Install for this attire"), remove = CompletedRenderButton("Remove attire render"),
                reapply = CompletedRenderButton("Re-apply all attire renders"), close = CompletedRenderButton("Close");
            reapply.ToolTip = "Run this after installing a wrestler's own render again, so their attire renders keep priority.";
            foreach (Button button in new[] { install, remove, reapply, close }) actions.Children.Add(button);
            Grid.SetRow(actions, 3); grid.Children.Add(actions);
            bool busy = false;
            List<AttireRenderOption> options = null;

            Action fill = () =>
            {
                int keep = list.SelectedIndex;
                List<AttireRenderAlias> registry;
                try { registry = AttireRenders.ReadRegistry(AttireRenders.RendersFolder(game)); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { registry = new List<AttireRenderAlias>(); status.Text = error.Message; }
                options = AttireRenders.Options(entry.WrestlerId, roster, registry);
                list.Items.Clear();
                foreach (AttireRenderOption option in options)
                {
                    var row = new StackPanel { Margin = new Thickness(4, 5, 4, 5), Opacity = option.Available ? 1.0 : 0.55 };
                    row.Children.Add(new TextBlock { FontWeight = FontWeights.SemiBold, Text = "Attire " + option.Attire.ToString(CultureInfo.InvariantCulture)
                        + (option.Picture == 0 ? "" : "   ·   picture " + option.Picture.ToString("X16", CultureInfo.InvariantCulture))
                        + (option.Existing != null ? "   ·   HAS ITS OWN RENDER" : "") });
                    row.Children.Add(new TextBlock { Text = option.Note, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 184)) });
                    list.Items.Add(new ListBoxItem { Content = row, Tag = option });
                }
                int first = options.FindIndex(option => option.Available);
                list.SelectedIndex = keep >= 0 && keep < list.Items.Count ? keep : first;
                if (first < 0 && string.IsNullOrEmpty(status.Text))
                    status.Text = "None of this wrestler's attires has a roster picture of its own, so the game shows the same picture for all of them and one render covers them all.";
            };
            Action controls = () =>
            {
                var option = (list.SelectedItem as ListBoxItem)?.Tag as AttireRenderOption;
                install.IsEnabled = !busy && option != null && option.Available;
                remove.IsEnabled = !busy && option != null && option.Existing != null;
                reapply.IsEnabled = close.IsEnabled = !busy;
                list.IsEnabled = !busy;
            };
            list.SelectionChanged += (sender, args) => controls();
            Func<Func<AttireRenderResult>, Task> run = async work =>
            {
                if (busy) return;
                if (!WweClosed()) { status.Text = "Close WWE 2K26 first."; return; }
                busy = true; controls(); status.Text = "Working…";
                try
                {
                    AttireRenderResult result = await Task.Run(work);
                    status.Text = result.Message + (result.Notes.Count == 0 ? "" : "\n" + string.Join("\n", result.Notes));
                    if (result.Success) report(result.Message);
                }
                catch (Exception error) { status.Text = BatchError(error).Message; }
                finally { busy = false; fill(); controls(); }
            };
            install.Click += async (sender, args) =>
            {
                var option = (list.SelectedItem as ListBoxItem)?.Tag as AttireRenderOption;
                if (option == null) return;
                string source = entry.Path;
                await run(() => AttireRenders.Install(game, source, entry.WrestlerId, option.Attire, roster, WweClosed));
            };
            remove.Click += async (sender, args) =>
            {
                var option = (list.SelectedItem as ListBoxItem)?.Tag as AttireRenderOption;
                if (option?.Existing == null) return;
                int alias = option.Existing.Alias;
                await run(() => AttireRenders.Remove(game, alias, WweClosed));
            };
            reapply.Click += async (sender, args) => await run(() => AttireRenders.Reapply(game));
            close.Click += (sender, args) => dialog.Close();
            fill(); controls();
            dialog.ShowDialog();
        }
    }
}
