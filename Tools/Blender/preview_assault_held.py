"""Renders a mock of the first-person held pose for an assault weapon (M4A1 / SCAR / UMP45).

Reproduces the Unity viewmodel chain without the Editor: camera (vertical FOV 90) -> WeaponHolder at
(0.3, -0.2, 0.5) -> weapon prefab at identity, next to the arms ProceduralFirstPersonArms solves (two-bone IK
from the shoulder anchors to the grip points on the holder, same bone lengths, poles and hand axes as the C#).

    blender -b --factory-startup --python Tools/Blender/preview_assault_held.py -- \
        --gun m4a1 --renders /path/to/dir [--samples 48] [--fov 90]

Unity (left-handed) is mapped into Blender with (x, y, z) -> (-x, -z, y), the same mapping the FBX export uses,
so a Blender camera looking down -Y matches Unity's view. Prints the palm/finger anchor points in holder space
so the gun's grip can be checked against them.
"""

import importlib.util
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from assault_kit import arg, make_material, u2b, look_at, V  # noqa: E402

GUN = arg("--gun", "m4a1")
RENDER_DIR = arg("--renders", os.path.abspath("held_out"))
SAMPLES = int(arg("--samples", "40"))
FOV = float(arg("--fov", "90"))

HOLDER = Vector((0.3, -0.2, 0.5))
ARM_SCALE = 1.3
UPPER_LEN = 0.29 * ARM_SCALE
LOWER_LEN = 0.27 * ARM_SCALE
SHOULDERS = {1.0: Vector((0.24, -0.52, -0.10)), -1.0: Vector((-0.12, -0.55, 0.02))}
GRIPS = {1.0: Vector((0.0, -0.09, -0.05)), -1.0: Vector((-0.03, -0.08, 0.03))}   # holder space

M = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))


def to_blender(v):
    return Vector((-v.x, -v.z, v.y))


def solve_arm(side):
    shoulder = SHOULDERS[side]
    target = HOLDER + GRIPS[side]
    pole = Vector((side * 0.35, -1.0, -0.2))
    to_t = target - shoulder
    dist = to_t.length
    d = to_t / dist
    along = (UPPER_LEN ** 2 - LOWER_LEN ** 2 + dist ** 2) / (2 * dist)
    height = math.sqrt(max(0.0, UPPER_LEN ** 2 - along ** 2))
    bend = pole - d * pole.dot(d)
    bend.normalize()
    elbow = shoulder + d * along + bend * height
    wrist = shoulder + d * dist
    lower_dir = (wrist - elbow).normalized()
    roll = Vector((-side, 0.0, 0.0))
    fwd = roll - lower_dir * roll.dot(lower_dir)
    fwd.normalize()
    up = -lower_dir
    x_axis = up.cross(fwd).normalized()
    y_axis = fwd.cross(x_axis)
    return shoulder, elbow, wrist, (x_axis, y_axis, fwd)


def ellipsoid(name, centre_unity, radii_unity, colour, axes=None, rough=0.6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=40, ring_count=20, location=(0, 0, 0))
    o = bpy.context.active_object
    o.name = name
    o.data.transform(Matrix.Diagonal((radii_unity[0], radii_unity[2], radii_unity[1], 1.0)))
    bpy.ops.object.shade_smooth()
    if axes is None:
        axes = (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
    ux, uy, uz = axes
    r_unity = Matrix(((ux.x, uy.x, uz.x), (ux.y, uy.y, uz.y), (ux.z, uy.z, uz.z)))
    m = (M @ r_unity @ M.inverted()).to_4x4()
    m.translation = to_blender(centre_unity)
    o.matrix_world = m
    o.data.materials.append(make_material(name + "Mat", colour, 0.0, rough))
    return o


def limb(name, a, b, radius, colour):
    mid = (a + b) * 0.5
    direction = (b - a)
    axes_y = direction.normalized()
    ref = Vector((0, 0, 1)) if abs(axes_y.z) < 0.9 else Vector((1, 0, 0))
    x = axes_y.cross(ref).normalized()
    z = x.cross(axes_y)
    return ellipsoid(name, mid, (radius, direction.length * 0.5 + radius * 0.5, radius), colour, (x, axes_y, z))


def build_arm(side, label):
    skin = (0.78, 0.62, 0.52)
    sleeve = (0.22, 0.32, 0.42)
    shoulder, elbow, wrist, axes = solve_arm(side)
    hx, hy, hz = axes
    sc = ARM_SCALE
    palm = wrist + hy * (-0.045 * sc)
    fingers = wrist + hy * (-0.095 * sc) + hz * (0.004 * sc)
    ellipsoid(label + "Palm", palm, (0.034 * sc, 0.05 * sc, 0.02 * sc), skin, axes)
    ellipsoid(label + "Fingers", fingers, (0.03 * sc, 0.03 * sc, 0.016 * sc), skin, axes)
    ellipsoid(label + "Thumb", wrist + hx * (-0.03 * sc) + hy * (-0.05 * sc) + hz * (0.018 * sc),
              (0.012 * sc, 0.03 * sc, 0.012 * sc), skin, axes)
    limb(label + "Forearm", elbow, wrist, 0.045 * sc * 0.9, sleeve)
    limb(label + "UpperArm", shoulder, elbow, 0.052 * sc * 0.9, sleeve)
    print("HELD_%s_PALM_HOLDER_LOCAL %s FINGERS %s WRIST %s" % (
        label, tuple(round(v, 4) for v in (palm - HOLDER)), tuple(round(v, 4) for v in (fingers - HOLDER)),
        tuple(round(v, 4) for v in (wrist - HOLDER))))
    return wrist


def load_gun():
    path = os.path.join(HERE, "build_%s.py" % GUN)
    spec = importlib.util.spec_from_file_location("gun_" + GUN, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def main():
    mod = load_gun()
    objs = mod.build_gun()
    holder = bpy.data.objects.new("Holder", None)
    bpy.context.scene.collection.objects.link(holder)
    holder.location = to_blender(HOLDER)
    for o in objs:
        o.parent = holder
    rwrist = build_arm(1.0, "R")
    lwrist = build_arm(-1.0, "L")

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("Sky")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.35, 0.40, 0.48, 1.0)
    bg.inputs["Strength"].default_value = 0.9
    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = 3.2
    so = bpy.data.objects.new("Sun", sun)
    scene.collection.objects.link(so)
    so.rotation_euler = (math.radians(55), 0, math.radians(35))

    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam_data.sensor_fit = "VERTICAL"
    cam_data.angle = math.radians(FOV)
    cam_data.clip_start = 0.05
    cam.location = (0, 0, 0)
    cam.rotation_euler = Vector((0, -1, 0)).to_track_quat("-Z", "Y").to_euler()

    os.makedirs(RENDER_DIR, exist_ok=True)

    def shoot(name):
        path = os.path.join(RENDER_DIR, "%s-%s.png" % (GUN, name))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print("HELD_RENDER", path)

    shoot("held-fp")

    cam_data.sensor_fit = "AUTO"
    cam_data.angle = math.radians(46.0)
    aim = to_blender(HOLDER + Vector((0.0, 0.02, 0.10))) - cam.location
    cam.rotation_euler = aim.to_track_quat("-Z", "Y").to_euler()
    shoot("held-fp-zoom")

    cam_data.angle = math.radians(42.0)
    cam.location = to_blender(Vector((1.35, -0.12, 0.30)))
    target = to_blender(HOLDER + Vector((0.0, 0.0, 0.12)))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    shoot("held-side")

    cam.location = to_blender(Vector((0.30, 0.70, 0.35)))
    target = to_blender(HOLDER + Vector((0.0, -0.02, 0.10)))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.angle = math.radians(46.0)
    shoot("held-top")


main()
