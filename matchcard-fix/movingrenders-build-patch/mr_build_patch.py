"""Replace Moving Renders' 24 Sep 2026 game-build entry with the 6 Nov 2026 build.

Only the build table entry changes: timestamp, image size and the two game
addresses (wrestler slot list - 4, wrestler object list) taken from MatchCast's
pattern-scan log on the 6 Nov build. Sign-in and everything else is untouched.
"""
import struct, sys, hashlib

OLD = dict(stamp=0x6AB5B298, size=0x3B559000, v1=0x4AB3390, v2=0x4A37D80)
NEW = dict(stamp=0x6AED7585, size=0x3C760000, v1=0x4AC0530, v2=0x4A44DF0)

def patch(data):
    data = bytearray(data)
    # cmp ecx, stamp  (81 F9 imm32) followed (within 16 bytes) by cmp eax, size (3D imm32)
    sig = b"\x81\xF9" + struct.pack("<I", OLD["stamp"])
    hits = [i for i in range(len(data) - 6) if data[i:i + 6] == sig]
    if len(hits) != 1: raise SystemExit("build check not found exactly once: %d" % len(hits))
    at = hits[0]
    window = bytes(data[at:at + 0x40])
    size_at = window.find(b"\x3D" + struct.pack("<I", OLD["size"]))
    if size_at < 0: raise SystemExit("size check not found next to the build check")
    size_at += at
    # mov dword ptr [rip+x], imm32  = C7 05 disp32 imm32 ; v1 then v2 right after the flag store
    def mov_imm(value, start):
        tail = struct.pack("<I", value)
        for i in range(start, start + 0x40):
            if data[i:i + 2] == b"\xC7\x05" and data[i + 6:i + 10] == tail: return i + 6
        raise SystemExit("address %#x not found after the build check" % value)
    v1_at = mov_imm(OLD["v1"], size_at)
    v2_at = mov_imm(OLD["v2"], v1_at)
    for name, pos, new in (("stamp", at + 2, NEW["stamp"]), ("size", size_at + 1, NEW["size"]),
                           ("v1", v1_at, NEW["v1"]), ("v2", v2_at, NEW["v2"])):
        data[pos:pos + 4] = struct.pack("<I", new)
        print("  %-5s at file offset %#x -> %#x" % (name, pos, new))
    return bytes(data)

if __name__ == "__main__":
    src, dst = sys.argv[1], sys.argv[2]
    raw = open(src, "rb").read()
    out = patch(raw)
    diff = sum(a != b for a, b in zip(raw, out))
    open(dst, "wb").write(out)
    print("bytes changed:", diff, "| sha256", hashlib.sha256(out).hexdigest())
