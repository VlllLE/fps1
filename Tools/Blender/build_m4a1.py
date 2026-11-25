"""Builds the detailed M4A1 carbine and exports it for Unity.

    blender -b --factory-startup --python Tools/Blender/build_m4a1.py -- \
        --out Assets/Weapons/Models/M4A1 [--renders <dir>] [--samples 64] [--res 100] [--no-export]

Gun frame (Unity axes, metres): +Z muzzle, +Y up, +X is the gun's right (ejection) side. The origin is chosen
so the right palm of ProceduralFirstPersonArms sits on the pistol grip and the left hand on the magwell
(see preview_assault_held.py). Objects: Body (receiver, rails, handguard, barrel, stock, grip ...),
Magazine (pivot at the top of the magazine, driven by the WeaponController reload animation) and Bolt (the
charging handle, pivot at its centre, driven by the same reload animation).
"""

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from assault_kit import *  # noqa: E402,F401,F403

BY = 0.045            # bore axis height
MUZZLE_Z = 0.532
ANCHORS = {
    "muzzle": (0.0, BY, MUZZLE_Z + 0.012),
    "eject": (0.024, 0.047, 0.060),
}


def make_materials():
    return [
        make_material("M4A1Metal", (0.085, 0.09, 0.10), 0.85, 0.38),
        make_material("M4A1Polymer", (0.035, 0.037, 0.040), 0.0, 0.55),
        make_material("M4A1Accent", (0.62, 0.42, 0.17), 0.85, 0.34),
    ]


def build_gun():
    reset_scene()
    mats = make_materials()
    M, P, A = MAT_METAL, MAT_POLY, MAT_ACCENT

    body = Builder("body")
    cut_objs = []

    # ------------------------------------------------------------------------------------------
    # Upper receiver (cut afterwards: ejection port)
    # ------------------------------------------------------------------------------------------
    upper = Builder("upper")
    up_prof = rrect_pts(0.0, 0.0405, 0.0176, 0.0262, 0.0065, 3)
    upper.prism_z(up_prof, -0.090, 0.172, M, bevel=0.0007, bseg=1)
    # rear receiver wall plate and front barrel-nut collar
    upper.box((0.0, 0.0405, -0.0925), (0.0360, 0.0540, 0.0050), M, bevel=0.0008, bseg=1)
    upper.lathe(V(0.0, BY, 0.158), V(0, 0, 1), [(0.0, 0.0200), (0.003, 0.0238), (0.020, 0.0238), (0.022, 0.0208)], 24, M)
    # brass deflector wedge behind the port
    upper.prism_x([(0.108, 0.036), (0.126, 0.036), (0.126, 0.050), (0.112, 0.056)], 0.0152, 0.0215, M, bevel=0.0004, bseg=1)
    # forward assist housing (angled boss behind the port)
    upper_obj = upper.commit("upper", mats)

    # port cutter (right side, rounded rectangle recess), reaches the bolt carrier
    cutter = Builder("portcut")
    cutter.prism_x(rrect_pts(0.060, 0.0455, 0.0400, 0.0145, 0.0060, 3), 0.0040, 0.0300, M)
    boolean(upper_obj, cutter.commit("portcut", mats))
    # ejection port is flanked by recessed slots on the left (carrier keys) -> small slit
    slit = Builder("slit")
    slit.box((-0.0150, 0.052, 0.020), (0.004, 0.006, 0.050), M)
    boolean(upper_obj, slit.commit("slit", mats))
    bevel_angle(upper_obj, 0.0006, 1, 40.0)

    # ------------------------------------------------------------------------------------------
    # Lower receiver (side profile extruded) with magwell opening
    # ------------------------------------------------------------------------------------------
    lower = Builder("lower")
    lp = [(-0.090, 0.018), (0.112, 0.018), (0.112, -0.058), (0.040, -0.058), (0.040, -0.021),
          (-0.040, -0.021), (-0.062, -0.026), (-0.090, -0.015)]
    lower.prism_x(lp, -0.0150, 0.0150, M, bevel=0.0010, bseg=2)
    lower.prism_x([(-0.060, -0.018), (-0.014, -0.018), (-0.016, -0.036), (-0.040, -0.040), (-0.058, -0.030)], -0.0145, 0.0145, M, bevel=0.0008, bseg=1)
    lower_obj = lower.commit("lower", mats)
    mw = Builder("magwell")
    mw.box((0.0, -0.02, 0.070), (0.0238, 0.090, 0.0462), M)
    # flared magwell mouth
    mw.box((0.0, -0.058, 0.070), (0.0290, 0.010, 0.0520), M)
    boolean(lower_obj, mw.commit("magwell", mats))
    bevel_angle(lower_obj, 0.0005, 1, 40.0)

    # ------------------------------------------------------------------------------------------
    # Pistol grip, trigger guard, trigger, controls
    # ------------------------------------------------------------------------------------------
    grip = Builder("grip")
    pistol_grip(grip, (0.0, 0.007, 0.006), 26.0, 0.118, P, P, hw_top=0.0168, depth_top=0.0300, swell=0.0045, hw_bot=0.0160)
    # grip bottom cap and screw
    gtop = V(0.0, 0.007, 0.006)
    d_down = V(0.0, -math.cos(math.radians(26.0)), -math.sin(math.radians(26.0)))
    gbot = gtop + d_down * 0.118
    grip.cyl(gbot + d_down * 0.0005, gbot - d_down * 0.0030, 0.0125, P, segs=20, ry=1.15, bevel=0.0007, up_hint=V(0, 0, 1))
    grip.screw(gbot - d_down * 0.0030, -d_down, 0.0042, M, segs=10)
    # backstrap beavertail where the grip meets the receiver
    grip.prism_x([(-0.062, -0.014), (-0.036, -0.014), (-0.014, -0.009), (-0.040, -0.040), (-0.066, -0.026)], -0.0140, 0.0140, P, bevel=0.0012, bseg=2)
    # trigger guard hoop
    guard_path = bezier((-0.017, 0.050), (-0.060, 0.050), (-0.070, 0.016), (-0.052, -0.004), 12)
    grip.sweep(guard_path, [(0.0040, 0.0017)] * len(guard_path), M, r=0.0012, cs=2, bevel=0.0)
    # trigger
    trig_path = bezier((-0.010, 0.027), (-0.022, 0.029), (-0.032, 0.033), (-0.044, 0.038), 8)
    grip.sweep(trig_path, [(0.0033, 0.0020)] * len(trig_path), M, r=0.0010, cs=2)
    # trigger/hammer pins
    for (pz, py) in ((0.019, 0.003), (0.001, 0.0035)):
        grip.cyl(V(-0.0165, py, pz), V(0.0165, py, pz), 0.0020, M, segs=10)
    # selector (left) with lever, mag release (right), bolt catch (left)
    grip.cyl(V(-0.0150, 0.001, -0.040), V(-0.0185, 0.001, -0.040), 0.0070, M, segs=18)
    grip.box((-0.0200, 0.001, -0.0475), (0.0024, 0.0050, 0.0180), M, bevel=0.0007, bseg=1, rot=(0, 0, 0))
    grip.cyl(V(-0.0150, 0.001, -0.040), V(-0.0182, 0.001, -0.040), 0.0035, A, segs=10)
    grip.cyl(V(0.0150, -0.007, 0.0420), V(0.0205, -0.007, 0.0420), 0.0058, M, segs=18, bevel=0.0004)
    grip.box((-0.0168, 0.004, 0.0335), (0.0040, 0.0150, 0.0120), M, bevel=0.0008, bseg=1)
    # takedown / pivot pins
    for pz in (-0.071, 0.108):
        grip.cyl(V(-0.0150, 0.0105, pz), V(-0.0195, 0.0105, pz), 0.0048, M, segs=14, bevel=0.0004)
        grip.cyl(V(0.0150, 0.0105, pz), V(0.0180, 0.0105, pz), 0.0036, M, segs=12)
        grip.cyl(V(-0.0150, 0.0105, pz), V(0.0150, 0.0105, pz), 0.0032, M, segs=10)

    # ------------------------------------------------------------------------------------------
    # Stock group
    # ------------------------------------------------------------------------------------------
    stock = Builder("stock")
    stock.cyl(V(0.0, 0.040, -0.090), V(0.0, 0.040, -0.215), 0.0143, M, segs=24)
    stock.lathe(V(0.0, 0.040, -0.088), V(0, 0, -1), [(0.0, 0.0172), (0.006, 0.0172), (0.0075, 0.0150), (0.0140, 0.0150), (0.0155, 0.0172), (0.0215, 0.0172)], 6, M, phase=0.52)
    stock.box((0.0, 0.0400, -0.1035), (0.0100, 0.0600, 0.0080), M, bevel=0.0007, bseg=1)  # QD end plate
    stock.cyl(V(-0.0075, 0.016, -0.1035), V(0.0075, 0.016, -0.1035), 0.0045, M, segs=10)
    def stock_bottom(z):
        pts = [(-0.126, -0.004), (-0.150, -0.010), (-0.195, -0.022), (-0.245, -0.046), (-0.289, -0.056)]
        for (z0, y0), (z1, y1) in zip(pts, pts[1:]):
            if z1 <= z <= z0:
                k = (z0 - z) / (z0 - z1)
                return lerp(y0, y1, k)
        return pts[-1][1]
    secs = []
    zs_stock = [-0.126, -0.140, -0.165, -0.195, -0.225, -0.255, -0.280, -0.292]
    for zz in zs_stock:
        k = (-0.126 - zz) / 0.166
        top = 0.0585 - 0.0035 * k * k
        bot = stock_bottom(zz)
        hw = lerp(0.0178, 0.0215, k)
        secs.append((zz, rrect_pts(0.0, (top + bot) / 2.0, hw, (top - bot) / 2.0, 0.0085, 3)))
    # sections() runs along its direction from the origin: origin z=0, direction -z (t = -zz)
    stock.sections(V(0.0, 0.0, 0.0), V(0, 0, -1), [(-zz, pts) for (zz, pts) in secs], P, up_hint=V(0, 1, 0))
    # cheek-weld ridge and underside latch/ribs
    stock.prism_x([(-0.150, 0.0580), (-0.270, 0.0580), (-0.270, 0.0640), (-0.215, 0.0640), (-0.180, 0.0610)], -0.0100, 0.0100, P, bevel=0.0020, bseg=2)
    stock.box((0.0, -0.0170, -0.160), (0.0110, 0.0100, 0.0420), M, bevel=0.0010, bseg=1)    # adjust lever
    stock.box((0.0, -0.0225, -0.165), (0.0080, 0.0060, 0.0300), A, bevel=0.0008, bseg=1)
    for i in range(11):                                                                            # length-adjust teeth
        stock.box((0.0, 0.0072, -0.140 - i * 0.0105), (0.0275, 0.0030, 0.0042), P, bevel=0.0006, bseg=1)
    # butt pad with ribs and sling loop
    stock.prism_x([(-0.289, 0.0560), (-0.3075, 0.0540), (-0.3115, 0.0020), (-0.3085, -0.0580), (-0.289, -0.0600)], -0.0220, 0.0220, P, bevel=0.0035, bseg=3)
    for i in range(11):
        stock.box((0.0, 0.0480 - i * 0.0100, -0.3130), (0.0380, 0.0040, 0.0016), P, bevel=0.0004, bseg=1)
    stock.cyl(V(-0.0150, -0.040, -0.2955), V(0.0150, -0.040, -0.2955), 0.0042, M, segs=10)
    stock.box((0.0, 0.0405, -0.2900), (0.0060, 0.0050, 0.0120), M, bevel=0.0006, bseg=1)

    # ------------------------------------------------------------------------------------------
    # Rail, handguard, barrel, gas block, sights
    # ------------------------------------------------------------------------------------------
    rail = Builder("rail")
    picatinny(rail, -0.082, 0.372, 0.0658, M)

    hand = Builder("hand")
    r_out, r_in = 0.0245, 0.0212
    z0h, z1h = 0.172, 0.378
    # hollow tube: outer wall, front lip, inner wall, rear lip (cyclic loft)
    u, v, w = basis_for(V(0, 0, 1))
    rings = []
    for (zz, rr) in ((z0h, r_out), (z1h, r_out), (z1h, r_in), (z0h, r_in)):
        rings.append([V(px, BY + py, zz) for (px, py) in circle_pts(rr, 32)])
    hand.loft(rings, P, cyclic=True)
    hand_obj = hand.commit("hand", mats)
    mlok = Builder("mlok")
    zs = [0.196 + i * 0.0300 for i in range(6)]
    for zc in zs:
        prof = stadium(zc, 0.0, 0.0230, 0.0070, 5)
        prof_zy = [(a, BY + b) for (a, b) in prof]
        mlok.prism_x(prof_zy, 0.014, 0.040, P)                    # 3 o'clock
        mlok.prism_x(prof_zy, -0.040, -0.014, P)                  # 9 o'clock
        mlok.prism_y([(b_, a_) for (a_, b_) in prof], BY - 0.040, BY - 0.012, P)  # 6 o'clock
    for zc in [0.211 + i * 0.0300 for i in range(5)]:
        vz = [(a_, BY + b_) for (a_, b_) in stadium(zc, 0.0, 0.0140, 0.0055, 4)]
        mlok.prism_x(vz, 0.014, 0.040, P, rot=(0, 0, -45.0), pivot=(0.0, BY, zc))
        mlok.prism_x(vz, -0.040, -0.014, P, rot=(0, 0, 45.0), pivot=(0.0, BY, zc))
    boolean(hand_obj, mlok.commit("mlok", mats))
    bevel_angle(hand_obj, 0.0004, 1, 40.0)
    hand2 = Builder("hand2")
    # delta ring / handguard cap collars
    hand2.lathe(V(0.0, BY, 0.169), V(0, 0, 1), [(0.0, 0.0215), (0.0015, 0.0275), (0.012, 0.0275), (0.0135, 0.0262), (0.016, 0.0262), (0.0175, 0.0252)], 28, M)
    hand2.lathe(V(0.0, BY, 0.372), V(0, 0, 1), [(0.0, 0.0258), (0.0015, 0.0262), (0.0085, 0.0262), (0.0095, 0.0215)], 28, M)
    for zq in (0.205, 0.300):                                                                         # QD sling sockets
        for sx in (-1.0, 1.0):
            hand2.cyl(V(sx * 0.0230, BY, zq), V(sx * 0.0300, BY, zq), 0.0045, A, segs=12, bevel=0.0005)
    # barrel: pencil profile, gas block, front sight base
    barrel = Builder("barrel")
    barrel.lathe(V(0.0, BY, 0.165), V(0, 0, 1), [(0.0, 0.0108), (0.213, 0.0108), (0.214, 0.0090), (0.300, 0.0090)], 20, M)
    barrel.lathe(V(0.0, BY, 0.375), V(0, 0, 1), [(0.0, 0.0125), (0.0015, 0.0140), (0.026, 0.0140), (0.0275, 0.0125), (0.034, 0.0125),
                                                    (0.0350, 0.0100), (0.065, 0.0100)], 22, M)
    # gas tube inside the handguard (visible through the slots)
    barrel.cyl(V(0.0, BY + 0.0150, 0.100), V(0.0, BY + 0.0150, 0.385), 0.0034, M, segs=8)
    # A2 flash hider
    fh = Builder("fh")
    fh.lathe(V(0.0, BY, 0.468), V(0, 0, 1), [(0.0, 0.0090), (0.004, 0.0113), (0.050, 0.0113), (0.058, 0.0110), (0.0625, 0.0098), (0.064, 0.0090)], 20, M)
    fh_obj = fh.commit("fh", mats)
    fhcut = Builder("fhcut")
    for k in range(5):
        ang = 90.0 + 36.0 + 72.0 * k
        fhcut.box((0.0, BY, 0.512), (0.0030, 0.0400, 0.0260), M, rot=(0, 0, ang - 90.0), pivot=(0.0, BY, 0.512))
    fhcut.cyl(V(0.0, BY, 0.518), V(0.0, BY, 0.540), 0.0058, M, segs=16)
    boolean(fh_obj, fhcut.commit("fhcut", mats))
    bevel_angle(fh_obj, 0.0004, 1, 40.0)

    # front sight tower (A2 style) and rear aperture sight
    sights = Builder("sights")
    fz = 0.396
    sights.box((0.0, 0.0720, fz), (0.0090, 0.0300, 0.0140), M, bevel=0.0007, bseg=1)
    sights.box((-0.0066, 0.0870, fz), (0.0020, 0.0140, 0.0120), M, bevel=0.0004, bseg=1)
    sights.box((0.0066, 0.0870, fz), (0.0020, 0.0140, 0.0120), M, bevel=0.0004, bseg=1)
    sights.cyl(V(0.0, 0.0780, fz), V(0.0, 0.0925, fz), 0.0013, M, segs=8)
    sights.cyl(V(-0.0100, 0.0620, fz), V(0.0100, 0.0620, fz), 0.0020, M, segs=8)
    rz = -0.047
    sights.box((0.0, 0.0775, rz), (0.0240, 0.0050, 0.0200), M, bevel=0.0007, bseg=1)
    sights.box((0.0, 0.0830, rz - 0.003), (0.0130, 0.0070, 0.0100), M, bevel=0.0006, bseg=1)
    sights.box((-0.0092, 0.0905, rz), (0.0022, 0.0260, 0.0075), M, bevel=0.0005, bseg=1)
    sights.box((0.0092, 0.0905, rz), (0.0022, 0.0260, 0.0075), M, bevel=0.0005, bseg=1)
    sights.lathe(V(0.0, 0.0925, rz - 0.0050), V(0, 0, 1), [(0.0, 0.0050), (0.0040, 0.0050), (0.0050, 0.0050)], 14, M)
    sights.cyl(V(-0.0140, 0.0775, rz + 0.002), V(0.0140, 0.0775, rz + 0.002), 0.0024, A, segs=8)
    sights_obj = sights.commit("sights", mats)
    # aperture hole
    ap = Builder("ap")
    ap.cyl(V(0.0, 0.0925, rz - 0.010), V(0.0, 0.0925, rz + 0.010), 0.0021, M, segs=10)
    boolean(sights_obj, ap.commit("ap", mats))

    # ------------------------------------------------------------------------------------------
    # Ejection port internals: bolt carrier visible, dust cover open, forward assist
    # ------------------------------------------------------------------------------------------
    inner = Builder("inner")
    inner.box((0.0010, 0.0455, 0.062), (0.0070, 0.0210, 0.0690), A, bevel=0.0008, bseg=1)
    inner.box((0.0060, 0.0360, 0.060), (0.0050, 0.0050, 0.0720), M, bevel=0.0005, bseg=1)
    inner.cyl(V(0.0030, 0.0485, 0.030), V(0.0030, 0.0485, 0.090), 0.0036, M, segs=10)
    # dust cover hinged at the lower port edge, swung open
    th = 35.0
    hinge = V(0.0176, 0.0312, 0.060)
    dc_c = hinge + V(math.sin(math.radians(th)), -math.cos(math.radians(th)), 0.0) * 0.0160
    inner.box(tuple(dc_c), (0.0020, 0.0200, 0.0700), M, bevel=0.0006, bseg=1, rot=(0, 0, th), pivot=tuple(dc_c))
    inner.cyl(V(0.0176, 0.0312, 0.0185), V(0.0176, 0.0312, 0.1020), 0.0018, M, segs=8)
    # forward assist
    inner.cyl(V(0.0140, 0.0290, -0.0100), V(0.0195, 0.0270, -0.0235), 0.0058, M, segs=14, bevel=0.0004)
    inner.cyl(V(0.0195, 0.0270, -0.0235), V(0.0215, 0.0262, -0.0285), 0.0045, M, segs=12, bevel=0.0004)

    # ------------------------------------------------------------------------------------------
    # Magazine (own object) and charging handle (own object)
    # ------------------------------------------------------------------------------------------
    mag_top = V(0.0, 0.0130, 0.070)
    mag = Builder("mag")
    path = bezier((0.0130, 0.0700), (-0.055, 0.0715), (-0.125, 0.0830), (-0.172, 0.1060), 24)
    n = len(path)
    sizes = []
    for i in range(n):
        k = i / (n - 1)
        sizes.append((lerp(0.0112, 0.0118, k), lerp(0.0206, 0.0214, k)))
    mag.sweep(path, sizes, P, r=0.0040, cs=3, bevel=0.0)
    # side ribs (horizontal ridges following the curve) and front lugs
    for i in range(3, n - 2, 1):
        k = i / (n - 1)
        (py, pz) = path[i]
        (ny, nz) = path[i + 1] if i + 1 < n else path[i]
        (my, mz) = path[i - 1]
        tang = Vector((0, ny - my, nz - mz)).normalized()
        ang = math.degrees(math.atan2(-tang.z, tang.y)) * 0.0
        if i % 2 == 0:
            for sx in (-1.0, 1.0):
                hw = sizes[i][0]
                mag.box((sx * (hw + 0.0003), py, pz), (0.0014, 0.0036, sizes[i][1] * 1.55), P, bevel=0.0005, bseg=1,
                        rot=(math.degrees(math.atan2(tang.z, -tang.y)) - 0.0, 0, 0), pivot=(sx * (hw + 0.0003), py, pz))
    # follower window ribs on the face and floor plate
    fy, fz = path[-1]
    mag.box((0.0, fy - 0.0030, fz + 0.0020), (0.0244, 0.0090, 0.0450), P, bevel=0.0022, bseg=2, rot=(-32.0, 0, 0), pivot=(0.0, fy - 0.0030, fz + 0.0020))
    mag.box((0.0, fy - 0.0075, fz + 0.0030), (0.0200, 0.0030, 0.0360), A, bevel=0.0008, bseg=1, rot=(-32.0, 0, 0), pivot=(0.0, fy - 0.0075, fz + 0.0030))
    # feed lips at the top
    mag.box((0.0, 0.0140, 0.0700), (0.0215, 0.0090, 0.0400), M, bevel=0.0006, bseg=1)
    mag_obj = mag.commit("Magazine", mats)

    bolt = Builder("bolt")
    cz = -0.108
    cy = 0.0630
    bolt.box((0.0, cy, cz - 0.004), (0.0140, 0.0100, 0.0480), M, bevel=0.0010, bseg=1)       # latch body in the rail channel
    for sx in (-1.0, 1.0):
        bolt.box((sx * 0.0200, cy - 0.001, cz - 0.016), (0.0120, 0.0115, 0.0270), M, bevel=0.0012, bseg=2, rot=(0, -sx * 12.0, 0), pivot=(sx * 0.0200, cy - 0.001, cz - 0.016))
        for i in range(5):
            bolt.box((sx * 0.0262, cy - 0.001, cz - 0.0075 - i * 0.0042), (0.0010, 0.0125, 0.0016), M)
    bolt.box((0.0, cy + 0.0075, cz - 0.014), (0.0090, 0.0030, 0.0200), A, bevel=0.0007, bseg=1)
    bolt.box((0.0, cy - 0.0020, cz + 0.020), (0.0100, 0.0070, 0.0240), M, bevel=0.0007, bseg=1)
    bolt_obj = bolt.commit("Bolt", mats)

    # ------------------------------------------------------------------------------------------
    # Body objects -> joined
    # ------------------------------------------------------------------------------------------
    committed = []
    for b_ in (grip, stock, rail, hand2, barrel, inner):
        committed.append(b_.commit(b_.name, mats))
    committed += [upper_obj, lower_obj, hand_obj, fh_obj, sights_obj]
    body_obj = join(committed, "Body")
    body_obj.data.name = "M4A1BodyMesh"

    set_origin_unity(mag_obj, mag_top)
    set_origin_unity(bolt_obj, V(0.0, cy, cz))
    objs = [body_obj, mag_obj, bolt_obj]
    for o in objs:
        shade_edges(o)
        unwrap(o)
        prune_unused_slots(o)
    return objs


def report(objs):
    total = 0
    for o in objs:
        st = mesh_stats(o)
        total += st["tris"]
        print("GUN_STATS %s verts=%d tris=%d nonmanifold=%d uv=%s mats=%s z=[%.3f,%.3f] y=[%.3f,%.3f] x=[%.3f,%.3f]" % (
            o.name, st["verts"], st["tris"], st["nonmanifold"], st["uv"], st["mat_faces"], st["z"][0], st["z"][1],
            st["y"][0], st["y"][1], st["x"][0], st["x"][1]))
    print("GUN_TOTAL_TRIS %d" % total)
    return total


VIEWS = [
    ("side", (1.35, 0.02, 0.115), (0.0, 0.02, 0.115), 50),
    ("side-left", (-1.35, 0.02, 0.115), (0.0, 0.02, 0.115), 50),
    ("threequarter", (0.75, 0.30, 0.95), (0.0, 0.01, 0.12), 48),
    ("threequarter-rear", (0.75, 0.30, -0.95), (0.0, 0.01, 0.10), 48),
    ("top", (0.40, 1.25, -0.25), (0.0, 0.02, 0.115), 46),
    ("detail-receiver", (0.28, 0.10, 0.08), (0.0, 0.03, 0.04), 55),
    ("detail-muzzle", (0.18, 0.08, 0.64), (0.0, 0.05, 0.45), 52),
    ("detail-stock", (0.30, 0.12, -0.50), (0.0, 0.01, -0.17), 52),
    ("detail-grip", (0.22, -0.06, 0.20), (0.0, -0.045, 0.02), 55),
]


def main():
    out = arg("--out", os.path.abspath("m4a1_out"))
    renders = arg("--renders", None)
    objs = build_gun()
    report(objs)
    if "--no-export" not in ARGS:
        fbx, glb = export_all(objs, out, "M4A1")
        print("GUN_EXPORTED", fbx, glb)
    if renders:
        render_views(VIEWS, "m4a1", renders, int(arg("--samples", "48")), int(arg("--res", "100")),
                     (0.0, 0.01, 0.12), -0.20)


if __name__ == "__main__":
    main()
