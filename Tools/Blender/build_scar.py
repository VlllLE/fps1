"""Builds the detailed SCAR-H battle rifle and exports it for Unity.

    blender -b --factory-startup --python Tools/Blender/build_scar.py -- \
        --out Assets/Weapons/Models/SCAR [--renders <dir>] [--samples 64] [--res 100] [--no-export]

Gun frame (Unity axes, metres): +Z muzzle, +Y up, +X the gun's right (ejection) side. Same grip/magwell frame as the
M4A1 so ProceduralFirstPersonArms keeps the right palm on the pistol grip and the left hand on the magwell.
Objects: Body (monolithic upper, polymer lower, free-float handguard, barrel, folding stock, sights ...),
Magazine (pivot at the top of the magazine) and Bolt (the side charging handle, pivot at the T-handle).
"""

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from assault_kit import *  # noqa: E402,F401,F403

BY = 0.050            # bore axis height
FH_Z0 = 0.510
MUZZLE_Z = FH_Z0 + 0.0825
ANCHORS = {
    "muzzle": (0.0, BY, MUZZLE_Z + 0.012),
    "eject": (0.026, 0.056, 0.062),
}


def make_materials():
    return [
        make_material("SCARMetal", (0.115, 0.088, 0.052), 0.65, 0.45),
        make_material("SCARPolymer", (0.085, 0.066, 0.042), 0.0, 0.55),
        make_material("SCARAccent", (0.018, 0.018, 0.020), 0.85, 0.35),
    ]


def build_gun():
    reset_scene()
    mats = make_materials()
    M, P, A = MAT_METAL, MAT_POLY, MAT_ACCENT

    # ------------------------------------------------------------------------------------------
    # Monolithic upper receiver
    # ------------------------------------------------------------------------------------------
    upper = Builder("upper")
    up_prof = rrect_pts(0.0, 0.0545, 0.0215, 0.0285, 0.0075, 3)
    upper.prism_z(up_prof, -0.100, 0.222, M, bevel=0.0007, bseg=1)
    upper.box((0.0, 0.0545, -0.1025), (0.0370, 0.0600, 0.0050), M, bevel=0.0008, bseg=1)
    # brass deflector behind the port and the rear charging-handle guide
    upper.prism_x([(0.112, 0.038), (0.130, 0.038), (0.130, 0.058), (0.116, 0.066)], 0.0160, 0.0232, M, bevel=0.0004, bseg=1)
    upper.box((0.0, 0.0545, 0.216), (0.0440, 0.0620, 0.0100), M, bevel=0.0008, bseg=1)       # barrel-nut shoulder
    upper_obj = upper.commit("upper", mats)

    cutter = Builder("portcut")
    cutter.prism_x(rrect_pts(0.062, 0.0560, 0.0420, 0.0160, 0.0070, 3), 0.0040, 0.0300, M)
    boolean(upper_obj, cutter.commit("portcut", mats))
    slot = Builder("chslot")
    slot.box((0.0245, 0.0455, 0.1300), (0.0160, 0.0070, 0.0900), M)                            # charging handle track
    slot.box((-0.0245, 0.0455, 0.1300), (0.0160, 0.0070, 0.0900), M)
    boolean(upper_obj, slot.commit("chslot", mats))
    bevel_angle(upper_obj, 0.0006, 1, 40.0)

    # ------------------------------------------------------------------------------------------
    # Polymer lower receiver and magwell
    # ------------------------------------------------------------------------------------------
    lower = Builder("lower")
    lp = [(-0.100, 0.028), (0.152, 0.028), (0.152, 0.006), (0.142, -0.030), (0.128, -0.046), (0.024, -0.046), (0.030, -0.024),
          (-0.040, -0.024), (-0.070, -0.030), (-0.100, -0.018)]
    lower.prism_x(lp, -0.0195, 0.0195, P, bevel=0.0012, bseg=2)
    lower_obj = lower.commit("lower", mats)
    mw = Builder("magwell")
    mw.box((0.0, -0.020, 0.075), (0.0300, 0.1400, 0.0610), P)
    mw.box((0.0, -0.046, 0.075), (0.0360, 0.0100, 0.0680), P)
    boolean(lower_obj, mw.commit("magwell", mats))
    bevel_angle(lower_obj, 0.0005, 1, 40.0)

    # ------------------------------------------------------------------------------------------
    # Pistol grip, trigger group, controls
    # ------------------------------------------------------------------------------------------
    grip = Builder("grip")
    gtop = V(0.0, 0.010, -0.002)
    tilt = 20.0
    pistol_grip(grip, (0.0, 0.010, -0.002), tilt, 0.122, P, P, hw_top=0.0172, depth_top=0.0320, swell=0.0050, hw_bot=0.0165)
    d_down = V(0.0, -math.cos(math.radians(tilt)), -math.sin(math.radians(tilt)))
    gbot = gtop + d_down * 0.122
    grip.cyl(gbot + d_down * 0.0005, gbot - d_down * 0.0030, 0.0128, P, segs=20, ry=1.15, bevel=0.0007, up_hint=V(0, 0, 1))
    grip.screw(gbot - d_down * 0.0030, -d_down, 0.0042, M, segs=10)
    grip.prism_x([(-0.070, -0.014), (-0.040, -0.014), (-0.012, -0.008), (-0.040, -0.042), (-0.072, -0.030)], -0.0150, 0.0150, P, bevel=0.0012, bseg=2)
    guard_path = bezier((-0.022, 0.043), (-0.070, 0.043), (-0.080, 0.012), (-0.060, -0.008), 14)
    grip.sweep(guard_path, [(0.0046, 0.0019)] * len(guard_path), P, r=0.0014, cs=2, bevel=0.0)
    trig_path = bezier((-0.012, 0.024), (-0.024, 0.026), (-0.034, 0.030), (-0.046, 0.036), 8)
    grip.sweep(trig_path, [(0.0034, 0.0021)] * len(trig_path), M, r=0.0010, cs=2)
    for (pz, py) in ((0.020, 0.003), (-0.002, 0.004)):
        grip.cyl(V(-0.0200, py, pz), V(0.0200, py, pz), 0.0021, M, segs=10)
    # ambidextrous selector, magazine release, bolt catch
    for sx in (-1.0, 1.0):
        grip.cyl(V(sx * 0.0195, 0.014, -0.040), V(sx * 0.0235, 0.014, -0.040), 0.0070, M, segs=18)
        grip.box((sx * 0.0255, 0.014, -0.0475), (0.0024, 0.0050, 0.0200), M, bevel=0.0007, bseg=1)
        grip.cyl(V(sx * 0.0195, 0.014, -0.040), V(sx * 0.0230, 0.014, -0.040), 0.0035, A, segs=10)
    grip.cyl(V(0.0195, -0.012, 0.0200), V(0.0270, -0.012, 0.0200), 0.0060, M, segs=18, bevel=0.0004)
    grip.cyl(V(0.0195, -0.012, 0.0200), V(0.0252, -0.012, 0.0200), 0.0038, A, segs=10)
    grip.box((-0.0200, 0.0, 0.0315), (0.0040, 0.0150, 0.0130), M, bevel=0.0008, bseg=1)
    for pz in (-0.075, 0.138):
        grip.cyl(V(-0.0195, 0.0120, pz), V(-0.0245, 0.0120, pz), 0.0050, M, segs=14, bevel=0.0004)
        grip.cyl(V(0.0195, 0.0120, pz), V(0.0225, 0.0120, pz), 0.0038, M, segs=12)
        grip.cyl(V(-0.0195, 0.0120, pz), V(0.0195, 0.0120, pz), 0.0033, M, segs=10)

    # ------------------------------------------------------------------------------------------
    # Folding stock: hinge, telescoping tube, polymer shell with cut-out, cheek riser, butt pad
    # ------------------------------------------------------------------------------------------
    stock_m = Builder("stock_m")
    stock_m.box((0.0, 0.0545, -0.1190), (0.0430, 0.0600, 0.0300), M, bevel=0.0010, bseg=2)   # hinge block
    stock_m.cyl(V(-0.0215, 0.0700, -0.1190), V(-0.0300, 0.0700, -0.1190), 0.0070, M, segs=18, bevel=0.0005)  # fold pivot
    stock_m.cyl(V(-0.0215, 0.0700, -0.1190), V(-0.0285, 0.0700, -0.1190), 0.0036, A, segs=10)
    stock_m.cyl(V(0.0, 0.0420, -0.1340), V(0.0, 0.0420, -0.2350), 0.0122, M, segs=20)           # telescoping tube
    for i in range(9):
        stock_m.box((0.0, 0.0540, -0.1450 - i * 0.0105), (0.0260, 0.0040, 0.0044), M, bevel=0.0006, bseg=1)  # length-adjust teeth
    stock_m.cyl(V(0.0215, 0.0700, -0.1190), V(0.0250, 0.0700, -0.1190), 0.0050, A, segs=12)

    shell = Builder("shell")
    secs = []
    zs_shell = [-0.150, -0.170, -0.200, -0.235, -0.270, -0.300, -0.328, -0.340]
    for zz in zs_shell:
        k = (-0.150 - zz) / 0.190
        top = 0.0790 - 0.0080 * k
        bot = lerp(0.0120, -0.0460, smoothstep(0.0, 1.0, k))
        hw = lerp(0.0205, 0.0220, k)
        secs.append((-zz, rrect_pts(0.0, (top + bot) / 2.0, hw, (top - bot) / 2.0, 0.0090, 3)))
    shell.sections(V(0.0, 0.0, 0.0), V(0, 0, -1), secs, P, up_hint=V(0, 1, 0))
    shell_obj = shell.commit("shell", mats)
    hole = Builder("shellhole")
    hole.prism_x([(a_, b_ + 0.0200) for (a_, b_) in stadium(-0.238, 0.0, 0.088, 0.0300, 6)], -0.040, 0.040, P)
    boolean(shell_obj, hole.commit("shellhole", mats))
    bevel_angle(shell_obj, 0.0010, 1, 35.0)

    stock = Builder("stock")
    stock.prism_x([(-0.160, 0.0770), (-0.295, 0.0670), (-0.295, 0.0720), (-0.235, 0.0800), (-0.185, 0.0840)], -0.0140, 0.0140, P, bevel=0.0020, bseg=2)  # cheek riser
    for i in range(5):
        stock.cyl(V(-0.0150, 0.0790, -0.200 - i * 0.0200), V(0.0150, 0.0790, -0.200 - i * 0.0200), 0.0022, A, segs=8)
    stock.prism_x([(-0.332, 0.0700), (-0.3555, 0.0680), (-0.3615, 0.0100), (-0.3585, -0.0470), (-0.332, -0.0490)], -0.0230, 0.0230, P, bevel=0.0035, bseg=3)  # butt pad
    pad_pts = [(0.0700, -0.3555), (0.0100, -0.3615), (-0.0470, -0.3585)]

    def pad_z(y):
        (ya, za), (yb, zb), (yc, zc) = pad_pts
        if y >= yb:
            return lerp(zb, za, (y - yb) / (ya - yb))
        return lerp(zb, zc, (yb - y) / (yb - yc))
    for i in range(12):
        yy = 0.0600 - i * 0.0100
        stock.box((0.0, yy, pad_z(yy) - 0.0004), (0.0400, 0.0040, 0.0016), P, bevel=0.0004, bseg=1)
    stock.cyl(V(-0.0160, -0.0360, -0.3180), V(0.0160, -0.0360, -0.3180), 0.0042, M, segs=10)          # sling loop pin
    stock.box((0.0, -0.0170, -0.205), (0.0120, 0.0080, 0.0420), M, bevel=0.0010, bseg=1)             # stock latch lever
    stock.box((0.0, -0.0225, -0.210), (0.0085, 0.0060, 0.0300), A, bevel=0.0008, bseg=1)
    stock.box((0.0215, 0.0460, -0.155), (0.0040, 0.0140, 0.0200), A, bevel=0.0007, bseg=1)           # fold release button

    # ------------------------------------------------------------------------------------------
    # Top rail, handguard, barrel, gas system, sights
    # ------------------------------------------------------------------------------------------
    rail = Builder("rail")
    picatinny(rail, -0.095, 0.545, 0.0805, M)

    hand = Builder("hand")
    hz0, hz1 = 0.212, 0.520
    rings = []
    for (zz, hw, hh, rr) in ((hz0, 0.0230, 0.0300, 0.0100), (hz1, 0.0230, 0.0300, 0.0100),
                             (hz1, 0.0200, 0.0270, 0.0075), (hz0, 0.0200, 0.0270, 0.0075)):
        rings.append([V(px, py, zz) for (px, py) in rrect_pts(0.0, 0.0525, hw, hh, rr, 3)])
    hand.loft(rings, P, cyclic=True)
    hand_obj = hand.commit("hand", mats)
    mlok = Builder("mlok")
    for i in range(7):
        zc = 0.246 + i * 0.0385
        prof = [(a_, 0.050 + b_) for (a_, b_) in stadium(zc, 0.0, 0.0260, 0.0075, 5)]
        mlok.prism_x(prof, 0.016, 0.040, P)
        mlok.prism_x(prof, -0.040, -0.016, P)
    for i in range(6):
        zc = 0.265 + i * 0.0385
        mlok.prism_y([(b_, a_) for (a_, b_) in stadium(zc, 0.0, 0.0260, 0.0075, 5)], 0.000, 0.0300, P)
    for i in range(6):                                                                        # diagonal vents, upper corners
        zc = 0.265 + i * 0.0385
        vz = [(a_, 0.0525 + b_) for (a_, b_) in stadium(zc, 0.0, 0.0140, 0.0050, 4)]
        mlok.prism_x(vz, 0.016, 0.040, P, rot=(0, 0, -40.0), pivot=(0.0, 0.0525, zc))
        mlok.prism_x(vz, -0.040, -0.016, P, rot=(0, 0, 40.0), pivot=(0.0, 0.0525, zc))
    boolean(hand_obj, mlok.commit("mlok", mats))
    bevel_angle(hand_obj, 0.0004, 1, 40.0)

    hand2 = Builder("hand2")
    hand2.sections(V(0.0, 0.0, 0.0), V(0, 0, 1), [(0.520, rrect_pts(0.0, 0.0525, 0.0238, 0.0308, 0.0105, 3)),
                                                  (0.5225, rrect_pts(0.0, 0.0525, 0.0240, 0.0310, 0.0105, 3)),
                                                  (0.536, rrect_pts(0.0, 0.0525, 0.0240, 0.0310, 0.0105, 3)),
                                                  (0.538, rrect_pts(0.0, 0.0525, 0.0150, 0.0160, 0.0070, 3))], M)
    hand2.sections(V(0.0, 0.0, 0.0), V(0, 0, 1), [(0.205, rrect_pts(0.0, 0.0525, 0.0238, 0.0308, 0.0105, 3)),
                                                  (0.208, rrect_pts(0.0, 0.0525, 0.0240, 0.0310, 0.0105, 3)),
                                                  (0.222, rrect_pts(0.0, 0.0525, 0.0240, 0.0310, 0.0105, 3)),
                                                  (0.224, rrect_pts(0.0, 0.0525, 0.0226, 0.0296, 0.0100, 3))], M)
    for zq in (0.240, 0.360, 0.480):                                                         # QD sling sockets
        for sx in (-1.0, 1.0):
            hand2.cyl(V(sx * 0.0225, 0.0525, zq), V(sx * 0.0295, 0.0525, zq), 0.0045, A, segs=12, bevel=0.0005)
    barrel = Builder("barrel")
    barrel.lathe(V(0.0, BY, 0.200), V(0, 0, 1), [(0.0, 0.0120), (0.180, 0.0120), (0.181, 0.0105), (0.330, 0.0105)], 20, M)
    barrel.lathe(V(0.0, BY, 0.340), V(0, 0, 1), [(0.0, 0.0150), (0.0020, 0.0165), (0.0260, 0.0165), (0.0280, 0.0140), (0.0330, 0.0140), (0.0340, 0.0112), (0.1700, 0.0112)], 22, M)
    barrel.cyl(V(0.0, 0.0705, 0.120), V(0.0, 0.0705, 0.345), 0.0036, M, segs=8)                # gas tube at 12 o'clock
    fh = Builder("fh")
    fh.lathe(V(0.0, BY, FH_Z0), V(0, 0, 1), [(0.0, 0.0112), (0.0045, 0.0134), (0.0720, 0.0134), (0.0770, 0.0128), (0.0820, 0.0108), (0.0825, 0.0100)], 24, M)
    fh_obj = fh.commit("fh", mats)
    fhcut = Builder("fhcut")
    for k in range(6):
        fhcut.box((0.0, BY, FH_Z0 + 0.0500), (0.0030, 0.0400, 0.0300), M, rot=(0, 0, 30.0 + 60.0 * k), pivot=(0.0, BY, FH_Z0 + 0.0500))
    fhcut.cyl(V(0.0, BY, FH_Z0 + 0.0300), V(0.0, BY, FH_Z0 + 0.0900), 0.0062, M, segs=16)
    boolean(fh_obj, fhcut.commit("fhcut", mats))
    bevel_angle(fh_obj, 0.0004, 1, 40.0)

    sights = Builder("sights")
    fz = 0.488                                                                                 # folding front sight
    sights.box((0.0, 0.0985, fz), (0.0200, 0.0120, 0.0300), M, bevel=0.0008, bseg=1)
    sights.box((0.0, 0.1130, fz), (0.0120, 0.0230, 0.0160), M, bevel=0.0007, bseg=1)
    sights.box((-0.0070, 0.1330, fz), (0.0022, 0.0200, 0.0150), M, bevel=0.0004, bseg=1)
    sights.box((0.0070, 0.1330, fz), (0.0022, 0.0200, 0.0150), M, bevel=0.0004, bseg=1)
    sights.cyl(V(0.0, 0.1220, fz), V(0.0, 0.1480, fz), 0.0014, M, segs=8)
    sights.cyl(V(-0.0110, 0.0985, fz), V(0.0110, 0.0985, fz), 0.0022, A, segs=8)
    rz = -0.052                                                                                # flip-up rear sight
    sights.box((0.0, 0.0955, rz), (0.0280, 0.0110, 0.0300), M, bevel=0.0008, bseg=1)
    sights.box((0.0, 0.1080, rz - 0.004), (0.0150, 0.0140, 0.0120), M, bevel=0.0006, bseg=1)
    sights.box((-0.0110, 0.1180, rz), (0.0024, 0.0280, 0.0090), M, bevel=0.0005, bseg=1)
    sights.box((0.0110, 0.1180, rz), (0.0024, 0.0280, 0.0090), M, bevel=0.0005, bseg=1)
    sights.cyl(V(-0.0190, 0.0985, rz + 0.002), V(0.0190, 0.0985, rz + 0.002), 0.0030, A, segs=12)    # windage drum
    sights_obj = sights.commit("sights", mats)
    ap = Builder("ap")
    ap.cyl(V(0.0, 0.1130, rz - 0.020), V(0.0, 0.1130, rz + 0.020), 0.0022, M, segs=10)
    boolean(sights_obj, ap.commit("ap", mats))

    # ------------------------------------------------------------------------------------------
    # Ejection port internals
    # ------------------------------------------------------------------------------------------
    inner = Builder("inner")
    inner.box((0.0030, 0.0560, 0.064), (0.0070, 0.0240, 0.0760), A, bevel=0.0008, bseg=1)        # bolt carrier
    inner.box((0.0080, 0.0400, 0.062), (0.0050, 0.0050, 0.0780), M, bevel=0.0005, bseg=1)
    inner.cyl(V(0.0070, 0.0600, 0.030), V(0.0070, 0.0600, 0.098), 0.0038, M, segs=10)
    inner.box((0.0220, 0.0365, 0.065), (0.0020, 0.0040, 0.0820), M, bevel=0.0004, bseg=1)       # port lower lip

    # ------------------------------------------------------------------------------------------
    # Magazine (20-round 7.62) and charging handle objects
    # ------------------------------------------------------------------------------------------
    mag_top = V(0.0, 0.0200, 0.075)
    mag = Builder("mag")
    path = bezier((0.0200, 0.0750), (-0.0500, 0.0755), (-0.1150, 0.0830), (-0.1780, 0.1040), 26)
    n = len(path)
    sizes = [(lerp(0.0138, 0.0143, i / (n - 1)), lerp(0.0285, 0.0298, i / (n - 1))) for i in range(n)]
    mag.sweep(path, sizes, P, r=0.0045, cs=3, bevel=0.0)
    for i in range(3, n - 2):
        if i % 2 == 0:
            (py, pz) = path[i]
            (ny, nz) = path[i + 1]
            (my, mz) = path[i - 1]
            tang = Vector((0, ny - my, nz - mz)).normalized()
            for sx in (-1.0, 1.0):
                hw = sizes[i][0]
                mag.box((sx * (hw + 0.0003), py, pz), (0.0014, 0.0040, sizes[i][1] * 1.55), P, bevel=0.0005, bseg=1,
                        rot=(math.degrees(math.atan2(tang.z, -tang.y)), 0, 0), pivot=(sx * (hw + 0.0003), py, pz))
    fy, fz2 = path[-1]
    mag.box((0.0, fy - 0.0030, fz2 + 0.0020), (0.0310, 0.0090, 0.0620), P, bevel=0.0022, bseg=2, rot=(-34.0, 0, 0), pivot=(0.0, fy - 0.0030, fz2 + 0.0020))
    mag.box((0.0, fy - 0.0075, fz2 + 0.0030), (0.0230, 0.0030, 0.0500), A, bevel=0.0008, bseg=1, rot=(-34.0, 0, 0), pivot=(0.0, fy - 0.0075, fz2 + 0.0030))
    mag.box((0.0, 0.0210, 0.0750), (0.0260, 0.0090, 0.0540), M, bevel=0.0006, bseg=1)
    mag_obj = mag.commit("Magazine", mats)

    bolt = Builder("bolt")
    cy, cz = 0.0455, 0.1150
    bolt.box((0.0232, cy, cz), (0.0042, 0.0100, 0.0480), M, bevel=0.0006, bseg=1)               # slider in the track
    bolt.cyl(V(0.0245, cy, cz), V(0.0345, cy, cz), 0.0036, M, segs=12)
    bolt.box((0.0368, cy, cz - 0.003), (0.0100, 0.0150, 0.0260), P, bevel=0.0018, bseg=2)       # T-handle grip
    for i in range(5):
        bolt.box((0.0425, cy, cz - 0.014 + i * 0.0060), (0.0010, 0.0130, 0.0020), M)
    bolt.cyl(V(0.0352, cy, cz - 0.003), V(0.0426, cy, cz - 0.003), 0.0042, A, segs=10)
    bolt_obj = bolt.commit("Bolt", mats)

    committed = []
    for b_ in (grip, stock_m, stock, rail, hand2, barrel, inner):
        committed.append(b_.commit(b_.name, mats))
    committed += [upper_obj, lower_obj, hand_obj, shell_obj, fh_obj, sights_obj]
    body_obj = join(committed, "Body")
    body_obj.data.name = "SCARBodyMesh"

    set_origin_unity(mag_obj, mag_top)
    set_origin_unity(bolt_obj, V(0.0345, cy, cz))
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
    ("side", (1.45, 0.02, 0.12), (0.0, 0.03, 0.12), 50),
    ("side-left", (-1.45, 0.02, 0.12), (0.0, 0.03, 0.12), 50),
    ("threequarter", (0.80, 0.30, 1.00), (0.0, 0.02, 0.13), 48),
    ("threequarter-rear", (0.80, 0.30, -1.00), (0.0, 0.02, 0.10), 48),
    ("top", (0.42, 1.30, -0.25), (0.0, 0.03, 0.12), 46),
    ("detail-receiver", (0.30, 0.12, 0.10), (0.0, 0.04, 0.06), 55),
    ("detail-muzzle", (0.20, 0.10, 0.72), (0.0, 0.06, 0.50), 52),
    ("detail-stock", (0.34, 0.12, -0.55), (0.0, 0.02, -0.20), 52),
    ("detail-grip", (0.24, -0.06, 0.22), (0.0, -0.040, 0.02), 55),
]


def main():
    out = arg("--out", os.path.abspath("scar_out"))
    renders = arg("--renders", None)
    objs = build_gun()
    report(objs)
    if "--no-export" not in ARGS:
        fbx, glb = export_all(objs, out, "SCAR")
        print("GUN_EXPORTED", fbx, glb)
    if renders:
        render_views(VIEWS, "scar", renders, int(arg("--samples", "48")), int(arg("--res", "100")),
                     (0.0, 0.02, 0.13), -0.20)


if __name__ == "__main__":
    main()
