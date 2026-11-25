"""Shared hard-surface modelling, export and render helpers for the weapon generators.

Used by build_shotgun.py, build_lmg.py and build_pistol.py (run headless, see each script).

Authoring convention: every coordinate is given in Unity weapon space as (s, u, f) =
(right, up, forward) in metres, the same space the prefabs use (+Z forward, muzzle on +Z).
`B()` maps it into Blender space (-s, -f, u), which is the mapping the FBX export
(axis_forward=-Z, axis_up=Y) undoes, so the imported mesh has identity rotation and scale 1 and
the model matches the prefab transforms (MuzzlePoint, ShellEjectPoint ...) one to one.
That mapping is a mirror, so face winding is fixed by recalculating normals per closed solid.

A model is a list of `Part` objects. Each Part becomes one mesh object in the FBX (siblings
at the FBX root, object origin = part pivot), so Unity gets e.g. `Magazine`, `Pump`, `Slide`
transforms that the runtime can animate.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

MAT_METAL, MAT_POLYMER, MAT_ACCENT = 0, 1, 2
MAT_SUFFIX = ("Metal", "Polymer", "Accent")


def B(p):
    """Unity (s, u, f) -> Blender world coordinates."""
    return Vector((-p[0], -p[2], p[1]))


def V(p):
    return Vector((p[0], p[1], p[2]))


def rotation(pitch=0.0, yaw=0.0, roll=0.0):
    """Rotation in Unity space (degrees). pitch about s (nose up positive), yaw about u, roll about f."""
    p, y, r = (math.radians(a) for a in (pitch, yaw, roll))
    mp = Matrix(((1, 0, 0), (0, math.cos(p), -math.sin(p)), (0, math.sin(p), math.cos(p))))
    my = Matrix(((math.cos(y), 0, math.sin(y)), (0, 1, 0), (-math.sin(y), 0, math.cos(y))))
    mr = Matrix(((math.cos(r), -math.sin(r), 0), (math.sin(r), math.cos(r), 0), (0, 0, 1)))
    return (my @ mp @ mr).to_4x4()


def frame(origin=(0, 0, 0), pitch=0.0, yaw=0.0, roll=0.0):
    """Matrix placing local (s, u, f) points at `origin` after rotating (Unity space)."""
    m = rotation(pitch, yaw, roll)
    m.translation = V(origin)
    return m


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def bezier(p0, p1, p2, p3, n):
    pts = []
    for i in range(n + 1):
        t = i / n
        a = (1 - t) ** 3
        b = 3 * (1 - t) ** 2 * t
        c = 3 * (1 - t) * t * t
        d = t ** 3
        pts.append(tuple(a * p0[k] + b * p1[k] + c * p2[k] + d * p3[k] for k in range(3)))
    return pts


def round_poly(pts, seg=3):
    """Polygon with optional per-vertex fillet radius: pts of (f, u) or (f, u, r)."""
    n = len(pts)
    out = []
    for i in range(n):
        p = pts[i]
        r = p[2] if len(p) > 2 else 0.0
        if r <= 0.0:
            out.append((p[0], p[1]))
            continue
        a = Vector(pts[i - 1][:2])
        c = Vector(pts[(i + 1) % n][:2])
        v = Vector(p[:2])
        d0 = (a - v)
        d1 = (c - v)
        l0, l1 = d0.length, d1.length
        if l0 < 1e-9 or l1 < 1e-9:
            out.append((p[0], p[1]))
            continue
        d0.normalize()
        d1.normalize()
        ang = math.acos(max(-1.0, min(1.0, d0.dot(d1))))
        if ang < 1e-3 or abs(ang - math.pi) < 1e-3:
            out.append((p[0], p[1]))
            continue
        t = min(r / math.tan(ang / 2.0), l0 * 0.5, l1 * 0.5)
        p0 = v + d0 * t
        p2 = v + d1 * t
        for k in range(seg + 1):
            s = k / seg
            q = (1 - s) ** 2 * p0 + 2 * (1 - s) * s * v + s * s * p2
            out.append((q.x, q.y))
    return out


# ---------------------------------------------------------------------------------------------
# Ring generators (lists of (s, u, f) points)
# ---------------------------------------------------------------------------------------------
def ellipse_ring(s, u, f, rs, ru, n=24, phase=0.0):
    return [(s + rs * math.cos(phase + 2 * math.pi * k / n), u + ru * math.sin(phase + 2 * math.pi * k / n), f)
            for k in range(n)]


def superellipse_ring(s, u, f, hw, hh, n=28, power=0.6):
    pts = []
    for k in range(n):
        a = 2 * math.pi * k / n
        c, sn = math.cos(a), math.sin(a)
        pts.append((s + hw * math.copysign(abs(c) ** power, c), u + hh * math.copysign(abs(sn) ** power, sn), f))
    return pts


def rect_ring(s, u, f, hw, hh, r=0.0, rseg=2):
    """Rectangle, optionally with rounded corners (constant vertex count for a given r>0)."""
    if r <= 0.0:
        return [(s - hw, u - hh, f), (s + hw, u - hh, f), (s + hw, u + hh, f), (s - hw, u + hh, f)]
    r = min(r, hw * 0.98, hh * 0.98)
    pts = []
    for cx, cy, a0 in ((s + hw - r, u - hh + r, -90), (s + hw - r, u + hh - r, 0),
                       (s - hw + r, u + hh - r, 90), (s - hw + r, u - hh + r, 180)):
        for k in range(rseg + 1):
            a = math.radians(a0 + 90.0 * k / rseg)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a), f))
    return pts


class Region:
    """Bounds (a0, a1, b0, b1) plus an optional predicate, used to place surface stipple."""

    def __init__(self, bounds, fn=None):
        self.bounds = bounds
        self.fn = fn

    def __call__(self, a, b):
        return self.fn(a, b) if self.fn else True


class Part:
    """One mesh object of the model. Geometry is authored in absolute Unity weapon space."""

    def __init__(self, name, pivot=(0, 0, 0)):
        self.name = name
        self.pivot = V(pivot)
        self.bm = bmesh.new()
        self.open_faces = []

    # -- low level ---------------------------------------------------------------------------
    def _vert(self, p):
        return self.bm.verts.new(B(p))

    def _face(self, verts, mat):
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return None
        f.material_index = mat
        return f

    def _finish(self, n0, bevel, seg, angle=38.0):
        bm = self.bm
        if bevel <= 0.0:
            return
        bm.faces.ensure_lookup_table()
        edges = set()
        lim = math.radians(angle)
        for f in bm.faces[n0:]:
            for e in f.edges:
                if len(e.link_faces) == 2:
                    try:
                        if e.calc_face_angle() > lim:
                            edges.add(e)
                    except ValueError:
                        pass
        if edges:
            bmesh.ops.bevel(bm, geom=list(edges), offset=bevel, segments=seg, profile=0.55,
                            affect="EDGES", clamp_overlap=True)

    # -- solids ------------------------------------------------------------------------------
    def loft(self, mat, rings, xf=None, cap0=True, cap1=True, bevel=0.0, seg=1, loop=False):
        n0 = len(self.bm.faces)
        if xf is not None:
            rings = [[tuple(xf @ V(p)) for p in ring] for ring in rings]
        vr = [[self._vert(p) for p in ring] for ring in rings]
        n = len(vr[0])
        for i in range(len(vr) - 1):
            for k in range(n):
                k2 = (k + 1) % n
                self._face([vr[i][k], vr[i][k2], vr[i + 1][k2], vr[i + 1][k]], mat)
        if loop:
            for k in range(n):
                k2 = (k + 1) % n
                self._face([vr[-1][k], vr[-1][k2], vr[0][k2], vr[0][k]], mat)
        if cap0 and not loop:
            self._face(list(vr[0]), mat)
        if cap1 and not loop:
            self._face(list(reversed(vr[-1])), mat)
        self._finish(n0, bevel, seg)

    def box(self, mat, c, size, xf=None, bevel=0.0012, seg=1, taper=None, r=0.0):
        """Axis-aligned box centred on c, size = (width s, height u, length f).
        taper = (width, height) scale at the front end."""
        s, u, f = c
        hw, hh, hl = size[0] / 2, size[1] / 2, size[2] / 2
        tw, th = taper if taper else (1.0, 1.0)
        rings = [rect_ring(s, u, f - hl, hw, hh, r), rect_ring(s, u, f + hl, hw * tw, hh * th, r)]
        self.loft(mat, rings, xf, bevel=bevel, seg=seg)

    def revolve(self, mat, prof, c=(0.0, 0.0), n=24, xf=None, bevel=0.0, seg=1, squash=1.0, phase=0.0):
        """Surface of revolution along f. prof = [(f, radius), ...] (radius may step in/out)."""
        rings = [ellipse_ring(c[0], c[1], f, r, r * squash, n, phase) for f, r in prof]
        self.loft(mat, rings, xf, bevel=bevel, seg=seg)

    def cyl(self, mat, c, r, length, axis="f", n=16, xf=None, bevel=0.0, seg=1, r1=None):
        """Cylinder centred on c along axis 'f', 's' or 'u'; r1 = radius at the +axis end."""
        r1 = r if r1 is None else r1
        s, u, f = c
        h = length / 2.0
        a = [(-h, r), (h, r1)]
        rings = []
        for off, rad in a:
            if axis == "f":
                rings.append(ellipse_ring(s, u, f + off, rad, rad, n))
            elif axis == "s":
                rings.append([(s + off, u + rad * math.sin(2 * math.pi * k / n), f + rad * math.cos(2 * math.pi * k / n))
                              for k in range(n)])
            else:
                rings.append([(s + rad * math.cos(2 * math.pi * k / n), u + off, f + rad * math.sin(2 * math.pi * k / n))
                              for k in range(n)])
        self.loft(mat, rings, xf, bevel=bevel, seg=seg)

    def extrude(self, mat, poly, s0, s1, xf=None, bevel=0.0012, seg=1, rseg=3):
        """Side-profile prism: poly of (f, u[, fillet radius]) extruded from s0 to s1."""
        pts = round_poly(poly, rseg)
        rings = [[(s0, u, f) for f, u in pts], [(s1, u, f) for f, u in pts]]
        self.loft(mat, rings, xf, bevel=bevel, seg=seg)

    def sweep(self, mat, path, hw, hh, side=(1, 0, 0), n=0, r=0.0, xf=None, bevel=0.0, seg=1,
              caps=True, scale=None):
        """Tube along a polyline. Section is a rectangle (hw along `side`, hh along the normal)
        or an ellipse when n>2. scale(i, count) -> multiplier for a tapering section."""
        pts = [V(p) for p in path]
        side_v = V(side).normalized()
        rings = []
        for i, p in enumerate(pts):
            if i == 0:
                t = pts[1] - pts[0]
            elif i == len(pts) - 1:
                t = pts[-1] - pts[-2]
            else:
                t = (pts[i + 1] - pts[i]).normalized() + (pts[i] - pts[i - 1]).normalized()
            t.normalize()
            nrm = t.cross(side_v)
            if nrm.length < 1e-5:
                nrm = t.cross(Vector((0, 1, 0)))
            nrm.normalize()
            sd = nrm.cross(t).normalized()
            k = scale(i, len(pts)) if scale else 1.0
            ring = []
            if n > 2:
                for j in range(n):
                    a = 2 * math.pi * j / n
                    q = p + sd * (hw * k * math.cos(a)) + nrm * (hh * k * math.sin(a))
                    ring.append(tuple(q))
            else:
                for cs, cn in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                    q = p + sd * (hw * k * cs) + nrm * (hh * k * cn)
                    ring.append(tuple(q))
            rings.append(ring)
        self.loft(mat, rings, xf, cap0=caps, cap1=caps, bevel=bevel, seg=seg)

    def sphere(self, mat, c, r, n=12, xf=None, squash=(1, 1, 1)):
        rings = []
        for i in range(1, n):
            a = math.pi * i / n
            rr = r * math.sin(a)
            rings.append(ellipse_ring(c[0], c[1], c[2] + r * squash[2] * math.cos(a), rr * squash[0], rr * squash[1], n * 2))
        self.loft(mat, rings, xf)

    def torus(self, mat, c, major, minor, axis="s", n=24, m=8, xf=None):
        """Ring of tube radius `minor` whose centreline circle (radius `major`) is perpendicular to `axis`."""
        rings = []
        for i in range(n):
            a = 2 * math.pi * i / n
            ring = []
            for j in range(m):
                b = 2 * math.pi * j / m
                rad = major + minor * math.cos(b)
                off = minor * math.sin(b)
                if axis == "s":
                    ring.append((c[0] + off, c[1] + rad * math.sin(a), c[2] + rad * math.cos(a)))
                elif axis == "u":
                    ring.append((c[0] + rad * math.cos(a), c[1] + off, c[2] + rad * math.sin(a)))
                else:
                    ring.append((c[0] + rad * math.cos(a), c[1] + rad * math.sin(a), c[2] + off))
            rings.append(ring)
        self.loft(mat, rings, xf, loop=True)

    # -- surface detail ----------------------------------------------------------------------
    def bumps(self, mat, region, plane, pitch=0.003, size=0.0022, height=0.0008, xf=None, stagger=True):
        """Pyramid stipple on a flat plane. plane = (axis, value, sign): axis 's' (region(f, u)), 'u' (region(s, f)) or 'f' (region(s, u)).
        Faces are open (no base), oriented outward, and skipped by normal recalculation."""
        axis, val, sign = plane
        a0, a1, b0, b1 = region.bounds
        rows = int((b1 - b0) / (pitch * (0.5 if stagger else 1.0))) + 1
        for ri in range(rows):
            b = b0 + ri * pitch * (0.5 if stagger else 1.0)
            off = (pitch * 0.5) if (stagger and ri % 2) else 0.0
            a = a0 + off
            while a < a1:
                if region(a, b):
                    self._pyramid(mat, axis, val, sign, a, b, size, height, xf)
                a += pitch

    def _pyramid(self, mat, axis, val, sign, a, b, size, height, xf):
        h = size / 2

        def pt(da, db, dn):
            if axis == "s":      # plane coordinates (f, u)
                p = (val + sign * dn, b + db, a + da)
            elif axis == "u":    # plane coordinates (s, f)
                p = (a + da, val + sign * dn, b + db)
            else:                # plane coordinates (s, u)
                p = (a + da, b + db, val + sign * dn)
            if xf is not None:
                p = tuple(xf @ V(p))
            return p

        base = [self._vert(pt(-h, -h, -0.0003)), self._vert(pt(h, -h, -0.0003)),
                self._vert(pt(h, h, -0.0003)), self._vert(pt(-h, h, -0.0003))]
        apex = self._vert(pt(0, 0, height))
        centre = (base[0].co + base[2].co) * 0.5
        for i in range(4):
            tri = [base[i], base[(i + 1) % 4], apex]
            nrm = (tri[1].co - tri[0].co).cross(tri[2].co - tri[0].co)
            if nrm.dot(apex.co - centre) < 0:
                tri.reverse()
            f = self._face(tri, mat)
            if f is not None:
                self.open_faces.append(f)

    def cut(self, cutters):
        """Boolean difference with a list of closed-solid Parts (exact solver)."""
        if not cutters:
            return
        self._flush_open()
        obj = self._temp_object("cut_target", self.bm)
        cbm = bmesh.new()
        for c in cutters:
            cbm_part = c.bm
            tmp = bpy.data.meshes.new("c")
            cbm_part.to_mesh(tmp)
            cbm.from_mesh(tmp)
            bpy.data.meshes.remove(tmp)
        bmesh.ops.recalc_face_normals(cbm, faces=cbm.faces[:])
        cobj = self._temp_object("cut_tool", cbm)
        mod = obj.modifiers.new("b", "BOOLEAN")
        mod.operation = "DIFFERENCE"
        mod.solver = "EXACT"
        mod.object = cobj
        mod.use_self = True
        dg = bpy.context.evaluated_depsgraph_get()
        ev = obj.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(ev)
        self.bm.free()
        self.bm = bmesh.new()
        self.bm.from_mesh(me)
        self.open_faces = []
        bpy.data.meshes.remove(me)
        for o in (obj, cobj):
            m = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.meshes.remove(m)
        cbm.free()

    def absorb(self, other):
        """Appends another Part's geometry (closed solids only; open stipple faces are not carried over)."""
        other._recalc()
        tmp = bpy.data.meshes.new("absorb")
        other.bm.to_mesh(tmp)
        self.bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)

    def _temp_object(self, name, bm):
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        obj = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(obj)
        return obj

    def _flush_open(self):
        # Fix normals on closed solids now, since the boolean needs consistent orientation.
        self._recalc()

    def _recalc(self):
        open_set = set(self.open_faces)
        closed = [f for f in self.bm.faces if f not in open_set]
        bmesh.ops.recalc_face_normals(self.bm, faces=closed)

    # -- output ------------------------------------------------------------------------------
    def triangle_count(self):
        return sum(len(f.verts) - 2 for f in self.bm.faces)


def make_material(name, color, metallic, roughness, bump=0.0, bump_scale=300.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = (color[0], color[1], color[2], 1.0)
    mat.metallic = metallic
    mat.roughness = roughness
    if bump > 0.0:
        tc = nt.nodes.new("ShaderNodeTexCoord")
        noise = nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = bump_scale
        noise.inputs["Detail"].default_value = 6.0
        bmp = nt.nodes.new("ShaderNodeBump")
        bmp.inputs["Strength"].default_value = bump
        bmp.inputs["Distance"].default_value = 0.002
        nt.links.new(tc.outputs["Object"], noise.inputs["Vector"])
        nt.links.new(noise.outputs["Fac"], bmp.inputs["Height"])
        nt.links.new(bmp.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def make_materials(prefix, metal, polymer, accent):
    """Each spec is (color, metallic, roughness[, bump]). Slots are always Metal, Polymer, Accent."""
    out = []
    for suffix, spec in zip(MAT_SUFFIX, (metal, polymer, accent)):
        bump = spec[3] if len(spec) > 3 else 0.0
        out.append(make_material("%s_%s" % (prefix, suffix), spec[0], spec[1], spec[2], bump))
    return out


def part_to_object(part, materials):
    bm = part.bm
    part._recalc()
    big = [f for f in bm.faces if len(f.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big, quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.transform(bm, matrix=Matrix.Translation(-B(part.pivot)), verts=bm.verts[:])
    lim = math.radians(38.0)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        try:
            e.smooth = e.calc_face_angle() <= lim
        except ValueError:
            e.smooth = True
    mesh = bpy.data.meshes.new(part.name + "Mesh")
    bm.to_mesh(mesh)
    used = sorted({p.material_index for p in mesh.polygons})
    remap = {old: new for new, old in enumerate(used)}
    for p in mesh.polygons:
        p.material_index = remap[p.material_index]
    for old in used:
        mesh.materials.append(materials[old])
    obj = bpy.data.objects.new(part.name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = B(part.pivot)
    return obj


def unwrap(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.002)
    bpy.ops.object.mode_set(mode="OBJECT")


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def build_objects(parts, materials):
    objs = []
    for p in parts:
        obj = part_to_object(p, materials)
        unwrap(obj)
        objs.append(obj)
    return objs


def stats(tag, objs, anchors=None):
    total = 0
    mats = {}
    nonman = 0
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        tris = sum(len(f.verts) - 2 for f in bm.faces)
        total += tris
        for f in bm.faces:
            name = o.data.materials[f.material_index].name
            mats[name] = mats.get(name, 0) + 1
        nonman += sum(1 for e in bm.edges if not e.is_manifold)
        for v in bm.verts:
            w = o.matrix_world @ v.co
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
        uv = o.data.uv_layers.active
        print("%s_PART %s tris=%d verts=%d uv=%s" % (tag, o.name, tris, len(bm.verts), "yes" if uv else "NO"))
        bm.free()
    print("%s_TRIS total=%d materials=%s" % (tag, total, mats))
    # Report in Unity (s, u, f)
    print("%s_BOUNDS_UNITY s[%.3f,%.3f] u[%.3f,%.3f] f[%.3f,%.3f]" % (tag, -hi.x, -lo.x, lo.z, hi.z, -hi.y, -lo.y))
    print("%s_OPEN_EDGES %d" % (tag, nonman))
    return total


def export(objs, out_dir, name):
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    fbx = os.path.join(out_dir, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, object_types={"MESH"}, use_mesh_modifiers=True,
        mesh_smooth_type="EDGE", add_leaf_bones=False, axis_forward="-Z", axis_up="Y",
        global_scale=1.0, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
        bake_space_transform=True, path_mode="STRIP", embed_textures=False, use_tspace=True)
    glb = os.path.join(out_dir, name + ".glb")
    bpy.ops.export_scene.gltf(
        filepath=glb, export_format="GLB", use_selection=True, export_apply=True, export_yup=True,
        export_materials="EXPORT", export_cameras=False, export_lights=False)
    print("EXPORTED", fbx, glb)


# ---------------------------------------------------------------------------------------------
# Renders
# ---------------------------------------------------------------------------------------------
def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_render(samples, res_x=1400, res_y=800, res_pct=100, world=(0.34, 0.37, 0.42), strength=0.7):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = res_x
    scene.render.resolution_y = res_y
    scene.render.resolution_percentage = res_pct
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("Studio")
    scene.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (world[0], world[1], world[2], 1.0)
    bg.inputs["Strength"].default_value = strength
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam, cam_data


def add_area(name, loc, energy, size, color=(1, 1, 1), target=(0, 0, 0), shape="SQUARE", size_y=None):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.size = size
    data.color = color
    if size_y is not None:
        data.shape = "RECTANGLE"
        data.size_y = size_y
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = loc
    look_at(obj, target)
    return obj


def add_floor(z, size=6.0, color=(0.16, 0.17, 0.19)):
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, 0, z))
    floor = bpy.context.active_object
    fm = bpy.data.materials.new("Floor")
    fm.use_nodes = True
    fb = fm.node_tree.nodes["Principled BSDF"]
    fb.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    fb.inputs["Roughness"].default_value = 0.75
    floor.data.materials.append(fm)
    return floor


def studio(floor_z, scale=1.0):
    """Key/rim/fill lights; energy follows the inverse-square law so framing scale does not change exposure."""
    add_floor(floor_z)

    def lamp(name, pos, irradiance, size, color=(1, 1, 1), **kw):
        loc = B(Vector(pos) * scale)
        add_area(name, loc, irradiance * loc.length ** 2, size * scale, color, **kw)

    lamp("Key", (0.5, 0.55, -0.25), 7.0, 0.55)
    lamp("Rim", (-0.5, 0.3, 0.45), 6.0, 0.45, (0.75, 0.85, 1.0))
    lamp("Fill", (0.6, 0.1, 0.5), 2.0, 0.7, (1.0, 0.9, 0.8))
    lamp("Top", (0.0, 0.9, 0.0), 3.0, 0.8)
    lamp("Strip", (0.55, 0.1, 0.0), 6.0, 0.1, shape="RECTANGLE", size_y=0.6 * scale)


def render_views(tag, render_dir, views, samples=48, floor_z=-0.2, scale=1.0, res=100):
    """views: (name, camera (s,u,f), target (s,u,f), focal mm)."""
    os.makedirs(render_dir, exist_ok=True)
    cam, cam_data = setup_render(samples, res_pct=res)
    studio(floor_z, scale)
    paths = []
    for name, loc, target, focal in views:
        cam_data.lens = focal
        cam.location = B(loc)
        look_at(cam, B(target))
        path = os.path.join(render_dir, "%s-%s.png" % (tag, name))
        bpy.context.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print("RENDER", path)
        paths.append(path)
    return paths


# ---------------------------------------------------------------------------------------------
# Held first-person mock (camera -> holder -> weapon, ProceduralFirstPersonArms IK port)
# ---------------------------------------------------------------------------------------------
HOLDER = Vector((0.3, -0.2, 0.5))
ARM_SCALE = 1.3
UPPER_LEN = 0.29 * ARM_SCALE
LOWER_LEN = 0.27 * ARM_SCALE
SHOULDERS = {1: Vector((0.24, -0.52, -0.10)), -1: Vector((-0.12, -0.55, 0.02))}
GRIPS = {1: Vector((0.0, -0.09, -0.05)), -1: Vector((-0.03, -0.08, 0.03))}


def solve_arm(side, target):
    shoulder = SHOULDERS[side]
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


_M = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))


def _ellipsoid(name, centre, radii, colour, axes=None, rough=0.6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=40, ring_count=20, location=(0, 0, 0))
    o = bpy.context.active_object
    o.name = name
    o.data.transform(Matrix.Diagonal((radii[0], radii[2], radii[1], 1.0)))
    bpy.ops.object.shade_smooth()
    if axes is None:
        axes = (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
    ux, uy, uz = axes
    r_unity = Matrix(((ux.x, uy.x, uz.x), (ux.y, uy.y, uz.y), (ux.z, uy.z, uz.z)))
    m = (_M @ r_unity @ _M.inverted()).to_4x4()
    m.translation = B(centre)
    o.matrix_world = m
    o.data.materials.append(make_material(name + "Mat", colour, 0.0, rough))
    return o


def _limb(name, a, b, radius, colour):
    mid = (a + b) * 0.5
    direction = b - a
    ay = direction.normalized()
    ref = Vector((0, 0, 1)) if abs(ay.z) < 0.9 else Vector((1, 0, 0))
    x = ay.cross(ref).normalized()
    z = x.cross(ay)
    return _ellipsoid(name, mid, (radius, direction.length * 0.5 + radius * 0.5, radius), colour, (x, ay, z))


def render_held(tag, render_dir, objs, samples=48, held_offset=(0, 0, 0), res=100, hands=True):
    """Moves the weapon objects to the holder and renders a first-person mock plus a close-up and a side view."""
    os.makedirs(render_dir, exist_ok=True)
    off = HOLDER + V(held_offset)
    for o in objs:
        o.location = o.location + B(off)
    skin = (0.78, 0.62, 0.52)
    sleeve = (0.22, 0.32, 0.42)
    centres = []
    if hands:
        for side in (1, -1):
            sh, el, wr, axes = solve_arm(side, HOLDER + GRIPS[side])
            hx, hy, hz = axes
            sc = ARM_SCALE
            tag_side = "R" if side > 0 else "L"
            _ellipsoid("Palm" + tag_side, wr + hy * (-0.045 * sc), (0.034 * sc, 0.05 * sc, 0.02 * sc), skin, axes)
            _ellipsoid("Fingers" + tag_side, wr + hy * (-0.095 * sc) + hz * (0.004 * sc), (0.03 * sc, 0.03 * sc, 0.016 * sc), skin, axes)
            _ellipsoid("Thumb" + tag_side, wr + hx * (-0.03 * sc * side) + hy * (-0.05 * sc) + hz * (0.018 * sc),
                       (0.012 * sc, 0.03 * sc, 0.012 * sc), skin, axes)
            _limb("Forearm" + tag_side, el, wr, 0.045 * sc * 0.9, sleeve)
            _limb("UpperArm" + tag_side, sh, el, 0.052 * sc * 0.9, sleeve)
            centres.append(wr)

    scene = bpy.context.scene
    cam, cam_data = setup_render(samples, 1600, 900, res, world=(0.35, 0.40, 0.48), strength=0.8)
    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = 3.0
    so = bpy.data.objects.new("Sun", sun)
    scene.collection.objects.link(so)
    so.rotation_euler = (math.radians(55), 0, math.radians(35))

    paths = []

    def shot(name):
        path = os.path.join(render_dir, "%s-%s.png" % (tag, name))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print("RENDER", path)
        paths.append(path)

    cam_data.sensor_fit = "VERTICAL"
    cam_data.angle = math.radians(90.0)
    cam_data.clip_start = 0.05
    cam.location = (0, 0, 0)
    cam.rotation_euler = Vector((0, -1, 0)).to_track_quat("-Z", "Y").to_euler()
    shot("held-fp")

    cam_data.sensor_fit = "AUTO"
    cam_data.angle = math.radians(34.0)
    cam.location = B(Vector((0.02, 0.0, -0.08)))
    aim = B(HOLDER + Vector((0.0, -0.02, 0.2))) - cam.location
    cam.rotation_euler = aim.to_track_quat("-Z", "Y").to_euler()
    shot("held-fp-zoom")

    cam_data.angle = math.radians(40.0)
    cam.location = B(Vector((1.1, -0.1, 0.55)))
    target = B(HOLDER + Vector((0.0, 0.0, 0.1)))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    shot("held-side")
    return paths


def check_renders(paths):
    """Non-blank check: pixel standard deviation over the image."""
    ok = True
    for p in paths:
        img = bpy.data.images.load(p)
        px = list(img.pixels)
        n = len(px) // 4
        lum = [0.2126 * px[i * 4] + 0.7152 * px[i * 4 + 1] + 0.0722 * px[i * 4 + 2] for i in range(0, n, 7)]
        mean = sum(lum) / len(lum)
        sd = math.sqrt(sum((x - mean) ** 2 for x in lum) / len(lum))
        print("RENDER_CHECK %s mean=%.3f sd=%.3f %s" % (os.path.basename(p), mean, sd, "OK" if sd > 0.02 else "BLANK?"))
        ok = ok and sd > 0.02
        bpy.data.images.remove(img)
    return ok


def parse_args():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def arg(name, default=None):
        if name in args:
            i = args.index(name)
            if i + 1 < len(args):
                return args[i + 1]
        return default

    return arg
