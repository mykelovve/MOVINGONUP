"""Mock-Blender checks for the face paint pass in gr_render.py (no Blender here)."""
import ast, os, sys, tempfile, types

SRC = sys.argv[1]
tree = ast.parse(open(SRC).read())
wanted = {"_FACE_PAINT_WORDS", "_FACE_PAINT_SKIP", "_face_paint_folders", "_jmtl_refs", "_face_paint_candidates", "_register_face_paint"}
body = [n for n in tree.body if (isinstance(n, ast.FunctionDef) and n.name in wanted)
        or (isinstance(n, ast.Assign) and any(getattr(t, "id", None) in wanted for t in n.targets))]
module = ast.Module(body=body, type_ignores=[])
passed = 0
def check(ok, label):
    global passed
    if not ok: raise SystemExit("FAIL " + label)
    passed += 1; print("PASS", label)

class Socket:
    def __init__(s, node, name, kind="RGBA"): s.node, s.name, s.type, s.links = node, name, kind, []
class Sockets(list):
    def __getitem__(s, key): return next(x for x in s if x.name == key) if isinstance(key, str) else list.__getitem__(s, key)
class Link:
    def __init__(s, a, b): s.from_socket, s.to_socket, s.from_node, s.to_node = a, b, a.node, b.node
class Node:
    count = 0
    def __init__(s, kind, ins=(), outs=()):
        Node.count += 1; s.id = Node.count; s.type = kind; s.image = None; s.location = (0, 0); s.interpolation = "Linear"
        s.inputs = Sockets(Socket(s, n, k) for n, k in ins); s.outputs = Sockets(Socket(s, n, k) for n, k in outs)
    def as_pointer(s): return s.id
class Links:
    def __init__(s): s.all = []
    def new(s, a, b):
        for old in list(b.links):
            b.links.remove(old); old.from_socket.links.remove(old); s.all.remove(old)
        link = Link(a, b); a.links.append(link); b.links.append(link); s.all.append(link); return link
class Nodes(list):
    def new(s, kind):
        if kind == "ShaderNodeTexImage": n = Node("TEX_IMAGE", [("Vector", "VECTOR")], [("Color", "RGBA"), ("Alpha", "VALUE")])
        elif kind == "ShaderNodeMix":
            n = Node("MIX", [("Factor", "VALUE"), ("Factor", "VECTOR"), ("A", "VALUE"), ("B", "VALUE"), ("A", "VECTOR"), ("B", "VECTOR"), ("A", "RGBA"), ("B", "RGBA")],
                     [("Result", "VALUE"), ("Result", "VECTOR"), ("Result", "RGBA")])
        else: raise RuntimeError(kind)
        s.append(n); return n
class Tree:
    def __init__(s): s.nodes, s.links = Nodes(), Links()
class Colorspace: name = "sRGB"
class Image:
    def __init__(s, path, alpha):
        s.filepath, s.name, s.alpha = path, os.path.basename(path), alpha; s.colorspace_settings = Colorspace(); s.alpha_mode = "STRAIGHT"
    def copy(s): c = Image(s.filepath, s.alpha); return c
    def scale(s, w, h): s.pixels = [v for a in s.alpha for v in (0.5, 0.5, 0.5, a)]
class Material(dict):
    _id = 1000
    def __init__(s, name): Material._id += 1; s.name, s.use_nodes, s.node_tree, s.id = name, True, Tree(), Material._id
    def as_pointer(s): return s.id

def build(root, attire_files, base_files):
    char = os.path.join(root, "112_Jeff_Hardy_TNA_2")
    for sub, files in (("BaseModel", base_files), ("Default_Attire", attire_files)):
        os.makedirs(os.path.join(char, sub, "Textures"), exist_ok=True); os.makedirs(os.path.join(char, sub, "Materials"), exist_ok=True)
        for name, data in files.items():
            folder = "Materials" if name.endswith(".jmtl") else "Textures"
            open(os.path.join(char, sub, folder, name), "wb").write(data)
    return char

def run(char, alphas, label):
    handlers = types.SimpleNamespace(render_pre=[], persistent=lambda f: f)
    images = types.SimpleNamespace(load=lambda path, check_existing=True: Image(path, alphas.get(os.path.basename(path).lower(), [1.0])),
                                   remove=lambda image: None)
    bpy = types.ModuleType("bpy"); bpy.app = types.SimpleNamespace(handlers=handlers); bpy.data = types.SimpleNamespace(images=images)
    sys.modules["bpy"] = bpy
    engine = tempfile.mkdtemp()
    ns = {"os": os, "sys": sys, "__file__": os.path.join(engine, "gr_render.py"),
          "_renderer_options": lambda: {"char_dir": char, "attire_mcd": "Default_Attire/Attire.mcd"}}
    exec(compile(module, SRC, "exec"), ns)
    ns["_register_face_paint"]()
    material = Material("head:skinned"); tree = material.node_tree
    head = Node("TEX_IMAGE", [("Vector", "VECTOR")], [("Color", "RGBA"), ("Alpha", "VALUE")]); head.image = Image("C:/png/head_color.png", [1.0])
    uv = Node("UVMAP", [], [("UV", "VECTOR")]); shader = Node("BSDF_PRINCIPLED", [("Base Color", "RGBA")], [("BSDF", "SHADER")])
    tree.nodes.extend([head, uv, shader]); tree.links.new(uv.outputs["UV"], head.inputs["Vector"]); tree.links.new(head.outputs["Color"], shader.inputs["Base Color"])
    obj = types.SimpleNamespace(type="MESH", material_slots=[types.SimpleNamespace(material=material)])
    scene = types.SimpleNamespace(objects=[obj])
    check(len(handlers.render_pre) == 1, label + ": one render_pre pass is registered")
    for frame in range(3): handlers.render_pre[0](scene)
    report = open(os.path.join(engine, "face_paint_report.txt")).read()
    return material, head, shader, report

root = tempfile.mkdtemp()
# 1. A paint layer with alpha in the attire folder.
char = build(root, {"facepaint_color.dds": b"p", "tops_color.dds": b"t"}, {"head_color.dds": b"h", "head_nrm.dds": b"n"})
material, head, shader, report = run(char, {"facepaint_color.dds": [0.0] * 90 + [1.0] * 10}, "paint layer")
source = shader.inputs["Base Color"].links[0].from_node
check(source.type == "MIX", "The head's base colour now comes from a mix")
paint = [n for n in material.node_tree.nodes if n.type == "TEX_IMAGE" and n is not head]
check(len(paint) == 1 and paint[0].image.name == "facepaint_color.dds", "Exactly one paint texture node, added once over three frames")
check(source.inputs[6].links[0].from_node is head and source.inputs[7].links[0].from_node is paint[0], "Head colour is A, paint is B")
check(source.inputs[0].links[0].from_socket.name == "Alpha", "The paint's own alpha decides where it covers the face")
check(paint[0].inputs["Vector"].links[0].from_node.type == "UVMAP", "The paint uses the head's UV map")
check(material.get("gorilla_face_paint") == 1 and "paint layer applied" in report, "Marked done and reported")
check("tops_color" not in report and "head_nrm" not in report, "Clothing and normal maps are never treated as paint")

# 2. A painted head colour inside the attire folder.
root = tempfile.mkdtemp()
char = build(root, {"head_color.dds": b"painted"}, {"head_color.dds": b"clean"})
material, head, shader, report = run(char, {}, "attire head")
check(head.image.filepath.endswith(os.path.join("Default_Attire", "Textures", "head_color.dds")), "The attire's own (painted) head colour replaces the clean one")
check(shader.inputs["Base Color"].links[0].from_node is head, "No extra nodes for a head colour swap")

# 3. Identical head colour in the attire: nothing changes.
root = tempfile.mkdtemp()
char = build(root, {"head_color.dds": b"same"}, {"head_color.dds": b"same"})
material, head, shader, report = run(char, {}, "same head")
check(head.image.filepath == "C:/png/head_color.png" and "no face paint layer" in report, "An identical attire head colour is left alone and reported as no paint")

# 4. Hash-named paint referenced by the head material.
root = tempfile.mkdtemp()
ref = bytes.fromhex("9d3058a98b10d164")
char = build(root, {}, {"head_color.dds": b"h", "64d1108ba958309d.dds": b"p", "1111111111111111.dds": b"x",
                        "head.jmtl": b"\x14\x00\x00\x00Mate" + b"\x00" * 4 + ref[::-1] + b"\x00" * 8})
material, head, shader, report = run(char, {"64d1108ba958309d.dds": [0.0] * 50 + [1.0] * 50}, "hashed paint")
check("64d1108ba958309d" in report and "1111111111111111" not in report, "Only a hash-named texture the head material references is a candidate")
check(shader.inputs["Base Color"].links[0].from_node.type == "MIX", "The referenced paint layer is applied")

# 5. A paint-named texture with no alpha at all is not laid over the face.
root = tempfile.mkdtemp()
char = build(root, {"paint_mask.dds": b"m"}, {"head_color.dds": b"h"})
material, head, shader, report = run(char, {}, "mask")
check(shader.inputs["Base Color"].links[0].from_node is head, "A *_mask texture is skipped")
print("PASS", passed, "face paint checks")
