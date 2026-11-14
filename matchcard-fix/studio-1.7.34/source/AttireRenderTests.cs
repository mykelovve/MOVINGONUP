using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GorillaStudio.NameFolders;

internal static class Program
{
    private static string root;
    private static int passed;
    private const ulong MainPic = 0x1111111111111111UL, Alt = 0x2222222222222222UL, Alt2 = 0x3333333333333333UL, Shared = 0x4444444444444444UL;

    private static int Main(string[] args)
    {
        root = Path.Combine(Path.GetTempPath(), "gorilla-attire-renders-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // Wrestler 312: attire 1 main picture, 2 own picture, 3 same as 2, 4 no picture, 5 shared with 595.
            // Wrestler 100: every attire shows the same picture.
            string table = Write("CharProfileTable.jsfb", Build(new Row(312, MainPic, Alt, Alt, 0, Shared), new Row(595, Shared), new Row(100, Alt2, Alt2)));
            var roster = DiscoveryRenderLinks.ReadAttires(table);
            Check(roster[312].Count == 5 && roster[312][3].Hash == 0 && roster[312][4].Number == 5, "Each attire keeps its number and own picture, including attires with no picture");
            Check(DiscoveryRenderLinks.Read(table)[312].Count == 3, "The existing flat picture list is unchanged");
            var options = AttireRenders.Options(312, roster, null);
            Check(!options[0].Available && options[0].Note.Contains("main roster picture"), "The main attire is installed normally, not as an attire render");
            Check(options[1].Available && options[2].Available && options[2].Note.Contains("Same picture as attire 2"), "Attires with their own picture can get a render; duplicates are explained");
            Check(!options[3].Available && options[3].Note.Contains("No roster picture"), "An attire with no picture is refused");
            Check(!options[4].Available && options[4].Note.Contains("595"), "A picture shared with another wrestler is refused and names them");
            Check(AttireRenders.Options(100, roster, null).All(o => !o.Available), "Attires that all show one picture are refused");

            string game = Path.Combine(root, "game"); string folder = Path.Combine(game, "Plugins", "GorillaRenders"); Directory.CreateDirectory(folder);
            string own = Gmr(Path.Combine(folder, "312_CM_Punk.gmr"), 312, MainPic, Alt, Shared);
            string source = Gmr(Path.Combine(root, "312_CM_Punk_attire2.gmr"), 312, MainPic, Alt, Shared);
            byte[] sourceBefore = File.ReadAllBytes(source);
            var closed = (Func<bool>)(() => true);
            Check(!AttireRenders.Install(game, source, 312, 2, roster, () => false).Success, "Nothing is installed while the game is running");
            var result = AttireRenders.Install(game, source, 312, 2, roster, closed);
            Check(result.Success && result.Alias == 65535, "Attire 2 gets the highest free alias slot: " + result.Message);
            string alias = Directory.GetFiles(folder, "0*.gmr").Single();
            Check(Path.GetFileName(alias).StartsWith("065535_attire2_CM_Punk_attire2"), "The alias file sorts before every wrestler render");
            var meta = Meta(alias);
            Check(meta.GetProperty("wrestler_id").GetInt32() == 65535 && meta.GetProperty("attire_of").GetInt32() == 312, "The alias render is its own slot and remembers the wrestler");
            Check(meta.GetProperty("render_hashes").EnumerateArray().Select(e => e.GetString()).SequenceEqual(new[] { "2222222222222222" }), "The alias render's only picture is the attire's picture");
            Check(meta.GetProperty("extra_ids").GetArrayLength() == 0 && meta.GetProperty("screens").GetString() == "both" && meta.GetProperty("taunt").GetString() == "Test", "Other wrestlers are not added; screens and taunt are kept");
            Check(Video(alias).SequenceEqual(Video(source)), "The video bytes are copied exactly");
            Check(File.ReadAllBytes(source).SequenceEqual(sourceBefore), "The saved source render is not changed");
            Check(Hashes(own).SequenceEqual(new[] { "1111111111111111", "4444444444444444" }), "The attire picture is taken out of the wrestler's own render");
            var registry = AttireRenders.ReadRegistry(folder);
            Check(registry.Count == 1 && registry[0].Alias == 65535 && registry[0].WrestlerId == 312 && registry[0].Attire == 2 && registry[0].Picture == Alt, "The attire render is recorded");
            Check(AttireRenders.Options(312, roster, registry)[1].Existing?.Alias == 65535, "The attire list shows the existing attire render");

            var again = AttireRenders.Install(game, source, 312, 3, roster, closed);
            Check(again.Success && again.Alias == 65535 && Directory.GetFiles(folder, "0*.gmr").Length == 1, "Attire 3 shares attire 2's picture, so it reuses the same slot and file");
            File.WriteAllText(Path.Combine(folder, "65534_Other.gmr"), "x");
            string table2 = Write("CharProfileTable2.jsfb", Build(new Row(312, MainPic, Alt, Alt2), new Row(65533, MainPic)));
            var roster2 = DiscoveryRenderLinks.ReadAttires(table2);
            var second = AttireRenders.Install(game, source, 312, 3, roster2, closed);
            Check(second.Success && second.Alias == 65532, "A new attire skips used slots and real roster IDs: " + second.Alias);
            File.Delete(Path.Combine(folder, "65534_Other.gmr"));

            // Re-installing the wrestler's own render brings every picture back; re-apply takes them out again.
            File.Delete(own); own = Gmr(Path.Combine(folder, "312_CM_Punk.gmr"), 312, MainPic, Alt, Alt2, Shared);
            var reapply = AttireRenders.Reapply(game);
            Check(reapply.Success && Hashes(own).SequenceEqual(new[] { "1111111111111111", "4444444444444444" }), "Re-apply removes every attire picture from the re-installed render: " + reapply.Message);

            var removed = AttireRenders.Remove(game, 65532, closed);
            Check(removed.Success && Directory.GetFiles(folder, "065532_*").Length == 0, "Removing deletes only that attire render");
            Check(Hashes(own).Contains("3333333333333333") && !Hashes(own).Contains("2222222222222222"), "Its picture goes back to the wrestler's own render; others stay with their attire render");
            Check(AttireRenders.ReadRegistry(folder).Select(i => i.Alias).SequenceEqual(new[] { 65535 }), "The registry keeps the remaining attire render");

            File.Delete(Directory.GetFiles(folder, "065535_*").Single());
            Check(AttireRenders.Reapply(game).Message.Contains("missing") && AttireRenders.ReadRegistry(folder).Count == 0, "A deleted attire file is forgotten by re-apply");

            var refused = AttireRenders.Install(game, source, 312, 1, roster, closed);
            Check(!refused.Success && refused.Message.Contains("main roster picture"), "Installing the main attire as an attire render is refused");
            Check(!AttireRenders.Install(game, own, 595, 1, roster, closed).Success, "A render of a different wrestler is refused");
            string broken = Path.Combine(root, "312_broken.gmr"); File.WriteAllBytes(broken, Encoding.ASCII.GetBytes("not a render"));
            Check(!AttireRenders.Install(game, broken, 312, 2, roster, closed).Success && Directory.GetFiles(folder, "0*").Length == 0, "A damaged render installs nothing");
            Check(!Directory.GetFiles(folder, "*.tmp").Any() && !Directory.GetFiles(folder, ".*").Any(), "No temporary files are left behind");
            Check(GorillaRenderMetadata.WriteAttireAlias(source, 312, Path.Combine(root, "x.gmr"), 312, "2222222222222222") == false, "An alias cannot reuse the wrestler's own slot");

            File.WriteAllText(Path.Combine(folder, AttireRenders.RegistryName), "# c\n70000 312 2222222222222222 2\n65000 0 2222222222222222 2\n65001 312 22 2\nbad\n65002 312 2222222222222222 4 # ok\n");
            var parsed = AttireRenders.ReadRegistry(folder);
            Check(parsed.Count == 1 && parsed[0].Alias == 65002 && parsed[0].Attire == 4, "Damaged registry lines are ignored");

            Console.WriteLine("PASS " + passed + " attire render checks");
            return 0;
        }
        finally { Directory.Delete(root, true); }
    }

    private static string Gmr(string path, int id, params ulong[] hashes)
    {
        var metadata = new Dictionary<string, object> { { "format", "GorillaRender" }, { "version", 1 }, { "wrestler_id", id }, { "name", "CM PUNK" },
            { "screens", "both" }, { "end", "loop" }, { "taunt", "Test" }, { "render_hashes", hashes.Select(h => h.ToString("X16")).ToArray() },
            { "extra_ids", new[] { 595 } }, { "shared_hashes", new string[0] }, { "render_names", new[] { "cm_punk" } } };
        byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(metadata));
        using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("BIKi")); foreach (uint word in new uint[] { 56, 2, 4, 2, 1024, 1024, 30, 1, 1048576, 0, 57, 61, 64 }) writer.Write(word);
            writer.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, (byte)id }); writer.Write(json); writer.Write((uint)json.Length); writer.Write(Encoding.ASCII.GetBytes("GMR1"));
        }
        return path;
    }
    private static JsonElement Meta(string path)
    { byte[] bytes = File.ReadAllBytes(path); int size = (int)BitConverter.ToUInt32(bytes, bytes.Length - 8); using (var json = JsonDocument.Parse(bytes.AsMemory(bytes.Length - size - 8, size))) return json.RootElement.Clone(); }
    private static byte[] Video(string path) { byte[] bytes = File.ReadAllBytes(path); int size = (int)BitConverter.ToUInt32(bytes, bytes.Length - 8); return bytes.Take(bytes.Length - 8 - size).ToArray(); }
    private static string[] Hashes(string path) { return Meta(path).GetProperty("render_hashes").EnumerateArray().Select(e => e.GetString()).ToArray(); }
    private static string Write(string name, byte[] bytes) { string path = Path.Combine(root, name); File.WriteAllBytes(path, bytes); return path; }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL " + label);
        passed++; Console.WriteLine("PASS " + label);
    }
    private static byte[] Build(params Row[] rows)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(16); writer.Write(Encoding.ASCII.GetBytes("Char"));
            writer.Write((ushort)6); writer.Write((ushort)8); writer.Write((ushort)4); writer.Write((ushort)0);
            writer.Write(8); writer.Write(4); writer.Write(rows.Length);
            int[] entries = new int[rows.Length];
            for (int index = 0; index < rows.Length; index++) { entries[index] = (int)stream.Position; writer.Write(0); }
            for (int index = 0; index < rows.Length; index++)
            {
                int wrapper = Table(writer, 8, 0, 4); Patch(writer, entries[index], wrapper - entries[index]);
                int profile = Table(writer, 12, 0, 0, 4, 0, 8); Patch(writer, wrapper + 4, profile - wrapper - 4);
                long end = stream.Position; stream.Position = profile + 4; writer.Write((ushort)rows[index].Id); stream.Position = end;
                int vector = (int)stream.Position; writer.Write(rows[index].Hashes.Length);
                int[] attires = new int[rows[index].Hashes.Length];
                for (int attire = 0; attire < attires.Length; attire++) { attires[attire] = (int)stream.Position; writer.Write(0); }
                Patch(writer, profile + 8, vector - profile - 8);
                for (int attire = 0; attire < attires.Length; attire++)
                {
                    int target = Table(writer, 16, 0, 0, 0, 0, 0, 0, 8);
                    Patch(writer, attires[attire], target - attires[attire]);
                    end = stream.Position; stream.Position = target + 8; writer.Write(rows[index].Hashes[attire]); stream.Position = end;
                }
            }
            writer.Flush(); return stream.ToArray();
        }
    }

    private static int Table(BinaryWriter writer, int size, params int[] fields)
    {
        int vtable = (int)writer.BaseStream.Position;
        writer.Write((ushort)(4 + fields.Length * 2)); writer.Write((ushort)size);
        foreach (int field in fields) writer.Write((ushort)field);
        int table = (int)writer.BaseStream.Position;
        writer.Write(table - vtable); writer.Write(new byte[size - 4]); return table;
    }
    private static void Patch(BinaryWriter writer, int position, int value)
    {
        long end = writer.BaseStream.Position; writer.BaseStream.Position = position; writer.Write(value); writer.BaseStream.Position = end;
    }
    private sealed class Row
    {
        public readonly int Id; public readonly ulong[] Hashes;
        public Row(int id, params ulong[] hashes) { Id = id; Hashes = hashes; }
    }
}
