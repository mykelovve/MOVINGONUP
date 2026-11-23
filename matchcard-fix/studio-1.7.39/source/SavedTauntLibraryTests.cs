using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GorillaStudio.NameFolders;

namespace GorillaStudio.NameFolders
{
    internal static class DirectoryLinks
    {
        internal static string Resolve(string path) { return Path.GetFullPath(path); }
        internal static void EnsureJunction(string link, string target) { throw new NotSupportedException(); }
    }
}

internal static class Program
{
    private static int passed;
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "gorilla-saved-" + Guid.NewGuid().ToString("N"));
        try
        {
            string app = Path.Combine(root, "Studio"), game = Path.Combine(root, "WWE 2K26");
            string data = Path.Combine(game, "discovery", "discovery_data"), moves = Path.Combine(game, "discovery", "_moves");
            Directory.CreateDirectory(Path.Combine(app, "data", "library"));
            File.WriteAllText(Path.Combine(app, "data", "library", "taunts.json"), "{\"taunts\":[]}");
            Directory.CreateDirectory(Path.Combine(data, "MoveData"));
            File.WriteAllText(Path.Combine(data, "MoveData", "CAMMoveData.jsfb"), "table");
            string bank = Path.Combine(data, "Animation", "Acts", "Basic");
            Directory.CreateDirectory(bank);
            File.WriteAllText(Path.Combine(bank, "MotionArea_51000_51300.clips"), "clips");
            Directory.CreateDirectory(Path.Combine(moves, "AnimSystem", "wwe"));
            File.WriteAllText(Path.Combine(moves, "AnimSystem", "wwe", "wwe.adefs"), "adefs");
            File.WriteAllText(Path.Combine(game, "pack.cak"), "cak");
            DiscoveryTauntSavedLibrary.StorageRoot = Path.Combine(root, "saved");

            // A finished library folder, as a refresh leaves it.
            string overlay = Path.Combine(root, "taunt-data", "abc-1"), library = Path.Combine(overlay, "data", "library");
            string decoded = Path.Combine(overlay, "data", "gamepack", "decoded");
            Directory.CreateDirectory(Path.Combine(library, "previews_game")); Directory.CreateDirectory(decoded);
            string key = "discovery51258a000_0123456789ab";
            File.WriteAllText(Path.Combine(library, "taunts.json"),
                "{\"taunts\":[{\"key\":\"" + key + "\",\"name\":\"Abadon 1 (AEW FF)\",\"clip\":\"51258a000\",\"seconds\":3.2}]}");
            File.WriteAllText(Path.Combine(decoded, "51258a000.npz"), "npz");
            var import = new DiscoveryTauntImport { Root = overlay, Library = library, Definitions = 2, Imported = 1, Available = 1, Missing = 1,
                ArchivesSearched = true, ArchiveCount = 18, ScanIncomplete = true, SearchRoots = new[] { data, moves } };
            import.Warnings.Add("18 archives could not be opened");
            import.Unavailable.Add(new DiscoveryUnavailableTaunt { Id = 51300, Variant = 0, Clip = "51300a000", Name = "Someone 1",
                Names = new List<string> { "Someone 1", "Alias" }, Kind = DiscoveryTauntUnavailableKind.MissingAnimation, Reason = "not located" });

            Func<string> print = () => DiscoveryTauntSavedLibrary.Fingerprint(app, game, data, CancellationToken.None);
            string first = print();
            Check(first != null && first == print(), "The source fingerprint is stable while nothing changes");
            Check(DiscoveryTauntSavedLibrary.TryLoad(app, game, data, first) == null, "Nothing is reused before a library was saved");
            import.ArchiveLookupPending = true;
            Check(!DiscoveryTauntSavedLibrary.Save(app, game, data, first, import), "A library whose archive lookup is still pending is not saved");
            import.ArchiveLookupPending = false;
            Check(DiscoveryTauntSavedLibrary.Save(app, game, data, first, import), "A finished library is saved (also when some archives were unreadable)");

            // Next start: same sources.
            var saved = DiscoveryTauntSavedLibrary.TryLoad(app, game, data, print());
            Check(saved != null && saved.Root == overlay && saved.Library == library, "On the next start the same library folder is reused");
            Check(saved.Taunts.Count == 1 && (string)saved.Taunts[0]["key"] == key, "Its playable taunts come back with the same keys");
            Check(saved.Unavailable.Count == 1 && saved.Unavailable[0].Clip == "51300a000" && saved.Unavailable[0].Names.Count == 2
                && saved.Unavailable[0].Kind == DiscoveryTauntUnavailableKind.MissingAnimation, "Unavailable taunts and their reasons come back");
            Check(saved.Missing == 1 && saved.ArchiveCount == 18 && saved.ScanIncomplete && saved.Warnings.Count == 1 && saved.SearchRoots.Count == 2,
                "Counts, warnings and search roots come back for Animation details");

            // What a render writes does not count as a Discovery change.
            string plugins = Path.Combine(game, "Plugins", "GorillaRenders");
            Directory.CreateDirectory(plugins);
            File.WriteAllText(Path.Combine(plugins, "1234.gmr"), "render");
            File.WriteAllText(Path.Combine(plugins, "_status.txt"), "status");
            File.WriteAllText(Path.Combine(game, "CakeHook.log"), "log");
            Directory.CreateDirectory(Path.Combine(game, "discovery", "discovery_data", "Pictures"));
            File.WriteAllText(Path.Combine(data, "Pictures", "render.dds"), "pic");
            Check(print() == first && DiscoveryTauntSavedLibrary.TryLoad(app, game, data, print()) != null,
                "After a render (gmr, status, logs, pictures) the saved library is still reused");

            // Real animation changes rebuild.
            Func<Action, string, bool> changes = (edit, label) => { edit(); string now = print(); bool ok = now != null && now != first
                && DiscoveryTauntSavedLibrary.TryLoad(app, game, data, now) == null; Check(ok, label); return ok; };
            string extra = Path.Combine(bank, "Clip #51259 [51259a000].clip");
            changes(() => File.WriteAllText(extra, "new clip"), "A new loose .clip triggers a rebuild");
            File.Delete(extra); Check(print() == first, "Removing it again restores the saved fingerprint");
            string clips = Path.Combine(bank, "MotionArea_51000_51300.clips");
            changes(() => { File.WriteAllText(clips, "clips edited"); }, "An edited .clips triggers a rebuild");
            File.WriteAllText(clips, "clips"); File.SetLastWriteTimeUtc(clips, File.GetLastWriteTimeUtc(clips));
            first = print(); DiscoveryTauntSavedLibrary.Save(app, game, data, first, import);
            changes(() => File.SetLastWriteTimeUtc(Path.Combine(moves, "AnimSystem", "wwe", "wwe.adefs"), DateTime.UtcNow.AddMinutes(3)), "A Move Installer reinstall (wwe.adefs) triggers a rebuild");
            first = print(); DiscoveryTauntSavedLibrary.Save(app, game, data, first, import);
            changes(() => File.WriteAllText(Path.Combine(moves, "new.clips"), "installed"), "A move installed into _moves triggers a rebuild");
            first = print(); DiscoveryTauntSavedLibrary.Save(app, game, data, first, import);
            changes(() => File.SetLastWriteTimeUtc(Path.Combine(data, "MoveData", "CAMMoveData.jsfb"), DateTime.UtcNow.AddMinutes(5)), "A changed moves table triggers a rebuild");
            first = print(); DiscoveryTauntSavedLibrary.Save(app, game, data, first, import);
            changes(() => File.WriteAllText(Path.Combine(game, "new.cak"), "cak"), "A new game .cak archive triggers a rebuild");
            first = print(); DiscoveryTauntSavedLibrary.Save(app, game, data, first, import);
            changes(() => File.WriteAllText(Path.Combine(app, "data", "library", "taunts.json"), "{\"taunts\":[{}]}"), "An updated bundled library triggers a rebuild");
            first = print(); DiscoveryTauntSavedLibrary.Save(app, game, data, first, import);
            Check(DiscoveryTauntSavedLibrary.TryLoad(app, game, data, first) != null, "The saved record follows the latest finished library");
            Check(DiscoveryTauntSavedLibrary.TryLoad(app, game, Path.Combine(root, "other"), first) == null, "A different Discovery folder never reuses it");

            // A damaged library folder is never shown.
            File.Delete(Path.Combine(decoded, "51258a000.npz"));
            Check(DiscoveryTauntSavedLibrary.TryLoad(app, game, data, first) == null, "A library missing a motion file is rebuilt instead of reused");
            File.WriteAllText(Path.Combine(decoded, "51258a000.npz"), "npz");
            File.AppendAllText(Path.Combine(library, "taunts.json"), " ");
            Check(DiscoveryTauntSavedLibrary.TryLoad(app, game, data, first) == null, "A changed library manifest is rebuilt instead of reused");

            // Previews made in the old folder carry into a rebuilt one.
            string next = Path.Combine(root, "taunt-data", "abc-2");
            Directory.CreateDirectory(Path.Combine(next, "data", "library", "previews_game"));
            File.WriteAllText(Path.Combine(library, "previews_game", key + ".png"), "atlas");
            File.WriteAllText(Path.Combine(library, "previews_game", "cena2.png"), "bundled");
            File.WriteAllText(Path.Combine(library, "previews_game", "discovery51111a000_ffffffffffff.png"), "old motion");
            int copied = DiscoveryTauntSavedLibrary.CarryPreviews(overlay, next, new[] { key, "cena2", "discovery_unavailable_51300a000" });
            Check(copied == 1 && File.ReadAllText(Path.Combine(next, "data", "library", "previews_game", key + ".png")) == "atlas",
                "A Discovery preview carries over to the rebuilt library for the same motion");
            Check(!File.Exists(Path.Combine(next, "data", "library", "previews_game", "discovery51111a000_ffffffffffff.png")),
                "Previews of motions that are no longer in the library are left behind");
            Check(DiscoveryTauntSavedLibrary.CarryPreviews(overlay, overlay, new[] { key }) == 0, "Nothing is copied onto itself");
            Console.WriteLine("PASS " + passed + " saved taunt library checks");
            return 0;
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
    private static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL " + label); passed++; Console.WriteLine("PASS " + label); }
}
