"""Renders a mock of the first-person held pose for a gun generated in Tools/Blender.

Reproduces the Unity viewmodel chain without the Editor: camera (FOV 90) -> WeaponHolder (0.3, -0.2, 0.5) ->
weapon at identity, next to the two arms that ProceduralFirstPersonArms solves (same shoulder anchors, grip
points, bone lengths, pole vectors and hand axes as the C# code). Used to check that the model's grip and
foregrip land where the hands are.

    blender -b --factory-startup --python Tools/Blender/preview_gun_held.py -- \
        --gun bolt|dmr --renders /path/to/dir [--samples 48]

Unity (left-handed, +Z forward, +X right) is mapped into Blender with (x, y, z) -> (-x, -z, y).
"""

import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gunlib as gl  # noqa: E402
from gunlib import U, make_material  # noqa: E402

arg = gl.parse_args()
GUN = arg("--gun", "bolt")
RENDER_DIR = arg("--renders", os.path.abspath("renders"))
SAMPLES = int(arg("--samples", "48"))

script_name = {"bolt": "build_bolt_sniper.py", "dmr": "build_dmr.py"}[GUN]
prefix = {"bolt": "bolt-sniper", "dmr": "dmr"}[GUN]
src = open(os.path.join(HERE, script_name)).read()
exec(compile(src[:src.rindex("\nmain()")], script_name, "exec"))

HOLDER = Vector((0.3, -0.2, 0.5))
ARM_SCALE = 1.3
UPPER_LEN = 0.29 * ARM_SCALE
LOWER_LEN = 0.27 * ARM_SCALE
SHOULDERS = {1: Vector((0.24, -0.52, -0.10)), -1: Vector((-0.12, -0.55, 0.02))}
GRIPS = {1: Vector((0.0, -0.09, -0.05)), -1: Vector((-0.03, -0.08, 0.03))}

M = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))


def solve_arm(side):
    shoulder = SHOULDERS[side]
    target = HOLDER + GRIPS[side]
    to_t = target - shoulder
    dist = to_t.length
    d = to_t / dist
    along = (UPPER_LEN ** 2 - LOWER_LEN ** 2 + dist ** 2) / (2 * dist)
    height = math.sqrt(max(0.0, UPPER_LEN ** 2 - along ** 2))
    pole = Vector((side * 0.35, -1.0, -0.2))
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
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, location=(0, 0, 0))
    o = bpy.context.active_object
    o.name = name
    o.data.transform(Matrix.Diagonal((radii_unity[0], radii_unity[2], radii_unity[1], 1.0)))
    bpy.ops.object.shade_smooth()
    if axes is None:
        axes = (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
    ux, uy, uz = axes
    r_unity = Matrix(((ux.x, uy.x, uz.x), (ux.y, uy.y, uz.y), (ux.z, uy.z, uz.z)))
    m = (M @ r_unity @ M.inverted()).to_4x4()
    m.translation = U(centre_unity)
    o.matrix_world = m
    o.data.materials.append(make_material(name + "Mat", colour, 0.0, rough))
    return o


def limb(name, a, b, radius, colour):
    mid = (a + b) * 0.5
    direction = b - a
    axes_y = direction.normalized()
    ref = Vector((0, 0, 1)) if abs(axes_y.z) < 0.9 else Vector((1, 0, 0))
    x = axes_y.cross(ref).normalized()
    z = x.cross(axes_y)
    return ellipsoid(name, mid, (radius, direction.length * 0.5 + radius * 0.5, radius), colour, (x, axes_y, z))


def add_arm(side, label):
    skin = (0.78, 0.62, 0.52)
    sleeve = (0.22, 0.32, 0.42)
    shoulder, elbow, wrist, axes = solve_arm(side)
    hx, hy, hz = axes
    sc = ARM_SCALE
    ellipsoid(label + "Palm", wrist + hy * (-0.045 * sc), (0.034 * sc, 0.05 * sc, 0.02 * sc), skin, axes)
    ellipsoid(label + "Fingers", wrist + hy * (-0.095 * sc) + hz * (0.004 * sc), (0.03 * sc, 0.03 * sc, 0.016 * sc), skin, axes)
    ellipsoid(label + "Thumb", wrist + hx * (-0.03 * sc * side * -1.0) + hy * (-0.05 * sc) + hz * (0.018 * sc),
              (0.012 * sc, 0.03 * sc, 0.012 * sc), skin, axes)
    limb(label + "Forearm", elbow, wrist, 0.045 * sc * 0.9, sleeve)
    limb(label + "UpperArm", shoulder, elbow, 0.052 * sc * 0.9, sleeve)
    return wrist


def main_preview():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    gl.make_materials()
    asm, info = build_assembly()
    objects = asm.build_groups()
    for o in objects:
        gl.shade_and_unwrap(o)
        gl.prune_unused_slots(o)

    holder = bpy.data.objects.new("WeaponHolder", None)
    bpy.context.scene.collection.objects.link(holder)
    holder.location = U(HOLDER)
    for o in objects:
        o.parent = holder

    right = add_arm(1, "R")
    left = add_arm(-1, "L")

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = int(arg("--res", "100"))
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
    cam_data.clip_start = 0.05
    cam.location = (0, 0, 0)
    cam.rotation_euler = Vector((0, -1, 0)).to_track_quat("-Z", "Y").to_euler()
    gl.render_to(os.path.join(RENDER_DIR, "%s-held-fp.png" % prefix))

    cam_data.sensor_fit = "AUTO"
    cam_data.angle = math.radians(40.0)
    cam.location = U(Vector((1.1, 0.05, 0.40)))
    target = U(Vector((0.3, -0.12, 0.75)))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    gl.render_to(os.path.join(RENDER_DIR, "%s-held-side.png" % prefix))

    cam.location = U(Vector((-0.55, 0.25, 0.10)))
    target = U(Vector((0.30, -0.15, 0.62)))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.angle = math.radians(52.0)
    gl.render_to(os.path.join(RENDER_DIR, "%s-held-over-shoulder.png" % prefix))


os.makedirs(RENDER_DIR, exist_ok=True)
main_preview()
