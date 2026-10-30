"""Secondary motion for hair bone chains while a render plays its frames.

Taunt clips animate the body only; the game moves hair with live physics.
Each hair bone (name prefix ``Hair_`` by default) is treated as a damped
spring at its tail: it lags behind the animated head, swings and settles.
The animated pose is never lost: every frame starts again from the pose the
motion (or the rest pose) gives that bone, and only the hair bones' own
basis matrices are written.  Settings live in ``hair_motion.cfg`` beside the
render entry point; without ``enabled 1`` nothing is registered.
"""

import os

REVISION = 1
CONFIG_NAME = "hair_motion.cfg"
DEFAULTS = {
    "enabled": "0",
    "prefixes": "Hair_",
    "stiffness": "0.12",   # 0..1: pull back towards the animated pose each frame
    "damping": "0.85",     # 0..1: share of last frame's swing that carries over
    "gravity": "0.05",     # sag per frame, in bone lengths
    "max_bones": "2000",
}
_active = None


def read_settings(directory):
    """Parse ``key value`` lines (``#`` comments) over the defaults."""
    settings = dict(DEFAULTS)
    path = os.path.join(directory, CONFIG_NAME)
    try:
        with open(path, "r", encoding="utf-8") as handle:
            for raw in handle:
                line = raw.split("#", 1)[0].strip()
                if not line:
                    continue
                parts = line.split(None, 1)
                if len(parts) == 2 and parts[0] in DEFAULTS:
                    settings[parts[0]] = parts[1].strip()
    except OSError:
        return None
    return settings


def _clamp(value, low, high):
    return max(low, min(high, value))


def _close(first, second, tolerance=1e-4):
    # Blender stores a written basis as loc/rot/scale, so reading it back can
    # differ in the last float bits; only a real re-key counts as a change.
    return all(abs(first[row][column] - second[row][column]) <= tolerance
               for row in range(4) for column in range(4))


class HairMotion:
    """Holds per-armature spring state between consecutive frames."""

    def __init__(self, mathutils, settings):
        self.mathutils = mathutils
        self.prefixes = tuple(item for item in settings["prefixes"].replace(",", " ").split() if item)
        self.stiffness = _clamp(float(settings["stiffness"]), 0.0, 1.0)
        self.damping = _clamp(float(settings["damping"]), 0.0, 1.0)
        self.gravity = _clamp(float(settings["gravity"]), 0.0, 5.0)
        self.max_bones = int(settings["max_bones"])
        self.state = {}
        self.last_frame = None
        self.reported = False

    def _is_hair(self, name):
        return name.startswith(self.prefixes)

    def _plan(self, obj):
        """Hair bones, parents first; bones with non-standard inheritance are left alone."""
        bones = obj.data.bones
        order = []
        for bone in bones:
            if not self._is_hair(bone.name):
                continue
            if not bone.use_inherit_rotation or getattr(bone, "inherit_scale", "FULL") != "FULL":
                continue
            depth, parent = 0, bone.parent
            while parent is not None:
                depth, parent = depth + 1, parent.parent
            order.append((depth, bone.name))
        order.sort()
        return [name for _, name in order][:self.max_bones]

    def step(self, scene):
        frame = scene.frame_current
        if frame == self.last_frame:
            return  # Already simulated (render_pre after frame_change_post).
        restart = self.last_frame is None or frame != self.last_frame + 1
        self.last_frame = frame
        total = 0
        for obj in scene.objects:
            if getattr(obj, "type", None) != "ARMATURE" or obj.pose is None:
                continue
            total += self._step_armature(obj, restart)
        if not self.reported:
            self.reported = True
            print("  hair motion: %d hair bone(s) simulated" % total, flush=True)

    def _step_armature(self, obj, restart):
        Matrix, Vector = self.mathutils.Matrix, self.mathutils.Vector
        key = obj.name_full if hasattr(obj, "name_full") else obj.name
        entry = self.state.get(key)
        if entry is None:
            names = self._plan(obj)
            if not names:
                self.state[key] = {"names": []}
                return 0
            entry = {"names": names, "original": {}, "written": {}, "pos": {}, "prev": {}}
            for name in names:
                entry["original"][name] = obj.pose.bones[name].matrix_basis.copy()
            self.state[key] = entry
        names = entry["names"]
        if not names:
            return 0
        world = obj.matrix_world
        inverse = world.inverted_safe()
        new_pose = {}
        for name in names:
            pbone = obj.pose.bones[name]
            bone = pbone.bone
            current = pbone.matrix_basis
            written = entry["written"].get(name)
            # If the motion re-keyed this bone, honour the newly evaluated basis.
            animated = current.copy() if written is not None and not _close(current, written) else entry["original"][name]
            parent = bone.parent
            if parent is None:
                parent_pose, relative = Matrix.Identity(4), bone.matrix_local.copy()
            else:
                parent_pose = new_pose.get(parent.name)
                if parent_pose is None:
                    parent_pose = obj.pose.bones[parent.name].matrix.copy()
                relative = parent.matrix_local.inverted_safe() @ bone.matrix_local
            target = parent_pose @ relative @ animated
            length = bone.length
            head = target.to_translation()
            tail = target @ Vector((0.0, length, 0.0))
            head_w, target_w = world @ head, world @ tail
            span = (target_w - head_w).length
            if span <= 1e-9:
                new_pose[name] = target
                continue
            if restart or name not in entry["pos"]:
                entry["pos"][name] = target_w.copy()
                entry["prev"][name] = target_w.copy()
            pos, prev = entry["pos"][name], entry["prev"][name]
            moved = pos + (pos - prev) * self.damping + Vector((0.0, 0.0, -self.gravity * span))
            moved += (target_w - moved) * self.stiffness
            direction = moved - head_w
            if direction.length <= 1e-9:
                direction = target_w - head_w
            moved = head_w + direction.normalized() * span
            entry["prev"][name], entry["pos"][name] = pos, moved
            wanted = (inverse @ moved) - head
            rotation = (tail - head).rotation_difference(wanted).to_matrix().to_4x4()
            pose = Matrix.Translation(head) @ rotation @ Matrix.Translation(-head) @ target
            new_pose[name] = pose
            basis = (parent_pose @ relative).inverted_safe() @ pose
            pbone.matrix_basis = basis
            entry["written"][name] = basis.copy()
        return len(names)


def register(directory):
    """Install frame handlers when hair_motion.cfg enables them."""
    global _active
    settings = read_settings(directory)
    if settings is None or settings["enabled"] != "1":
        return None
    import bpy
    import mathutils

    motion = HairMotion(mathutils, settings)
    handlers = bpy.app.handlers

    def on_frame(scene, depsgraph=None):
        try:
            motion.step(scene)
        except Exception as error:  # A cosmetic pass must never stop a render.
            print("  hair motion: skipped this frame:", str(error), flush=True)

    persistent = getattr(handlers, "persistent", None)
    if persistent is not None:
        on_frame = persistent(on_frame)
    on_frame.gorilla_hair_motion_revision = REVISION
    for name in ("frame_change_post", "render_pre"):
        collection = getattr(handlers, name, None)
        if collection is None:
            continue
        for previous in list(collection):
            if getattr(previous, "gorilla_hair_motion_revision", None) is not None:
                collection.remove(previous)
        collection.append(on_frame)
    _active = motion
    print("  hair motion: enabled (stiffness %.2f, damping %.2f, gravity %.2f)"
          % (motion.stiffness, motion.damping, motion.gravity), flush=True)
    return motion
