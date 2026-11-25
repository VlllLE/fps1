"""Shared modelling, export, check and render helpers for the Blender-built gun models.

Everything is authored in Unity weapon space (x right, y up, z forward, metres, origin at the
receiver/grip) and converted to Blender space on vertex creation with (x, y, z) -> (-x, -z, y). That
mapping is exactly what the FBX exporter below undoes (axis_forward=-Z, axis_up=Y), so an imported
model has identity rotation and scale in Unity with the muzzle toward +Z.

Used by build_bolt_sniper.py and build_dmr.py (run with Blender's bundled Python, see those files).
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

METAL, POLYMER, GLASS, ACCENT = 0, 1, 2, 3
MATERIAL_NAMES = ["GunMetal", "GunPolymer", "SniperGlass", "SniperAccent"]


def U(x, y=None, z=None):
    """Unity-space point -> Blender-space Vector."""
    if y is None:
        x, y, z = x
    return Vector((-x, -z, y))


def from_blender(v):
    return Vector((-v.x, v.z, -v.y))


def unity_vec(*p):
    return Vector(p if len(p) == 3 else p[0])


# ---------------------------------------------------------------------------------------------
# Materials
# ---------------------------------------------------------------------------------------------
def make_material(name, color, metallic, roughness, **extra):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    for key, value in extra.items():
        bsdf.inputs[key.replace("_", " ")].default_value = value
    mat.diffuse_color = (color[0], color[1], color[2], 1.0)
    mat.metallic = metallic
    mat.roughness = roughness
    return mat


def make_materials(accent=(0.78, 0.30, 0.06)):
    return [
        make_material("GunMetal", (0.12, 0.125, 0.14), 0.9, 0.38),
        make_material("GunPolymer", (0.075, 0.082, 0.062), 0.0, 0.62),
        make_material("SniperGlass", (0.03, 0.09, 0.14), 0.0, 0.02, Transmission_Weight=0.9, IOR=1.5),
        make_material("SniperAccent", accent, 0.2, 0.45),
    ]


# ---------------------------------------------------------------------------------------------
# Geometry builder
# ---------------------------------------------------------------------------------------------
def _basis_from_axis(axis, up_hint=None):
    a = Vector(axis).normalized()
    up = Vector(up_hint) if up_hint is not None else Vector((0, 1, 0))
    if abs(a.dot(up.normalized())) > 0.98:
        up = Vector((1, 0, 0)) if abs(a.x) < 0.9 else Vector((0, 0, 1))
    u = (up - a * up.dot(a)).normalized()
    v = a.cross(u)
    return a, u, v


class Builder:
    """A bmesh-backed part. All input coordinates are Unity space."""

    def __init__(self, name, group="Body"):
        self.name = name
        self.group = group
        self.bm = bmesh.new()

    # -- primitives -------------------------------------------------------------------------
    def _vert(self, p):
        return self.bm.verts.new(U(p))

    def face(self, verts, mat=METAL):
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return None
        f.material_index = mat
        return f

    def loft(self, rings, mat=METAL, caps=(True, True), closed=True, mats=None):
        """rings: list of lists of Unity points (equal length). Returns the vertex rings."""
        vr = [[self._vert(p) for p in ring] for ring in rings]
        n = len(vr[0])
        span = n if closed else n - 1
        for i in range(len(vr) - 1):
            m = mats[i] if mats is not None else mat
            for k in range(span):
                k2 = (k + 1) % n
                self.face([vr[i][k], vr[i][k2], vr[i + 1][k2], vr[i + 1][k]], m)
        if closed:
            if caps[0]:
                self.face(vr[0], mat)
            if caps[1]:
                self.face(list(reversed(vr[-1])), mat)
        return vr

    def revolve(self, origin, axis, profile, seg=32, mat=METAL, mats=None, up=None,
                radmod=None, closed_profile=False, ellipse=(1.0, 1.0), caps=False):
        """Surface of revolution. profile = [(t, r), ...] along `axis` from `origin`.

        radmod(i, t, theta) multiplies the radius, used for flutes and knurling. r == 0 points collapse
        to a single vertex on the axis. Open profiles produce an open surface (closed by r=0 ends).
        """
        a, u, v = _basis_from_axis(axis, up)
        origin = Vector(origin)
        rings = []
        for i, (t, r) in enumerate(profile):
            ring = []
            for k in range(seg):
                th = 2.0 * math.pi * k / seg
                rr = r * (radmod(i, t, th) if radmod else 1.0)
                ring.append(origin + a * t + u * (math.cos(th) * rr * ellipse[0]) + v * (math.sin(th) * rr * ellipse[1]))
            rings.append(ring)
        vr = [[self._vert(p) for p in ring] for ring in rings]
        count = len(vr) if closed_profile else len(vr) - 1
        for i in range(count):
            j = (i + 1) % len(vr)
            m = mats[i] if mats is not None else mat
            for k in range(seg):
                k2 = (k + 1) % seg
                self.face([vr[i][k], vr[i][k2], vr[j][k2], vr[j][k]], m)
        if caps:
            self.face(vr[0], mat)
            self.face(list(reversed(vr[-1])), mat)
        return vr

    def cyl(self, p0, p1, r0, r1=None, seg=24, mat=METAL, caps=True, radmod=None, up=None):
        p0, p1 = Vector(p0), Vector(p1)
        axis = p1 - p0
        L = axis.length
        return self.revolve(p0, axis, [(0.0, r0), (L, r0 if r1 is None else r1)], seg=seg, mat=mat,
                            caps=caps, radmod=radmod, up=up)

    def sphere(self, center, r, seg=16, rings=10, scale=(1, 1, 1), mat=METAL):
        m = Matrix.Diagonal(Vector((scale[0] * r, scale[1] * r, scale[2] * r, 1.0)))
        m = Matrix.Translation(Vector(center)) @ m
        before = set(self.bm.verts)
        bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=1.0, matrix=Matrix.Identity(4))
        new = [v for v in self.bm.verts if v not in before]
        for v in new:
            p = m @ from_blender(v.co)
            v.co = U(p)
        self._set_mat(new, mat)
        return new

    def box(self, center, size, mat=METAL, bevel=0.0, seg=2, axes=None, bevel_angle=None):
        """Axis-aligned (or rotated via axes=(ex, ey, ez) Unity vectors) box with optional bevel."""
        cx, cy, cz = center
        sx, sy, sz = size
        ex, ey, ez = axes if axes else (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
        pts = {}
        verts = []
        for i in (-1, 1):
            for j in (-1, 1):
                for k in (-1, 1):
                    p = Vector(center) + ex * (i * sx / 2) + ey * (j * sy / 2) + ez * (k * sz / 2)
                    pts[(i, j, k)] = self._vert(p)
                    verts.append(pts[(i, j, k)])
        quads = [((-1, -1, -1), (-1, 1, -1), (-1, 1, 1), (-1, -1, 1)),
                 ((1, -1, -1), (1, -1, 1), (1, 1, 1), (1, 1, -1)),
                 ((-1, -1, -1), (-1, -1, 1), (1, -1, 1), (1, -1, -1)),
                 ((-1, 1, -1), (1, 1, -1), (1, 1, 1), (-1, 1, 1)),
                 ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1)),
                 ((-1, -1, 1), (-1, 1, 1), (1, 1, 1), (1, -1, 1))]
        for q in quads:
            self.face([pts[c] for c in q], mat)
        if bevel > 0:
            self.bevel_new(verts, bevel, seg, bevel_angle)
        return verts

    def prism(self, poly, plane, c0, c1, mat=METAL, bevel=0.0, seg=2, bevel_angle=None):
        """Extrude a 2D polygon. plane 'zy': poly=(z, y) extruded along x from c0..c1; 'zx': poly=(z, x)
        along y; 'xy': poly=(x, y) along z."""
        def pt(a, b, c):
            if plane == "zy":
                return Vector((c, b, a))
            if plane == "zx":
                return Vector((b, c, a))
            return Vector((a, b, c))
        rings = [[pt(a, b, c0) for (a, b) in poly], [pt(a, b, c1) for (a, b) in poly]]
        vr = self.loft(rings, mat)
        if bevel > 0:
            self.bevel_new([v for ring in vr for v in ring], bevel, seg, bevel_angle)
        return vr

    def prism_rounded(self, poly, plane, stations, mat=METAL, bevel=0.0, seg=2, pivot=None, bevel_angle=None):
        """Like prism() but with several cross sections: stations = [(c, scale), ...] where the polygon
        is scaled about `pivot` (default centroid) so sides taper and the part reads as rounded."""
        if pivot is None:
            pivot = (sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly))

        def pt(a, b, c):
            if plane == "zy":
                return Vector((c, b, a))
            if plane == "zx":
                return Vector((b, c, a))
            return Vector((a, b, c))
        rings = []
        for c, sc in stations:
            rings.append([pt(pivot[0] + (a - pivot[0]) * sc, pivot[1] + (b - pivot[1]) * sc, c) for (a, b) in poly])
        vr = self.loft(rings, mat)
        if bevel > 0:
            self.bevel_new([v for ring in vr for v in ring], bevel, seg, bevel_angle)
        return vr

    def sweep(self, path, section, mat=METAL, up=None, scales=None, caps=(True, True), mats=None):
        """Extrude a closed 2D section (s, t) along a polyline with parallel-transport frames."""
        path = [Vector(p) for p in path]
        rings = []
        prev_u = None
        for i, p in enumerate(path):
            if i == 0:
                tangent = path[1] - path[0]
            elif i == len(path) - 1:
                tangent = path[-1] - path[-2]
            else:
                tangent = path[i + 1] - path[i - 1]
            a = tangent.normalized()
            if prev_u is None:
                _, u, v = _basis_from_axis(a, up)
            else:
                u = (prev_u - a * prev_u.dot(a)).normalized()
                v = a.cross(u)
            prev_u = u
            sc = scales[i] if scales else 1.0
            sx, sy = (sc, sc) if not isinstance(sc, tuple) else sc
            rings.append([p + u * (s * sx) + v * (t * sy) for (s, t) in section])
        return self.loft(rings, mat, caps=caps, mats=mats)

    # -- helpers ----------------------------------------------------------------------------
    def _set_mat(self, verts, mat):
        vs = set(verts)
        for f in self.bm.faces:
            if all(v in vs for v in f.verts):
                f.material_index = mat

    def bevel_new(self, verts, offset, seg=2, angle=None):
        vs = set(verts)
        edges = set()
        for v in vs:
            for e in v.link_edges:
                if e.other_vert(v) in vs:
                    edges.add(e)
        if angle is not None:
            keep = []
            for e in edges:
                try:
                    a = e.calc_face_angle()
                except ValueError:
                    continue
                if a >= angle:
                    keep.append(e)
            edges = keep
        bmesh.ops.bevel(self.bm, geom=list(edges), offset=offset, segments=seg, affect="EDGES",
                        profile=0.5, clamp_overlap=True)

    def cut(self, cutter, solver="EXACT"):
        """Boolean difference against another Builder. Result replaces this builder's mesh."""
        a = self.to_object(self.name + "_cutA", keep=True)
        b = cutter.to_object(self.name + "_cutB", keep=True)
        mod = a.modifiers.new("cut", "BOOLEAN")
        mod.operation = "DIFFERENCE"
        mod.object = b
        mod.solver = solver
        bpy.context.view_layer.objects.active = a
        bpy.ops.object.modifier_apply(modifier="cut")
        self.bm.free()
        self.bm = bmesh.new()
        self.bm.from_mesh(a.data)
        bpy.data.objects.remove(a)
        bpy.data.objects.remove(b)

    def merge(self, other):
        """Append another builder's geometry (already in Blender space)."""
        mesh = bpy.data.meshes.new("tmp")
        other.bm.to_mesh(mesh)
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)

    def finish_bm(self):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=2e-6)
        bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=1e-7)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        big = [f for f in bm.faces if len(f.verts) > 4]
        if big:
            bmesh.ops.triangulate(bm, faces=big, quad_method="BEAUTY", ngon_method="EAR_CLIP")

    def to_object(self, name=None, keep=False):
        self.finish_bm()
        mesh = bpy.data.meshes.new((name or self.name) + "Mesh")
        self.bm.to_mesh(mesh)
        obj = bpy.data.objects.new(name or self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for name_m in MATERIAL_NAMES:
            obj.data.materials.append(bpy.data.materials.get(name_m))
        return obj

    def tri_count(self):
        self.finish_bm()
        return sum(len(f.verts) - 2 for f in self.bm.faces)


# ---------------------------------------------------------------------------------------------
# Assembly: named parts grouped into export objects
# ---------------------------------------------------------------------------------------------
class Assembly:
    def __init__(self, gun_name):
        self.gun_name = gun_name
        self.parts = {}
        self.group_pivots = {}

    def part(self, name, group="Body"):
        b = Builder(name, group)
        self.parts[name] = b
        return b

    def pivot(self, group, p):
        self.group_pivots[group] = Vector(p)

    # -- finalisation -----------------------------------------------------------------------
    def part_objects(self):
        """Temporary per-part objects (world transform identity) for checks."""
        objs = {}
        for name, b in self.parts.items():
            o = b.to_object("chk_" + name, keep=True)
            objs[name] = o
        return objs

    def build_groups(self):
        groups = {}
        for name, b in self.parts.items():
            groups.setdefault(b.group, []).append(b)
        objects = []
        for gname, builders in groups.items():
            main = Builder(gname, gname)
            for b in builders:
                main.merge(b)
            obj = main.to_object(gname)
            pivot = self.group_pivots.get(gname)
            if pivot is not None:
                shift = U(pivot)
                obj.data.transform(Matrix.Translation(-shift))
                obj.location = shift
            objects.append(obj)
        return objects


def tri_count(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def shade_and_unwrap(obj, sharp_deg=38.0):
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    for f in bm.faces:
        f.smooth = True
    limit = math.radians(sharp_deg)
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        try:
            e.smooth = e.calc_face_angle() <= limit
        except ValueError:
            e.smooth = True
    bm.to_mesh(mesh)
    bm.free()
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.002)
    bpy.ops.object.mode_set(mode="OBJECT")


def prune_unused_slots(obj):
    used = sorted({p.material_index for p in obj.data.polygons})
    if len(used) == len(obj.material_slots):
        return
    remap = {old: new for new, old in enumerate(used)}
    mats = [obj.data.materials[i] for i in used]
    for p in obj.data.polygons:
        p.material_index = remap[p.material_index]
    obj.data.materials.clear()
    for m in mats:
        obj.data.materials.append(m)


def non_manifold_edges(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    n = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    bm.free()
    return n


# ---------------------------------------------------------------------------------------------
# Connectivity checks (Blender-side equivalent of SniperMountingTests)
# ---------------------------------------------------------------------------------------------
def _bvh(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    tree = BVHTree.FromBMesh(bm)
    pts = [v.co.copy() for v in bm.verts]
    bm.free()
    return tree, pts


def mesh_gap(obj_a, obj_b):
    """Approximate minimum distance between two meshes using vertex-to-surface queries."""
    ta, pa = _bvh(obj_a)
    tb, pb = _bvh(obj_b)
    if ta.overlap(tb):
        return 0.0
    best = 1e9
    for p in pa:
        hit = tb.find_nearest(p)
        if hit[0] is not None:
            best = min(best, hit[3])
    for p in pb:
        hit = ta.find_nearest(p)
        if hit[0] is not None:
            best = min(best, hit[3])
    return best


def aabb(obj):
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def unity_bounds(obj):
    """(min, max) in Unity space."""
    pts = [from_blender(obj.matrix_world @ v.co) for v in obj.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def connectivity_report(objs, root_name, tol=0.002):
    """Graph-connectivity of parts via exact-ish gap; returns the detached part names."""
    names = list(objs)
    trees = {n: _bvh(objs[n]) for n in names}

    def gap(a, b):
        ta, pa = trees[a]
        tb, pb = trees[b]
        if ta.overlap(tb):
            return 0.0
        best = 1e9
        for p in pa[::2]:
            hit = tb.find_nearest(p)
            if hit[0] is not None:
                best = min(best, hit[3])
        for p in pb[::2]:
            hit = ta.find_nearest(p)
            if hit[0] is not None:
                best = min(best, hit[3])
        return best

    reached = {root_name}
    queue = [root_name]
    edges = []
    while queue:
        cur = queue.pop()
        for n in names:
            if n in reached:
                continue
            g = gap(cur, n)
            if g <= tol:
                reached.add(n)
                queue.append(n)
                edges.append((cur, n, g))
    return [n for n in names if n not in reached], edges


# ---------------------------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------------------------
def export_models(objects, out_dir, base_name):
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    fbx_path = os.path.join(out_dir, base_name + ".fbx")
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
    glb_path = os.path.join(out_dir, base_name + ".glb")
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
    print("EXPORTED", fbx_path, glb_path)
    return fbx_path, glb_path


# ---------------------------------------------------------------------------------------------
# Renders
# ---------------------------------------------------------------------------------------------
def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_render(samples, res_pct, width=1500, height=800, floor_z=None):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = res_pct
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.cycles.max_bounces = 6
    scene.cycles.glass_bounces = 6

    world = bpy.data.worlds.new("Studio")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.12, 0.13, 0.16, 1.0)
    bg.inputs["Strength"].default_value = 0.6

    if floor_z is not None:
        bpy.ops.mesh.primitive_plane_add(size=6.0, location=(0, 0, floor_z))
        floor = bpy.context.active_object
        floor.name = "RenderFloor"
        fm = bpy.data.materials.new("Floor")
        fm.use_nodes = True
        fb = fm.node_tree.nodes["Principled BSDF"]
        fb.inputs["Base Color"].default_value = (0.05, 0.055, 0.065, 1.0)
        fb.inputs["Roughness"].default_value = 0.45
        floor.data.materials.append(fm)

    def area(name, loc, energy, size, aim=(0, 0, 0), color=(1, 1, 1)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.size = size
        data.color = color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = loc
        look_at(obj, aim)
        return obj

    return area


def add_camera(name="Cam"):
    cam_data = bpy.data.cameras.new(name)
    cam = bpy.data.objects.new(name, cam_data)
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    return cam, cam_data


def render_to(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDER", path)


def standard_lights(area, centre):
    cx, cy, cz = centre
    area("Key", (cx - 0.9, cy - 0.9, cz + 1.1), 55, 0.9, centre)
    area("Rim", (cx + 0.9, cy + 0.9, cz + 0.6), 45, 0.7, centre, (0.75, 0.85, 1.0))
    area("Fill", (cx - 1.2, cy + 0.9, cz + 0.2), 18, 1.2, centre, (1.0, 0.9, 0.8))
    area("Top", (cx, cy, cz + 1.6), 22, 1.0, centre)


def parse_args():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def arg(name, default=None):
        if name in args:
            i = args.index(name)
            if i + 1 < len(args):
                return args[i + 1]
        return default
    return arg
