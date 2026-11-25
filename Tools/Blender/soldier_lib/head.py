"""Head: face, ears, eyes, neck, helmet, headset, goggles. All in the head mesh group."""

import math

import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

from .geometry import (ARMOR, CLOTH, SKIN, GEAR, ACCENT, LENS, EYE, G_HEAD, R_HEAD, M_NECK, euler_matrix,
                       lerp, smoothstep)

HEAD_C = Vector((0.0, 1.675, -0.006))
HEAD_R = (0.0765, 0.106, 0.096)


def G(v, mu, s):
    return math.exp(-((v - mu) / s) ** 2)


def sphere_dir(a, b, c):
    """Cube -> sphere mapping with near-uniform quads."""
    x2, y2, z2 = a * a, b * b, c * c
    return Vector((a * math.sqrt(max(0.0, 1 - y2 / 2 - z2 / 2 + y2 * z2 / 3)),
                   b * math.sqrt(max(0.0, 1 - z2 / 2 - x2 / 2 + z2 * x2 / 3)),
                   c * math.sqrt(max(0.0, 1 - x2 / 2 - y2 / 2 + x2 * y2 / 3))))


def face_q(q):
    """Displace a point q (relative to the head centre) to sculpt the face. Returns new q."""
    rx, ry, rz = HEAD_R
    X, Y, Z = q
    n = Vector((X / rx ** 2, Y / ry ** 2, Z / rz ** 2)).normalized()
    front = smoothstep(-0.015, 0.06, Z)

    # Lower face: narrower jaw, forward chin.
    low = smoothstep(-0.015, -0.10, Y)
    X *= 1.0 - 0.26 * low
    Z += (0.008 * low * smoothstep(0.0, 0.07, Z)) + 0.004 * low
    # Cheekbones and brow: widen slightly at eye / cheek level.
    X *= 1.0 + 0.045 * G(Y, -0.012, 0.035)
    # Flatter forehead, rounder skull at the back.
    Z -= 0.010 * smoothstep(0.03, 0.10, Y) * front

    d = 0.0
    ax = abs(X)
    # eye sockets
    d += -0.0085 * G(ax, 0.031, 0.015) * G(Y, 0.010, 0.012) * front
    # brow ridge
    d += 0.0060 * G(ax, 0.028, 0.030) * G(Y, 0.028, 0.0085) * front
    # cheek (zygomatic) fullness
    d += 0.0050 * G(ax, 0.052, 0.020) * G(Y, -0.032, 0.020) * front
    # cheek hollow under the cheekbone
    d += -0.0030 * G(ax, 0.050, 0.014) * G(Y, -0.060, 0.016) * front
    # temples
    d += -0.0035 * G(ax, 0.070, 0.012) * G(Y, 0.018, 0.020) * front
    # mouth region: lips
    d += 0.0040 * G(X, 0.0, 0.020) * G(Y, -0.052, 0.0050) * front
    d += 0.0045 * G(X, 0.0, 0.021) * G(Y, -0.064, 0.0058) * front
    d += -0.0030 * G(X, 0.0, 0.026) * G(Y, -0.0575, 0.0021) * front
    # chin
    d += 0.0085 * G(X, 0.0, 0.020) * G(Y, -0.090, 0.012) * front
    d += -0.0040 * G(X, 0.0, 0.020) * G(Y, -0.077, 0.0045) * front
    # philtrum / under nose
    d += -0.0020 * G(X, 0.0, 0.010) * G(Y, -0.040, 0.007) * front

    p = Vector((X, Y, Z)) + n * d

    # nose (pushed forward and slightly down)
    nose = 0.0
    nose += 0.0150 * G(p.x, 0.0, 0.0085) * G(p.y, -0.006, 0.026)
    nose += 0.0095 * G(p.x, 0.0, 0.0085) * G(p.y, -0.026, 0.0085)
    nose += 0.0050 * G(abs(p.x), 0.014, 0.0055) * G(p.y, -0.028, 0.0070)
    nose -= 0.0020 * G(abs(p.x), 0.0095, 0.0040) * G(p.y, -0.0345, 0.0035)
    p.z += nose * front
    p.y -= 0.0016 * G(p.x, 0.0, 0.01) * G(p.y, -0.025, 0.012) * front
    return p


def build_face_mesh():
    """Sculpted head shell in Unity coordinates (temp bmesh, open at the back/top under the helmet)."""
    rx, ry, rz = HEAD_R
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=2.0)
    bmesh.ops.subdivide_edges(tmp, edges=tmp.edges[:], cuts=14, use_grid_fill=True)
    for v in tmp.verts:
        d = sphere_dir(v.co.x, v.co.y, v.co.z).normalized()
        q = Vector((d.x * rx, d.y * ry, d.z * rz))
        v.co = HEAD_C + face_q(q)
    # Remove what the helmet hides entirely (top, back); the cut is far above/behind the visible rim.
    kill = []
    for f in tmp.faces:
        c = f.calc_center_median()
        phi = math.atan2(c.x, c.z - (-0.010))
        if c.y > rim_height(phi) - 0.006:
            kill.append(f)
    bmesh.ops.delete(tmp, geom=kill, context="FACES")
    tmp.verts.ensure_lookup_table()
    tmp.faces.ensure_lookup_table()
    return tmp


def surface_point(bvh, x, y, z_from=0.5):
    """Head surface point at world (x, y) found by casting a ray backwards along -Z."""
    hit = bvh.ray_cast(Vector((x, y, z_from)), Vector((0, 0, -1)))
    if hit[0] is None:
        return None, None
    return hit[0], hit[1]


def project(bvh, p, off=0.0):
    loc, nor, idx, dist = bvh.find_nearest(p)
    if loc is None:
        return p
    return loc + nor * off


def rim_height(phi):
    c = math.cos(phi)
    s = abs(math.sin(phi))
    rim = lerp(1.738, 1.662, smoothstep(0.55, -1.0, c))
    rim += 0.022 * math.exp(-((s - 1.0) / 0.22) ** 2) * (1.0 if c > -0.35 else 0.3)
    return rim


def helmet_fn(u, v, rim_pad=0.0, grow=0.0):
    """Helmet shell parametrisation. u = azimuth around (0 = forward), v = 0 crown ... 1 rim."""
    cy, cz = 1.712, -0.010
    rx, ry, rz = 0.1005 + grow, 0.124 + grow, 0.1235 + grow
    phi = 2.0 * math.pi * u
    c = math.cos(phi)
    rim = rim_height(phi) - rim_pad
    v = 0.035 + 0.965 * v
    cos_a = max(-0.95, min(0.99, (rim - cy) / ry))
    amax = math.acos(cos_a)
    a = amax * v
    sa = math.sin(a)
    x = rx * sa * math.sin(phi)
    y = cy + ry * math.cos(a)
    z = cz + rz * sa * math.cos(phi)
    # slight brim at the front and flare at the rim
    flare = 1.0 + 0.05 * smoothstep(0.85, 1.0, v)
    x = x * flare
    z = cz + (z - cz) * flare
    if c > 0.6:
        z += 0.006 * smoothstep(0.6, 1.0, c) * smoothstep(0.7, 1.0, v)
    return Vector((x, y, z))


def build_neck(m):
    m.set_tag(mode=M_NECK, grp=G_HEAD, reg=R_HEAD)
    path = [Vector((0, 1.395, 0.004)), Vector((0, 1.44, 0.008)), Vector((0, 1.50, 0.014)), Vector((0, 1.605, 0.030))]
    m.tube(path, [0.060, 0.056, 0.053, 0.051], [0.057, 0.053, 0.051, 0.050], n=20, mat=SKIN, caps=("flat", "flat"),
           ref=Vector((1, 0, 0)))
    # Adam's apple and neck muscles.
    m.ellipsoid(Vector((0, 1.488, 0.046)), (0.010, 0.014, 0.010), nu=8, nv=6, mat=SKIN)
    for s in (1, -1):
        m.tube([Vector((s * 0.026, 1.43, 0.030)), Vector((s * 0.022, 1.50, 0.034))], [0.014, 0.011], [0.010, 0.008],
               n=8, mat=SKIN, caps=("round", "round"))


def build_head(m):
    """Face shell, ears, eyes, brows. Returns a BVH of the face for placing straps etc."""
    m.set_tag(mode=2, grp=G_HEAD, reg=R_HEAD, bone="Head")
    tmp = build_face_mesh()
    bvh = BVHTree.FromBMesh(tmp)

    # Hair on the sides/back where the helmet leaves the head visible: mark faces by position.
    def mat_fn(f):
        c = f.calc_center_median() - HEAD_C
        if c.y > 0.0 and abs(c.x) > 0.062 and c.z < -0.01:
            return GEAR
        if c.y > -0.035 and c.z < -0.045:
            return GEAR
        return SKIN

    m.add_bm(tmp, Matrix.Identity(4), SKIN, closed=False, flip=True, mat_fn=mat_fn)
    tmp.free()

    # Ears
    for s in (1, -1):
        base = Vector((s * 0.0775, 1.668, -0.014))
        R = euler_matrix(0, -s * 10.0, 0) @ euler_matrix(8, 0, 0)
        m.ellipsoid(base, (0.0075, 0.029, 0.0185), R=R, nu=12, nv=10, mat=SKIN)
        m.ellipsoid(base + Vector((s * 0.0045, 0.0, 0.003)), (0.0042, 0.019, 0.0105), R=R, nu=10, nv=8, mat=SKIN)

    # Eyes
    for s in (1, -1):
        ex, ey = s * 0.0315, 1.686
        hit, nor = surface_point(bvh, ex, ey)
        z = (hit.z if hit is not None else 0.082) - 0.0055
        center = Vector((ex, ey, z))
        m.ellipsoid(center, (0.0118, 0.0118, 0.0118), nu=14, nv=10, mat=EYE)
        m.ellipsoid(center + Vector((0, 0, 0.0085)), (0.0062, 0.0062, 0.0042), nu=10, nv=8, mat=LENS)
        # eyelids as a ring around the opening
        ring = []
        for k in range(14):
            a = 2.0 * math.pi * k / 14
            ring.append(center + Vector((0.0122 * math.cos(a), 0.0058 * math.sin(a) - 0.0004, 0.0074)))
        ring.append(ring[0])
        m.tube(ring, 0.0036, 0.0026, n=6, mat=SKIN, caps=("flat", "flat"))
        # eyebrow following the brow ridge
        pts = []
        for i in range(7):
            t = i / 6.0
            bx = s * (0.011 + 0.040 * t)
            by = 1.708 + 0.007 * math.sin(math.pi * (t * 0.85 + 0.05)) - 0.004 * t
            h, _ = surface_point(bvh, bx, by)
            bz = (h.z if h is not None else 0.085) + 0.0012
            pts.append(Vector((bx, by, bz)))
        m.tube(pts, [0.0032, 0.0034, 0.0034, 0.0032, 0.0030, 0.0026, 0.0018], 0.0018, n=6, mat=GEAR,
               caps=("round", "round"), ref=Vector((0, 0, 1)))

    # Mouth line with a slight smile and nostrils.
    pts = []
    for i in range(9):
        t = i / 8.0
        mx = (t - 0.5) * 0.050
        my = 1.607 + 0.0045 * (2.0 * t - 1.0) ** 2 - 0.0016
        h, _ = surface_point(bvh, mx, my)
        pts.append(Vector((mx, my, (h.z if h is not None else 0.08) + 0.0006)))
    m.tube(pts, [0.0016, 0.0032, 0.0040, 0.0042, 0.0042, 0.0042, 0.0040, 0.0032, 0.0016], 0.0020, n=6, mat=GEAR,
           caps=("round", "round"), ref=Vector((0, 0, 1)))
    for s in (1, -1):
        nx, ny = s * 0.0105, 1.636
        h, _ = surface_point(bvh, nx, ny)
        m.ellipsoid(Vector((nx, ny, (h.z if h is not None else 0.095) + 0.0004)), (0.0036, 0.0024, 0.0020), nu=8, nv=6,
                    mat=GEAR)

    return bvh


def build_helmet(m, bvh):
    m.set_tag(mode=2, grp=G_HEAD, reg=R_HEAD, bone="Head")
    inside = Vector((0, 1.712, -0.01))
    m.surface(lambda u, v: helmet_fn(u, v), 32, 12, 0.0085, mat=ARMOR, close_u=True, inside=inside)

    # Accent identification patches on the helmet (follow the shell surface).
    def patch(u0, u1, v0, v1, grow=0.0035, mat=ACCENT, thick=0.003):
        m.surface(lambda u, v: helmet_fn(lerp(u0, u1, u), lerp(v0, v1, v), grow=grow), 6, 3, thick, mat=mat,
                  inside=inside)

    patch(0.755, 0.805, 0.30, 0.62)      # right side tab
    patch(0.195, 0.245, 0.30, 0.62)      # left side tab
    patch(0.465, 0.535, 0.22, 0.42, grow=0.0032)  # rear band

    # NVG shroud/mount at the front, side rails and rear pouch.
    m.box(Vector((0, 1.806, 0.071)), (0.048, 0.030, 0.022), R=euler_matrix(-32, 0, 0), bevel=0.005, seg=1, mat=ARMOR)
    for s in (1, -1):
        m.box(Vector((s * 0.098, 1.752, 0.020)), (0.012, 0.070, 0.026), R=euler_matrix(0, 0, -s * 8), bevel=0.003, mat=GEAR)
        for i in range(4):
            m.box(Vector((s * 0.1055, 1.727 + i * 0.016, 0.020)), (0.004, 0.008, 0.026), bevel=0.0015, mat=GEAR)
    m.box(Vector((0, 1.745, -0.142)), (0.090, 0.056, 0.036), R=euler_matrix(-12, 0, 0), bevel=0.008, seg=1, mat=GEAR)
    m.box(Vector((0, 1.745, -0.1625)), (0.060, 0.034, 0.004), bevel=0.0015, mat=ARMOR)
    # Shell edge band (rubber trim).
    m.surface(lambda u, v: helmet_fn(u, lerp(0.955, 1.0, v), grow=0.0016, rim_pad=-0.001), 32, 1, 0.0035, mat=GEAR,
              close_u=True, inside=inside)

    # Chin strap and cup.
    for s in (1, -1):
        raw = [Vector((s * 0.0865, 1.690, 0.018)), Vector((s * 0.0835, 1.652, 0.036)),
               Vector((s * 0.062, 1.598, 0.060)), Vector((s * 0.032, 1.567, 0.070)), Vector((s * 0.006, 1.560, 0.074))]
        path = []
        for p in raw:
            path.append(project(bvh, p, 0.0042) if True else p)
        m.tube(path, 0.0070, 0.0020, n=6, mat=GEAR, sq=0.6, caps=("flat", "flat"), ref=Vector((0, 0, 1)))
    m.ellipsoid(Vector((0, 1.563, 0.078)), (0.024, 0.0075, 0.020), nu=10, nv=6, mat=GEAR)
    # Buckles
    for s in (1, -1):
        m.box(Vector((s * 0.0845, 1.676, 0.0262)), (0.010, 0.016, 0.008), bevel=0.002, mat=ARMOR)


def build_headset(m, bvh):
    m.set_tag(mode=2, grp=G_HEAD, reg=R_HEAD, bone="Head")
    for s in (1, -1):
        c = Vector((s * 0.0975, 1.676, -0.012))
        R = euler_matrix(0, -s * 4, 0)
        m.ellipsoid(c, (0.020, 0.036, 0.034), R=R, nu=12, nv=8, mat=GEAR)
        m.ellipsoid(c + Vector((s * 0.0155, 0, 0)), (0.0105, 0.028, 0.026), R=R, nu=12, nv=6, mat=ARMOR)
        m.tube([c + Vector((s * 0.024, 0.0, 0.0)), c + Vector((s * 0.0265, 0.0, 0.0))], [0.0155, 0.0155], [0.0155, 0.0155],
               n=14, mat=ACCENT, caps=("flat", "flat"), ref=Vector((0, 0, 1)))
        # headband arc up to the helmet rail
        m.tube([c + Vector((s * 0.002, 0.030, 0.0)), Vector((s * 0.1015, 1.722, -0.006)), Vector((s * 0.1045, 1.748, 0.0))],
               0.0045, 0.0120, n=6, mat=GEAR, caps=("flat", "flat"), ref=Vector((0, 0, 1)), sq=0.7)
    # Boom microphone on the right ear cup.
    s = 1
    start = Vector((0.1125, 1.662, 0.000))
    raw = [start, Vector((0.1090, 1.642, 0.030)), Vector((0.098, 1.618, 0.070)), Vector((0.072, 1.600, 0.104)),
           Vector((0.040, 1.596, 0.120)), Vector((0.022, 1.600, 0.122))]
    m.tube(raw, 0.0020, n=6, mat=GEAR, caps=("round", "flat"))
    m.ellipsoid(Vector((0.017, 1.601, 0.1225)), (0.0085, 0.0075, 0.0070), nu=10, nv=8, mat=GEAR)
    m.ellipsoid(Vector((0.0145, 1.601, 0.1225)), (0.0025, 0.0065, 0.0060), nu=8, nv=6, mat=ARMOR)


def build_goggles(m):
    m.set_tag(mode=2, grp=G_HEAD, reg=R_HEAD, bone="Head")
    inside = Vector((0, 1.712, -0.01))
    # Elastic strap around the helmet.
    m.surface(lambda u, v: helmet_fn(u, lerp(0.20, 0.34, v), grow=0.0050), 32, 1, 0.0032, mat=GEAR, close_u=True,
              inside=inside)

    # Frame + lens, sitting on the front of the helmet, curved with the shell.
    def curve(x, y, off):
        a = x / 0.095
        z = 0.1235 * math.cos(math.asin(max(-0.98, min(0.98, a * 0.9)))) - 0.010 + off
        return Vector((x, y, z))

    def frame_fn(u, v):
        x = lerp(-0.074, 0.074, u)
        y = lerp(1.746, 1.782, v)
        return curve(x, y - 0.0175 * 0.0, 0.010 + 0.006 * (1 - abs(2 * u - 1) ** 2))

    m.surface(frame_fn, 10, 2, 0.012, mat=GEAR, inside=Vector((0, 1.76, -0.02)))

    def lens_fn(u, v):
        x = lerp(-0.067, 0.067, u)
        y = lerp(1.7505, 1.7775, v)
        return curve(x, y, 0.0180 + 0.006 * (1 - abs(2 * u - 1) ** 2))

    m.surface(lens_fn, 10, 2, 0.003, mat=LENS, inside=Vector((0, 1.76, -0.02)))
    # Nose cut-out bridge and side clips.
    m.box(Vector((0, 1.745, 0.1215)), (0.020, 0.010, 0.012), bevel=0.003, mat=GEAR)
    for s in (1, -1):
        m.box(Vector((s * 0.088, 1.766, 0.080)), (0.014, 0.030, 0.020), R=euler_matrix(0, s * 35, 0), bevel=0.003, mat=ARMOR)


def _tris(m):
    return sum(len(f.verts) - 2 for f in m.bm.faces)


def build_head_group(m, report=False):
    t0 = _tris(m)
    build_neck(m)
    t1 = _tris(m)
    bvh = build_head(m)
    t2 = _tris(m)
    build_helmet(m, bvh)
    t3 = _tris(m)
    build_headset(m, bvh)
    t4 = _tris(m)
    build_goggles(m)
    t5 = _tris(m)
    print("HEAD_TRIS neck %d face %d helmet %d headset %d goggles %d" % (t1 - t0, t2 - t1, t3 - t2, t4 - t3, t5 - t4))
