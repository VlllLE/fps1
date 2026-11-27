"""Retargets a downloaded, rigged humanoid onto the game's 19-bone skeleton and exports an FBX.

Run (Blender 4.x, background):
    blender -b --factory-startup --python Tools/Blender/real_characters/retarget.py -- <Look> <out_dir> [preview_dir]

The source GLBs live in Assets/Characters/Looks/Source~ (see Assets/Characters/Looks/CREDITS.md). The game binds
imported bodies with translation-only bindposes taken from HumanoidParts rest positions, so the exported mesh has to be
in the game's rest pose: arms hanging straight down, joints close to the game's rest joint positions. Each source
rig is therefore re-posed here by linear blend skinning with one affine matrix per game bone, then re-skinned to
``soldier_lib.rig.build_armature``'s skeleton.
"""

import math
import os
import random
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))

from soldier_lib import rig  # noqa: E402
from soldier_lib.geometry import BONES, U2B  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SOURCE_DIR = os.path.join(REPO, "Assets", "Characters", "Looks", "Source~")

DOWN = Vector((0.0, 0.0, -1.0))

QUATERNIUS = {
    "Hips": "Hips", "Spine": "Abdomen", "Chest": "Torso", "Neck": "Neck", "Head": "Head",
    "ShoulderL": "Shoulder.L", "ShoulderR": "Shoulder.R",
    "UpperArmL": "UpperArm.L", "UpperArmR": "UpperArm.R", "ForearmL": "LowerArm.L", "ForearmR": "LowerArm.R",
    "HandL": "Wrist.L", "HandR": "Wrist.R",
    "ThighL": "UpperLeg.L", "ThighR": "UpperLeg.R", "ShinL": "LowerLeg.L", "ShinR": "LowerLeg.R",
    "FootL": "Foot.L", "FootR": "Foot.R",
}
GAMERIG = {
    "Hips": "DEF-pelvis", "Spine": "DEF-spine", "Chest": "DEF-Chest", "Neck": "DEF-neck", "Head": "DEF-head",
    "ShoulderL": "DEF-shoulder.L", "ShoulderR": "DEF-shoulder.R",
    "UpperArmL": "DEF-arm.L", "UpperArmR": "DEF-arm.R", "ForearmL": "DEF-lowerarm.L", "ForearmR": "DEF-lowerarm.R",
    "HandL": "DEF-hand.L", "HandR": "DEF-hand.R",
    "ThighL": "DEF-leg.L", "ThighR": "DEF-leg.R", "ShinL": "DEF-shin-L", "ShinR": "DEF-shin-R",
    "FootL": "DEF-foot.L", "FootR": "DEF-foot.R",
}

LOOKS = {
    "Operator": {
        "source": "swat.glb", "bones": QUATERNIUS, "scale": 1.0, "drop": ["Icosphere"],
        "ankle_height": 0.09, "arm_x": 0.19, "texture_size": 0,
    },
    "Soldier": {
        "source": "military_man.glb", "bones": GAMERIG, "scale": 1.05, "drop": ["Icosphere"],
        "ankle_height": None, "arm_x": 0.19, "texture_size": 1024,
    },
    "Ghillie": {
        "source": "boonie_soldier.glb", "bones": GAMERIG, "scale": 1.05, "drop": ["Icosphere"],
        "ankle_height": None, "arm_x": 0.19, "texture_size": 1024, "ghillie": True,
    },
}


def log(*args):
    print("RETARGET", *args)


def import_source(cfg):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(SOURCE_DIR, cfg["source"]))
    for o in list(bpy.data.objects):
        if o.type == "MESH" and any(d in o.name for d in cfg["drop"]):
            bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    for o in list(bpy.data.objects):
        if o.type not in ("MESH", "ARMATURE"):
            bpy.data.objects.remove(o, do_unlink=True)
    return meshes, arm


def source_to_game_map(arm, mapping):
    """Source bone name -> game bone: the nearest mapped ancestor (or Hips)."""
    inverse = {v: k for k, v in mapping.items()}
    result = {}
    for b in arm.data.bones:
        cur = b
        while cur is not None and cur.name not in inverse:
            cur = cur.parent
        result[b.name] = inverse[cur.name] if cur is not None else "Hips"
    return result


def bone_world(arm, mapping, game_bone):
    return arm.matrix_world @ arm.data.bones[mapping[game_bone]].head_local


def descendant_tip(arm, mapping, game_bone):
    """Average head of the leaf descendants of a bone (the finger tips for the hand)."""
    root = arm.data.bones[mapping[game_bone]]
    tips = [b for b in root.children_recursive if not b.children]
    if not tips:
        return arm.matrix_world @ root.tail_local
    total = Vector((0.0, 0.0, 0.0))
    for b in tips:
        total += arm.matrix_world @ b.head_local
    return total / len(tips)


def affine(translation, linear):
    m = linear.to_4x4()
    m.translation = translation
    return m


def limb_matrix(head_s, to_s, head_t, length_t):
    """Rotate the segment head_s->to_s to point straight down from head_t, stretched to length_t (None keeps it)."""
    d = to_s - head_s
    length_s = d.length
    d.normalize()
    rot = d.rotation_difference(DOWN).to_matrix()
    k = 1.0 if length_t is None else length_t / max(length_s, 1e-4)
    outer = Matrix([[d[r] * d[c] for c in range(3)] for r in range(3)])
    linear = rot @ (Matrix.Identity(3) + (k - 1.0) * outer)
    return Matrix.Translation(head_t) @ affine(Vector((0, 0, 0)), linear) @ Matrix.Translation(-head_s), length_s * k


def bone_matrices(arm, cfg):
    """One affine world-space matrix per game bone moving the (pre-scaled) source rest pose onto the game rest pose."""
    mapping = cfg["bones"]
    s = cfg["scale"]

    def src(b):
        return s * bone_world(arm, mapping, b)

    matrices = {b: Matrix.Identity(4) for b in BONES}
    thigh_game_z = U2B(Vector(rig.REST["ThighL"])).z
    delta = thigh_game_z - src("ThighL").z
    for b in ("Hips", "Spine", "Chest", "Neck", "Head", "ShoulderL", "ShoulderR"):
        matrices[b] = Matrix.Translation((0.0, 0.0, delta))
    log("upper body lift", round(delta, 4))

    upper_len = rig.REST["UpperArmL"][1] - rig.REST["ForearmL"][1]
    fore_len = rig.REST["ForearmL"][1] - rig.REST["HandL"][1]
    thigh_len = rig.REST["ThighL"][1] - rig.REST["ShinL"][1]
    shin_len = rig.REST["ShinL"][1] - rig.REST["FootL"][1]
    for side, sd in (("L", 1.0), ("R", -1.0)):
        up = src("UpperArm" + side)
        head_t = Vector((sd * cfg["arm_x"], up.y, up.z + delta))
        tip = s * descendant_tip(arm, mapping, "Hand" + side)
        segments = (("UpperArm", src("Forearm" + side), upper_len), ("Forearm", src("Hand" + side), fore_len),
                    ("Hand", tip, None))
        for bone, to_s, length in segments:
            matrices[bone + side], used = limb_matrix(src(bone + side), to_s, head_t, length)
            head_t = head_t + DOWN * used

        thigh = src("Thigh" + side)
        knee = src("Shin" + side)
        foot = src("Foot" + side)
        if cfg["ankle_height"] is not None:
            foot = Vector((foot.x, foot.y, cfg["ankle_height"] * s))
        head_t = Vector((sd * 0.11, thigh.y, thigh_game_z))
        matrices["Thigh" + side], used = limb_matrix(thigh, knee, head_t, thigh_len)
        head_t = head_t + DOWN * used
        matrices["Shin" + side], used = limb_matrix(knee, foot, head_t, shin_len)
        head_t = head_t + DOWN * used
        matrices["Foot" + side] = Matrix.Translation(head_t - foot)
    return matrices


def collect_mesh_data(meshes, arm, mapping_game):
    """World-space vertices, per-vertex game-bone weights, faces and materials for each source mesh."""
    data = []
    for obj in meshes:
        me = obj.data
        world = obj.matrix_world
        groups = {g.index: g.name for g in obj.vertex_groups}
        verts = []
        weights = []
        for v in me.vertices:
            verts.append(world @ v.co)
            folded = {}
            for g in v.groups:
                name = groups.get(g.group)
                if name is None or name not in mapping_game or g.weight <= 0.0:
                    continue
                gb = mapping_game[name]
                folded[gb] = folded.get(gb, 0.0) + g.weight
            if not folded:
                folded = {"Hips": 1.0}
            top = sorted(folded.items(), key=lambda kv: -kv[1])[:4]
            total = sum(w for _, w in top)
            weights.append({b: w / total for b, w in top})
        data.append({"obj": obj, "verts": verts, "weights": weights})
    return data


def deform(data, matrices):
    for entry in data:
        out = []
        for p, wd in zip(entry["verts"], entry["weights"]):
            acc = Vector((0.0, 0.0, 0.0))
            for b, w in wd.items():
                acc += (matrices[b] @ p) * w
            out.append(acc)
        entry["verts"] = out


def build_mesh(entry, name):
    src = entry["obj"].data
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in entry["verts"]], [], [tuple(p.vertices) for p in src.polygons])
    me.update()
    for p_new, p_old in zip(me.polygons, src.polygons):
        p_new.material_index = p_old.material_index
        p_new.use_smooth = p_old.use_smooth
    if src.uv_layers.active is not None:
        uv_new = me.uv_layers.new(name="UVMap")
        uv_old = src.uv_layers.active
        for li in range(len(src.loops)):
            uv_new.data[li].uv = uv_old.data[li].uv
    for m in src.materials:
        me.materials.append(m)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def assign_weights(obj, weights):
    for b in BONES:
        obj.vertex_groups.new(name=b)
    for vi, wd in enumerate(weights):
        for b, w in wd.items():
            obj.vertex_groups[b].add([vi], w, "REPLACE")


def export_textures(materials, out_dir, look, size):
    os.makedirs(out_dir, exist_ok=True)
    done = {}
    for m in materials:
        if not m or not m.use_nodes:
            continue
        for n in m.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image is not None:
                img = n.image
                if img.name not in done:
                    if size and (img.size[0] > size or img.size[1] > size):
                        img.scale(size, size)
                    path = os.path.join(out_dir, "%s_%s.png" % (look, "albedo" if not done else "albedo%d" % len(done)))
                    img.file_format = "PNG"
                    img.filepath_raw = path
                    img.save()
                    done[img.name] = path
                    log("texture", path, tuple(img.size))


def export_fbx(objs, arm, path):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    for o in objs + [arm]:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, use_mesh_modifiers=False,
        add_leaf_bones=False, bake_anim=False, axis_forward="-Z", axis_up="Y", global_scale=1.0,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", bake_space_transform=False,
        mesh_smooth_type="EDGE", armature_nodetype="NULL", path_mode="STRIP", use_armature_deform_only=False,
        primary_bone_axis="Y", secondary_bone_axis="X")


def bounds(objs):
    lo = Vector((1e9,) * 3)
    hi = Vector((-1e9,) * 3)
    for o in objs:
        for v in o.data.vertices:
            for i in range(3):
                lo[i] = min(lo[i], v.co[i])
                hi[i] = max(hi[i], v.co[i])
    return lo, hi


def render_views(objs, arm, out_dir, look):
    if not out_dir:
        return
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    cam = bpy.data.cameras.new("cam")
    cam_obj = bpy.data.objects.new("cam", cam)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj
    cam.type = "ORTHO"
    sun = bpy.data.lights.new("sun", "SUN")
    sun.energy = 3.0
    sun_obj = bpy.data.objects.new("sun", sun)
    sun_obj.rotation_euler = (math.radians(50), 0, math.radians(-30))
    scene.collection.objects.link(sun_obj)
    scene.world = bpy.data.worlds.new("w")
    scene.world.color = (0.55, 0.55, 0.6)
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = 520
    scene.render.resolution_y = 720
    poses = {
        "rest": {},
        "pose": {"UpperArmL": (-70, 0, 0), "UpperArmR": (-70, 0, 0), "ForearmL": (-40, 0, 0), "ForearmR": (-40, 0, 0),
                 "ThighL": (-30, 0, 0), "ThighR": (25, 0, 0), "ShinL": (40, 0, 0), "Spine": (10, 0, 0)},
    }
    views = {"front": (0, -6, 0, 90, 0, 0), "side": (6, 0, 0, 90, 0, 90), "back": (0, 6, 0, 90, 0, 180)}
    lo, hi = bounds(objs)
    cen = (lo + hi) / 2
    cam.ortho_scale = (hi.z - lo.z) * 1.2
    for pose_name, rot in poses.items():
        rig.set_pose(arm, rot)
        for view_name, (x, y, z, rx, ry, rz) in views.items():
            cam_obj.location = cen + Vector((x, y, z))
            cam_obj.rotation_euler = (math.radians(rx), math.radians(ry), math.radians(rz))
            scene.render.filepath = os.path.join(out_dir, "%s_%s_%s.png" % (look, pose_name, view_name))
            bpy.ops.render.render(write_still=True)
    rig.reset_pose(arm)


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    look, out_dir = args[0], args[1]
    preview_dir = args[2] if len(args) > 2 else None
    cfg = LOOKS[look]
    meshes, src_arm = import_source(cfg)
    to_game = source_to_game_map(src_arm, cfg["bones"])
    data = collect_mesh_data(meshes, src_arm, to_game)
    s = cfg["scale"]
    for entry in data:
        entry["verts"] = [s * v for v in entry["verts"]]
    matrices = bone_matrices(src_arm, cfg)
    for b in BONES:
        log("bone", b, tuple(round(x, 3) for x in matrices[b].translation))
    deform(data, matrices)

    materials = []
    new_objs = []
    for entry in data:
        o = build_mesh(entry, "%s_%s" % (look, entry["obj"].name.replace(".", "_").replace(" ", "_")))
        assign_weights(o, entry["weights"])
        new_objs.append(o)
        for m in o.data.materials:
            if m not in materials:
                materials.append(m)
    for o in meshes:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.objects.remove(src_arm, do_unlink=True)

    if cfg.get("ghillie"):
        sys.path.insert(0, HERE)
        import ghillie
        new_objs = ghillie.apply(new_objs, look)
        materials = []
        for o in new_objs:
            for m in o.data.materials:
                if m not in materials:
                    materials.append(m)

    lo, hi = bounds(new_objs)
    log("bounds", tuple(round(x, 3) for x in lo), tuple(round(x, 3) for x in hi), "height", round(hi.z - lo.z, 3))

    arm = rig.build_armature(look + "_Armature")
    for o in new_objs:
        o.parent = arm
        mod = o.modifiers.new("Armature", "ARMATURE")
        mod.object = arm

    export_textures(materials, out_dir, look, cfg["texture_size"])
    export_fbx(new_objs, arm, os.path.join(out_dir, look + ".fbx"))
    with open(os.path.join(out_dir, look + ".height.txt"), "w") as fh:
        fh.write("%.4f" % (hi.z - lo.z))
    render_views(new_objs, arm, preview_dir, look)


main()
