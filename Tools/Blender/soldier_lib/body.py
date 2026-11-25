"""Uniform base layer: torso shirt, sleeves, trousers, belt loops, boots, gloves."""

import math

from mathutils import Vector

from .geometry import (ARMOR, CLOTH, GEAR, ACCENT, SKIN, G_ARM_L, G_ARM_R, R_TORSO, R_ARM_L, R_ARM_R, R_LEG_L, R_LEG_R,
                       euler_matrix, lerp, smoothstep)

# Joint layout shared with the C# HumanoidProportions class.
HIPS_Y, WAIST_Y, CHEST_Y, SHOULDER_Y, NECK_Y, HEAD_Y = 0.98, 1.04, 1.22, 1.41, 1.44, 1.52
HIP_HALF, SHOULDER_HALF = 0.11, 0.215
KNEE_Y, HIP_JOINT_Y, ANKLE_Y = 0.52, 0.94, 0.10
UPPER_ARM, FOREARM = 0.29, 0.27
ELBOW_Y = SHOULDER_Y - UPPER_ARM
WRIST_Y = ELBOW_Y - FOREARM


def V3(x, y, z):
    return Vector((x, y, z))


def interp_table(table, y):
    """table: list of (y, a, b, ...) sorted ascending in y -> interpolated tuple."""
    if y <= table[0][0]:
        return table[0][1:]
    for i in range(len(table) - 1):
        y0, y1 = table[i][0], table[i + 1][0]
        if y <= y1:
            t = smoothstep(y0, y1, y)
            t = (y - y0) / (y1 - y0) * 0.5 + t * 0.5
            return tuple(lerp(a, b, t) for a, b in zip(table[i][1:], table[i + 1][1:]))
    return table[-1][1:]


# (y, rx, zf, zb, cz)
TORSO_TABLE = [
    (0.84, 0.140, 0.095, 0.105, 0.000),
    (0.98, 0.150, 0.098, 0.104, 0.000),
    (1.06, 0.150, 0.098, 0.098, 0.000),
    (1.14, 0.150, 0.100, 0.102, 0.000),
    (1.24, 0.158, 0.110, 0.103, 0.000),
    (1.32, 0.166, 0.112, 0.103, 0.000),
    (1.38, 0.168, 0.104, 0.098, 0.000),
    (1.42, 0.150, 0.088, 0.086, 0.000),
    (1.45, 0.112, 0.076, 0.078, 0.000),
    (1.48, 0.084, 0.069, 0.069, 0.000),
    (1.51, 0.068, 0.066, 0.066, 0.000),
]


def torso_ring(m, y, grow=0.0, n=28, sq=0.9, table=TORSO_TABLE, dz=0.0):
    rx, zf, zb, cz = interp_table(table, y)
    pts = []
    for k in range(n):
        a = 2.0 * math.pi * k / n
        c, s = math.cos(a), math.sin(a)
        x = (rx + grow) * math.copysign(abs(c) ** sq, c)
        zz = (zf + grow) if s >= 0 else (zb + grow)
        z = cz + dz + zz * math.copysign(abs(s) ** sq, s)
        pts.append(V3(x, y, z))
    return pts


def torso_station(y, grow=0.0):
    rx, zf, zb, cz = interp_table(TORSO_TABLE, y)
    return rx + grow, zf + grow, zb + grow, cz


def wrinkle(y, center, width, freq, amp):
    return amp * math.exp(-((y - center) / width) ** 2) * math.sin((y - center) * 2.0 * math.pi * freq)


def build_shirt(m):
    m.base(R_TORSO)
    m.default_mat = CLOTH
    ys = [1.00 + i * 0.0225 for i in range(24)]
    rings = []
    for y in ys:
        if y > 1.51:
            break
        ring = torso_ring(m, y, n=28)
        out = []
        for k, p in enumerate(ring):
            a = 2.0 * math.pi * k / len(ring)
            f = 1.0 + 0.010 * math.sin(a * 5.0 + y * 24.0) * (0.4 + 0.6 * smoothstep(1.0, 1.2, y)) \
                + wrinkle(y, 1.10, 0.05, 38.0, 0.012) * (0.5 + 0.5 * math.sin(a))
            c = V3(0, y, 0)
            q = c + (p - c) * f
            q.y = p.y
            out.append(q)
        rings.append(out)
    m.loft(rings, CLOTH)

    # Stand collar around the neck.
    path = [V3(0, 1.440, 0.004), V3(0, 1.480, 0.006), V3(0, 1.515, 0.010)]
    m.tube(path, [0.071, 0.067, 0.065], [0.069, 0.065, 0.063], n=24, mat=CLOTH, caps=("flat", "flat"))


def sleeve_radius(y):
    table = [(WRIST_Y, 0.037, 0.034), (0.90, 0.036, 0.034), (0.98, 0.041, 0.039), (1.06, 0.045, 0.043),
             (1.12, 0.047, 0.046), (1.20, 0.049, 0.048), (1.30, 0.055, 0.053), (1.38, 0.056, 0.054), (1.41, 0.052, 0.050)]
    return interp_table(table, y)


def build_sleeve(m, side):
    reg = R_ARM_R if side > 0 else R_ARM_L
    m.base(reg, grp=G_ARM_R if side > 0 else G_ARM_L)
    x = side * SHOULDER_HALF
    ys = []
    y = 1.405
    while y > WRIST_Y - 0.005:
        ys.append(y)
        y -= 0.015
    ys.append(WRIST_Y - 0.004)
    path = [V3(x, yy, 0.0) for yy in ys]

    def ru(i):
        return sleeve_radius(ys[i])[0]

    def rv(i):
        return sleeve_radius(ys[i])[1]

    def mod(s, a):
        yy = ys[min(len(ys) - 1, int(s * (len(ys) - 1) + 0.5))]
        front = max(0.0, math.sin(a))
        back = max(0.0, -math.sin(a))
        f = 1.0
        f += wrinkle(yy, ELBOW_Y, 0.055, 52.0, 0.034) * (0.35 + 0.65 * front)
        f += wrinkle(yy, ELBOW_Y - 0.01, 0.08, 40.0, 0.016) * (0.4 + 0.6 * back)
        f += 0.010 * math.sin(a * 3.0 + yy * 33.0) * smoothstep(0.88, 1.0, yy)
        f += 0.014 * math.exp(-((yy - 0.90) / 0.03) ** 2)
        return f

    ru_l = [ru(i) for i in range(len(ys))]
    rv_l = [rv(i) for i in range(len(ys))]
    m.tube(path, ru_l, rv_l, n=22, mat=CLOTH, mod=mod, ref=V3(1, 0, 0), caps=("round", "flat"), tip_scale=0.8)

    # Velcro cuff strap and elbow seam.
    m.set_tag(reg=reg)
    m.tube([V3(x, WRIST_Y + 0.03, 0.0), V3(x, WRIST_Y - 0.002, 0.0)], [0.040, 0.040], [0.038, 0.038], n=22,
           mat=GEAR, caps=("flat", "flat"))


def leg_radius(y):
    table = [(0.20, 0.060, 0.062), (0.26, 0.062, 0.064), (0.34, 0.060, 0.063), (0.42, 0.063, 0.068),
             (0.52, 0.066, 0.074), (0.60, 0.072, 0.080), (0.74, 0.083, 0.090), (0.88, 0.092, 0.100),
             (0.98, 0.096, 0.104)]
    return interp_table(table, y)


def build_trousers(m, side):
    reg = R_LEG_R if side > 0 else R_LEG_L
    m.base(reg)
    x = side * HIP_HALF
    ys = []
    y = 0.985
    while y > 0.185:
        ys.append(y)
        y -= 0.03 if y > 0.30 else 0.022
    ys.append(0.185)
    path = [V3(x, yy, 0.0) for yy in ys]
    ru_l, rv_l = [], []
    for yy in ys:
        a, b = leg_radius(yy)
        # blousing bunch over the boot top
        bunch = 0.012 * smoothstep(0.17, 0.25, yy) * (1.0 - smoothstep(0.26, 0.32, yy))
        ru_l.append(a + bunch)
        rv_l.append(b + bunch)

    def mod(s, a):
        yy = ys[min(len(ys) - 1, int(s * (len(ys) - 1) + 0.5))]
        front = max(0.0, math.sin(a))
        f = 1.0
        f += wrinkle(yy, KNEE_Y, 0.06, 46.0, 0.030) * (0.3 + 0.7 * front)
        f += wrinkle(yy, KNEE_Y - 0.02, 0.07, 36.0, 0.014) * (0.3 + 0.7 * (1 - front))
        f += wrinkle(yy, 0.93, 0.08, 28.0, 0.020) * (0.3 + 0.7 * front)
        f += 0.012 * math.sin(a * 2.0 + yy * 21.0 + side) * smoothstep(0.3, 0.8, yy)
        f += 0.018 * math.sin(a * 6.0 + yy * 90.0) * math.exp(-((yy - 0.235) / 0.04) ** 2)
        return f

    m.tube(path, ru_l, rv_l, n=24, mat=CLOTH, mod=mod, ref=V3(1, 0, 0), caps=("round", "flat"), tip_scale=0.6)

    # Pelvis / seat fill between the legs.
    if side > 0:
        rings = []
        for y in (0.74, 0.77, 0.80, 0.84, 0.90, 0.97, 1.03, 1.075):
            rx = lerp(0.060, 0.125, smoothstep(0.74, 0.80, y)) if y < 0.80 else lerp(0.125, 0.172, smoothstep(0.80, 0.95, y)) if y < 0.99 else lerp(0.172, 0.158, smoothstep(0.99, 1.08, y))
            zf = lerp(0.040, 0.070, smoothstep(0.74, 0.80, y)) * (1.0 if y < 0.80 else 1.0) + lerp(0.0, 0.010, smoothstep(0.80, 0.97, y)) + 0.020 * smoothstep(0.99, 1.06, y)
            zb = lerp(0.050, 0.080, smoothstep(0.74, 0.80, y)) + lerp(0.0, 0.006, smoothstep(0.80, 0.97, y)) + 0.020 * smoothstep(0.99, 1.06, y)
            pts = []
            n = 30
            for k in range(n):
                a = 2.0 * math.pi * k / n
                c, s = math.cos(a), math.sin(a)
                pts.append(V3(rx * math.copysign(abs(c) ** 0.85, c), y,
                              (zf if s >= 0 else zb) * math.copysign(abs(s) ** 0.85, s)))
            rings.append(pts)
        m.base(R_TORSO)
        m.loft(rings, CLOTH)


def build_cargo_pockets(m, side):
    reg = R_LEG_R if side > 0 else R_LEG_L
    m.xfer(reg)
    # Pocket on the front-outer quadrant of the thigh, angled with the leg surface.
    ang = side * 40.0
    cx, cz = side * (HIP_HALF + 0.062), 0.076
    R = euler_matrix(0, ang, 0)
    m.box(V3(cx, 0.715, cz), (0.100, 0.150, 0.030), R=R, bevel=0.006, seg=1, mat=CLOTH)
    m.box(V3(cx + side * 0.007, 0.775, cz + 0.007), (0.106, 0.060, 0.034), R=R, bevel=0.005, seg=1, mat=CLOTH)
    m.box(V3(cx + side * 0.012, 0.752, cz + 0.021), (0.020, 0.012, 0.010), R=R, bevel=0.003, mat=ARMOR)


def build_boot(m, side):
    reg = R_LEG_R if side > 0 else R_LEG_L
    x = side * HIP_HALF
    # Foot (rigid to the foot bone). Stations along z (forward).
    zs = [-0.092, -0.080, -0.055, -0.020, 0.025, 0.070, 0.120, 0.165, 0.205, 0.232, 0.245]
    half_w = [0.030, 0.040, 0.046, 0.048, 0.050, 0.054, 0.056, 0.053, 0.044, 0.030, 0.012]
    y_bot = [0.020, 0.012, 0.010, 0.010, 0.010, 0.010, 0.012, 0.020, 0.034, 0.048, 0.062]
    y_top = [0.105, 0.135, 0.150, 0.145, 0.125, 0.105, 0.088, 0.078, 0.074, 0.070, 0.066]
    m.rigid("FootR" if side > 0 else "FootL", reg=reg)
    rings = []
    for z, hw, yb, yt in zip(zs, half_w, y_bot, y_top):
        rings.append(m.ring(V3(x, (yb + yt) * 0.5 + 0.018, z), V3(1, 0, 0), V3(0, 1, 0), hw, (yt - yb) * 0.5 + 0.003, 16, 0.78))
    m.loft(rings, GEAR)

    # Rubber sole with heel block.
    soles = []
    sz = [-0.098, -0.090, -0.050, 0.00, 0.08, 0.16, 0.215, 0.245, 0.256]
    sw = [0.030, 0.038, 0.047, 0.049, 0.055, 0.056, 0.047, 0.033, 0.014]
    for z, hw in zip(sz, sw):
        yb = 0.0 if z < 0.2 else 0.0 + (z - 0.2) * 0.35
        ring = m.ring(V3(x, (yb + 0.036) * 0.5, z), V3(1, 0, 0), V3(0, 1, 0), hw + 0.004, (0.036 - yb) * 0.5, 14, 0.5)
        soles.append(ring)
    m.loft(soles, ARMOR)
    m.box(V3(x, 0.025, -0.060), (0.082, 0.05, 0.050), bevel=0.008, seg=1, mat=ARMOR)
    # Tread lugs.
    for i in range(7):
        z = 0.02 + i * 0.033
        w = 0.055 - max(0.0, z - 0.14) * 0.5
        m.box(V3(x, 0.004, z), (max(0.03, w * 2), 0.010, 0.014), bevel=0.003, mat=ARMOR)
    # Toe cap and laces.
    m.ellipsoid(V3(x, 0.050, 0.205), (0.046, 0.030, 0.052), nu=14, nv=8, mat=ARMOR)
    for i in range(6):
        z = 0.145 - i * 0.027
        yy = 0.083 + 0.050 * smoothstep(0.0, 1.0, i / 5.0)
        m.cyl(V3(x - 0.034, yy + 0.004, z), V3(x + 0.034, yy + 0.004, z), 0.0042, n=6, mat=CLOTH)
    m.box(V3(x, 0.118, 0.020), (0.052, 0.024, 0.090), R=euler_matrix(-18, 0, 0), bevel=0.008, mat=GEAR)

    # Shaft (blended between foot and shin).
    m.base(reg)
    path = [V3(x, 0.070, 0.000), V3(x, 0.14, 0.002), V3(x, 0.215, 0.004), V3(x, 0.262, 0.006)]
    m.tube(path, [0.054, 0.056, 0.055, 0.058], [0.058, 0.060, 0.057, 0.060], n=20, mat=GEAR, caps=("flat", "flat"))
    path = [V3(x, 0.250, 0.006), V3(x, 0.272, 0.006)]
    m.tube(path, [0.060, 0.061], [0.062, 0.064], n=20, mat=ARMOR, caps=("flat", "flat"))


def build_glove(m, side, detail=1.0):
    """Hand with a curled grip, in rest pose hanging below the wrist. Palm faces +Z, thumb on the +side."""
    reg = R_ARM_R if side > 0 else R_ARM_L
    m.base(reg, grp=G_ARM_R if side > 0 else G_ARM_L)
    x0 = side * SHOULDER_HALF
    wy = WRIST_Y
    n = 14 if detail > 1.2 else 12

    # Palm / back of hand.
    stations = [(wy + 0.012, 0.037, 0.027, 0.0), (wy - 0.012, 0.039, 0.025, 0.0), (wy - 0.045, 0.045, 0.022, 0.001),
                (wy - 0.080, 0.047, 0.019, 0.002), (wy - 0.098, 0.046, 0.017, 0.003)]
    path = [V3(x0, s[0], s[3]) for s in stations]
    m.tube(path, [s[1] for s in stations], [s[2] for s in stations], n=n + 4, mat=GEAR, sq=0.8,
           caps=("flat", "round"), tip_scale=0.45)
    # Palm pad.
    m.ellipsoid(V3(x0 + side * 0.006, wy - 0.052, 0.014), (0.032, 0.036, 0.010), nu=12, nv=8, mat=GEAR)

    # Cuff band.
    m.tube([V3(x0, wy + 0.018, 0.0), V3(x0, wy - 0.004, 0.0)], [0.040, 0.040], [0.038, 0.038], n=n + 6, mat=GEAR,
           caps=("flat", "flat"), sq=0.9)
    m.tube([V3(x0, wy + 0.004, 0.0), V3(x0, wy - 0.006, 0.0)], [0.0415, 0.0415], [0.0395, 0.0395], n=n + 6, mat=ACCENT,
           caps=("flat", "flat"), sq=0.9)

    # Knuckle guard on the back of the hand.
    R = euler_matrix(-6, 0, 0)
    m.box(V3(x0, wy - 0.070, -0.0215), (0.078, 0.052, 0.008), R=R, bevel=0.003, seg=1, mat=ARMOR)
    for i in range(4):
        xx = x0 + side * (0.030 - i * 0.020)
        m.ellipsoid(V3(xx, wy - 0.097, -0.017), (0.0095, 0.0085, 0.0075), nu=8, nv=6, mat=ARMOR)
    # Wrist strap.
    m.box(V3(x0, wy - 0.012, -0.023), (0.052, 0.016, 0.006), bevel=0.002, mat=ACCENT)

    # Fingers (index .. pinky from the thumb side).
    finger_x = [0.0345, 0.0115, -0.0115, -0.0335]
    lens = [(0.040, 0.026, 0.022), (0.043, 0.029, 0.023), (0.040, 0.027, 0.022), (0.032, 0.021, 0.019)]
    curl = [(32, 46, 34), (36, 50, 36), (40, 52, 38), (44, 54, 40)]
    for i in range(4):
        fx = x0 + side * finger_x[i]
        base = V3(fx, wy - 0.094, 0.004)
        p = [base]
        ang = 0.0
        cur = base
        for seg in range(3):
            ang += math.radians(curl[i][seg])
            d = V3(side * 0.012 * (1.5 - i) * 0.0, -math.cos(ang), math.sin(ang))
            cur = cur + d * lens[i][seg]
            p.append(cur)
        r0 = 0.0090 - i * 0.0004
        radii = [r0, r0 * 0.96, r0 * 0.88, r0 * 0.80]
        m.tube(p, radii, [r * 0.92 for r in radii], n=8 if detail < 1.2 else 10, mat=GEAR, sq=0.9, caps=("flat", "round"),
               tip_scale=0.9, ref=V3(1, 0, 0))
        # knuckle shell bump + finger pad seam
        m.ellipsoid(base + V3(0, -0.004, -0.010), (r0 * 0.9, 0.010, 0.006), nu=8, nv=6, mat=ARMOR)

    # Thumb.
    tb = V3(x0 + side * 0.034, wy - 0.052, 0.010)
    d1 = V3(side * 0.55, -0.62, 0.55).normalized()
    d2 = V3(side * 0.30, -0.55, 0.78).normalized()
    t1 = tb + d1 * 0.036
    t2 = t1 + d2 * 0.030
    m.tube([tb, t1, t2], [0.0125, 0.0108, 0.0092], [0.0115, 0.0100, 0.0086], n=10, mat=GEAR, caps=("flat", "round"),
           tip_scale=0.9, ref=V3(0, 0, 1))
    m.ellipsoid(tb + V3(side * -0.004, 0.008, 0.0), (0.020, 0.024, 0.016), nu=10, nv=8, mat=GEAR)
