using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace GorillaStudio.NameFolders
{
    /// <summary>
    /// Remembers the last completed Discovery taunt library and the exact
    /// source files it was built from (paths, sizes and write times of the
    /// moves table, every .clip/.clips under the Discovery data and _moves
    /// folders, the Move Installer's wwe.adefs, the game's .cak archives and
    /// this helper build). While none of those change, Studio reuses that
    /// library and its engine folder instead of scanning and rebuilding, so
    /// Discovery taunts are ready at startup like the bundled base-game
    /// taunts, and previews/caches made for that folder stay valid.
    /// </summary>
    public static class DiscoveryTauntSavedLibrary
    {
        private const string Format = "gorilla-saved-taunt-library-v1";
        private const int MaximumFiles = 400000;

        /// <summary>Folder for saved records; tests may redirect it.</summary>
        public static string StorageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GorillaStudio", "NameFolders", "taunt-saved-library");

        /// <summary>Returns null when any source cannot be inspected (the caller then rebuilds normally).</summary>
        public static string Fingerprint(string applicationRoot, string gameRoot, string discoveryRoot, CancellationToken token)
        {
            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
                {
                    writer.Write(Format);
                    var helper = typeof(DiscoveryTauntSavedLibrary).Assembly;
                    writer.Write(helper.GetName().Version.ToString());
                    if (!string.IsNullOrEmpty(helper.Location)) Stamp(writer, helper.Location, "helper");
                    string app = Path.GetFullPath(applicationRoot);
                    writer.Write(Norm(app));
                    string manifest = Path.Combine(app, "data", "library", "taunts.json");
                    writer.Write(Hash(File.ReadAllBytes(manifest)));
                    Stamp(writer, Path.Combine(app, "data", "faces", "index.json"), "faces");
                    writer.Write(Norm(gameRoot ?? ""));
                    writer.Write(Norm(discoveryRoot));
                    token.ThrowIfCancellationRequested();
                    string table = DiscoveryTaunts.FindTable(discoveryRoot);
                    writer.Write(table == null ? "" : Norm(table));
                    if (table != null)
                    {
                        Stamp(writer, table, "table");
                        string data = DiscoveryTauntLibrary.SourceRootFor(discoveryRoot, table);
                        writer.Write(Norm(data));
                        int count = 0;
                        Walk(writer, data, IsMotion, token, ref count);
                        string moves = DiscoveryMotionAssets.MovesRoot(Path.GetFullPath(data));
                        writer.Write(moves == null ? "" : Norm(moves));
                        if (moves != null) Walk(writer, moves, IsMotion, token, ref count);
                        string adefs = AdefsMotions.AdefsPath(Path.GetFullPath(data));
                        writer.Write(adefs == null ? "" : Norm(adefs));
                        if (adefs != null) Stamp(writer, adefs, "adefs");
                        if (!string.IsNullOrWhiteSpace(gameRoot) && Directory.Exists(gameRoot))
                            Walk(writer, Path.GetFullPath(gameRoot), path => string.Equals(Path.GetExtension(path), ".cak", StringComparison.OrdinalIgnoreCase), token, ref count);
                    }
                    writer.Flush();
                    stream.Position = 0;
                    using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(stream));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException
                || error is NotSupportedException || error is System.Security.SecurityException || error is InvalidDataException)
            { return null; }
        }

        /// <summary>
        /// Saves a finished library. A result whose archive lookup is still
        /// pending is never saved. A lookup that finished with unreadable
        /// archives is saved: the same files give the same result, and any
        /// change to them (or Refresh data) rebuilds.
        /// </summary>
        public static bool Save(string applicationRoot, string gameRoot, string discoveryRoot, string fingerprint, DiscoveryTauntImport import)
        {
            if (fingerprint == null || import == null || import.ArchiveLookupPending
                || string.IsNullOrWhiteSpace(import.Root) || string.IsNullOrWhiteSpace(import.Library)) return false;
            string temporary = null;
            try
            {
                string taunts = Path.Combine(import.Library, "taunts.json");
                if (!File.Exists(taunts)) return false;
                var record = new Dictionary<string, object> {
                    { "format", Format }, { "fingerprint", fingerprint },
                    { "root", import.Root }, { "library", import.Library }, { "taunts_sha256", Hash(File.ReadAllBytes(taunts)) },
                    { "definitions", import.Definitions }, { "imported", import.Imported }, { "available", import.Available },
                    { "missing", import.Missing }, { "unsupported", import.Unsupported }, { "read_failures", import.ReadFailures },
                    { "cache_failures", import.CacheFailures }, { "archive_imported", import.ArchiveImported },
                    { "verified_imported", import.VerifiedImported }, { "verified_pack", import.VerifiedPackHash ?? "" },
                    { "archive_count", import.ArchiveCount }, { "archives_searched", import.ArchivesSearched },
                    { "archive_status", import.ArchiveStatus ?? "" }, { "animation_root", import.AnimationRootFound },
                    { "linked", import.HasLinkedSources }, { "incomplete", import.ScanIncomplete },
                    { "search_roots", (import.SearchRoots ?? new string[0]).ToArray() },
                    { "warnings", import.Warnings.ToArray() },
                    { "unavailable", import.Unavailable.Select(item => new Dictionary<string, object> {
                        { "id", item.Id }, { "variant", item.Variant }, { "clip", item.Clip ?? "" }, { "name", item.Name ?? "" },
                        { "names", (item.Names ?? new List<string>()).ToArray() }, { "kind", item.Kind.ToString() }, { "reason", item.Reason ?? "" }
                    }).ToArray() }
                };
                string path = RecordPath(applicationRoot, gameRoot, discoveryRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, DiscoveryTauntLibrary.Json(record), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException
                || error is NotSupportedException || error is InvalidOperationException)
            { return false; }
            finally
            {
                if (temporary != null) try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        /// <summary>The saved library when its sources are unchanged and its engine folder is intact; otherwise null.</summary>
        public static DiscoveryTauntImport TryLoad(string applicationRoot, string gameRoot, string discoveryRoot, string fingerprint)
        {
            if (fingerprint == null) return null;
            try
            {
                string path = RecordPath(applicationRoot, gameRoot, discoveryRoot);
                if (!File.Exists(path) || new FileInfo(path).Length > 64L * 1024 * 1024) return null;
                Dictionary<string, object> record = DiscoveryTauntLibrary.JsonObject(File.ReadAllText(path));
                if (record == null || Text(record, "format") != Format || Text(record, "fingerprint") != fingerprint) return null;
                string root = Text(record, "root"), library = Text(record, "library");
                if (root.Length == 0 || library.Length == 0 || !Directory.Exists(root)) return null;
                string taunts = Path.Combine(library, "taunts.json");
                if (!File.Exists(taunts)) return null;
                byte[] bytes = File.ReadAllBytes(taunts);
                if (Hash(bytes) != Text(record, "taunts_sha256")) return null;
                Dictionary<string, object> manifest = DiscoveryTauntLibrary.JsonObject(Encoding.UTF8.GetString(bytes));
                object raw;
                if (manifest == null || !manifest.TryGetValue("taunts", out raw) || !(raw is IEnumerable)) return null;
                var entries = new List<Dictionary<string, object>>();
                string decoded = Path.Combine(root, "data", "gamepack", "decoded");
                foreach (object value in (IEnumerable)raw)
                {
                    var entry = value as Dictionary<string, object>;
                    if (entry == null) return null;
                    string clip = Text(entry, "clip");
                    // Every playable motion must still be in the engine folder.
                    if (clip.Length > 0 && !File.Exists(Path.Combine(decoded, clip + ".npz"))) return null;
                    entries.Add(entry);
                }
                var result = new DiscoveryTauntImport {
                    Root = root, Library = library, Taunts = entries,
                    Definitions = Int(record, "definitions"), Imported = Int(record, "imported"), Available = entries.Count,
                    Missing = Int(record, "missing"), Unsupported = Int(record, "unsupported"), ReadFailures = Int(record, "read_failures"),
                    CacheFailures = Int(record, "cache_failures"), ArchiveImported = Int(record, "archive_imported"),
                    VerifiedImported = Int(record, "verified_imported"), VerifiedPackHash = Text(record, "verified_pack"),
                    ArchiveCount = Int(record, "archive_count"), ArchivesSearched = Bool(record, "archives_searched"),
                    ArchiveStatus = Text(record, "archive_status"), AnimationRootFound = Bool(record, "animation_root"),
                    HasLinkedSources = Bool(record, "linked"), ScanIncomplete = Bool(record, "incomplete"), SearchRoots = Strings(record, "search_roots").ToArray()
                };
                result.Warnings.AddRange(Strings(record, "warnings"));
                object unavailable;
                if (record.TryGetValue("unavailable", out unavailable) && unavailable is IEnumerable)
                    foreach (object value in (IEnumerable)unavailable)
                    {
                        var item = value as Dictionary<string, object>;
                        if (item == null) return null;
                        DiscoveryTauntUnavailableKind kind;
                        if (!Enum.TryParse(Text(item, "kind"), out kind)) kind = DiscoveryTauntUnavailableKind.MissingAnimation;
                        result.Unavailable.Add(new DiscoveryUnavailableTaunt {
                            Id = Int(item, "id"), Variant = Int(item, "variant"), Clip = Text(item, "clip"), Name = Text(item, "name"),
                            Names = Strings(item, "names"), Kind = kind, Reason = Text(item, "reason")
                        });
                    }
                return result;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException
                || error is NotSupportedException || error is InvalidOperationException || error is InvalidCastException
                || error is FormatException || error is OverflowException
#if NET8_0_OR_GREATER
                || error is System.Text.Json.JsonException
#endif
                )
            { return null; }
        }

        /// <summary>
        /// Copies Discovery taunt browser previews from the library being
        /// replaced into the new one. Their keys carry the motion hash, so a
        /// preview only carries over for the identical animation.
        /// </summary>
        public static int CarryPreviews(string previousRoot, string newRoot, IEnumerable<string> keys)
        {
            if (string.IsNullOrWhiteSpace(previousRoot) || string.IsNullOrWhiteSpace(newRoot) || keys == null) return 0;
            int copied = 0;
            try
            {
                if (string.Equals(Norm(previousRoot), Norm(newRoot), StringComparison.OrdinalIgnoreCase)) return 0;
                string from = Path.Combine(previousRoot, "data", "library", "previews_game");
                string to = Path.Combine(newRoot, "data", "library", "previews_game");
                if (!Directory.Exists(from) || !Directory.Exists(to)) return 0;
                foreach (string key in keys.Distinct(StringComparer.Ordinal))
                {
                    if (string.IsNullOrEmpty(key) || !(key.StartsWith("discovery", StringComparison.Ordinal) || key.Contains("_discovery_"))
                        || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || DiscoveryTauntSearch.IsUnavailableKey(key)) continue;
                    string source = Path.Combine(from, key + ".png"), target = Path.Combine(to, key + ".png");
                    if (!File.Exists(source) || File.Exists(target)) continue;
                    try { File.Copy(source, target, false); copied++; }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException) { }
            return copied;
        }

        private static string RecordPath(string applicationRoot, string gameRoot, string discoveryRoot)
        {
            string key = Norm(applicationRoot) + "\n" + Norm(gameRoot ?? "") + "\n" + Norm(discoveryRoot);
            return Path.Combine(StorageRoot, Hash(Encoding.UTF8.GetBytes(key.ToLowerInvariant())).Substring(0, 32) + ".json");
        }

        private static void Walk(BinaryWriter writer, string root, Func<string, bool> include, CancellationToken token, ref int count)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            pending.Push(Path.GetFullPath(root));
            writer.Write("walk:" + Norm(root));
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string directory = pending.Pop();
                string physical = directory;
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) physical = DirectoryLinks.Resolve(directory);
                if (!visited.Add(Norm(physical))) continue;
                foreach (string file in Directory.GetFiles(directory).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    if (!include(file)) continue;
                    if (++count > MaximumFiles) throw new InvalidDataException("Too many source files to remember.");
                    Stamp(writer, file, null);
                }
                foreach (string child in Directory.GetDirectories(directory).OrderByDescending(value => value, StringComparer.OrdinalIgnoreCase))
                    pending.Push(child);
            }
        }

        private static void Stamp(BinaryWriter writer, string path, string label)
        {
            var file = new FileInfo(path);
            writer.Write(label ?? Norm(path));
            writer.Write(file.Exists);
            if (!file.Exists) return;
            writer.Write(file.Length);
            writer.Write(file.LastWriteTimeUtc.Ticks);
        }

        private static bool IsMotion(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".clip", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".clips", StringComparison.OrdinalIgnoreCase);
        }

        private static string Norm(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string Text(Dictionary<string, object> item, string key)
        {
            object value;
            return item.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
        }

        private static int Int(Dictionary<string, object> item, string key)
        {
            object value;
            return item.TryGetValue(key, out value) && value != null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static bool Bool(Dictionary<string, object> item, string key)
        {
            object value;
            return item.TryGetValue(key, out value) && value is bool && (bool)value;
        }

        private static List<string> Strings(Dictionary<string, object> item, string key)
        {
            object value;
            if (!item.TryGetValue(key, out value) || !(value is IEnumerable) || value is string) return new List<string>();
            return ((IEnumerable)value).Cast<object>().Select(element => Convert.ToString(element, CultureInfo.InvariantCulture)).ToList();
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
        }

        private static string Hex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return text.ToString();
        }
    }
}
