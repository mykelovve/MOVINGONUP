using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace GorillaStudio.NameFolders
{
    /// <summary>
    /// Recovers taunt animations that the Move Installer wrote into its own copy of
    /// wwe.adefs (discovery\_moves\AnimSystem\wwe\wwe.adefs). That file is Oodle
    /// compressed ("_rev", "czis" size, "uzis" size, then the Oodle stream) and is
    /// unpacked with the game's own oo2core_9_win64.dll. Inside it, a requested clip is
    /// accepted only where its 8-byte clip ID starts a complete motion record that the
    /// normal motion decoder reads in full. Found records are cached as loose clips.
    /// </summary>
    public static class AdefsMotions
    {
        internal const int HeaderBytes = 24;
        private const long MaximumUnpackedBytes = 1536L * 1024 * 1024;

        /// <summary>Unpacks an adefs file; <paramref name="unpack"/> performs Oodle decompression.</summary>
        public static byte[] Unpack(byte[] file, Func<byte[], int, byte[]> unpack)
        {
            if (file == null || file.Length < HeaderBytes) throw new InvalidDataException("The wwe.adefs file is truncated.");
            if (Encoding.ASCII.GetString(file, 0, 4) != "_rev" || Encoding.ASCII.GetString(file, 8, 4) != "czis" || Encoding.ASCII.GetString(file, 16, 4) != "uzis")
                throw new InvalidDataException("The wwe.adefs file does not have the expected _rev/czis/uzis header.");
            uint compressed = BitConverter.ToUInt32(file, 12), unpacked = BitConverter.ToUInt32(file, 20);
            if (compressed != file.Length - HeaderBytes) throw new InvalidDataException("The wwe.adefs compressed size does not match the file.");
            if (unpacked == 0 || unpacked > MaximumUnpackedBytes) throw new InvalidDataException("The wwe.adefs unpacked size is not supported.");
            var stream = new byte[compressed];
            Array.Copy(file, HeaderBytes, stream, 0, compressed);
            byte[] raw = unpack(stream, (int)unpacked);
            if (raw == null || raw.Length != unpacked) throw new InvalidDataException("The wwe.adefs file did not unpack to its stated size.");
            return raw;
        }

        /// <summary>Finds complete, decodable motion records for the requested clips in unpacked adefs bytes.</summary>
        public static Dictionary<string, byte[]> FindRecords(byte[] data, IEnumerable<string> clips, CancellationToken token)
        {
            var wanted = new Dictionary<ulong, string>();
            foreach (string clip in clips) wanted[DiscoveryMotionAssets.ClipId(clip)] = clip;
            var found = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            if (data == null || wanted.Count == 0) return found;
            for (int offset = 0; offset + 0xA0 <= data.Length; offset++)
            {
                if ((offset & 0xFFFFF) == 0) token.ThrowIfCancellationRequested();
                string clip;
                if (!wanted.TryGetValue(BitConverter.ToUInt64(data, offset), out clip) || found.ContainsKey(clip)) continue;
                long tracks = BitConverter.ToUInt32(data, offset + 0x20), extra = BitConverter.ToUInt32(data, offset + 0x60);
                long descriptors = BitConverter.ToUInt32(data, offset + 0x78), words = BitConverter.ToUInt32(data, offset + 0x90);
                if (tracks == 0 || tracks > 4096 || extra > 4096 || descriptors != tracks * 6 || words > 64L * 1024 * 1024) continue;
                long length = 0xA0 + 8 * (tracks + extra + descriptors + words);
                if (length > data.Length - offset || length > 128L * 1024 * 1024) continue;
                var record = new byte[length];
                Array.Copy(data, offset, record, 0, length);
                try { DiscoveryMotionDecoder.Decode(record); }
                catch (InvalidDataException) { continue; }
                found[clip] = record;
                if (found.Count == wanted.Count) break;
            }
            return found;
        }

        /// <summary>
        /// Looks for <paramref name="clips"/> in the Move Installer's wwe.adefs beside the
        /// Discovery data folder. Results are cached per adefs version under
        /// <paramref name="cacheRoot"/>; the file is only unpacked when a clip is not cached yet.
        /// </summary>
        public static DiscoveryMotionFallbacks Find(string gameRoot, string discoveryDataRoot, IEnumerable<string> clips,
            string cacheRoot, Action<string> progress, CancellationToken token)
        {
            var result = new DiscoveryMotionFallbacks();
            string adefs = AdefsPath(discoveryDataRoot);
            var requested = (clips ?? Enumerable.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (adefs == null || requested.Count == 0) return result;
            try
            {
                var info = new FileInfo(adefs);
                string stamp = Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(adefs).ToLowerInvariant() + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks));
                string directory = Path.Combine(cacheRoot, "adefs-motions", stamp.Substring(0, 32));
                Directory.CreateDirectory(directory);
                string absentPath = Path.Combine(directory, "_absent.txt");
                var absent = new HashSet<string>(File.Exists(absentPath) ? File.ReadAllLines(absentPath) : new string[0], StringComparer.OrdinalIgnoreCase);
                var pending = new List<string>();
                foreach (string clip in requested)
                {
                    DiscoveryMotionAsset cached;
                    if (Cached(directory, clip, out cached)) result.Assets[clip] = cached;
                    else if (!absent.Contains(clip)) pending.Add(clip);
                }
                if (pending.Count > 0)
                {
                    progress?.Invoke("Unpacking the Move Installer's wwe.adefs to look for " + pending.Count.ToString(CultureInfo.InvariantCulture) + " installed taunt animation(s)…");
                    string oodle = string.IsNullOrWhiteSpace(gameRoot) ? null : Path.Combine(gameRoot, "oo2core_9_win64.dll");
                    if (oodle == null || !File.Exists(oodle))
                    { result.Warnings.Add("wwe.adefs from the Move Installer was not read: the game's oo2core_9_win64.dll was not found in the game folder."); return result; }
                    byte[] raw = Unpack(File.ReadAllBytes(adefs), (stream, size) => OodleNative.Decompress(oodle, stream, size));
                    token.ThrowIfCancellationRequested();
                    var found = FindRecords(raw, pending, token);
                    foreach (var pair in found)
                    {
                        string output = Path.Combine(directory, pair.Key + ".clip"), temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        File.WriteAllBytes(temporary, pair.Value);
                        if (File.Exists(output)) File.Delete(output);
                        File.Move(temporary, output);
                        result.Assets[pair.Key] = new DiscoveryMotionAsset { Path = output, Offset = 0, Length = pair.Value.Length, Id = DiscoveryMotionAssets.ClipId(pair.Key), Loose = true };
                    }
                    foreach (string clip in pending.Where(clip => !found.ContainsKey(clip))) absent.Add(clip);
                    File.WriteAllLines(absentPath, absent.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
                    result.Warnings.Add(found.Count.ToString(CultureInfo.InvariantCulture) + " of " + pending.Count.ToString(CultureInfo.InvariantCulture)
                        + " missing taunt animation(s) were recovered from the Move Installer's wwe.adefs (" + adefs + ").");
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException
                || error is DllNotFoundException || error is EntryPointNotFoundException || error is BadImageFormatException
                || error is PlatformNotSupportedException || error is ArgumentException || error is System.ComponentModel.Win32Exception)
            {
                result.Warnings.Add("The Move Installer's wwe.adefs could not be read: " + error.Message);
            }
            return result;
        }

        /// <summary>discovery\_moves\AnimSystem\wwe\wwe.adefs beside the Discovery data folder, if present.</summary>
        public static string AdefsPath(string discoveryDataRoot)
        {
            string moves = DiscoveryMotionAssets.MovesRoot(discoveryDataRoot);
            if (moves == null) return null;
            string path = Path.Combine(moves, "AnimSystem", "wwe", "wwe.adefs");
            return File.Exists(path) ? path : null;
        }

        private static bool Cached(string directory, string clip, out DiscoveryMotionAsset asset)
        {
            asset = null;
            string path = Path.Combine(directory, clip + ".clip");
            try
            {
                if (!File.Exists(path)) return false;
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 0xA0 || BitConverter.ToUInt64(bytes, 0) != DiscoveryMotionAssets.ClipId(clip)) return false;
                asset = new DiscoveryMotionAsset { Path = path, Offset = 0, Length = bytes.Length, Id = DiscoveryMotionAssets.ClipId(clip), Loose = true };
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { return false; }
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>Calls OodleLZ_Decompress in the game's own oo2core DLL (Windows only).</summary>
    internal static class OodleNative
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string path);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate long OodleDecompress(byte[] compressed, long compressedLength, byte[] raw, long rawLength, int fuzzSafe, int checkCrc,
            int verbosity, IntPtr decodeBufferBase, long decodeBufferSize, IntPtr callback, IntPtr callbackUserData,
            IntPtr decoderMemory, long decoderMemorySize, int threadPhase);

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, OodleDecompress> Loaded = new Dictionary<string, OodleDecompress>(StringComparer.OrdinalIgnoreCase);

        internal static byte[] Decompress(string dll, byte[] compressed, int size)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) throw new PlatformNotSupportedException("Oodle decompression needs Windows.");
            OodleDecompress function;
            lock (Gate)
            {
                if (!Loaded.TryGetValue(dll, out function))
                {
                    IntPtr module = LoadLibraryW(dll);
                    if (module == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not load " + dll);
                    IntPtr address = GetProcAddress(module, "OodleLZ_Decompress");
                    if (address == IntPtr.Zero) throw new EntryPointNotFoundException("OodleLZ_Decompress was not found in " + dll + ".");
                    function = (OodleDecompress)Marshal.GetDelegateForFunctionPointer(address, typeof(OodleDecompress));
                    Loaded[dll] = function;
                }
            }
            var raw = new byte[size];
            long written = function(compressed, compressed.Length, raw, raw.Length, 1, 0, 0, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 3);
            if (written != size) throw new InvalidDataException("Oodle unpacked " + written.ToString(CultureInfo.InvariantCulture) + " bytes; " + size.ToString(CultureInfo.InvariantCulture) + " were expected.");
            return raw;
        }
    }
}
