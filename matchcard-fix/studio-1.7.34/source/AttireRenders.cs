using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GorillaStudio.NameFolders
{
    /// <summary>One alternate attire that has its own installed moving render.</summary>
    public sealed class AttireRenderAlias
    {
        public int Alias { get; internal set; }
        public int WrestlerId { get; internal set; }
        public ulong Picture { get; internal set; }
        public int Attire { get; internal set; }
        public string PictureText { get { return Picture.ToString("X16", CultureInfo.InvariantCulture); } }
    }

    /// <summary>What an attire can do, for one attire of a wrestler.</summary>
    public sealed class AttireRenderOption
    {
        public int Attire { get; internal set; }
        public ulong Picture { get; internal set; }
        public bool Available { get; internal set; }
        public string Note { get; internal set; }
        public AttireRenderAlias Existing { get; internal set; }
    }

    public sealed class AttireRenderResult
    {
        public bool Success { get; internal set; }
        public int Alias { get; internal set; }
        public string Message { get; internal set; }
        public List<string> Notes { get; } = new List<string>();
    }

    /// <summary>
    /// Gives an alternate attire its own moving render using only data the Moving Renders
    /// plugin already reads. The plugin resolves the roster picture shown on screen to a
    /// slot number through every installed render's render_hashes (first match wins, in
    /// folder order), then plays that slot's render. An attire whose own roster picture
    /// differs from the main attire's can therefore get a copy of its render installed
    /// under a free alias slot whose only picture key is that attire's picture. The alias
    /// file name starts with 0 so it is read before the wrestler's own render, and the
    /// picture is also removed from the wrestler's own installed render so either order
    /// works. Aliases are remembered in _attire_renders.txt in Plugins\GorillaRenders.
    /// The plugin itself is never changed.
    /// </summary>
    public static class AttireRenders
    {
        public const string RegistryName = "_attire_renders.txt";
        public const int LowestAlias = 60000, HighestAlias = 65535;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static readonly Regex SlotFile = new Regex(@"^(\d+)_.*\.gmr$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string RendersFolder(string gameRoot)
        { return Path.Combine(Path.GetFullPath(gameRoot), "Plugins", "GorillaRenders"); }

        /// <summary>Explains, per attire, whether it can get its own moving render.</summary>
        public static List<AttireRenderOption> Options(int wrestlerId, IReadOnlyDictionary<int, List<AttirePicture>> roster,
            IEnumerable<AttireRenderAlias> registry)
        {
            var result = new List<AttireRenderOption>();
            List<AttirePicture> attires;
            if (roster == null || !roster.TryGetValue(wrestlerId, out attires)) return result;
            var known = (registry ?? Enumerable.Empty<AttireRenderAlias>()).Where(item => item.WrestlerId == wrestlerId).ToList();
            ulong main = attires.Select(item => item.Hash).FirstOrDefault(hash => hash != 0);
            int distinct = attires.Where(item => item.Hash != 0).Select(item => item.Hash).Distinct().Count();
            foreach (AttirePicture attire in attires)
            {
                var option = new AttireRenderOption { Attire = attire.Number, Picture = attire.Hash,
                    Existing = known.FirstOrDefault(item => item.Picture == attire.Hash && attire.Hash != 0) };
                int[] others = roster.Where(pair => pair.Key != wrestlerId && pair.Value.Any(item => item.Hash == attire.Hash))
                    .Select(pair => pair.Key).OrderBy(id => id).ToArray();
                int first = attires.First(item => item.Hash == attire.Hash).Number;
                if (attire.Hash == 0) option.Note = "No roster picture in the game data, so the game cannot tell this attire apart.";
                else if (others.Length != 0)
                    option.Note = "Its roster picture is also used by wrestler " + string.Join(", ", others.Select(id => id.ToString(CultureInfo.InvariantCulture)))
                        + "; an attire render would replace theirs too.";
                else if (distinct < 2)
                    option.Note = "Every attire of this wrestler shows the same roster picture, so the game cannot tell them apart. Give this attire its own roster picture first.";
                else if (attire.Hash == main)
                    option.Note = "This is the wrestler's main roster picture. Install the main render normally.";
                else
                {
                    option.Available = true;
                    option.Note = first != attire.Number ? "Same picture as attire " + first.ToString(CultureInfo.InvariantCulture) + "; both use one attire render."
                        : option.Existing != null ? "Has its own render (slot " + option.Existing.Alias.ToString(CultureInfo.InvariantCulture) + ")." : "Can have its own moving render.";
                }
                result.Add(option);
            }
            return result;
        }

        public static List<AttireRenderAlias> ReadRegistry(string folder)
        {
            var result = new List<AttireRenderAlias>();
            string path = Path.Combine(folder, RegistryName);
            if (!File.Exists(path)) return result;
            foreach (string raw in File.ReadAllLines(path, Utf8))
            {
                string line = raw; int hash = line.IndexOf('#'); if (hash >= 0) line = line.Substring(0, hash);
                string[] words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                int alias, wrestler, attire = 0; ulong picture;
                if (words.Length < 3 || !int.TryParse(words[0], NumberStyles.None, CultureInfo.InvariantCulture, out alias)
                    || !int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out wrestler)
                    || !ulong.TryParse(words[2], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out picture)
                    || words[2].Length != 16 || alias < 1 || alias > 65535 || wrestler < 1 || wrestler > 65535 || picture == 0) continue;
                if (words.Length > 3) int.TryParse(words[3], NumberStyles.None, CultureInfo.InvariantCulture, out attire);
                if (result.Any(item => item.Alias == alias)) continue;
                result.Add(new AttireRenderAlias { Alias = alias, WrestlerId = wrestler, Picture = picture, Attire = attire });
            }
            return result;
        }

        private static void WriteRegistry(string folder, IEnumerable<AttireRenderAlias> items)
        {
            var text = new StringBuilder();
            text.Append("# Gorilla Studio attire renders: alias-slot wrestler-id roster-picture attire-number\n");
            text.Append("# Each alias slot is a copy of an attire's render that the Moving Renders plugin plays for that roster picture.\n");
            foreach (AttireRenderAlias item in items.OrderBy(item => item.WrestlerId).ThenBy(item => item.Attire))
                text.Append(item.Alias.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(item.WrestlerId.ToString(CultureInfo.InvariantCulture))
                    .Append(' ').Append(item.PictureText).Append(' ').Append(item.Attire.ToString(CultureInfo.InvariantCulture)).Append('\n');
            string path = Path.Combine(folder, RegistryName), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, text.ToString(), Utf8);
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }

        /// <summary>The slot number of an installed render file name, or 0.</summary>
        public static int SlotOf(string path)
        {
            Match match = SlotFile.Match(Path.GetFileName(path) ?? "");
            int id; return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id >= 1 && id <= 65535 ? id : 0;
        }

        private static bool IsAliasFile(string path) { return (Path.GetFileName(path) ?? "").StartsWith("0", StringComparison.Ordinal) && SlotOf(path) != 0; }

        /// <summary>Reuses the slot already given to this attire picture, else the highest free alias slot.</summary>
        public static int ChooseAlias(IEnumerable<AttireRenderAlias> registry, ISet<int> taken, int wrestlerId, ulong picture)
        {
            AttireRenderAlias existing = (registry ?? Enumerable.Empty<AttireRenderAlias>()).FirstOrDefault(item => item.WrestlerId == wrestlerId && item.Picture == picture);
            if (existing != null) return existing.Alias;
            var used = new HashSet<int>(taken ?? new HashSet<int>());
            foreach (AttireRenderAlias item in registry ?? Enumerable.Empty<AttireRenderAlias>()) used.Add(item.Alias);
            for (int alias = HighestAlias; alias >= LowestAlias; alias--) if (!used.Contains(alias)) return alias;
            return 0;
        }

        /// <summary>Installs <paramref name="source"/> (a completed render of <paramref name="wrestlerId"/>) as that attire's own render.</summary>
        public static AttireRenderResult Install(string gameRoot, string source, int wrestlerId, int attireNumber,
            IReadOnlyDictionary<int, List<AttirePicture>> roster, Func<bool> gameClosed)
        {
            var result = new AttireRenderResult();
            try
            {
                if (gameClosed == null || !gameClosed()) return Fail(result, "Close WWE 2K26 before installing an attire render.");
                if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(Path.Combine(gameRoot, "Plugins")))
                    return Fail(result, "Choose your installed WWE 2K26 game folder (with its Plugins folder) first.");
                string folder = RendersFolder(gameRoot);
                Directory.CreateDirectory(folder);
                List<AttireRenderAlias> registry = ReadRegistry(folder);
                AttireRenderOption option = Options(wrestlerId, roster, registry).FirstOrDefault(item => item.Attire == attireNumber);
                if (option == null) return Fail(result, "Attire " + attireNumber.ToString(CultureInfo.InvariantCulture) + " was not found for wrestler " + wrestlerId.ToString(CultureInfo.InvariantCulture) + " in the current Discovery data.");
                if (!option.Available) return Fail(result, option.Note);
                List<string> sourceHashes;
                if (!GorillaRenderMetadata.TryReadRosterHashes(source, wrestlerId, out sourceHashes))
                    return Fail(result, "The chosen render is not a completed render of wrestler " + wrestlerId.ToString(CultureInfo.InvariantCulture) + ".");

                var taken = new HashSet<int>(roster.Keys);
                foreach (string file in Directory.GetFiles(folder, "*.gmr", SearchOption.TopDirectoryOnly)) { int slot = SlotOf(file); if (slot != 0) taken.Add(slot); }
                // An alias already held by this same attire picture is reused, so its own file does not block it.
                AttireRenderAlias previous = registry.FirstOrDefault(item => item.WrestlerId == wrestlerId && item.Picture == option.Picture);
                if (previous != null) taken.Remove(previous.Alias);
                int alias = ChooseAlias(registry, taken, wrestlerId, option.Picture);
                if (alias == 0) return Fail(result, "No free alias slot is left between " + LowestAlias.ToString(CultureInfo.InvariantCulture) + " and " + HighestAlias.ToString(CultureInfo.InvariantCulture) + ".");
                if (roster.ContainsKey(alias)) return Fail(result, "Alias slot " + alias.ToString(CultureInfo.InvariantCulture) + " is now a real roster ID. Remove that attire render and install it again.");

                string stem = Regex.Replace(Path.GetFileNameWithoutExtension(source), @"^\d+_", "");
                stem = Regex.Replace(stem, @"[^A-Za-z0-9_\-]", "_");
                if (stem.Length > 60) stem = stem.Substring(0, 60);
                string name = "0" + alias.ToString(CultureInfo.InvariantCulture) + "_attire" + attireNumber.ToString(CultureInfo.InvariantCulture) + "_" + stem + ".gmr";
                string target = Path.Combine(folder, name), staged = Path.Combine(folder, ".attire-" + Guid.NewGuid().ToString("N") + ".gmr.tmp");
                if (!GorillaRenderMetadata.WriteAttireAlias(source, wrestlerId, staged, alias, option.Picture.ToString("X16", CultureInfo.InvariantCulture)))
                    return Fail(result, "The chosen render could not be copied as an attire render. Make sure it is a finished render (Completed renders shows it as available).");
                try
                {
                    foreach (string old in Directory.GetFiles(folder, "*.gmr", SearchOption.TopDirectoryOnly).Where(path => IsAliasFile(path) && SlotOf(path) == alias))
                        File.Delete(old);
                    File.Move(staged, target); staged = null;
                }
                finally { if (staged != null) try { File.Delete(staged); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

                registry.RemoveAll(item => item.Alias == alias || (item.WrestlerId == wrestlerId && item.Picture == option.Picture));
                registry.Add(new AttireRenderAlias { Alias = alias, WrestlerId = wrestlerId, Picture = option.Picture, Attire = attireNumber });
                WriteRegistry(folder, registry);
                int stripped = StripFromOwnRenders(folder, wrestlerId, new[] { option.Picture }, result.Notes);
                result.Success = true; result.Alias = alias;
                result.Message = "Attire " + attireNumber.ToString(CultureInfo.InvariantCulture) + " of wrestler " + wrestlerId.ToString(CultureInfo.InvariantCulture)
                    + " now has its own moving render (slot " + alias.ToString(CultureInfo.InvariantCulture) + ", file " + name + ")."
                    + (stripped == 0 ? " The wrestler's own render is not installed yet; install it normally for the other attires." : "")
                    + " Restart WWE 2K26 to see it.";
                return result;
            }
            catch (Exception error) when (Expected(error)) { return Fail(result, error.Message); }
        }

        /// <summary>Removes an attire render and gives its picture back to the wrestler's own render.</summary>
        public static AttireRenderResult Remove(string gameRoot, int alias, Func<bool> gameClosed)
        {
            var result = new AttireRenderResult();
            try
            {
                if (gameClosed == null || !gameClosed()) return Fail(result, "Close WWE 2K26 before removing an attire render.");
                string folder = RendersFolder(gameRoot);
                List<AttireRenderAlias> registry = ReadRegistry(folder);
                AttireRenderAlias item = registry.FirstOrDefault(entry => entry.Alias == alias);
                if (item == null) return Fail(result, "Slot " + alias.ToString(CultureInfo.InvariantCulture) + " is not an attire render made by Studio.");
                foreach (string old in Directory.GetFiles(folder, "*.gmr", SearchOption.TopDirectoryOnly).Where(path => IsAliasFile(path) && SlotOf(path) == alias))
                    File.Delete(old);
                registry.Remove(item);
                WriteRegistry(folder, registry);
                foreach (string own in OwnRenders(folder, item.WrestlerId))
                {
                    List<string> hashes;
                    if (!GorillaRenderMetadata.TryReadRosterHashes(own, item.WrestlerId, out hashes) || hashes.Contains(item.PictureText)) continue;
                    hashes.Add(item.PictureText);
                    if (!GorillaRenderMetadata.SetRosterHashes(own, item.WrestlerId, hashes))
                        result.Notes.Add("Could not give the picture back to " + Path.GetFileName(own) + "; install that render again.");
                }
                result.Success = true; result.Alias = alias;
                result.Message = "Removed the attire render for attire " + item.Attire.ToString(CultureInfo.InvariantCulture) + " of wrestler "
                    + item.WrestlerId.ToString(CultureInfo.InvariantCulture) + ". That attire shows the wrestler's own render again after a restart.";
                return result;
            }
            catch (Exception error) when (Expected(error)) { return Fail(result, error.Message); }
        }

        /// <summary>
        /// After a wrestler's own render is installed again (which restores every attire
        /// picture), takes the attire pictures back out so the attire renders keep working.
        /// Registry entries whose attire file is gone are dropped.
        /// </summary>
        public static AttireRenderResult Reapply(string gameRoot)
        {
            var result = new AttireRenderResult();
            try
            {
                string folder = RendersFolder(gameRoot);
                if (!Directory.Exists(folder)) { result.Success = true; result.Message = "No installed renders."; return result; }
                List<AttireRenderAlias> registry = ReadRegistry(folder);
                if (registry.Count == 0) { result.Success = true; result.Message = "No attire renders."; return result; }
                var present = new HashSet<int>(Directory.GetFiles(folder, "*.gmr", SearchOption.TopDirectoryOnly).Where(IsAliasFile).Select(SlotOf));
                List<AttireRenderAlias> kept = registry.Where(item => present.Contains(item.Alias)).ToList();
                if (kept.Count != registry.Count) WriteRegistry(folder, kept);
                foreach (var group in kept.GroupBy(item => item.WrestlerId))
                    StripFromOwnRenders(folder, group.Key, group.Select(item => item.Picture), result.Notes);
                result.Success = result.Notes.Count == 0;
                result.Message = kept.Count.ToString(CultureInfo.InvariantCulture) + " attire render(s) active"
                    + (registry.Count != kept.Count ? "; " + (registry.Count - kept.Count).ToString(CultureInfo.InvariantCulture) + " missing file(s) forgotten" : "") + ".";
                return result;
            }
            catch (Exception error) when (Expected(error)) { return Fail(result, error.Message); }
        }

        private static IEnumerable<string> OwnRenders(string folder, int wrestlerId)
        {
            return Directory.GetFiles(folder, "*.gmr", SearchOption.TopDirectoryOnly).Where(path => !IsAliasFile(path) && SlotOf(path) == wrestlerId).ToArray();
        }

        private static int StripFromOwnRenders(string folder, int wrestlerId, IEnumerable<ulong> pictures, List<string> notes)
        {
            var remove = new HashSet<string>(pictures.Select(hash => hash.ToString("X16", CultureInfo.InvariantCulture)), StringComparer.OrdinalIgnoreCase);
            int count = 0;
            foreach (string own in OwnRenders(folder, wrestlerId))
            {
                count++;
                List<string> hashes;
                if (!GorillaRenderMetadata.TryReadRosterHashes(own, wrestlerId, out hashes)) { notes.Add("Could not read " + Path.GetFileName(own) + "."); continue; }
                List<string> kept = hashes.Where(hash => !remove.Contains(hash)).ToList();
                if (kept.Count == hashes.Count) continue;
                if (kept.Count == 0) { notes.Add(Path.GetFileName(own) + " has no other roster picture left, so it was left unchanged."); continue; }
                if (!GorillaRenderMetadata.SetRosterHashes(own, wrestlerId, kept))
                    notes.Add("Could not update " + Path.GetFileName(own) + "; the attire render still comes first because its file is read first.");
            }
            return count;
        }

        private static AttireRenderResult Fail(AttireRenderResult result, string message) { result.Success = false; result.Message = message; return result; }

        private static bool Expected(Exception error)
        {
            return error is IOException || error is UnauthorizedAccessException || error is ArgumentException
                || error is NotSupportedException || error is InvalidDataException || error is System.Security.SecurityException;
        }
    }
}
