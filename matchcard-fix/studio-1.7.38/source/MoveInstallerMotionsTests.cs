using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
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
        string root = Path.Combine(Path.GetTempPath(), "gorilla-moves-" + Guid.NewGuid().ToString("N"));
        try
        {
            string discovery = Path.Combine(root, "discovery"), data = Path.Combine(discovery, "discovery_data"), moves = Path.Combine(discovery, "_moves");
            string dataBasic = Path.Combine(data, "Animation", "Acts", "Basic"), movesBasic = Path.Combine(moves, "Animation", "Acts", "Basic");
            Directory.CreateDirectory(dataBasic);
            File.WriteAllBytes(Path.Combine(dataBasic, "MotionArea_60170_60190.clips"), Container(("60186a000", 1)));
            var before = DiscoveryMotionAssets.Read(data);
            Check(before.SearchRoots.Count == 1 && before.Find("60186a000") != null, "Without a _moves folder only discovery_data is read");

            Directory.CreateDirectory(movesBasic);
            File.WriteAllBytes(Path.Combine(movesBasic, "MotionArea_51000_51300.clips"), Container(("51249a000", 2), ("51262a000", 3)));
            File.WriteAllBytes(Path.Combine(movesBasic, "Clip #51242 [51242a000].clip"), Record("51242a000", 4));
            File.WriteAllBytes(Path.Combine(dataBasic, "MotionArea_51000_51300.clips"), Container(("51249a000", 9)));
            var after = DiscoveryMotionAssets.Read(data);
            Check(after.SearchRoots.Count == 2 && after.SearchRoots[1].EndsWith("_moves"), "The Move Installer's _moves folder is added as a second search root");
            Check(after.Find("51262a000") != null && after.Find("51242a000") != null, "Taunts installed only in _moves (packed and loose) are found");
            Check(after.Find("60186a000") != null, "discovery_data taunts are still found");
            var omega = after.Find("51249a000");
            Check(omega != null && omega.Path.Contains("_moves"), "An installed move in _moves overrides the same clip in discovery_data, like the baked _moves.cak");
            Check(DiscoveryMotionAssets.MovesRoot(data) != null && DiscoveryMotionAssets.MovesRoot(moves) == null, "_moves is only taken as the sibling of discovery_data");

            // wwe.adefs from the Move Installer: header, unpack hook and record search.
            byte[] omegaRecord = Motion("51249a000", 4), annaRecord = Motion("51262a000", 7);
            var noise = new byte[9000]; new Random(5).NextBytes(noise);
            var raw = new List<byte>(noise.Take(1237)); raw.AddRange(omegaRecord); raw.AddRange(noise.Take(3011)); raw.AddRange(annaRecord); raw.AddRange(noise);
            byte[] decoy = BitConverter.GetBytes(DiscoveryMotionAssets.ClipId("51242a000")); raw.InsertRange(500, decoy);
            byte[] unpacked = raw.ToArray();
            byte[] file = Adefs(unpacked);
            byte[] back = AdefsMotions.Unpack(file, (stream, size) => { Check(size == unpacked.Length && stream.Length == file.Length - 24, "Unpack passes the Oodle stream and its stated size"); return stream; });
            Check(back.SequenceEqual(unpacked), "Unpack returns the unpacked bytes");
            Throws("A file without the _rev/czis/uzis header is refused", () => AdefsMotions.Unpack(unpacked, (s, n) => s));
            byte[] wrongSize = (byte[])file.Clone(); Array.Copy(BitConverter.GetBytes((uint)5), 0, wrongSize, 12, 4);
            Throws("A compressed size that does not match the file is refused", () => AdefsMotions.Unpack(wrongSize, (s, n) => s));
            Throws("An unpacker that returns the wrong size is refused", () => AdefsMotions.Unpack(file, (s, n) => new byte[n - 1]));
            var records = AdefsMotions.FindRecords(unpacked, new[] { "51249a000", "51262a000", "51242a000", "51300a000" }, System.Threading.CancellationToken.None);
            Check(records.Count == 2, "Both installed taunts are found at odd positions inside the unpacked data");
            Check(records["51249a000"].SequenceEqual(omegaRecord) && records["51262a000"].SequenceEqual(annaRecord), "Each found record is the complete motion record");
            Check(!records.ContainsKey("51242a000"), "An ID match that is not followed by a valid motion is ignored");
            Check(DecodeOk(records["51249a000"]), "A found record decodes with the normal motion decoder");

            // The Move Installer renames installed clips "dsco_<pack>_<move>_<variant>" and lists them in wwe.adefs.
            string pack = "024a6ef9bdda464d8c45453e5beb984b", other = "42f30353581148999ca34ee01d7ef7d0";
            byte[] table = Encoding.ASCII.GetBytes("..__vk....dsco_" + pack + "_51249_0..?....dsco_" + pack + "_51249_0..x..dsco_" + pack + "_33945_10..58212a007..dsco_" + other + "_51260_0..dsco_" + pack + "_51260_0..");
            var listed = AdefsMotions.InstalledClipNames(table);
            Check(listed.Count == 4, "Installed dsco clip names are read from wwe.adefs once each");
            Check(AdefsMotions.RenamedClip(listed, "51249a000") == "dsco_" + pack + "_51249_0", "Kenny Omega 3 (51249a000) maps to its installed clip");
            Check(AdefsMotions.RenamedClip(listed, "33945a010") == "dsco_" + pack + "_33945_10", "The variant number maps to the last part of the installed name");
            Check(AdefsMotions.RenamedClip(listed, "33945a001") == null && AdefsMotions.RenamedClip(listed, "3394a010") == null, "A different variant or move never matches");
            Check(AdefsMotions.RenamedClip(listed, "51260a000") != null, "A move installed by two packs still maps to one installed clip");
            Check(AdefsMotions.RenamedClip(listed, "not-a-clip") == null, "Non-canonical names are ignored");

            // End to end: a taunt installed by the Move Installer is found by its normal clip name.
            string gameRoot = Path.Combine(root, "WWE 2K26"), installedData = Path.Combine(gameRoot, "discovery", "discovery_data");
            string bank = Path.Combine(installedData, "Animation", "animbanks", "ingame", "general_purpose");
            string adefsDir = Path.Combine(gameRoot, "discovery", "_moves", "AnimSystem", "wwe");
            Directory.CreateDirectory(bank); Directory.CreateDirectory(adefsDir);
            string installedName = "dsco_" + pack + "_51258_0";
            byte[] installedRecord = Motion(installedName, 9);
            File.WriteAllBytes(Path.Combine(bank, "grabs.clips"), ContainerOf(installedRecord));
            byte[] adefsText = Encoding.ASCII.GetBytes("..Trace..." + installedName + "..?.." + installedName + "....");
            File.WriteAllBytes(Path.Combine(adefsDir, "wwe.adefs"), Adefs(adefsText));
            File.WriteAllText(Path.Combine(gameRoot, "oo2core_9_win64.dll"), "stand-in");
            AdefsMotions.NamesCacheRoot = Path.Combine(root, "names-cache");
            int unpackCalls = 0;
            AdefsMotions.Unpacker = (dll, stream, size) => { unpackCalls++; Check(dll == Path.Combine(gameRoot, "oo2core_9_win64.dll"), "The game's own Oodle DLL is used"); return stream; };
            var installedLibrary = DiscoveryMotionAssets.Read(installedData);
            var abadon = installedLibrary.Find("51258a000");
            Check(abadon != null && abadon.Read().SequenceEqual(installedRecord), "Abadon 1 (51258a000) is found through its installed dsco clip in grabs.clips");
            Check(installedLibrary.Find("51258a001") == null && installedLibrary.Find("51259a000") == null, "Other variants and moves are not matched");
            Check(installedLibrary.InstalledClipNote != null && installedLibrary.InstalledClipNote.StartsWith("1 Move Installer"), "The animation report says how many installed names were read");
            DiscoveryMotionAssets.Read(installedData);
            Check(unpackCalls == 1, "wwe.adefs is unpacked once; later reads use the cached name list");
            File.WriteAllBytes(Path.Combine(adefsDir, "wwe.adefs"), Adefs(Encoding.ASCII.GetBytes("nothing installed")));
            File.SetLastWriteTimeUtc(Path.Combine(adefsDir, "wwe.adefs"), DateTime.UtcNow.AddMinutes(1));
            Check(DiscoveryMotionAssets.Read(installedData).Find("51258a000") == null && unpackCalls == 2, "A changed wwe.adefs is read again");
            Console.WriteLine("PASS " + passed + " Move Installer motion checks");
            return 0;
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }


    private static byte[] Motion(string clip, int frames)
    {
        // One bone, constant channels: the smallest record the motion decoder reads in full.
        var data = new byte[0xA0 + 8 + 48];
        Array.Copy(BitConverter.GetBytes(DiscoveryMotionAssets.ClipId(clip)), data, 8); // clip may be a dsco_ name
        Array.Copy(BitConverter.GetBytes(60f), 0, data, 0x0C, 4);
        foreach (int at in new[] { 0x20, 0x24 }) Array.Copy(BitConverter.GetBytes(1u), 0, data, at, 4);
        Array.Copy(BitConverter.GetBytes((uint)frames), 0, data, 0x44, 4);
        foreach (int at in new[] { 0x78, 0x7C }) Array.Copy(BitConverter.GetBytes(6u), 0, data, at, 4);
        Array.Copy(BitConverter.GetBytes(0x1234567890ABCDEFUL), 0, data, 0xA0, 8);
        return data;
    }
    private static byte[] Adefs(byte[] stream)
    {
        var file = new List<byte>(Encoding.ASCII.GetBytes("_rev")); file.AddRange(BitConverter.GetBytes(6u));
        file.AddRange(Encoding.ASCII.GetBytes("czis")); file.AddRange(BitConverter.GetBytes((uint)stream.Length));
        file.AddRange(Encoding.ASCII.GetBytes("uzis")); file.AddRange(BitConverter.GetBytes((uint)stream.Length));
        file.AddRange(stream); return file.ToArray();
    }
    private static bool DecodeOk(byte[] record) { try { return DiscoveryMotionDecoder.Decode(record).Frames > 0; } catch (InvalidDataException) { return false; } }
    private static void Throws(string label, Action action)
    {
        try { action(); } catch (InvalidDataException) { passed++; Console.WriteLine("PASS " + label); return; }
        throw new Exception("FAIL " + label);
    }
    private static byte[] ContainerOf(byte[] record)
    {
        var file = new List<byte>(new byte[16]); file.AddRange(BitConverter.GetBytes(1u)); file.AddRange(BitConverter.GetBytes(1u));
        file.AddRange(record.Take(8)); file.AddRange(BitConverter.GetBytes((uint)record.Length)); file.AddRange(BitConverter.GetBytes(40u));
        file.AddRange(record); return file.ToArray();
    }
    private static ulong Id(string clip) { return DiscoveryMotionAssets.ClipId(clip); }
    private static byte[] Record(string clip, byte fill)
    {
        var bytes = new byte[200]; Array.Copy(BitConverter.GetBytes(Id(clip)), bytes, 8);
        for (int i = 8; i < bytes.Length; i++) bytes[i] = fill; return bytes;
    }
    private static byte[] Container(params (string clip, byte fill)[] items)
    {
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
        {
            writer.Write(0UL); writer.Write(0UL); writer.Write((uint)items.Length); writer.Write((uint)items.Length);
            int offset = 24 + 16 * items.Length;
            foreach (var item in items) { writer.Write(Id(item.clip)); writer.Write(200u); writer.Write((uint)offset); offset += 200; }
            foreach (var item in items) writer.Write(Record(item.clip, item.fill));
            writer.Flush(); return stream.ToArray();
        }
    }
    private static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL " + label); passed++; Console.WriteLine("PASS " + label); }
}
