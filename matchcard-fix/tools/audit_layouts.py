"""Audit every match-card layout/player slot in Scre (.jsfb) projects.

Python port of the read side of src/GorillaStudio.NameFolders/MatchCardLayouts.cs.
Usage: python3 -I audit_layouts.py <UI-parent-folder> <project.jsfb> [...]
"""
import os, struct, sys

LAYOUTS = ["1on1", "1on2", "1on3", "1on4", "1on5", "1on6", "1on7", "2on2", "2on3", "3on3", "4on4",
           "5man", "6man", "8man", "fatal_4way", "promo_1", "promo_2", "triple_threat",
           "triple_threat_tag", "fatal_4way_tag"]
SLOTS = [2, 3, 4, 5, 6, 7, 8, 4, 5, 6, 8, 5, 6, 8, 4, 1, 2, 3, 6, 8]


def fnv(s):
    h = 14695981039346656037
    for b in s.lower().encode():
        h = ((h ^ b) * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return h


def movie_path(layout, player):
    return "UI/Assets/movies/gorilla_renders/ph_L%02d_%dP.bk2" % (layout, player)


class Screen:
    def __init__(self, b):
        self.b = b
        assert b[4:8] == b"Scre", "not Scre"
        self.nodes = []
        root = self.target(0)
        for c in self.vector(root, 3):
            self.walk(c, root, [])

    def u32(self, p): return struct.unpack_from("<I", self.b, p)[0]
    def u16(self, p): return struct.unpack_from("<H", self.b, p)[0]

    def shape(self, t):
        vt = t - struct.unpack_from("<i", self.b, t)[0]
        length, size = self.u16(vt), self.u16(vt + 2)
        return [self.u16(vt + 4 + 2 * i) for i in range((length - 4) // 2)], size

    def field(self, t, i):
        offs, _ = self.shape(t)
        return 0 if i >= len(offs) or offs[i] == 0 else t + offs[i]

    def target(self, f): return f + self.u32(f)

    def name(self, t):
        f = self.field(t, 1)
        if not f: return "?"
        p = self.target(f); n = self.u32(p)
        return self.b[p + 4:p + 4 + n].decode()

    def byte(self, t, i):
        f = self.field(t, i)
        return -1 if not f else self.b[f]

    def floats(self, t, i):
        f = self.field(t, i)
        if not f: return None
        v = self.target(f); a, b = self.field(v, 0), self.field(v, 1)
        if not a or not b: return None
        return struct.unpack_from("<f", self.b, a)[0], struct.unpack_from("<f", self.b, b)[0]

    def vector(self, t, i):
        f = self.field(t, i)
        if not f: return []
        v = self.target(f); n = self.u32(v)
        return [self.target(v + 4 + 4 * k) for k in range(n)]

    def children(self, t): return self.vector(t, 55)

    def walk(self, pos, parent, path):
        names = path + [self.name(pos)]
        self.nodes.append((pos, parent, names))
        for c in self.children(pos):
            self.walk(c, pos, names)


def close(a, b): return abs(a - b) <= 1e-6 * max(1, abs(b))


def audit(ui_parent, project):
    s = Screen(open(project, "rb").read())
    rows = []
    for li, lname in enumerate(LAYOUTS):
        groups = [n for n in s.nodes if len(n[2]) >= 2 and n[2][-2] == lname
                  and len(n[2][-1]) == 2 and n[2][-1][1] == "P" and n[2][-1][0] in "12345678"]
        if not groups:
            rows.append((lname, li, SLOTS[li], 0, ["layout not in project"]))
            continue
        containers = {}
        for g in groups: containers.setdefault(g[1], []).append(g)
        for _, gs in containers.items():
            problems, ok = [], 0
            label = "/".join(gs[0][2][:-1])
            if sorted(g[2][-1] for g in gs) != ["%dP" % p for p in range(1, SLOTS[li] + 1)]:
                problems.append("unexpected player groups %s" % sorted(g[2][-1] for g in gs))
            for pos, _, path in sorted(gs, key=lambda g: g[2][-1]):
                player = int(path[-1][0])
                kids = s.children(pos); names = [s.name(k) for k in kids]
                clips = [k for k, n in zip(kids, names) if n == "GM_clip"]
                p = path[-1]
                if not clips:
                    problems.append("%s: no GM_clip movie layer" % p); continue
                m = clips[0]
                af = s.field(m, 0)
                asset = struct.unpack_from("<Q", s.b, af)[0] if af else None
                if s.byte(m, 2) != 7 or s.byte(m, 51) != 1: problems.append("%s: GM_clip not an autoplay movie" % p)
                if asset != fnv(movie_path(li, player)):
                    problems.append("%s: movie points to wrong asset" % p)
                if names[:2] != ["CAS_dummy", "SS_dummy"]: problems.append("%s: children %s" % (p, names))
                else:
                    mk = s.floats(kids[1], 5); want = 1 + (8 * li + player) / 1048576
                    if not mk or not close(mk[0], want) or not close(mk[1], want): problems.append("%s: plugin marker wrong %s" % (p, mk))
                tr = (s.floats(m, 3), s.floats(m, 4), s.floats(m, 5))
                if not (tr[0] and close(tr[0][1], -551) and tr[1] and close(tr[1][0], 1024)): problems.append("%s: movie transform %s" % (p, tr))
                if not os.path.isfile(os.path.join(ui_parent, movie_path(li, player))):
                    problems.append("%s: movie file missing %s" % (p, os.path.basename(movie_path(li, player))))
                if not any(str(x).startswith(p) for x in problems): ok += 1
            rows.append((lname, li, SLOTS[li], ok, problems, label))
    return rows


if __name__ == "__main__":
    ui_parent = sys.argv[1]
    for proj in sys.argv[2:]:
        print("=" * 80); print(os.path.relpath(proj, ui_parent))
        for r in audit(ui_parent, proj):
            lname, li, slots, ok, problems = r[:5]
            status = "OK " if ok == slots and not problems else "BAD"
            print("  %s L%02d %-18s %d/%d slots ready%s" % (status, li, lname, ok, slots,
                  ("  <- " + "; ".join(problems[:4]) + (" ..." if len(problems) > 4 else "")) if problems else ""))
            if len(r) > 5 and problems == []: pass
