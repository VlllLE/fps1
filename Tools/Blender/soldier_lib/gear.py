"""Tactical gear: plate carrier, plates, pouches, straps, belt, holster, backpack and radio, pads."""

import math

from mathutils import Vector

from .geometry import (ARMOR, CLOTH, GEAR, ACCENT, R_TORSO, R_ARM_L, R_ARM_R, R_LEG_L, R_LEG_R,
                       G_ARM_L, G_ARM_R, euler_matrix, lerp, smoothstep)
from .body import V3, torso_ring, torso_station, SHOULDER_HALF, ELBOW_Y

SQ = 0.9


def torso_z(y, x, front=True, grow=0.0):
    """Z of the torso surface (superellipse of body.torso_ring) at height y and lateral x."""
    rx, zf, zb, cz = torso_station(y, grow)
    t = min(abs(x) / rx, 0.999)
    c2 = t ** (2.0 / SQ)
    s = max(0.0, 1.0 - c2) ** (0.5 * SQ)
    return cz + (zf if front else -zb) * s


def torso_normal_tilt(y, x, front=True):
    """Rotation (degrees about Y) that aligns a flat piece with the torso surface at lateral x."""
    e = 0.004
    z0 = torso_z(y, x - e, front)
    z1 = torso_z(y, x + e, front)
    return math.degrees(math.atan2(z1 - z0, 2 * e)) * (1 if front else 1)


def torso_pitch(y, front=True):
    """Rotation (degrees about X) aligning a piece with the torso's front/back slope at height y."""
    e = 0.006
    z0 = torso_z(y - e, 0.0, front)
    z1 = torso_z(y + e, 0.0, front)
    return -math.degrees(math.atan2(z1 - z0, 2 * e))


def place_front(m, y, x, size, off, front=True, mat=GEAR, bevel=0.005, seg=1, tilt=1.0, pitch=1.0):
    """Box lying on the torso surface (front or back) at (x, y), ``off`` metres proud of the skin."""
    z = torso_z(y, x, front, 0.0)
    sgn = 1.0 if front else -1.0
    ang = torso_normal_tilt(y, x, front)
    R = euler_matrix(torso_pitch(y, front) * pitch * (1 if front else -1), -ang * tilt * sgn if front else ang * tilt, 0)
    nx = Vector((0, 0, sgn))
    c = V3(x, y, z) + nx * (off + size[2] * 0.5)
    return m.box(c, size, R=R, bevel=bevel, seg=seg, mat=mat)


def build_vest(m):
    """Plate carrier: shell loft around the torso, shoulder straps, plates and webbing."""
    m.xfer(R_TORSO)
    ys = [1.13 + i * 0.0225 for i in range(13)]
    rings = []
    for y in ys:
        if y > 1.395:
            break
        g = 0.017 + 0.004 * smoothstep(1.13, 1.2, y)
        ring = torso_ring(m, y, grow=g, n=28, sq=SQ)
        rings.append(ring)
    m.loft(rings, GEAR)

    m.rigid("Chest", reg=R_TORSO)


def build_shoulder_straps(m):
    """Padded straps over each shoulder following the trapezius slope."""
    m.rigid("Chest", reg=R_TORSO)
    for s in (1, -1):
        x = s * 0.105
        path = []
        n = 10
        for i in range(n):
            t = i / (n - 1.0)
            yf = 1.385
            # y follows the shoulder slope (torso loft rx narrows toward the neck)
            ytop = 1.388 + 0.056 * math.sin(t * math.pi) ** 0.8
            zf = torso_z(min(ytop, 1.45), x, True, 0.0) + 0.0
            zb = torso_z(min(ytop, 1.45), x, False, 0.0)
            z = lerp(zf, zb, t)
            path.append(Vector((x - s * 0.012 * math.sin(t * math.pi), ytop + 0.014, z)))
        m.tube(path, 0.026, 0.0075, n=8, mat=GEAR, sq=0.9, caps=("flat", "flat"), ref=Vector((1, 0, 0)))
        # Accent tab on the shoulder strap (team colour)
        mid = path[n // 2]
        m.box(mid + Vector((0, 0.010, 0)), (0.040, 0.004, 0.060), bevel=0.0015, mat=ACCENT)


def build_plates(m):
    m.rigid("Chest", reg=R_TORSO)
    # Front and back ballistic plates.
    place_front(m, 1.285, 0.0, (0.205, 0.255, 0.018), 0.022, True, ARMOR, bevel=0.012, seg=2)
    place_front(m, 1.285, 0.0, (0.205, 0.255, 0.018), 0.022, False, ARMOR, bevel=0.012, seg=2)
    # Bevel plate edge frames and side plates.
    for s in (1, -1):
        place_front(m, 1.255, s * 0.155, (0.060, 0.150, 0.014), 0.020, True, ARMOR, bevel=0.005)
        place_front(m, 1.255, s * 0.150, (0.060, 0.150, 0.014), 0.020, False, ARMOR, bevel=0.005)
    # Chest rig: admin panel with Accent identification strip above the plate.
    place_front(m, 1.395, 0.0, (0.190, 0.034, 0.010), 0.022, True, ACCENT, bevel=0.003)
    place_front(m, 1.425, 0.0, (0.150, 0.025, 0.010), 0.020, False, ACCENT, bevel=0.003)
    # Plate retention: centre buckle / cable ties.
    place_front(m, 1.158, 0.0, (0.050, 0.020, 0.012), 0.020, True, ARMOR, bevel=0.003)


def build_pouches(m):
    m.rigid("Chest", reg=R_TORSO)
    # Triple rifle magazine pouch across the lower front of the vest (flap + strap + button each).
    for i, x in enumerate((-0.095, -0.032, 0.031, 0.094)):
        y = 1.155
        place_front(m, y, x, (0.057, 0.090, 0.030), 0.030, True, GEAR, bevel=0.007)
        place_front(m, y + 0.037, x, (0.058, 0.028, 0.034), 0.030, True, GEAR, bevel=0.005)
        place_front(m, y + 0.037, x, (0.016, 0.008, 0.036), 0.034, True, ARMOR, bevel=0.002)
    # Utility pouches on the cummerbund sides.
    for s in (1, -1):
        place_front(m, 1.17, s * 0.146, (0.074, 0.100, 0.044), 0.020, True, GEAR, bevel=0.008)
        place_front(m, 1.205, s * 0.146, (0.076, 0.034, 0.048), 0.020, True, GEAR, bevel=0.005)
    # Radio / comms pouch and tourniquet on the front chest.
    place_front(m, 1.352, 0.0, (0.060, 0.050, 0.026), 0.034, True, GEAR, bevel=0.005, tilt=0.0)
    # MOLLE webbing rows on the cummerbund.
    for y in (1.265, 1.285, 1.305, 1.325):
        for s in (1, -1):
            place_front(m, y, s * 0.154, (0.058, 0.008, 0.006), 0.022, True, GEAR, bevel=0.002)


def build_belt(m):
    m.xfer(R_TORSO)
    ys = [1.010 + i * 0.016 for i in range(5)]
    rings = [torso_ring(m, y, grow=0.016, n=28, sq=SQ) for y in ys]
    m.loft(rings, GEAR)
    m.rigid("Hips", reg=R_TORSO)
    place_front(m, 1.043, 0.0, (0.058, 0.044, 0.012), 0.015, True, ARMOR, bevel=0.004)
    place_front(m, 1.043, 0.0, (0.040, 0.026, 0.014), 0.020, True, GEAR, bevel=0.003)
    # Holster on the right hip, dump pouch on the left.
    c = V3(0.158, 0.935, -0.035)
    m.box(c, (0.040, 0.150, 0.070), R=euler_matrix(0, 0, -4), bevel=0.008, seg=1, mat=GEAR)
    m.box(c + Vector((0.020, -0.010, 0.0)), (0.012, 0.100, 0.052), bevel=0.003, mat=ARMOR)
    m.box(c + Vector((-0.003, 0.080, 0.002)), (0.034, 0.016, 0.050), bevel=0.003, mat=GEAR)
    m.box(c + Vector((0.0, -0.055, 0.0)), (0.050, 0.014, 0.076), bevel=0.003, mat=GEAR)
    c = V3(-0.150, 0.965, -0.060)
    m.box(c, (0.058, 0.090, 0.095), R=euler_matrix(0, 0, 4), bevel=0.010, seg=1, mat=GEAR)
    m.box(c + Vector((0.0, 0.042, 0.0)), (0.062, 0.028, 0.100), R=euler_matrix(0, 0, 4), bevel=0.006, mat=GEAR)
    m.box(c + Vector((-0.002, 0.020, 0.052)), (0.016, 0.034, 0.008), bevel=0.002, mat=ARMOR)


def build_backpack(m):
    m.rigid("Chest", reg=R_TORSO)
    zb = -torso_station(1.25)[2] - 0.030
    cy = 1.255
    # Main assault-pack body: tapered box with a rounded back.
    m.box(V3(0, cy, zb - 0.095), (0.280, 0.340, 0.170), bevel=0.028, seg=2, mat=GEAR,
          taper=lambda p: (1.0 - 0.10 * (0.5 - p.y / 0.34) * 0.5, 1.0))
    # Top lid and compression straps.
    m.box(V3(0, cy + 0.180, zb - 0.095), (0.264, 0.040, 0.174), bevel=0.012, seg=1, mat=GEAR)
    for x in (-0.085, 0.085):
        m.box(V3(x, cy + 0.04, zb - 0.186), (0.026, 0.300, 0.012), bevel=0.003, mat=ARMOR)
        m.box(V3(x, cy + 0.0, zb - 0.192), (0.036, 0.020, 0.012), bevel=0.003, mat=ARMOR)
    # Side pouches.
    for s in (1, -1):
        m.box(V3(s * 0.158, cy - 0.060, zb - 0.075), (0.040, 0.160, 0.110), bevel=0.012, seg=1, mat=GEAR)
        m.box(V3(s * 0.158, cy + 0.030, zb - 0.075), (0.044, 0.030, 0.114), bevel=0.006, mat=GEAR)
    # MOLLE rows on the back panel.
    for k in range(5):
        m.box(V3(0, cy - 0.110 + k * 0.040, zb - 0.1885), (0.200, 0.008, 0.006), bevel=0.002, mat=GEAR)
    # Bedroll / accent stripe across the pack.
    m.box(V3(0, cy - 0.0, zb - 0.190), (0.200, 0.030, 0.006), bevel=0.0015, mat=ACCENT)
    # Radio on the left of the pack with antenna.
    m.box(V3(-0.095, cy + 0.222, zb - 0.040), (0.070, 0.110, 0.060), bevel=0.010, seg=1, mat=ARMOR)
    m.box(V3(-0.095, cy + 0.222, zb - 0.0085), (0.052, 0.080, 0.006), bevel=0.002, mat=GEAR)
    m.cyl(V3(-0.095, cy + 0.280, zb - 0.040), V3(-0.095, cy + 0.318, zb - 0.040), 0.012, 0.010, n=10, mat=GEAR)
    m.tube([V3(-0.095, cy + 0.316, zb - 0.040), V3(-0.098, cy + 0.50, zb - 0.052), V3(-0.108, cy + 0.66, zb - 0.062)],
           [0.0045, 0.0030, 0.0016], n=6, mat=GEAR, caps=("flat", "flat"))
    m.ellipsoid(V3(-0.095, cy + 0.300, zb - 0.040), (0.0165, 0.012, 0.0165), nu=10, nv=6, mat=ARMOR)


def build_pads(m):
    """Shoulder, elbow and knee pads. Shoulder pads are separate meshes so FP view can hide them."""
    for s, grp, reg in ((1, G_ARM_R, R_ARM_R), (-1, G_ARM_L, R_ARM_L)):
        bone = "UpperArmR" if s > 0 else "UpperArmL"
        m.set_tag(mode=2, bone=bone, grp=grp, reg=reg)
        c = V3(s * (SHOULDER_HALF + 0.010), 1.388, 0.000)
        R = euler_matrix(0, 0, -s * 18)
        m.ellipsoid(c, (0.062, 0.050, 0.074), R=R, nu=14, nv=8, mat=ARMOR)
        m.ellipsoid(c + Vector((s * 0.028, -0.032, 0)), (0.050, 0.030, 0.070), R=euler_matrix(0, 0, -s * 40), nu=12, nv=6,
                    mat=ARMOR)
        m.box(c + Vector((s * 0.004, 0.031, 0.0)), (0.030, 0.008, 0.050), R=R, bevel=0.002, mat=ACCENT)
        # Bolts / strap
        for z in (-0.045, 0.045):
            m.box(c + Vector((s * 0.0, 0.043, z)), (0.016, 0.008, 0.008), bevel=0.002, mat=GEAR)
    for s, reg in ((1, R_ARM_R), (-1, R_ARM_L)):
        m.xfer(reg, grp=G_ARM_R if s > 0 else G_ARM_L)
        c = V3(s * SHOULDER_HALF, ELBOW_Y - 0.002, -0.050)
        m.box(c, (0.070, 0.082, 0.032), R=euler_matrix(0, 0, 0), bevel=0.012, seg=2, mat=GEAR)
        m.box(c + Vector((0, 0.0, -0.014)), (0.050, 0.058, 0.014), bevel=0.006, seg=1, mat=ARMOR)
    for s, reg in ((1, R_LEG_R), (-1, R_LEG_L)):
        m.xfer(reg)
        c = V3(s * 0.112, 0.520, 0.087)
        m.box(c, (0.098, 0.130, 0.036), R=euler_matrix(-6, 0, 0), bevel=0.014, seg=2, mat=ARMOR)
        m.box(c + Vector((0, -0.004, 0.0)), (0.082, 0.112, 0.042), R=euler_matrix(-6, 0, 0), bevel=0.012, seg=1, mat=GEAR)
        m.box(c + Vector((0, 0.080, -0.036)), (0.092, 0.012, 0.016), bevel=0.003, mat=GEAR)
        m.box(c + Vector((0, -0.080, -0.040)), (0.092, 0.012, 0.016), bevel=0.003, mat=GEAR)


def build_gear(m):
    build_vest(m)
    build_shoulder_straps(m)
    build_plates(m)
    build_pouches(m)
    build_belt(m)
    build_backpack(m)
    build_pads(m)
