"""Builds the detailed karambit knife and exports it for Unity.

Run headless:

    blender -b --factory-startup --python Tools/Blender/build_karambit.py -- \
        --out Assets/Weapons/Models/Karambit --renders /path/to/render/dir

Coordinate convention (matches the Unity weapon prefabs, +Z forward, +Y up, metres):
the knife is authored in Blender space where the blade points toward Blender -Y and up is +Z.
The FBX exporter maps Blender -Y to Unity +Z (axis_forward=-Z, axis_up=Y, baked transform),
so the imported mesh has identity rotation, scale 1 and the blade along +Z. The origin is the
centre of the grip, so the Unity prefab only needs to place that point inside the hand.

All dimensions are real-world metres; total length is about 0.20 m.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        if i + 1 < len(ARGS):
            return ARGS[i + 1]
    return default


OUT_DIR = arg("--out", os.path.abspath("karambit_out"))
RENDER_DIR = arg("--renders", None)
RENDER_SAMPLES = int(arg("--samples", "48"))

# ---------------------------------------------------------------------------------------------
# Dimensions (metres). Axes below are (side, up, fwd); fwd is the blade direction.
# ---------------------------------------------------------------------------------------------
HANDLE_REAR = -0.045
HANDLE_FRONT = 0.044
HANDLE_HALF_W = 0.0125
HANDLE_HALF_H = 0.0170

LINER_T = 0.0025
BOLSTER_FRONT = 0.0600

BLADE_F0 = 0.050           # blade base along the handle axis (inside the bolster)
BLADE_R = 0.056            # spine radius of curvature
BLADE_SWEEP = 1.70         # radians of curvature along the spine (~97 degrees)
BLADE_SPINE_U = 0.0165     # spine height above the handle axis at the base
BLADE_W_MAX = 0.031        # widest point of the blade (the belly)
BLADE_BELLY_T = 0.28       # where along the blade the belly peaks
BLADE_HALF_T = 0.0021      # half thickness of the spine near the base
FALSE_EDGE_START = 0.50    # sharpened false edge begins here along the spine

RING_MAJOR = 0.0158
RING_MINOR_U = 0.0036
RING_MINOR_X = 0.0046
RING_FRONT_F = HANDLE_REAR - LINER_T - 0.0010   # centre of the ring tube where it meets the liner
RING_CENTER_U = -0.003

MAT_STEEL, MAT_HANDLE, MAT_ACCENT = 0, 1, 2


def P(s, u, f):
    """(side, up, forward) -> Blender world coordinates (blade forward = -Y)."""
    return Vector((s, -f, u))


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


# ---------------------------------------------------------------------------------------------
# Materials (Principled BSDF, values mirror the existing Unity Standard materials)
# ---------------------------------------------------------------------------------------------
def make_material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = (color[0], color[1], color[2], 1.0)
    mat.metallic = metallic
    mat.roughness = roughness
    return mat


def make_materials():
    return [
        make_material("KarambitBlade", (0.80, 0.82, 0.85), 0.80, 0.22),
        make_material("KarambitHandle", (0.07, 0.10, 0.08), 0.0, 0.62),
        make_material("KarambitAccent", (0.90, 0.30, 0.06), 0.55, 0.38),
    ]


# ---------------------------------------------------------------------------------------------
# Geometry helpers
# ---------------------------------------------------------------------------------------------
def superellipse(hw, hh, sides, power=0.62, cx=0.0, cu=0.0):
    pts = []
    for k in range(sides):
        a = 2.0 * math.pi * k / sides
        c, s = math.cos(a), math.sin(a)
        x = hw * math.copysign(abs(c) ** power, c)
        u = hh * math.copysign(abs(s) ** power, s)
        pts.append((cx + x, cu + u))
    return pts


def new_part(materials, mat_index):
    return {"bm": bmesh.new(), "mat": mat_index}


def add_face(part, verts):
    try:
        f = part["bm"].faces.new(verts)
    except ValueError:
        return None
    f.material_index = part["mat"]
    return f


def loft_closed(part, sections, cap_start=True, cap_end=True):
    """sections: list of lists of Blender-space Vector rings (same length)."""
    bm = part["bm"]
    rings = [[bm.verts.new(p) for p in ring] for ring in sections]
    n = len(rings[0])
    for i in range(len(rings) - 1):
        for k in range(n):
            k2 = (k + 1) % n
            add_face(part, [rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]])
    if cap_start:
        add_face(part, rings[0])
    if cap_end:
        add_face(part, list(reversed(rings[-1])))
    return rings


def finish_part(part, name, materials):
    bm = part["bm"]
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-7)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for m in materials:
        mesh.materials.append(m)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


# ---------------------------------------------------------------------------------------------
# Parts
# ---------------------------------------------------------------------------------------------
def groove_factor(f):
    centres = (-0.0315, -0.0125, 0.0065, 0.0255)
    g = 0.0
    for c in centres:
        g += math.exp(-(((f - c) / 0.0046) ** 2))
    return g


def build_handle(materials):
    part = new_part(materials, MAT_HANDLE)
    sections = []
    count = 64
    for i in range(count + 1):
        u = i / count
        f = HANDLE_REAR + (HANDLE_FRONT - HANDLE_REAR) * u
        taper = 0.93 + 0.07 * u
        flare = 1.0 + 0.05 * smoothstep(0.82, 1.0, u) - 0.06 * smoothstep(0.12, 0.0, u)
        g = groove_factor(f)
        hw = HANDLE_HALF_W * taper * flare * (1.0 - 0.05 * g)
        hh = HANDLE_HALF_H * taper * flare * (1.0 - 0.11 * g)
        ring = [P(s, v, f) for s, v in superellipse(hw, hh, 28)]
        sections.append(ring)
    loft_closed(part, sections)
    return finish_part(part, "HandleScales", materials)


def build_liners_and_bolster(materials):
    part = new_part(materials, MAT_ACCENT)

    def liner(f0, f1, scale):
        secs = []
        for f in (f0, f1):
            hw = HANDLE_HALF_W * 0.97 * scale
            hh = HANDLE_HALF_H * 0.97 * scale
            secs.append([P(s, v, f) for s, v in superellipse(hw, hh, 28)])
        loft_closed(part, secs)

    liner(HANDLE_REAR - LINER_T, HANDLE_REAR, 1.025)
    liner(HANDLE_FRONT, HANDLE_FRONT + LINER_T, 1.025)

    bolster_f0 = HANDLE_FRONT + LINER_T
    stops = [
        (bolster_f0, 1.00, 1.03),
        (bolster_f0 + 0.0030, 1.06, 1.08),
        (bolster_f0 + 0.0075, 0.90, 1.06),
        (BOLSTER_FRONT - 0.0012, 0.62, 1.00),
        (BOLSTER_FRONT, 0.46, 0.94),
    ]
    secs = []
    for f, ws, hs in stops:
        hw = HANDLE_HALF_W * ws
        hh = HANDLE_HALF_H * hs
        secs.append([P(s, v, f) for s, v in superellipse(hw, hh, 28)])
    loft_closed(part, secs)

    # Forward finger guard below the cutting edge (the "choil" hook).
    guard = []
    for f, hw, hh, cu in (
        (bolster_f0 + 0.0020, 0.0100, 0.0070, -0.0150),
        (bolster_f0 + 0.0090, 0.0075, 0.0072, -0.0188),
        (bolster_f0 + 0.0150, 0.0050, 0.0058, -0.0215),
        (bolster_f0 + 0.0185, 0.0030, 0.0030, -0.0225),
    ):
        guard.append([P(s, v, f) for s, v in superellipse(hw, hh, 20, 0.8, 0.0, cu)])
    loft_closed(part, guard)
    return finish_part(part, "BolsterGuard", materials)


def build_pins(materials):
    part = new_part(materials, MAT_STEEL)
    for f in (-0.0315, -0.0125, 0.0065, 0.0255):
        half_w = HANDLE_HALF_W * 0.93 * 1.0 + 0.0007
        for u_off in (0.0,):
            secs = []
            r = 0.0030
            for s, rr in ((-half_w - 0.0006, r * 0.75), (-half_w + 0.0003, r),
                          (half_w - 0.0003, r), (half_w + 0.0006, r * 0.75)):
                ring = []
                for k in range(18):
                    a = 2.0 * math.pi * k / 18
                    ring.append(P(s, u_off + rr * math.sin(a), f + rr * math.cos(a)))
                secs.append(ring)
            loft_closed(part, secs)
    return finish_part(part, "Pins", materials)


def build_ring(materials):
    part = new_part(materials, MAT_ACCENT)
    cf = RING_FRONT_F - RING_MAJOR
    major_seg, minor_seg = 56, 14
    grid = []
    for i in range(major_seg):
        a = 2.0 * math.pi * i / major_seg
        ring = []
        for j in range(minor_seg):
            b = 2.0 * math.pi * j / minor_seg
            radial = RING_MAJOR + RING_MINOR_U * math.cos(b)
            f = cf + radial * math.cos(a)
            u = RING_CENTER_U + radial * math.sin(a) * 1.08
            s = RING_MINOR_X * math.sin(b)
            ring.append(P(s, u, f))
        grid.append(ring)
    bm = part["bm"]
    verts = [[bm.verts.new(p) for p in ring] for ring in grid]
    for i in range(major_seg):
        i2 = (i + 1) % major_seg
        for j in range(minor_seg):
            j2 = (j + 1) % minor_seg
            add_face(part, [verts[i][j], verts[i][j2], verts[i2][j2], verts[i2][j]])
    return finish_part(part, "FingerRing", materials)


def blade_curve(t):
    """Returns (spine (u, f), radial (u, f)) for blade parameter t in [0, 1]."""
    theta = t * BLADE_SWEEP
    radial = (math.cos(theta), math.sin(theta))
    centre = (BLADE_SPINE_U - BLADE_R, BLADE_F0)
    spine = (centre[0] + radial[0] * BLADE_R, centre[1] + radial[1] * BLADE_R)
    return spine, radial


def blade_width(t):
    if t < BLADE_BELLY_T:
        k = math.sin(0.5 * math.pi * t / BLADE_BELLY_T)
        return BLADE_W_MAX * (0.80 + 0.20 * k)
    x = (t - BLADE_BELLY_T) / (1.0 - BLADE_BELLY_T)
    return BLADE_W_MAX * max(0.0, 1.0 - x ** 2.0) ** 0.85


def build_blade(materials):
    part = new_part(materials, MAT_STEEL)
    count = 110
    # Cross-section: edge, hollow grind, flat, shoulder, false-edge ridge.
    # (distance from spine as a fraction of the width, thickness factor)
    profile = [(1.00, 0.0), (0.74, 0.34), (0.50, 1.0), (0.14, 1.0), (0.0, 1.0)]
    rings = []
    for i in range(count):
        t = i / count
        spine, radial = blade_curve(t)
        w = blade_width(t)
        ht = BLADE_HALF_T * (1.0 - 0.38 * t) * (1.0 - smoothstep(0.90, 1.0, t))
        false_edge = smoothstep(FALSE_EDGE_START, 0.92, t)
        left, right = [], []
        for d, thick in profile:
            fac = thick
            if d == 0.0:
                fac = 1.0 - 0.93 * false_edge
            elif d == 0.14:
                fac = 1.0 - 0.45 * false_edge
            h = ht * fac
            u = spine[0] - radial[0] * w * d
            f = spine[1] - radial[1] * w * d
            left.append(P(-h, u, f))
            right.append(P(h, u, f))
        edge = left[0]
        ring = [edge] + left[1:] + list(reversed(right[1:]))
        rings.append(ring)

    bm = part["bm"]
    verts = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(verts[0])
    for i in range(count - 1):
        for k in range(n):
            k2 = (k + 1) % n
            add_face(part, [verts[i][k], verts[i][k2], verts[i + 1][k2], verts[i + 1][k]])
    tip_spine, _ = blade_curve(1.0)
    tip = bm.verts.new(P(0.0, tip_spine[0], tip_spine[1]))
    for k in range(n):
        k2 = (k + 1) % n
        add_face(part, [verts[count - 1][k], verts[count - 1][k2], tip])
    add_face(part, list(reversed(verts[0])))
    obj = finish_part(part, "Blade", materials)

    # Lanyard / thumb hole near the base of the blade.
    t_hole = 0.25
    spine, radial = blade_curve(t_hole)
    w = blade_width(t_hole)
    hole_u = spine[0] - radial[0] * w * 0.45
    hole_f = spine[1] - radial[1] * w * 0.45
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32, radius=0.0062, depth=0.02,
        location=P(0.0, hole_u, hole_f), rotation=Euler((0.0, math.pi / 2, 0.0)))
    cutter = bpy.context.active_object
    mod = obj.modifiers.new("hole", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.object = cutter
    mod.solver = "EXACT"
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    return obj


# ---------------------------------------------------------------------------------------------
# Assembly
# ---------------------------------------------------------------------------------------------
def shade_by_material(obj):
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        mats = {f.material_index for f in e.link_faces}
        steel_only = mats == {MAT_STEEL}
        limit = math.radians(9.0) if steel_only else math.radians(32.0)
        try:
            angle = e.calc_face_angle()
        except ValueError:
            continue
        e.smooth = angle <= limit
    bm.to_mesh(mesh)
    bm.free()


def unwrap(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.003)
    bpy.ops.object.mode_set(mode="OBJECT")


def build_knife():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    materials = make_materials()

    parts = [
        build_blade(materials),
        build_handle(materials),
        build_liners_and_bolster(materials),
        build_pins(materials),
        build_ring(materials),
    ]
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    knife = bpy.context.active_object
    knife.name = "Karambit"
    knife.data.name = "KarambitMesh"
    for o in (knife,):
        o.location = (0, 0, 0)
        o.rotation_euler = (0, 0, 0)
        o.scale = (1, 1, 1)
    shade_by_material(knife)
    unwrap(knife)
    return knife


def report(knife):
    bm = bmesh.new()
    bm.from_mesh(knife.data)
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    counts = {}
    for f in bm.faces:
        counts[f.material_index] = counts.get(f.material_index, 0) + 1
    print("KARAMBIT_STATS verts=%d faces=%d tris=%d materials=%s" % (
        len(bm.verts), len(bm.faces), sum(len(f.verts) - 2 for f in bm.faces), counts))
    print("KARAMBIT_BOUNDS_BLENDER x[%.4f,%.4f] y[%.4f,%.4f] z[%.4f,%.4f]" % (
        min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))
    print("KARAMBIT_LENGTH_M %.4f  (forward = -Y)" % (max(ys) - min(ys)))
    nonman = sum(1 for e in bm.edges if not e.is_manifold)
    print("KARAMBIT_NONMANIFOLD_EDGES %d" % nonman)
    bm.free()


def export(knife):
    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    knife.select_set(True)
    bpy.context.view_layer.objects.active = knife

    fbx_path = os.path.join(OUT_DIR, "Karambit.fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        bake_space_transform=True,
        path_mode="STRIP",
        embed_textures=False,
        use_tspace=True,
    )

    glb_path = os.path.join(OUT_DIR, "Karambit.glb")
    bpy.ops.export_scene.gltf(
        filepath=glb_path,
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
    )
    print("KARAMBIT_EXPORTED", fbx_path, glb_path)


# ---------------------------------------------------------------------------------------------
# Renders (Cycles CPU, preview quality)
# ---------------------------------------------------------------------------------------------
def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_render_scene(knife):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = RENDER_SAMPLES
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = int(arg("--res", "100"))
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"

    world = bpy.data.worlds.new("Studio")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.10, 0.11, 0.14, 1.0)
    bg.inputs["Strength"].default_value = 0.5

    bpy.ops.mesh.primitive_plane_add(size=2.0, location=(0, 0, -0.065))
    floor = bpy.context.active_object
    fm = bpy.data.materials.new("Floor")
    fm.use_nodes = True
    fb = fm.node_tree.nodes["Principled BSDF"]
    fb.inputs["Base Color"].default_value = (0.05, 0.055, 0.065, 1.0)
    fb.inputs["Roughness"].default_value = 0.45
    floor.data.materials.append(fm)

    def area(name, loc, energy, size, color=(1, 1, 1)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.size = size
        data.color = color
        obj = bpy.data.objects.new(name, data)
        bpy.context.scene.collection.objects.link(obj)
        obj.location = loc
        look_at(obj, (0, 0, 0))
        return obj

    area("Key", (-0.35, -0.25, 0.45), 2.6, 0.30)
    area("Rim", (0.35, 0.35, 0.25), 2.2, 0.25, (0.75, 0.85, 1.0))
    area("Fill", (-0.5, 0.35, 0.10), 0.8, 0.45, (1.0, 0.9, 0.8))
    area("Top", (0.0, 0.0, 0.7), 1.2, 0.5)
    strip = area("Strip", (-0.45, -0.05, 0.12), 3.0, 0.4, (1.0, 1.0, 1.0))
    strip.data.shape = "RECTANGLE"
    strip.data.size = 0.08
    strip.data.size_y = 0.5

    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    scene.camera = cam
    return cam, cam_data


def render_views():
    if not RENDER_DIR:
        return
    os.makedirs(RENDER_DIR, exist_ok=True)
    knife = bpy.data.objects["Karambit"]
    cam, cam_data = setup_render_scene(knife)
    centre = (0.0, -0.015, -0.005)
    views = [
        ("side", (-0.62, centre[1], centre[2]), 70, None),
        ("threequarter", (-0.30, -0.33, 0.17), 62, None),
        ("top", (0.0, centre[1], 0.58), 62, None),
        ("detail-blade", (-0.15, -0.12, 0.03), 62, (0.0, -0.085, -0.02)),
        ("detail-handle", (-0.09, 0.16, 0.06), 60, (0.0, 0.02, 0.0)),
    ]
    for name, loc, focal, target in views:
        cam_data.lens = focal
        cam.location = loc
        look_at(cam, target if target else centre)
        path = os.path.join(RENDER_DIR, "karambit-blender-%s.png" % name)
        bpy.context.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print("KARAMBIT_RENDER", path)


def main():
    knife = build_knife()
    report(knife)
    export(knife)
    render_views()


main()
