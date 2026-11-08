import sys, types, itertools
ids = itertools.count(1)
class Socket:
    def __init__(s, node, name): s.node, s.name, s.links, s.default_value = node, name, [], 0.0
class Link:
    def __init__(s, a, b): s.from_socket, s.to_socket, s.from_node, s.to_node = a, b, a.node, b.node
class Sockets(list):
    def __getitem__(s, k):
        if isinstance(k, str): return next(x for x in s if x.name == k)
        return list.__getitem__(s, k)
class Node:
    def __init__(s, type_, ins=(), outs=(), image=None):
        s.type, s.image, s.label, s.operation, s.use_clamp = type_, image, "", None, False
        s.inputs = Sockets(Socket(s, n) for n in ins); s.outputs = Sockets(Socket(s, n) for n in outs)
        s.is_active_output = True
KINDS = {"ShaderNodeSeparateColor": ("SEPRGB", ["Color"], ["Red", "Green", "Blue"]),
         "ShaderNodeMath": ("MATH", ["A", "B"], ["Value"])}
class Nodes(list):
    def new(s, kind):
        t, i, o = KINDS[kind]; n = Node(t, i, o); s.append(n); return n
class Links(list):
    def new(s, a, b):
        l = Link(a, b); a.links.append(l); b.links.append(l); s.append(l); return l
class Tree:
    def __init__(s): s.nodes, s.links = Nodes(), Links()
class Image:
    def __init__(s, name): s.name = name
class Material:
    def __init__(s, name, image_name, alpha_linked=False):
        s.name, s.use_nodes, s.ptr = name, True, next(ids)
        s.node_tree = t = Tree()
        out = Node("OUTPUT_MATERIAL", ["Surface"]); bs = Node("BSDF_PRINCIPLED", ["Base Color", "Alpha"], ["BSDF"])
        tex = Node("TEX_IMAGE", [], ["Color", "Alpha"], Image(image_name)); t.nodes += [out, bs, tex]
        t.links.new(bs.outputs["BSDF"], out.inputs["Surface"]); t.links.new(tex.outputs["Color"], bs.inputs["Base Color"])
        if alpha_linked: t.links.new(tex.outputs["Alpha"], bs.inputs["Alpha"])
    def as_pointer(s): return s.ptr
    def copy(s):
        c = Material(s.name + ".001", next(n for n in s.node_tree.nodes if n.type == "TEX_IMAGE").image.name)
        return c
class Slot:
    def __init__(s, m): s.material, s.link = m, "DATA"
class Obj:
    def __init__(s, name, mats): s.name, s.type, s.hide_render = name, "MESH", False; s.material_slots = [Slot(m) for m in mats]
handlers = types.SimpleNamespace(render_pre=[], persistent=lambda f: f)
bpy = types.ModuleType("bpy"); bpy.app = types.SimpleNamespace(handlers=handlers); sys.modules["bpy"] = bpy

src = open(sys.argv[1]).read()
start = src.index("def _register_hair_coverage_repair():"); end = src.index("try:\n    _register_hair_coverage_repair()")
ns = {}; exec(src[start:end], ns); ns["_register_hair_coverage_repair"]()
repair = handlers.render_pre[-1]

body_hair = Material("M_Combinations", "combinations_rai.dds")
hair_ok = Material("M_Hair", "hair_rai.dds", alpha_linked=True)
shirt = Material("M_Tops", "tops_color.dds")
chest = Obj("combinations_ALPHA:skinned", [body_hair]); legs = Obj("combinations_ALPHA.001:skinned", [body_hair])
body = Obj("body:skinned", [body_hair]); head_hair = Obj("hair_ALPHA:skinned", [hair_ok]); tops = Obj("tops:skinned", [shirt])
scene = types.SimpleNamespace(objects=[chest, legs, body, head_hair, tops])
repair(scene); repair(scene)
c = chest.material_slots[0].material
bs = next(n for n in c.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
assert c is not body_hair and bs.inputs["Alpha"].links, "body hair must get a cut-out"
alpha_src = bs.inputs["Alpha"].links[0].from_node
assert alpha_src.type == "MATH" and alpha_src.use_clamp, "coverage is clamped"
sub = alpha_src.inputs[0].links[0].from_node; sep = sub.inputs[0].links[0].from_node
assert sep.type == "SEPRGB" and sub.inputs[0].links[0].from_socket.name == "Green", "coverage comes from the green channel"
assert getattr(c, "blend_method", None) == "HASHED" and getattr(c, "surface_render_method", None) == "DITHERED"
assert legs.material_slots[0].material is c, "objects sharing the material get the same fixed copy"
assert body.material_slots[0].material is body_hair and not next(n for n in body_hair.node_tree.nodes if n.type=="BSDF_PRINCIPLED").inputs["Alpha"].links, "a body sharing it is untouched"
assert head_hair.material_slots[0].material is hair_ok, "hair already cut out is left alone"
assert tops.material_slots[0].material is shirt, "clothing is ignored"
print("PASS hair coverage: body hair cut out, shared copy reused, body/hair/clothing untouched")
