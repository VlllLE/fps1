"""Renders a mock of the first-person held pose for the karambit.

Reproduces the Unity viewmodel chain (all values from Assets/Prefabs/Player.prefab and
Assets/Weapons/Prefabs/Karambit.prefab) so the grip offset can be checked without the Unity
Editor: camera (FOV 90) -> WeaponHolder (0.3, -0.2, 0.5) -> Karambit/Model pose, next to the
FirstPersonArms right hand sphere and forearm.

    blender -b --factory-startup --python Tools/Blender/preview_karambit_held.py -- \
        --renders /path/to/dir [--samples 48]

Unity (left-handed, +Z forward, +X right) is mapped into Blender with (x, y, z) -> (-x, -z, y),
the same mapping the FBX export uses, so a Blender camera looking down -Y matches Unity's view.
"""

import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.argv = [sys.argv[0]] + (["--"] + sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
src = open(os.path.join(HERE, "build_karambit.py")).read()
exec(compile(src[:src.rindex("\nmain()")], "build_karambit.py", "exec"))

# Model pose in the Karambit prefab (Model child): position and rotation relative to WeaponHolder.
HOLDER = Vector((0.3, -0.2, 0.5))
MODEL_POS = Vector((0.0, -0.30, 0.33))
MODEL_QUAT_XYZW = (-0.130347, -0.051888, -0.006831, 0.990086)
# FirstPersonArms (0, -0.35, 0.35) + FP_RightHand (0.3, -0.15, 0.48).
HAND = Vector((0.3, -0.50, 0.83))
FOREARM = Vector((0.28, -0.47, 0.70))
UPPER = Vector((0.22, -0.40, 0.50))

M = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))


def to_blender(v):
    return Vector((-v.x, -v.z, v.y))


def unity_pose_matrix(pos, quat_xyzw):
    x, y, z, w = quat_xyzw
    r = Quaternion((w, x, y, z)).to_matrix()
    rb = M @ r @ M.inverted()
    m = rb.to_4x4()
    m.translation = to_blender(pos)
    return m


def ellipsoid(name, centre_unity, radii_unity, colour, rough=0.6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, location=to_blender(centre_unity))
    o = bpy.context.active_object
    o.name = name
    o.scale = (radii_unity[0], radii_unity[2], radii_unity[1])
    bpy.ops.object.shade_smooth()
    mat = make_material(name + "Mat", colour, 0.0, rough)
    o.data.materials.append(mat)
    return o


def main_preview():
    knife = build_knife()
    knife.matrix_world = unity_pose_matrix(HOLDER + MODEL_POS, MODEL_QUAT_XYZW)

    skin = (0.55, 0.38, 0.30)
    sleeve = (0.22, 0.32, 0.42)
    ellipsoid("Hand", HAND, (0.04, 0.04, 0.04), skin)
    ellipsoid("Forearm", FOREARM, (0.035, 0.12, 0.035), sleeve)
    ellipsoid("UpperArm", UPPER, (0.04, 0.14, 0.04), sleeve)

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = RENDER_SAMPLES
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("Sky")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.35, 0.40, 0.48, 1.0)
    bg.inputs["Strength"].default_value = 0.8

    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = 3.0
    so = bpy.data.objects.new("Sun", sun)
    scene.collection.objects.link(so)
    so.rotation_euler = (math.radians(55), 0, math.radians(35))

    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    cam_data.sensor_fit = "VERTICAL"
    cam_data.angle = math.radians(90.0)
    cam_data.clip_start = 0.1
    cam.location = (0, 0, 0)
    cam.rotation_euler = Vector((0, -1, 0)).to_track_quat("-Z", "Y").to_euler()  # Blender -Y == Unity +Z
    name_a = os.path.join(RENDER_DIR, "karambit-blender-held-fp.png")
    scene.render.filepath = name_a
    bpy.ops.render.render(write_still=True)
    print("KARAMBIT_RENDER", name_a)

    cam_data.sensor_fit = "AUTO"
    cam_data.angle = math.radians(32.0)
    aim = to_blender(HAND + Vector((0.0, 0.03, 0.0))) - cam.location
    cam.rotation_euler = aim.to_track_quat("-Z", "Y").to_euler()
    name_c = os.path.join(RENDER_DIR, "karambit-blender-held-fp-zoom.png")
    scene.render.filepath = name_c
    bpy.ops.render.render(write_still=True)
    print("KARAMBIT_RENDER", name_c)

    cam_data.angle = math.radians(40.0)
    cam.location = to_blender(Vector((1.0, -0.18, 0.62)))
    target = to_blender(HAND + Vector((0, 0.02, 0.02)))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    name_b = os.path.join(RENDER_DIR, "karambit-blender-held-side.png")
    scene.render.filepath = name_b
    bpy.ops.render.render(write_still=True)
    print("KARAMBIT_RENDER", name_b)


os.makedirs(RENDER_DIR, exist_ok=True)
main_preview()
