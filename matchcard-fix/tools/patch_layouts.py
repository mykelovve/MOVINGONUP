"""Add GM_clip movie layers to chosen layouts of a Scre project.

Port of MatchCardLayouts.Patch/Copier. The template movie is an existing,
verified GM_clip from the same project (the C# repair path clones existing
movies the same way). Only child-vector pointers of patched player groups
change; everything else is appended.
Usage: python3 -I patch_layouts.py <in.jsfb> <out.jsfb> <layout> [<layout> ...]
"""
import struct, sys
sys.path.insert(0, __import__("os").path.dirname(__file__))
from audit_layouts import Screen, LAYOUTS, SLOTS, fnv, movie_path, audit

SCALAR = {0, 2, 30, 39, 51, 52, 53}


class Copier:
    def __init__(self, src): self.o = bytearray(src)

    def alloc(self, size, residue=0):
        while len(self.o) % 8 != residue: self.o.append(0)
        p = len(self.o); self.o.extend(b"\0" * size); return p

    def put32(self, p, v): struct.pack_into("<I", self.o, p, v & 0xFFFFFFFF)
    def r32(self, p): return struct.unpack_from("<I", self.o, p)[0]
    def r16(self, p): return struct.unpack_from("<H", self.o, p)[0]

    def point(self, field, target):
        assert target > field and field % 4 == 0 and target % 4 == 0
        self.put32(field, target - field)

    def string(self, value):
        b = value.encode(); p = self.alloc(5 + len(b))
        self.put32(p, len(b)); self.o[p + 4:p + 4 + len(b)] = b; return p

    def table(self, s, pos, node):
        offs, size = s.shape(pos)
        vt_src = pos - struct.unpack_from("<i", s.b, pos)[0]
        vlen = 4 + 2 * len(offs); vt = self.alloc(vlen)
        self.o[vt:vt + vlen] = s.b[vt_src:vt_src + vlen]
        copy = self.alloc(size, pos % 8)
        self.o[copy:copy + size] = s.b[pos:pos + size]
        self.put32(copy, copy - vt)
        for i, off in enumerate(offs):
            if off == 0: continue
            field = pos + off
            if not node:
                width = min([x for x in offs if x > off] or [size]) - off
                if width > 8: raise ValueError("non-scalar movie property")
                continue
            if i in SCALAR: continue
            if i == 1: self.point(copy + off, self.string(s.name(pos)))
            elif i in (55, 56):
                if s.u32(s.target(field)) != 0: raise ValueError("non-leaf slot children")
                self.point(copy + off, self.alloc(4))
            else: self.point(copy + off, self.table(s, s.target(field), False))
        return copy

    def field(self, t, i):
        vt = t - self.r32(t); length = self.r16(vt)
        assert 4 + 2 * i < length; off = self.r16(vt + 4 + 2 * i); assert off
        return t + off

    def name(self, t, v): self.point(self.field(t, 1), self.string(v))
    def asset(self, t, h): struct.pack_into("<Q", self.o, self.field(t, 0), h)

    def floats(self, t, i, a, b):
        f = self.field(t, i); sub = f + self.r32(f)
        struct.pack_into("<f", self.o, self.field(sub, 0), a)
        struct.pack_into("<f", self.o, self.field(sub, 1), b)

    def result(self):
        while len(self.o) % 8: self.o.append(0)
        return bytes(self.o)


def patch(src, layouts):
    s = Screen(src)
    clips = [k for pos, _, path in s.nodes if len(path) >= 2 and path[-2] == "1on1"
             for k in s.children(pos) if s.name(k) == "GM_clip"]
    template = clips[0]
    c = Copier(src); changes = set()
    for lname in layouts:
        li = LAYOUTS.index(lname)
        groups = [n for n in s.nodes if len(n[2]) >= 2 and n[2][-2] == lname and len(n[2][-1]) == 2 and n[2][-1][1] == "P"]
        assert len(groups) == SLOTS[li], (lname, len(groups))
        for pos, _, path in groups:
            player = int(path[-1][0]); kids = s.children(pos)
            names = [s.name(k) for k in kids]
            assert names == ["CAS_dummy", "SS_dummy"] and all(s.byte(k, 2) == 2 for k in kids), (path, names)
            vec = c.alloc(16); c.put32(vec, 3)
            cas = c.table(s, kids[0], True); still = c.table(s, kids[1], True)
            mov = c.table(s, template, True)
            c.name(mov, "GM_clip"); c.asset(mov, fnv(movie_path(li, player)))
            c.floats(mov, 3, 0.0, -551.0); c.floats(mov, 4, 1024.0, 1024.0)
            c.floats(mov, 5, 1102 / 1024, 1102 / 1024)
            m = 1 + (8 * li + player) / 1048576; c.floats(still, 5, m, m)
            c.point(vec + 4, cas); c.point(vec + 8, still); c.point(vec + 12, mov)
            cf = s.field(pos, 55); c.point(cf, vec); changes.add(cf)
    out = c.result()
    allowed = {cf + i for cf in changes for i in range(4)}
    bad = [i for i in range(len(src)) if src[i] != out[i] and i not in allowed]
    assert not bad, "non-slot bytes changed"
    return out


if __name__ == "__main__":
    src = open(sys.argv[1], "rb").read()
    open(sys.argv[2], "wb").write(patch(src, sys.argv[3:]))


def strip(src, layouts):
    """Remove GM_clip movie layers from the named layouts (restores the plain
    CAS_dummy/SS_dummy slot with an unmarked 1.0 still scale)."""
    s = Screen(src); c = Copier(src); changes = set()
    for lname in layouts:
        groups = [n for n in s.nodes if len(n[2]) >= 2 and n[2][-2] == lname and len(n[2][-1]) == 2 and n[2][-1][1] == "P"]
        for pos, _, path in groups:
            kids = s.children(pos); names = [s.name(k) for k in kids]
            assert names == ["CAS_dummy", "SS_dummy", "GM_clip"], (path, names)
            vec = c.alloc(12); c.put32(vec, 2)
            cas = c.table(s, kids[0], True); still = c.table(s, kids[1], True)
            c.floats(still, 5, 1.0, 1.0)
            c.point(vec + 4, cas); c.point(vec + 8, still)
            cf = s.field(pos, 55); c.point(cf, vec); changes.add(cf)
    out = c.result()
    allowed = {cf + i for cf in changes for i in range(4)}
    assert not [i for i in range(len(src)) if src[i] != out[i] and i not in allowed], "non-slot bytes changed"
    return out
