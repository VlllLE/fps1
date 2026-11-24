"""Renders a mock of the first-person held pose for the karambit.

Reproduces the Unity viewmodel chain so the grip offset can be checked without the Unity Editor:
camera (FOV 90) -> WeaponHolder (0.3, -0.2, 0.5) -> Karambit/Model pose, next to the arms that
ProceduralFirstPersonArms solves (two-bone IK from the shoulder anchor to the right grip point on
the holder, same bone lengths, pole and hand axes as the C# code).

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
# Overridable for experiments: --pos x,y,z --quat x,y,z,w
HOLDER = Vector((0.3, -0.2, 0.5))
MODEL_POS = Vector(tuple(float(v) for v in arg("--pos", "0,0,0").split(",")))
MODEL_QUAT_XYZW = tuple(float(v) for v in arg("--quat", "0,0,0,1").split(","))

ARM_SCALE = 1.3
UPPER_LEN = 0.29 * ARM_SCALE
LOWER_LEN = 0.27 * ARM_SCALE
RIGHT_SHOULDER = Vector((0.24, -0.52, -0.10))
RIGHT_GRIP = Vector((0.0, -0.09, -0.05))   # holder space, ProceduralFirstPersonArms.rightGrip
POLE = Vector((0.35, -1.0, -0.2))


def solve_arm():
    target = HOLDER + RIGHT_GRIP
    to_t = target - RIGHT_SHOULDER
    dist = to_t.length
    d = to_t / dist
    along = (UPPER_LEN ** 2 - LOWER_LEN ** 2 + dist ** 2) / (2 * dist)
    height = math.sqrt(max(0.0, UPPER_LEN ** 2 - along ** 2))
    bend = POLE - d * POLE.dot(d)
    bend.normalize()
    elbow = RIGHT_SHOULDER + d * along + bend * height
    wrist = RIGHT_SHOULDER + d * dist
    lower_dir = (wrist - elbow).normalized()
    roll = Vector((-1.0, 0.0, 0.0))
    fwd = roll - lower_dir * roll.dot(lower_dir)
    fwd.normalize()
    up = -lower_dir
    x_axis = up.cross(fwd).normalized()
    y_axis = fwd.cross(x_axis)
    return elbow, wrist, (x_axis, y_axis, fwd)

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


def ellipsoid(name, centre_unity, radii_unity, colour, axes=None, rough=0.6):
    """Ellipsoid with local radii; axes = unity-space basis (x, y, z) of the part, if rotated."""
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


def main_preview():
    knife = build_knife()
    knife.matrix_world = unity_pose_matrix(HOLDER + MODEL_POS, MODEL_QUAT_XYZW)

    skin = (0.78, 0.62, 0.52)
    sleeve = (0.22, 0.32, 0.42)
    elbow, wrist, axes = solve_arm()
    hx, hy, hz = axes
    palm = wrist + hy * (-0.045 * ARM_SCALE)
    sc = ARM_SCALE
    ellipsoid("Palm", wrist + hy * (-0.045 * sc), (0.034 * sc, 0.05 * sc, 0.02 * sc), skin, axes)
    ellipsoid("Fingers", wrist + hy * (-0.095 * sc) + hz * (0.004 * sc), (0.03 * sc, 0.03 * sc, 0.016 * sc), skin, axes)
    ellipsoid("Thumb", wrist + hx * (-0.03 * sc) + hy * (-0.05 * sc) + hz * (0.018 * sc), (0.012 * sc, 0.03 * sc, 0.012 * sc), skin, axes)
    limb("Forearm", elbow, wrist, 0.045 * sc * 0.9, sleeve)
    limb("UpperArm", RIGHT_SHOULDER, elbow, 0.052 * sc * 0.9, sleeve)
    print("KARAMBIT_PALM_HOLDER_LOCAL", tuple(round(v, 4) for v in (palm - HOLDER)))
    print("KARAMBIT_HAND_AXES", [tuple(round(v, 3) for v in a) for a in axes])
    HAND = wrist

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
