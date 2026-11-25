"""Geometry toolkit for the soldier generator.

Everything is authored in *Unity-style metres*: +X is the character's right, +Y up, +Z forward
(the same axes the game's HumanoidProportions use). ``U2B`` converts a point to Blender space
(Z up, character faces -Y, character's right at -X). The conversion mirrors handedness, so face
orientation is never trusted from construction order: every primitive orients its own closed shell
with a signed-volume check instead.

Each vertex carries three integer tags used later for skinning and for splitting the model into
meshes: ``mode`` (0 base skin, 1 transfer weights from the base, 2 rigid to ``bone``), ``bone``
(index into BONES) and ``grp`` (mesh group), plus ``reg`` (body region used to mask weights).
"""

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

# Material slot order (kept identical in the FBX and in the Unity SoldierModelSet asset).
ARMOR, CLOTH, SKIN, GEAR, ACCENT, LENS, EYE = range(7)
MATERIAL_NAMES = ["Soldier_Armor", "Soldier_Cloth", "Soldier_Skin", "Soldier_Gear",
                  "Soldier_Accent", "Soldier_Lens", "Soldier_Eye"]

# Bone order equals the C# HumanoidBone enum.
BONES = ["Hips", "Spine", "Chest", "Neck", "Head", "ShoulderL", "ShoulderR", "UpperArmL", "UpperArmR",
         "ForearmL", "ForearmR", "HandL", "HandR", "ThighL", "ThighR", "ShinL", "ShinR", "FootL", "FootR"]
BONE_INDEX = {n: i for i, n in enumerate(BONES)}

# Mesh groups (one SkinnedMeshRenderer each in Unity).
G_BODY, G_HEAD, G_ARM_L, G_ARM_R = range(4)
GROUP_NAMES = ["Soldier_Body", "Soldier_Head", "Soldier_ArmL", "Soldier_ArmR"]

# Weight modes.
M_BASE, M_XFER, M_RIGID, M_NECK = 0, 1, 2, 3

# Regions for weight masks.
R_TORSO, R_ARM_L, R_ARM_R, R_LEG_L, R_LEG_R, R_HEAD = range(6)


def U2B(p):
    return Vector((-p[0], -p[2], p[1]))


def B2U(p):
    return Vector((-p[0], p[2], -p[1]))


def lerp(a, b, t):
    return a + (b - a) * t


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def sgnpow(x, e):
    return math.copysign(abs(x) ** e, x)


def frame_from_axis(axis, ref=None):
    """Orthonormal (u, v, w) with w along ``axis`` and u as close to ``ref`` as possible."""
    w = Vector(axis).normalized()
    ref = Vector(ref) if ref is not None else Vector((1, 0, 0))
    u = ref - w * ref.dot(w)
    if u.length < 1e-5:
        u = Vector((0, 0, 1)) - w * w.z
        if u.length < 1e-5:
            u = Vector((1, 0, 0)) - w * w.x
    u.normalize()
    v = w.cross(u)
    return u, v, w


def rot_matrix(axis_x=None, axis_y=None, axis_z=None):
    """3x3 matrix with the given columns (any one may be omitted)."""
    if axis_x is not None and axis_y is not None:
        x = Vector(axis_x).normalized()
        y = Vector(axis_y).normalized()
        z = x.cross(y).normalized()
    elif axis_y is not None and axis_z is not None:
        y = Vector(axis_y).normalized()
        z = Vector(axis_z).normalized()
        x = y.cross(z).normalized()
    else:
        z = Vector(axis_z).normalized()
        x = Vector(axis_x).normalized()
        y = z.cross(x).normalized()
    return Matrix((x, y, z)).transposed()


def euler_matrix(rx=0.0, ry=0.0, rz=0.0):
    """Rotation about X, Y, Z (degrees) applied in that order, as a 3x3 matrix."""
    return (Matrix.Rotation(math.radians(rz), 3, "Z") @ Matrix.Rotation(math.radians(ry), 3, "Y")
            @ Matrix.Rotation(math.radians(rx), 3, "X"))


class Model:
    """Accumulates all geometry in one bmesh with per-vertex skinning tags."""

    def __init__(self):
        self.bm = bmesh.new()
        self.l_mode = self.bm.verts.layers.int.new("mode")
        self.l_bone = self.bm.verts.layers.int.new("bone")
        self.l_grp = self.bm.verts.layers.int.new("grp")
        self.l_reg = self.bm.verts.layers.int.new("reg")
        self.tag = dict(mode=M_BASE, bone=0, grp=G_BODY, reg=R_TORSO)
        self._faces = None
        self.default_mat = CLOTH

    # ----------------------------------------------------------------------------------------
    # tagging
    # ----------------------------------------------------------------------------------------
    def set_tag(self, mode=None, bone=None, grp=None, reg=None):
        if mode is not None:
            self.tag["mode"] = mode
        if bone is not None:
            self.tag["bone"] = BONE_INDEX[bone] if isinstance(bone, str) else bone
        if grp is not None:
            self.tag["grp"] = grp
        if reg is not None:
            self.tag["reg"] = reg

    def base(self, reg, grp=G_BODY):
        self.set_tag(mode=M_BASE, grp=grp, reg=reg)

    def xfer(self, reg, grp=G_BODY):
        self.set_tag(mode=M_XFER, grp=grp, reg=reg)

    def rigid(self, bone, reg=None, grp=G_BODY):
        self.set_tag(mode=M_RIGID, bone=bone, grp=grp)
        if reg is not None:
            self.set_tag(reg=reg)

    # ----------------------------------------------------------------------------------------
    # low level
    # ----------------------------------------------------------------------------------------
    def V(self, p):
        v = self.bm.verts.new(U2B(p))
        v[self.l_mode] = self.tag["mode"]
        v[self.l_bone] = self.tag["bone"]
        v[self.l_grp] = self.tag["grp"]
        v[self.l_reg] = self.tag["reg"]
        return v

    def _begin(self):
        self._faces = []

    def F(self, verts, mat):
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return None
        f.material_index = mat
        f.smooth = True
        self._faces.append(f)
        return f

    def _end(self, closed=True, hint=None, flip=False):
        """Orient the faces built since _begin. Closed shells use signed volume; open ones a hint point/normal."""
        faces = [f for f in self._faces if f.is_valid]
        self._faces = None
        if not faces:
            return faces
        if closed:
            vol = 0.0
            for f in faces:
                vs = [v.co for v in f.verts]
                for i in range(1, len(vs) - 1):
                    vol += vs[0].dot(vs[i].cross(vs[i + 1]))
            if vol < 0.0:
                for f in faces:
                    f.normal_flip()
        elif flip:
            for f in faces:
                f.normal_flip()
        elif hint is not None:
            f0 = faces[0]
            f0.normal_update()
            if B2U(f0.normal).dot(Vector(hint)) < 0.0:
                for f in faces:
                    f.normal_flip()
        return faces

    # ----------------------------------------------------------------------------------------
    # primitives
    # ----------------------------------------------------------------------------------------
    def loft(self, rings, mat=None, caps=(True, True), closed=True, orient=True):
        """Skin through rings (lists of Unity-space points, equal length)."""
        mat = self.default_mat if mat is None else mat
        self._begin()
        vs = [[self.V(p) for p in ring] for ring in rings]
        n = len(vs[0])
        top = n if closed else n - 1
        for i in range(len(vs) - 1):
            for k in range(top):
                k2 = (k + 1) % n
                self.F([vs[i][k], vs[i][k2], vs[i + 1][k2], vs[i + 1][k]], mat)
        if caps[0]:
            self.F(list(vs[0]), mat)
        if caps[1]:
            self.F(list(reversed(vs[-1])), mat)
        faces = self._end(closed=orient and closed and caps[0] and caps[1])
        return vs, faces

    def ring(self, c, u, v, ru, rv, n=12, sq=1.0, phase=0.0, mod=None):
        pts = []
        for k in range(n):
            a = 2.0 * math.pi * k / n + phase
            cu, su = math.cos(a), math.sin(a)
            k_r = mod(a) if mod else 1.0
            pts.append(c + u * (ru * k_r * sgnpow(cu, sq)) + v * (rv * k_r * sgnpow(su, sq)))
        return pts

    def tube(self, path, ru, rv=None, n=12, mat=None, sq=1.0, ref=None, caps=("round", "round"),
             mod=None, phase=0.0, tip_scale=1.0):
        """Tube along a polyline of Unity points. ru/rv: scalar, list per station or fn(s)."""
        mat = self.default_mat if mat is None else mat
        path = [Vector(p) for p in path]
        m = len(path)
        rv = ru if rv is None else rv

        def val(r, i):
            if callable(r):
                return r(i / (m - 1) if m > 1 else 0.0)
            if isinstance(r, (list, tuple)):
                return r[i]
            return r

        rings = []
        ref_v = Vector(ref) if ref is not None else Vector((1, 0, 0))
        prev_u = None
        tangents = []
        for i in range(m):
            a = path[max(i - 1, 0)]
            b = path[min(i + 1, m - 1)]
            t = (b - a)
            if t.length < 1e-9:
                t = Vector((0, -1, 0))
            tangents.append(t.normalized())
        for i in range(m):
            w = tangents[i]
            if prev_u is None:
                u, v, _ = frame_from_axis(w, ref_v)
            else:
                u = prev_u - w * prev_u.dot(w)
                if u.length < 1e-6:
                    u, v, _ = frame_from_axis(w, ref_v)
                u.normalize()
                v = w.cross(u)
            prev_u = u
            s = i / (m - 1) if m > 1 else 0.0
            md = (lambda a, s=s: mod(s, a)) if mod else None
            rings.append(self.ring(path[i], u, v, val(ru, i), val(rv, i), n, sq, phase, md))

        mat_i = mat
        self._begin()
        vs = [[self.V(p) for p in ring] for ring in rings]
        for i in range(m - 1):
            for k in range(n):
                k2 = (k + 1) % n
                self.F([vs[i][k], vs[i][k2], vs[i + 1][k2], vs[i + 1][k]], mat_i)

        def cap(end):
            idx = 0 if end == 0 else m - 1
            kind = caps[end]
            ring = vs[idx]
            if kind is None:
                return
            w = tangents[idx] * (-1 if end == 0 else 1)
            c = path[idx]
            if kind == "flat":
                self.F(list(ring) if end == 0 else list(reversed(ring)), mat_i)
                return
            ru_e, rv_e = val(ru, idx), val(rv, idx)
            depth = (ru_e + rv_e) * 0.5 * tip_scale
            prev = ring
            for fr, dz in ((0.86, 0.5), (0.5, 0.87)):
                pts = []
                for p in rings[idx]:
                    pts.append(c + (p - c) * fr + w * depth * dz)
                cur = [self.V(p) for p in pts]
                for k in range(n):
                    k2 = (k + 1) % n
                    self.F([prev[k], prev[k2], cur[k2], cur[k]], mat_i)
                prev = cur
            tip = self.V(c + w * depth)
            for k in range(n):
                k2 = (k + 1) % n
                self.F([prev[k], prev[k2], tip], mat_i)

        cap(0)
        cap(1)
        faces = self._end(closed=True)
        return vs, faces

    def cyl(self, a, b, ra, rb=None, n=12, mat=None, caps=("flat", "flat"), sq=1.0, ref=None):
        a, b = Vector(a), Vector(b)
        rb = ra if rb is None else rb
        return self.tube([a, b], [ra, rb], n=n, mat=mat, sq=sq, ref=ref, caps=caps)

    def add_bm(self, tmp, M, mat=None, closed=True, flip=False, mat_fn=None):
        """Copy a temp bmesh (local coords) into the model, transformed by Unity-space 4x4 ``M``."""
        mat = self.default_mat if mat is None else mat
        self._begin()
        vmap = {}
        for v in tmp.verts:
            vmap[v.index] = self.V(M @ v.co)
        for f in tmp.faces:
            mi = mat_fn(f) if mat_fn else mat
            self.F([vmap[v.index] for v in f.verts], mi)
        faces = self._end(closed=closed, flip=flip)
        return faces

    def box(self, center, size, R=None, bevel=0.0, seg=1, mat=None, taper=None):
        """Rounded box. size = full extents along the local axes of R (3x3)."""
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        for v in tmp.verts:
            v.co.x *= size[0]
            v.co.y *= size[1]
            v.co.z *= size[2]
            if taper:
                k = taper(v.co)
                v.co.x *= k[0]
                v.co.z *= k[1]
        if bevel > 0:
            bmesh.ops.bevel(tmp, geom=list(tmp.edges) + list(tmp.verts), offset=bevel, segments=seg,
                            profile=0.5, affect="EDGES")
        tmp.verts.ensure_lookup_table()
        tmp.faces.ensure_lookup_table()
        tmp.edges.ensure_lookup_table()
        M = Matrix.Translation(Vector(center))
        if R is not None:
            M = M @ R.to_4x4()
        faces = self.add_bm(tmp, M, mat)
        tmp.free()
        return faces

    def ellipsoid(self, center, radii, R=None, nu=16, nv=10, mat=None, scale_fn=None):
        tmp = bmesh.new()
        bmesh.ops.create_uvsphere(tmp, u_segments=nu, v_segments=nv, radius=1.0)
        for v in tmp.verts:
            x, y, z = v.co.x, v.co.z, -v.co.y
            v.co = Vector((x * radii[0], y * radii[1], z * radii[2]))
            if scale_fn:
                v.co = scale_fn(v.co)
        M = Matrix.Translation(Vector(center))
        if R is not None:
            M = M @ R.to_4x4()
        faces = self.add_bm(tmp, M, mat)
        tmp.free()
        return faces

    def surface(self, fn, nu, nv, thick, mat=None, close_u=False, flip=False, inside=None):
        """Solid shell from a parametric surface fn(u, v) -> Unity point, u,v in [0,1].

        The shell is offset *inwards* (opposite the surface normal, which follows fn's u x v) by
        ``thick`` and closed with side walls. Returns the vertex grid of the outer surface.
        """
        mat = self.default_mat if mat is None else mat
        pts = []
        for i in range(nu + 1):
            row = []
            for j in range(nv + 1):
                row.append(Vector(fn(i / nu, j / nv)))
            pts.append(row)

        if inside is not None:
            mid = pts[nu // 2][nv // 2]
            i0, i1 = max(nu // 2 - 1, 0), min(nu // 2 + 1, nu)
            j0, j1 = max(nv // 2 - 1, 0), min(nv // 2 + 1, nv)
            n0 = (pts[i1][nv // 2] - pts[i0][nv // 2]).cross(pts[nu // 2][j1] - pts[nu // 2][j0])
            flip = n0.dot(mid - Vector(inside)) < 0.0

        def normal_at(i, j):
            i0, i1 = max(i - 1, 0), min(i + 1, nu)
            j0, j1 = max(j - 1, 0), min(j + 1, nv)
            if close_u and i == 0:
                i0 = nu - 1
            if close_u and i == nu:
                i1 = 1
            du = pts[i1][j] - pts[i0][j]
            dv = pts[i][j1] - pts[i][j0]
            n = du.cross(dv)
            if n.length < 1e-12:
                if inside is not None:
                    return (pts[i][j] - Vector(inside)).normalized()
                return Vector((0, 1, 0))
            n.normalize()
            return -n if flip else n

        self._begin()
        outer = [[None] * (nv + 1) for _ in range(nu + 1)]
        inner = [[None] * (nv + 1) for _ in range(nu + 1)]
        for i in range(nu + 1):
            for j in range(nv + 1):
                if close_u and i == nu:
                    outer[i][j] = outer[0][j]
                    inner[i][j] = inner[0][j]
                    continue
                p = pts[i][j]
                n = normal_at(i, j)
                outer[i][j] = self.V(p)
                inner[i][j] = self.V(p - n * thick)
        for i in range(nu):
            for j in range(nv):
                self.F([outer[i][j], outer[i + 1][j], outer[i + 1][j + 1], outer[i][j + 1]], mat)
                self.F([inner[i][j], inner[i][j + 1], inner[i + 1][j + 1], inner[i + 1][j]], mat)
        for j in range(nv):
            if not close_u:
                self.F([outer[0][j], outer[0][j + 1], inner[0][j + 1], inner[0][j]], mat)
                self.F([outer[nu][j], inner[nu][j], inner[nu][j + 1], outer[nu][j + 1]], mat)
        for i in range(nu):
            self.F([outer[i][0], inner[i][0], inner[i + 1][0], outer[i + 1][0]], mat)
            self.F([outer[i][nv], outer[i + 1][nv], inner[i + 1][nv], inner[i][nv]], mat)
        faces = self._end(closed=True)
        return outer, faces

    # ----------------------------------------------------------------------------------------
    def vertex_count(self):
        return len(self.bm.verts)


def mirror_x(p):
    return Vector((-p[0], p[1], p[2]))
