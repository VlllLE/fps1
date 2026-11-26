"""Render posed previews of the Unity soldier holding / dying.

The pose JSON is written by the EditMode test `PoseDumpTests` (env var POSE_OUT=<path>) from the same
HumanoidAnimator / HumanoidDeathAnimator code the game runs; this script only skins Soldier.glb with those bone
transforms and renders them, so previews match what the procedural animation does.

    blender -b --python Tools/Blender/preview_poses.py -- poses.json out_dir [engine] [name_filter]

Unity (left-handed, +Y up, +Z forward) -> Blender glTF import: (x, y, z) -> (-x, -z, y).
"""

import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOLDIER = os.path.join(REPO, "Assets", "Characters", "Models", "Soldier.glb")
WEAPONS = os.path.join(REPO, "Assets", "Weapons", "Models")

# Unity -> Blender change of basis (a reflection, so rotations are conjugated rather than copied).
M = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))
M4 = M.to_4x4()


def u2b(p):
    return M @ Vector(p)


def rot_u2b(q):
    r = Quaternion((q[3], q[0], q[1], q[2])).to_matrix()
    return M @ r @ M.transposed()


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def setup(engine, res):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.view_settings.view_transform = "Standard"
    if engine == "CYCLES":
        sc.render.engine = "CYCLES"
        sc.cycles.device = "CPU"
        sc.cycles.samples = 24
        sc.cycles.use_denoising = True
    else:
        sc.render.engine = "BLENDER_WORKBENCH"
        sc.display.shading.light = "STUDIO"
        sc.display.shading.color_type = "MATERIAL"
        sc.display.shading.show_shadows = True
        sc.display.shading.show_cavity = True
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.16, 0.18, 0.22, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    sc.world = world
    bpy.ops.mesh.primitive_plane_add(size=10, location=(0, 0, -0.002))
    floor = bpy.context.active_object
    mat = bpy.data.materials.new("Floor")
    mat.diffuse_color = (0.22, 0.23, 0.25, 1)
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.22, 0.23, 0.25, 1)
    floor.data.materials.append(mat)
    if engine == "CYCLES":
        for name, loc, energy in (("Key", (-2.2, 2.4, 2.6), 600), ("Fill", (2.6, 2.0, 1.4), 220), ("Top", (0, 0, 3.4), 200)):
            d = bpy.data.lights.new(name, "AREA")
            d.energy, d.size = energy, 2.0
            o = bpy.data.objects.new(name, d)
            sc.collection.objects.link(o)
            o.location = loc
            o.rotation_euler = (Vector((0, 0, 1)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam, cam_data


def import_soldier():
    bpy.ops.import_scene.gltf(filepath=SOLDIER)
    return next(o for o in bpy.data.objects if o.type == "ARMATURE")


def pose_armature(arm, bones):
    order = []

    def visit(b):
        order.append(b)
        for c in b.children:
            visit(c)

    for b in arm.data.bones:
        if b.parent is None:
            visit(b)
    for b in order:
        data = bones.get(b.name)
        if data is None:
            continue
        rest3 = b.matrix_local.to_3x3()
        mat = Matrix.Translation(u2b(data["p"])) @ (rot_u2b(data["q"]) @ rest3).to_4x4()
        arm.pose.bones[b.name].matrix = mat
        bpy.context.view_layer.update()


def import_gun(model, pose):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(WEAPONS, model, model + ".glb"))
    new = [o for o in bpy.data.objects if o not in before]
    root = bpy.data.objects.new("GunRoot", None)
    bpy.context.scene.collection.objects.link(root)
    for o in new:
        if o.parent is None:
            o.parent = root
    root.matrix_world = Matrix.Translation(u2b(pose["p"])) @ rot_u2b(pose["q"]).to_4x4()
    for o in new:
        if o.type == "MESH" and "magazine" in o.name.lower():
            o.location += u2b(pose["mag"])
        if o.type == "MESH" and "bolt" in o.name.lower() and any(abs(v) > 1e-5 for v in pose["bolt"]):
            o.location += u2b(pose["bolt"])
    return root


def look(cam, cam_data, loc, target, ortho=None, lens=60):
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    if ortho:
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = ortho
    else:
        cam_data.type = "PERSP"
        cam_data.lens = lens


VIEWS = {
    # name: (unity camera offset from target, target (unity), ortho scale or None)
    "side": ((3.0, 0.0, 0.0), 1.9, 1.7),
    "front": ((1.7, 0.4, 2.4), 1.8, None),
    "wide": ((3.6, 0.9, 1.0), 1.0, 2.6),
    "frontwide": ((0.0, 0.9, 3.6), 1.0, 2.6),
    "close": ((3.0, 0.0, 0.0), 0.0, 0.85),
    "closefront": ((1.6, 0.5, 2.0), 0.0, 0.85),
    "closetop": ((0.0, 3.0, 0.0), 0.0, 0.85),
    "closerear": ((1.5, 0.5, -1.7), 0.0, 0.85),
    "upper": ((3.0, 0.0, 0.0), 0.0, 1.25),
    "upperrear": ((2.0, 0.3, -2.4), 0.0, 1.25),
    "upperfront": ((2.2, 0.3, 2.2), 0.0, 1.25),
}


def render_scene(scene, out_dir, engine, res):
    reset()
    cam, cam_data = setup(engine, res)
    arm = import_soldier()
    pose_armature(arm, scene["bones"])
    if scene.get("gun"):
        import_gun(scene["gun"]["model"], scene["gun"])
    for view in scene.get("views", [scene.get("view", "side")]):
        offset, ty, ortho = VIEWS[view]
        centre = scene.get("target", [0.0, 1.05, 0.15])
        if view.startswith("close") and scene.get("gun"):
            g = scene["gun"]
            q = g["q"]
            fwd = rot_u2b(q) @ u2b((0, 0, 0.22))
            c = Vector(g["p"]) + Vector((-fwd.x, fwd.z, -fwd.y))
            centre = [c.x, c.y, c.z]
        target = u2b(centre)
        look(cam, cam_data, u2b((centre[0] + offset[0], centre[1] + offset[1], centre[2] + offset[2])), target, ortho=ortho and ortho * scene.get("zoom", 1.0))
        bpy.context.scene.render.filepath = os.path.join(out_dir, "%s-%s.png" % (scene["name"], view))
        bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    path, out_dir = argv[0], argv[1]
    engine = argv[2] if len(argv) > 2 else "WORKBENCH"
    filt = argv[3].split(",") if len(argv) > 3 else None
    os.makedirs(out_dir, exist_ok=True)
    with open(path) as fh:
        scenes = json.load(fh)
    res = (1000, 900)
    for scene in scenes:
        if filt and not any(f in scene["name"] for f in filt):
            continue
        render_scene(scene, out_dir, engine, res)


main()
