"""Shared modelling kit for the detailed assault weapons (M4A1, SCAR-H, UMP45).

Imported by build_m4a1.py / build_scar.py / build_ump45.py (and preview_assault_held.py). Not run on its
own. Everything is authored in *Unity-labelled* coordinates (x = right of the gun, y = up, z = forward /
muzzle, metres) and converted to Blender space when a mesh is committed:

    unity (x, y, z) -> blender (-x, -z, y)     (the mapping the FBX exporter undoes)

The mapping is a reflection, so faces are re-wound on commit. The FBX export uses axis_forward=-Z,
axis_up=Y with baked transforms, so the imported mesh has identity rotation, scale 1 and +Z forward.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        if i + 1 < len(ARGS):
            return ARGS[i + 1]
    return default


MAT_METAL, MAT_POLY, MAT_ACCENT = 0, 1, 2
MAT_SUFFIX = ("Metal", "Polymer", "Accent")


def V(x, y, z):
    return Vector((x, y, z))


def u2b(p):
    return Vector((-p[0], -p[2], p[1]))


def b2u(p):
    return Vector((-p[0], p[2], -p[1]))


# ---------------------------------------------------------------------------------------------
# Materials / scene
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


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


# ---------------------------------------------------------------------------------------------
# Generic helpers
# ---------------------------------------------------------------------------------------------
def basis_for(direction, up_hint=None):
    """Orthonormal (u, v, w): w = direction, u/v span the cross-section plane (u is 'side')."""
    w = Vector(direction).normalized()
    hint = Vector(up_hint) if up_hint is not None else (Vector((0, 1, 0)) if abs(w.y) < 0.95 else Vector((1, 0, 0)))
    u = hint.cross(w).normalized()   # hint=up -> u is +x for w=+z
    v = w.cross(u).normalized()
    return u, v, w


def circle_pts(r, segs, phase=0.0, rx=1.0, ry=1.0):
    pts = []
    for k in range(segs):
        a = phase + 2.0 * math.pi * k / segs
        pts.append((r * rx * math.cos(a), r * ry * math.sin(a)))
    return pts


def rrect_pts(cx, cy, hw, hh, r, cs=3):
    """Rounded rectangle, counter-clockwise, 4*(cs+1) points."""
    r = max(1e-6, min(r, hw, hh))
    pts = []
    corners = [(cx + hw - r, cy + hh - r, 0.0), (cx - hw + r, cy + hh - r, 90.0),
               (cx - hw + r, cy - hh + r, 180.0), (cx + hw - r, cy - hh + r, 270.0)]
    for ccx, ccy, a0 in corners:
        for k in range(cs + 1):
            a = math.radians(a0 + 90.0 * k / cs)
            pts.append((ccx + r * math.cos(a), ccy + r * math.sin(a)))
    return pts


def lerp(a, b, t):
    return a + (b - a) * t


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def bezier(p0, p1, p2, p3, n):
    pts = []
    for i in range(n + 1):
        t = i / n
        a = (1 - t) ** 3
        b = 3 * (1 - t) ** 2 * t
        c = 3 * (1 - t) * t * t
        d = t ** 3
        pts.append((a * p0[0] + b * p1[0] + c * p2[0] + d * p3[0],
                    a * p0[1] + b * p1[1] + c * p2[1] + d * p3[1]))
    return pts


def edges_of(faces):
    es = set()
    for f in faces:
        for e in f.edges:
            es.add(e)
    return list(es)


class Builder:
    """Accumulates closed shells in Unity space inside a single bmesh."""

    def __init__(self, name="part"):
        self.name = name
        self.bm = bmesh.new()

    # -- low level -------------------------------------------------------------------------------
    def _ring_verts(self, pts3):
        return [self.bm.verts.new(p) for p in pts3]

    def _face(self, verts, mat):
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return None
        f.material_index = mat
        return f

    def _finish(self, faces, mat_unused=None, bevel=0.0, bseg=2, rot=None, pivot=None):
        faces = [f for f in faces if f is not None and f.is_valid]
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        if rot is not None:
            verts = list({v for f in faces for v in f.verts})
            m = Euler(tuple(math.radians(a) for a in rot), "XYZ").to_matrix().to_4x4()
            p = Vector(pivot) if pivot is not None else Vector((0, 0, 0))
            bmesh.ops.transform(self.bm, matrix=Matrix.Translation(p) @ m @ Matrix.Translation(-p), verts=verts)
        if bevel and bevel > 0.0:
            es = [e for e in edges_of(faces) if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(25.0)]
            if es:
                try:
                    bmesh.ops.bevel(self.bm, geom=es, offset=bevel, segments=bseg, profile=0.5,
                                    affect="EDGES", clamp_overlap=True, loop_slide=True)
                except Exception:
                    pass
        return faces

    # -- primitives ------------------------------------------------------------------------------
    def loft(self, rings, mat, cap0=True, cap1=True, bevel=0.0, bseg=2, rot=None, pivot=None, cyclic=False):
        n = len(rings[0])
        vr = [self._ring_verts(r) for r in rings]
        faces = []
        pairs = len(vr) if cyclic else len(vr) - 1
        if cyclic:
            cap0 = cap1 = False
        for i in range(pairs):
            for k in range(n):
                k2 = (k + 1) % n
                j = (i + 1) % len(vr)
                faces.append(self._face([vr[i][k], vr[i][k2], vr[j][k2], vr[j][k]], mat))
        if cap0:
            faces.append(self._face(list(reversed(vr[0])), mat))
        if cap1:
            faces.append(self._face(vr[-1], mat))
        return self._finish(faces, bevel=bevel, bseg=bseg, rot=rot, pivot=pivot)

    def box(self, c, s, mat, bevel=0.0, bseg=2, taper=(1.0, 1.0), rot=None, pivot=None, skew=(0.0, 0.0)):
        """Box centred at c with full size s=(x,y,z). taper scales the +z end's (x,y) size;
        skew shifts the +z end (x,y)."""
        hx, hy, hz = s[0] / 2.0, s[1] / 2.0, s[2] / 2.0
        r0 = [V(c[0] - hx, c[1] - hy, c[2] - hz), V(c[0] + hx, c[1] - hy, c[2] - hz),
              V(c[0] + hx, c[1] + hy, c[2] - hz), V(c[0] - hx, c[1] + hy, c[2] - hz)]
        tx, ty = hx * taper[0], hy * taper[1]
        r1 = [V(c[0] - tx + skew[0], c[1] - ty + skew[1], c[2] + hz), V(c[0] + tx + skew[0], c[1] - ty + skew[1], c[2] + hz),
              V(c[0] + tx + skew[0], c[1] + ty + skew[1], c[2] + hz), V(c[0] - tx + skew[0], c[1] + ty + skew[1], c[2] + hz)]
        return self.loft([r0, r1], mat, bevel=bevel, bseg=bseg, rot=rot, pivot=pivot if pivot is not None else c)

    def lathe(self, origin, direction, profile, segs, mat, up_hint=None, ry=1.0, rx=1.0, cap0=True, cap1=True,
              bevel=0.0, bseg=1, phase=0.0, offset_uv=(0.0, 0.0)):
        """Revolve profile [(t, r), ...] (t along direction from origin) into a closed solid."""
        u, v, w = basis_for(direction, up_hint)
        o = Vector(origin)
        rings = []
        for t, r in profile:
            ring = []
            for (px, py) in circle_pts(r, segs, phase, rx, ry):
                ring.append(o + u * (px + offset_uv[0]) + v * (py + offset_uv[1]) + w * t)
            rings.append(ring)
        return self.loft(rings, mat, cap0=cap0, cap1=cap1, bevel=bevel, bseg=bseg)

    def cyl(self, p0, p1, r, mat, segs=16, r1=None, ry=1.0, rx=1.0, bevel=0.0, bseg=1, up_hint=None, phase=0.0):
        d = Vector(p1) - Vector(p0)
        L = d.length
        r1 = r if r1 is None else r1
        return self.lathe(p0, d, [(0.0, r), (L, r1)], segs, mat, up_hint=up_hint, ry=ry, rx=rx, bevel=bevel,
                          bseg=bseg, phase=phase)

    def sections(self, origin, direction, secs, mat, up_hint=None, cap0=True, cap1=True, bevel=0.0, bseg=1):
        """Loft of 2D sections [(t, [(u, v), ...])] placed along `direction` from `origin`."""
        u, v, w = basis_for(direction, up_hint)
        o = Vector(origin)
        rings = [[o + u * px + v * py + w * t for (px, py) in pts] for t, pts in secs]
        return self.loft(rings, mat, cap0=cap0, cap1=cap1, bevel=bevel, bseg=bseg)

    def prism_x(self, profile, x0, x1, mat, bevel=0.0, bseg=2, rot=None, pivot=None):
        """Polygon in the (z, y) plane extruded across x from x0 to x1."""
        r0 = [V(x0, y, z) for (z, y) in profile]
        r1 = [V(x1, y, z) for (z, y) in profile]
        return self.loft([r0, r1], mat, bevel=bevel, bseg=bseg, rot=rot, pivot=pivot)

    def prism_z(self, profile, z0, z1, mat, bevel=0.0, bseg=2, rot=None, pivot=None):
        """Polygon in the (x, y) plane extruded along z from z0 to z1."""
        r0 = [V(x, y, z0) for (x, y) in profile]
        r1 = [V(x, y, z1) for (x, y) in profile]
        return self.loft([r0, r1], mat, bevel=bevel, bseg=bseg, rot=rot, pivot=pivot)

    def prism_y(self, profile, y0, y1, mat, bevel=0.0, bseg=2, rot=None, pivot=None):
        """Polygon in the (x, z) plane extruded along y."""
        r0 = [V(x, y0, z) for (x, z) in profile]
        r1 = [V(x, y1, z) for (x, z) in profile]
        return self.loft([r0, r1], mat, bevel=bevel, bseg=bseg, rot=rot, pivot=pivot)

    def sweep(self, path, sizes, mat, r=0.002, cs=3, cap0=True, cap1=True, bevel=0.0, bseg=1, cx=0.0):
        """Rounded-rectangle section swept along a path of (y, z) points in the side plane.
        sizes: list of (half_width_x, half_depth_in_plane) per path point."""
        rings = []
        n = len(path)
        for i, (py, pz) in enumerate(path):
            a = Vector((0, path[min(i + 1, n - 1)][0] - path[max(i - 1, 0)][0], path[min(i + 1, n - 1)][1] - path[max(i - 1, 0)][1])).normalized()
            nrm = Vector((0, -a.z, a.y))  # in-plane normal (points forward/up for a downward tangent)
            hw, hd = sizes[i]
            ring = []
            for (qx, qd) in rrect_pts(cx, 0.0, hw, hd, r, cs):
                p = Vector((qx, py, pz)) + nrm * qd
                ring.append(p)
            rings.append(ring)
        return self.loft(rings, mat, cap0=cap0, cap1=cap1, bevel=bevel, bseg=bseg)

    def pyramid(self, base_c, u, v, n, hu, hv, h, mat):
        """Four-sided pyramid (no base face) on a plane: used for stipple texture."""
        b = [base_c + u * hu + v * hv, base_c - u * hu + v * hv, base_c - u * hu - v * hv, base_c + u * hu - v * hv]
        apex = base_c + n * h
        vs = [self.bm.verts.new(p) for p in b]
        va = self.bm.verts.new(apex)
        faces = []
        for k in range(4):
            faces.append(self._face([vs[k], vs[(k + 1) % 4], va], mat))
        faces.append(self._face(list(reversed(vs)), mat))
        return self._finish(faces)

    def stipple(self, plane_c, u, v, n, nu, nv, pitch_u, pitch_v, size, height, mat, mask=None):
        """Grid of pyramids on a plane. u, v: in-plane unit axes; n: outward normal."""
        faces = []
        for i in range(nu):
            for j in range(nv):
                if mask is not None and not mask(i, j):
                    continue
                c = plane_c + u * ((i - (nu - 1) / 2.0) * pitch_u) + v * ((j - (nv - 1) / 2.0) * pitch_v)
                faces += self.pyramid(c, u, v, n, size, size, height, mat)
        return faces

    def screw(self, p, axis, r, mat, segs=10, length=0.0016, socket=0.5):
        """Screw / pin head: short cylinder with a hex-ish socket recess."""
        n = Vector(axis).normalized()
        p = Vector(p)
        faces = self.lathe(p, n, [(0.0, r * 0.92), (length * 0.35, r), (length, r * 0.94)], segs, mat, bevel=0.0)
        top = None
        best = -1e9
        for f in faces:
            if f is not None and f.is_valid and len(f.verts) == segs:
                d = f.calc_center_median().dot(n)
                if d > best:
                    best = d
                    top = f
        if top is not None:
            try:
                res = bmesh.ops.inset_individual(self.bm, faces=[top], thickness=r * (1.0 - socket))
                inner = res["faces"][0] if res.get("faces") else top
                bmesh.ops.translate(self.bm, vec=-n * length * 0.5, verts=list(inner.verts))
            except Exception:
                pass
        return faces

    # -- output ----------------------------------------------------------------------------------
    def stats(self):
        return len(self.bm.verts), sum(len(f.verts) - 2 for f in self.bm.faces)

    def commit(self, name, materials, link=True):
        bm = self.bm
        for v in bm.verts:
            v.co = u2b(v.co)
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        mesh = bpy.data.meshes.new(name + "Mesh")
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name, mesh)
        if link:
            bpy.context.scene.collection.objects.link(obj)
        for m in materials:
            obj.data.materials.append(m)
        return obj


# ---------------------------------------------------------------------------------------------
# Object-level operations (Blender space)
# ---------------------------------------------------------------------------------------------
def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def boolean(obj, cutter, op="DIFFERENCE", delete_cutter=True, self_intersect=True):
    activate(obj)
    m = obj.modifiers.new("bool", "BOOLEAN")
    m.operation = op
    m.operand_type = "OBJECT"
    m.object = cutter
    m.solver = "EXACT"
    m.use_self = self_intersect
    bpy.ops.object.modifier_apply(modifier=m.name)
    if delete_cutter:
        bpy.data.objects.remove(cutter, do_unlink=True)


def bevel_angle(obj, width, segments=1, angle=35.0):
    activate(obj)
    m = obj.modifiers.new("bev", "BEVEL")
    m.limit_method = "ANGLE"
    m.angle_limit = math.radians(angle)
    m.width = width
    m.segments = segments
    m.profile = 0.5
    m.harden_normals = False
    bpy.ops.object.modifier_apply(modifier=m.name)


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    return o


def set_origin_unity(obj, unity_point):
    """Moves the object origin to a Unity-space point, keeping the geometry where it is."""
    bp = u2b(unity_point)
    me = obj.data
    me.transform(Matrix.Translation(-bp))
    obj.location = bp
    me.update()


def shade_edges(obj, limit_deg=34.0, metal_limit_deg=22.0):
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=bm.edges[:])
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        try:
            ang = e.calc_face_angle()
        except ValueError:
            continue
        mats = {f.material_index for f in e.link_faces}
        lim = math.radians(limit_deg)
        e.smooth = ang <= lim
    bm.to_mesh(me)
    bm.free()


def unwrap(obj):
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.002)
    bpy.ops.object.mode_set(mode="OBJECT")


def mesh_stats(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    nonman = sum(1 for e in bm.edges if not e.is_manifold)
    counts = {}
    for f in bm.faces:
        counts[f.material_index] = counts.get(f.material_index, 0) + 1
    xs = [b2u(v.co).x for v in bm.verts]
    ys = [b2u(v.co).y for v in bm.verts]
    zs = [b2u(v.co).z for v in bm.verts]
    uv = len(obj.data.uv_layers) > 0
    res = {"verts": len(bm.verts), "tris": tris, "nonmanifold": nonman, "mat_faces": counts, "uv": uv,
           "x": (min(xs), max(xs)), "y": (min(ys), max(ys)), "z": (min(zs), max(zs))}
    bm.free()
    return res


def prune_unused_slots(obj):
    used = sorted({p.material_index for p in obj.data.polygons})
    if len(used) == len(obj.data.materials):
        return
    remap = {old: new for new, old in enumerate(used)}
    for p in obj.data.polygons:
        p.material_index = remap[p.material_index]
    keep = [obj.data.materials[i] for i in used]
    obj.data.materials.clear()
    for m in keep:
        obj.data.materials.append(m)


def export_all(objs, out_dir, basename):
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    fbx_path = os.path.join(out_dir, basename + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="EDGE",
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
    glb_path = os.path.join(out_dir, basename + ".glb")
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
    return fbx_path, glb_path


# ---------------------------------------------------------------------------------------------
# Rendering (Cycles CPU, preview quality)
# ---------------------------------------------------------------------------------------------
def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_render_scene(floor_y_unity, samples, res_pct=100, size=(1400, 800), world_strength=0.55):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.resolution_percentage = res_pct
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"

    world = bpy.data.worlds.new("Studio")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.12, 0.13, 0.16, 1.0)
    bg.inputs["Strength"].default_value = world_strength

    bpy.ops.mesh.primitive_plane_add(size=6.0, location=(0, 0, floor_y_unity))
    floor = bpy.context.active_object
    fm = bpy.data.materials.new("Floor")
    fm.use_nodes = True
    fb = fm.node_tree.nodes["Principled BSDF"]
    fb.inputs["Base Color"].default_value = (0.06, 0.065, 0.075, 1.0)
    fb.inputs["Roughness"].default_value = 0.5
    floor.data.materials.append(fm)

    def area(name, loc, energy, size_, color=(1, 1, 1), target=(0, 0, 0)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.size = size_
        data.color = color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = loc
        look_at(obj, target)
        return obj

    return area


def add_studio_lights(area, centre_b):
    c = Vector(centre_b)
    area("Key", c + Vector((-0.9, -0.7, 1.1)), 55.0, 0.8, target=c)
    area("Rim", c + Vector((0.9, 0.9, 0.7)), 45.0, 0.6, (0.75, 0.85, 1.0), target=c)
    area("Fill", c + Vector((-1.2, 0.9, 0.3)), 16.0, 1.0, (1.0, 0.92, 0.82), target=c)
    area("Top", c + Vector((0.0, 0.0, 1.6)), 30.0, 1.0, target=c)
    strip = area("Strip", c + Vector((-1.0, -0.1, 0.35)), 60.0, 0.5, target=c)
    strip.data.shape = "RECTANGLE"
    strip.data.size = 0.15
    strip.data.size_y = 0.9


def render_views(views, prefix, render_dir, samples, res_pct, centre_u, floor_y_u):
    """views: list of (name, cam_unity, target_unity, focal_mm)."""
    os.makedirs(render_dir, exist_ok=True)
    area = setup_render_scene(floor_y_u, samples, res_pct)
    add_studio_lights(area, u2b(centre_u))
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam_data.clip_start = 0.01
    paths = []
    for name, loc, target, focal in views:
        cam_data.lens = focal
        cam.location = u2b(loc)
        look_at(cam, u2b(target))
        path = os.path.join(render_dir, "%s-%s.png" % (prefix, name))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
        print("GUN_RENDER", path)
    return paths


# ---------------------------------------------------------------------------------------------
# Shared weapon parts
# ---------------------------------------------------------------------------------------------
def stadium(cu, cv, length, width, n=5):
    """Capsule polygon with its long axis along the first coordinate."""
    pts = []
    r = width / 2.0
    hl = length / 2.0 - r
    for k in range(n + 1):
        a = -math.pi / 2.0 + math.pi * k / n
        pts.append((cu + hl + r * math.cos(a), cv + r * math.sin(a)))
    for k in range(n + 1):
        a = math.pi / 2.0 + math.pi * k / n
        pts.append((cu - hl + r * math.cos(a), cv + r * math.sin(a)))
    return pts


def picatinny(b, z0, z1, y_base, mat, x_c=0.0, pitch=0.01016, tooth=0.0049, h=0.0062, hw=0.0106, core_hw=0.0074):
    """Rail teeth (and the core bar under them) along z. y_base is the bottom of the teeth."""
    y0 = y_base
    prof = [(-hw, y0 + 0.0015), (-hw + 0.0012, y0), (hw - 0.0012, y0), (hw, y0 + 0.0015), (hw, y0 + h - 0.0012),
            (hw - 0.0012, y0 + h), (-hw + 0.0012, y0 + h), (-hw, y0 + h - 0.0012)]
    prof = [(x_c + x, y) for x, y in prof]
    n = int((z1 - z0) / pitch)
    z = z0 + (z1 - z0 - n * pitch) / 2.0
    for i in range(n):
        b.prism_z(prof, z + i * pitch, z + i * pitch + tooth, mat)
    b.box((x_c, y0 + 0.0014, (z0 + z1) / 2.0), (2.0 * core_hw, 0.0036, z1 - z0), mat, bevel=0.0004, bseg=1)


def pistol_grip(b, top, tilt_deg, length, mat, stip_mat, hw_top=0.0158, depth_top=0.0215, swell=0.0035,
                hw_bot=0.0150, grooves=3, stipple=True, back_curve=0.0030):
    """Ergonomic pistol grip with a palm swell, finger grooves and diamond stipple on both sides.
    top: top-centre point; the grip runs down and back at tilt_deg from vertical."""
    a = math.radians(tilt_deg)
    d_down = V(0.0, -math.cos(a), -math.sin(a))
    secs = []
    N = 26
    for i in range(N + 1):
        t = length * i / N
        k = i / N
        hw = lerp(hw_top, hw_bot, k) + 0.0010 * math.sin(k * math.pi)
        back = -depth_top * 0.5 - swell * math.sin(min(1.0, k * 1.25) * math.pi * 0.9) - back_curve * k
        front = depth_top * 0.5 - 0.0015 * k
        if grooves:
            for g in range(grooves):
                c = 0.30 + 0.20 * g
                front -= 0.0016 * math.exp(-((k - c) / 0.055) ** 2)
        if k > 0.9:
            front -= 0.0030 * smoothstep(0.9, 1.0, k)
            back += 0.0030 * smoothstep(0.9, 1.0, k)
        cy = (front + back) / 2.0
        hd = (front - back) / 2.0
        secs.append((t, rrect_pts(0.0, cy, hw, hd, min(0.0075, hd * 0.55), 3)))
    # section plane: u = x, v = in-plane forward-ish axis (perpendicular to d_down)
    u, v, w = basis_for(d_down, (1.0, 0.0, 0.0) if False else None)
    b.sections(V(*top), d_down, secs, mat, up_hint=V(0, 0, 1), cap0=True, cap1=True)
    if not stipple:
        return d_down
    # stipple on both sides
    u2, v2, w2 = basis_for(d_down, V(0, 0, 1))
    for sx in (-1.0, 1.0):
        faces = []
        nu, nv = 24, 5
        for i in range(nu):
            k = 0.16 + 0.70 * i / (nu - 1)
            t = length * k
            hw = lerp(hw_top, hw_bot, k) + 0.0010 * math.sin(k * math.pi)
            for j in range(nv):
                off = (j - (nv - 1) / 2.0) * 0.0044
                c = V(*top) + d_down * t + v2 * (off + 0.0) + V(sx * (hw - 0.0007), 0.0, 0.0)
                faces += b.pyramid(c, w2, v2, V(sx, 0.0, 0.0), 0.0016, 0.0016, 0.0015, stip_mat)
    return d_down
