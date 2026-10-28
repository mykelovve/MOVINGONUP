using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GorillaStudio.NameFolders;

// Checks the plugin-limit match-type selection against a real Discovery UI folder.
// The app root is synthesized: its template is a real card's playing "bg" movie and
// its placeholders are the folder's own 101 ph_*.bk2 files.
internal static class Program
{
    private static int passed;
    private static string temporary;
    private static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: MatchCardLayoutSetTests folder-containing-UI template-card.jsfb [keep-output-directory]"); return 2; }
        temporary = Path.Combine(Path.GetTempPath(), "gorilla-matchcard-sets-" + Guid.NewGuid().ToString("N"));
        try
        {
            string source = Path.GetFullPath(args[0]);
            string app = Path.Combine(temporary, "app"), data = Path.Combine(temporary, "discovery_data");
            Copy(Path.GetFullPath(args[1]), Path.Combine(app, "data", "gamepack", "screens", "template_matchup.jsfb"));
            string movies = Path.Combine("UI", "Assets", "movies", "gorilla_renders");
            foreach (string file in Directory.GetFiles(Path.Combine(source, movies), "ph_*.bk2"))
                Copy(file, Path.Combine(app, "data", "gamepack", "screens", "BakeMe", movies, Path.GetFileName(file)));
            foreach (string file in Directory.GetFiles(Path.Combine(source, "UI"), "*", SearchOption.AllDirectories))
                Copy(file, Path.Combine(data, file.Substring(source.Length + 1)));
            var before = Snapshot(data);

            Check(MatchCardLayouts.ReadSelection(app).SequenceEqual(new[] { "1on1", "2on2", "triple_threat", "fatal_4way" }), "Without a selection file the confirmed working set is used");
            var check = MatchCardLayouts.Apply(app, data, true);
            Console.WriteLine(check.Summary);
            Check(check.Failed.Count == 0, "The real folder is read without failures: " + string.Join(" | ", check.Failed.Take(3)));
            Check(Snapshot(data).SequenceEqual(before), "A check changes no file");
            var dynamite = check.Projects.Single(project => project.Path.EndsWith("AEW_Dynamite_2026.jsfb"));

            Check(dynamite.MissingSlots == 4 && dynamite.ExtraSlots == 5 && !dynamite.LayoutReady, "Dynamite needs fatal four-way added and five-person removed");
            Check(check.StrippedSlots > 0 && check.Projects.Any(project => project.ExtraSlots == 88), "Complete 101-slot cards report 88 slots over the limit");

            var setup = MatchCardLayouts.Apply(app, data, false);
            Console.WriteLine(setup.Summary);
            Check(setup.Failed.Count == 0 && setup.BakeRequired && Directory.Exists(setup.BackupDirectory), "Setup succeeds, keeps backups and requests a bake");
            Check(setup.Unsupported.Count == 0, "No card is left over the limit or with custom layers: " + string.Join(" | ", setup.Unsupported.Take(3)));
            foreach (var pair in before.Where(pair => pair.Key.EndsWith(".jsfb")))
            {
                string backup = Path.Combine(setup.BackupDirectory, pair.Key + ".bak");
                if (File.Exists(backup)) Check(Hash(File.ReadAllBytes(backup)) == pair.Value, "Backup is the exact original: " + pair.Key);
            }
            var repeat = MatchCardLayouts.Apply(app, data, false);
            Check(repeat.Patched == 0 && !repeat.BakeRequired && repeat.Projects.All(project => project.LayoutReady), "A second setup changes nothing and every card is ready");
            var after = Snapshot(data);
            Check(repeat.Projects.Count == check.Projects.Count, "The same cards are recognized after setup");

            File.WriteAllText(Path.Combine(app, MatchCardLayouts.SelectionFile), "# test\n1on1\n2on2\n8man\n");
            var eight = MatchCardLayouts.Apply(app, data, false);
            Check(eight.Failed.Count == 0 && eight.Unsupported.Count == 0 && eight.AddedSlots > 0 && eight.StrippedSlots > 0, "A custom 14-clip selection swaps match types");
            Check(eight.SelectedLayouts.SequenceEqual(new[] { "1on1", "2on2", "8man" }), "The report names the chosen match types");
            if (args.Length > 2) { CopyTree(data, Path.Combine(Path.GetFullPath(args[2]), "eight")); }

            string upToSix = "1on1\n1on2\n1on3\n1on4\n1on5\n2on2\n2on3\n3on3\n5man\n6man\nfatal_4way\ntriple_threat\ntriple_threat_tag\n";
            File.WriteAllText(Path.Combine(app, MatchCardLayouts.SelectionFile), upToSix);
            var original = MatchCardLayouts.Apply(app, data, false);
            Check(original.Failed.Count == 1 && original.Patched == 0 && original.Failed[0].Contains("plugin_limit 120"), "59 clips are refused for the original 16-movie plugin and the message names the opt-in");
            File.WriteAllText(Path.Combine(app, MatchCardLayouts.SelectionFile), "plugin_limit 120\n" + upToSix);
            var all = MatchCardLayouts.Apply(app, data, false);
            Check(all.Failed.Count == 0 && all.Unsupported.Count == 0 && all.MovieLimit == 120 && all.SelectedLayouts.Count == 13, "With the capacity companion, every 2-to-6-person match type is built: " + string.Join(" | ", all.Unsupported.Concat(all.Failed).Take(2)));
            Check(all.Summary.Contains("WWE2K26_MatchCardCapacity"), "The summary says the companion is required");
            var allAgain = MatchCardLayouts.Apply(app, data, false);
            Check(allAgain.Patched == 0 && allAgain.Projects.All(project => project.LayoutReady), "The 59-clip setup is idempotent and every card is ready");
            if (args.Length > 2) { CopyTree(data, Path.Combine(Path.GetFullPath(args[2]), "uptosix")); }
            File.Delete(Path.Combine(app, MatchCardLayouts.SelectionFile));
            var back = MatchCardLayouts.Apply(app, data, false);
            Check(back.Failed.Count == 0 && back.Unsupported.Count == 0, "Returning to the default set succeeds");
            if (args.Length > 2) { CopyTree(data, Path.Combine(Path.GetFullPath(args[2]), "default")); }

            var stable = Snapshot(data);
            foreach (string bad in new[] { "1on1\n4on4\n8man\n", "1on1\nfatal4way\n", "1on1\n1on1\n", "# nothing\n", "plugin_limit 50\n1on1\n", "plugin_limit 120\nplugin_limit 120\n1on1\n" })
            {
                File.WriteAllText(Path.Combine(app, MatchCardLayouts.SelectionFile), bad);
                var refused = MatchCardLayouts.Apply(app, data, false);
                Check(refused.Failed.Count == 1 && refused.Patched == 0, "An invalid selection is refused: " + refused.Failed.FirstOrDefault());
                var current = Snapshot(data);
                Check(current.Keys.Where(key => !key.StartsWith("Gorilla Studio Layout Backups")).All(key => stable.ContainsKey(key) && stable[key] == current[key]), "A refused selection changes no layout");
            }
            Console.WriteLine("PASS " + passed + " checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error.Message); return 1; }
        finally { try { Directory.Delete(temporary, true); } catch (IOException) { } }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
    private static void Copy(string from, string to) { Directory.CreateDirectory(Path.GetDirectoryName(to)); File.Copy(from, to, true); }
    private static void CopyTree(string from, string to)
    {
        foreach (string file in Directory.GetFiles(from, "*.jsfb", SearchOption.AllDirectories))
            if (!file.Contains("Gorilla Studio Layout Backups")) Copy(file, Path.Combine(to, file.Substring(from.Length + 1)));
    }
    private static string Hash(byte[] bytes) { using (var sha = System.Security.Cryptography.SHA256.Create()) return Convert.ToHexString(sha.ComputeHash(bytes)); }
    private static SortedDictionary<string, string> Snapshot(string root)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) result[file.Substring(root.Length + 1)] = Hash(File.ReadAllBytes(file));
        return result;
    }
}
