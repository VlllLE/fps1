"""Builds the detailed UMP45 submachine gun and exports it for Unity.

    blender -b --factory-startup --python Tools/Blender/build_ump.py -- \
        --out Assets/Weapons/Models/UMP [--renders <dir>] [--samples 64] [--res 100] [--no-export]

Gun frame (Unity axes, metres): +Z muzzle, +Y up, +X the gun's right (ejection) side. Same grip/magwell frame as the
M4A1/SCAR so ProceduralFirstPersonArms keeps the right palm on the pistol grip and the left hand on the magwell.
Objects: Body (polymer receiver halves, top rail, forend, barrel, skeleton stock, sights ...),
Magazine (pivot at the top of the magazine) and Bolt (the left-hand charging handle, pivot at the T-handle).
"""

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from assault_kit import *  # noqa: E402,F401,F403

BY = 0.042            # bore axis height
FH_Z0 = 0.340
MUZZLE_Z = FH_Z0 + 0.058
ANCHORS = {
    "muzzle": (0.0, BY, MUZZLE_Z + 0.012),
    "eject": (0.024, 0.046, 0.075),
}


def make_materials():
    return [
        make_material("UMPMetal", (0.095, 0.100, 0.110), 0.85, 0.40),
        make_material("UMPPolymer", (0.026, 0.028, 0.031), 0.0, 0.58),
        make_material("UMPAccent", (0.55, 0.38, 0.14), 0.85, 0.34),
    ]


def build_gun():
    reset_scene()
    mats = make_materials()
    M, P, A = MAT_METAL, MAT_POLY, MAT_ACCENT

    # ------------------------------------------------------------------------------------------
    # Upper receiver (polymer, rounded) with ejection port, charging-handle track, rear cap
    # ------------------------------------------------------------------------------------------
    upper = Builder("upper")
    upper.prism_z(rrect_pts(0.0, 0.0420, 0.0225, 0.0270, 0.0110, 3), -0.088, 0.240, P, bevel=0.0007, bseg=1)
    upper.lathe(V(0.0, 0.0420, -0.088), V(0, 0, -1), [(0.0, 0.0150), (0.0020, 0.0200), (0.0120, 0.0200), (0.0135, 0.0170), (0.0165, 0.0170)], 24, P, ry=1.20)
    upper.box((0.0, 0.0450, -0.1100), (0.0090, 0.0110, 0.0050), M, bevel=0.0006, bseg=1)       # rear end-cap latch
    upper.prism_x([(0.126, 0.030), (0.140, 0.030), (0.140, 0.046), (0.130, 0.052)], 0.0160, 0.0232, P, bevel=0.0004, bseg=1)  # deflector
    upper_obj = upper.commit("upper", mats)

    port = Builder("portcut")
    port.prism_x(rrect_pts(0.078, 0.0470, 0.0460, 0.0125, 0.0050, 3), 0.0040, 0.0300, P)
    boolean(upper_obj, port.commit("portcut", mats))
    track = Builder("chtrack")
    track.box((-0.0235, 0.0510, 0.0800), (0.0160, 0.0070, 0.0900), P)
    boolean(upper_obj, track.commit("chtrack", mats))
    # recessed grip panels on both flanks of the receiver
    panels = Builder("panels")
    for sx in (-1.0, 1.0):
        for (zc, ln) in ((0.178, 0.040), (0.210, 0.026)):
            panels.prism_x([(a_, 0.032 + b_) for (a_, b_) in stadium(zc, 0.0, ln, 0.0090, 5)], 0.0214 if sx > 0 else -0.0300, 0.0300 if sx > 0 else -0.0214, P)
    for i in range(9):                                                                         # rear grip grooves
        zg = -0.078 + i * 0.0092
        for sx in (-1.0, 1.0):
            panels.box((sx * 0.0228, 0.0420, zg), (0.0040, 0.0300, 0.0036), P)
    boolean(upper_obj, panels.commit("panels", mats))
    bevel_angle(upper_obj, 0.0006, 1, 40.0)

    # ------------------------------------------------------------------------------------------
    # Lower receiver and magwell
    # ------------------------------------------------------------------------------------------
    lower = Builder("lower")
    lp = [(-0.088, 0.030), (0.150, 0.030), (0.150, -0.018), (0.112, -0.036), (0.112, -0.046), (0.020, -0.046), (0.020, -0.020),
          (-0.040, -0.020), (-0.062, -0.026), (-0.088, -0.014)]
    lower.prism_x(lp, -0.0200, 0.0200, P, bevel=0.0012, bseg=2)
    lower_obj = lower.commit("lower", mats)
    mw = Builder("magwell")
    mw.box((0.0, -0.020, 0.0620), (0.0270, 0.1100, 0.0520), P)
    mw.box((0.0, -0.046, 0.0620), (0.0320, 0.0080, 0.0580), P)
    boolean(lower_obj, mw.commit("magwell", mats))
    bevel_angle(lower_obj, 0.0005, 1, 40.0)

    # ------------------------------------------------------------------------------------------
    # Grip, trigger guard, controls
    # ------------------------------------------------------------------------------------------
    grip = Builder("grip")
    gtop = V(0.0, 0.005, -0.004)
    tilt = 18.0
    pistol_grip(grip, (0.0, 0.005, -0.004), tilt, 0.116, P, P, hw_top=0.0172, depth_top=0.0330, swell=0.0050, hw_bot=0.0166)
    d_down = V(0.0, -math.cos(math.radians(tilt)), -math.sin(math.radians(tilt)))
    gbot = gtop + d_down * 0.116
    grip.cyl(gbot + d_down * 0.0005, gbot - d_down * 0.0030, 0.0128, P, segs=20, ry=1.15, bevel=0.0007, up_hint=V(0, 0, 1))
    grip.screw(gbot - d_down * 0.0030, -d_down, 0.0042, M, segs=10)
    grip.prism_x([(-0.062, -0.012), (-0.036, -0.012), (-0.010, -0.006), (-0.036, -0.040), (-0.066, -0.028)], -0.0150, 0.0150, P, bevel=0.0012, bseg=2)
    guard_path = bezier((-0.022, 0.026), (-0.062, 0.030), (-0.072, 0.008), (-0.052, -0.008), 14)
    grip.sweep(guard_path, [(0.0044, 0.0019)] * len(guard_path), P, r=0.0014, cs=2, bevel=0.0)
    trig_path = bezier((-0.006, 0.012), (-0.018, 0.014), (-0.028, 0.018), (-0.040, 0.024), 8)
    grip.sweep(trig_path, [(0.0032, 0.0020)] * len(trig_path), M, r=0.0010, cs=2)
    for (pz, py) in ((0.014, 0.002), (-0.003, 0.0035)):
        grip.cyl(V(-0.0190, py, pz), V(0.0190, py, pz), 0.0020, M, segs=10)
    for sx in (-1.0, 1.0):                                                                         # ambidextrous selector
        grip.cyl(V(sx * 0.0185, 0.0040, -0.034), V(sx * 0.0225, 0.0040, -0.034), 0.0065, M, segs=18)
        grip.box((sx * 0.0245, 0.0040, -0.0405), (0.0024, 0.0048, 0.0180), M, bevel=0.0007, bseg=1)
        grip.cyl(V(sx * 0.0185, 0.0040, -0.034), V(sx * 0.0220, 0.0040, -0.034), 0.0032, A, segs=10)
    grip.cyl(V(0.0185, -0.014, 0.0090), V(0.0255, -0.014, 0.0090), 0.0058, M, segs=18, bevel=0.0004)    # magazine release
    grip.cyl(V(0.0185, -0.014, 0.0090), V(0.0245, -0.014, 0.0090), 0.0036, A, segs=10)
    grip.box((-0.0192, 0.002, 0.0200), (0.0040, 0.0140, 0.0120), M, bevel=0.0008, bseg=1)           # bolt catch
    for pz in (-0.064, 0.118):
        grip.cyl(V(-0.0185, 0.0040, pz), V(-0.0235, 0.0040, pz), 0.0046, M, segs=14, bevel=0.0004)
        grip.cyl(V(0.0185, 0.0040, pz), V(0.0215, 0.0040, pz), 0.0036, M, segs=12)
        grip.cyl(V(-0.0185, 0.0040, pz), V(0.0185, 0.0040, pz), 0.0030, M, segs=10)

    # ------------------------------------------------------------------------------------------
    # Skeleton stock: hinge, two side plates with cut-outs, cross struts, butt plate
    # ------------------------------------------------------------------------------------------
    stock = Builder("stock")
    stock.box((0.0, 0.0420, -0.1000), (0.0440, 0.0480, 0.0240), P, bevel=0.0012, bseg=2)           # hinge housing
    stock.cyl(V(-0.0220, 0.0420, -0.1000), V(-0.0300, 0.0420, -0.1000), 0.0075, M, segs=18, bevel=0.0005)
    stock.cyl(V(-0.0220, 0.0420, -0.1000), V(-0.0285, 0.0420, -0.1000), 0.0036, A, segs=10)
    stock.cyl(V(0.0220, 0.0420, -0.1000), V(0.0260, 0.0420, -0.1000), 0.0050, A, segs=12)
    stock.cyl(V(0.0, 0.0340, -0.2000), V(0.0, 0.0340, -0.3220), 0.0052, M, segs=12)               # centre rods
    stock.cyl(V(0.0, 0.0020, -0.2000), V(0.0, 0.0020, -0.3220), 0.0052, M, segs=12)
    for zz in (-0.205, -0.262, -0.318):
        stock.box((0.0, 0.0180, zz), (0.0360, 0.0400, 0.0070), M, bevel=0.0010, bseg=1)             # cross frames
    stock.prism_x([(-0.322, 0.0680), (-0.3390, 0.0640), (-0.3440, 0.0150), (-0.3420, -0.0420), (-0.322, -0.0460)],
                  -0.0230, 0.0230, P, bevel=0.0035, bseg=3)                                          # butt plate
    for i in range(10):
        stock.box((0.0, 0.0540 - i * 0.0105, -0.3450 + 0.0004 * abs(i - 4)), (0.0400, 0.0040, 0.0016), P, bevel=0.0004, bseg=1)
    for i in range(14):
        zz = -0.125 - i * 0.0135
        for sx in (-1.0, 1.0):
            stock.box((sx * 0.0236, 0.0560, zz), (0.0030, 0.0030, 0.0070), P, bevel=0.0004, bseg=1)
    plates = Builder("plates")
    for sx in (-1.0, 1.0):
        plates.prism_x([(-0.1100, 0.0640), (-0.2000, 0.0600), (-0.3220, 0.0680), (-0.3220, -0.0460), (-0.2650, -0.0360), (-0.1100, 0.0100)],
                       sx * 0.0150, sx * 0.0230, P, bevel=0.0010, bseg=1)
    plates_obj = plates.commit("plates", mats)
    holes = Builder("plateholes")
    holes.prism_x([(a_, b_ + 0.0180) for (a_, b_) in stadium(-0.236, 0.0, 0.0960, 0.0300, 6)], -0.040, 0.040, P)
    holes.prism_x([(a_, b_ + 0.0180) for (a_, b_) in stadium(-0.151, 0.0, 0.0300, 0.0300, 6)], -0.040, 0.040, P)
    boolean(plates_obj, holes.commit("plateholes", mats))
    bevel_angle(plates_obj, 0.0010, 1, 35.0)

    # ------------------------------------------------------------------------------------------
    # Rail, forend, barrel, flash hider, sights
    # ------------------------------------------------------------------------------------------
    rail = Builder("rail")
    picatinny(rail, -0.082, 0.236, 0.0672, M)

    fore = Builder("fore")
    secs = []
    for (zz, hw, hh, cy) in ((0.230, 0.0230, 0.0260, 0.0420), (0.258, 0.0225, 0.0255, 0.0415), (0.300, 0.0200, 0.0225, 0.0405),
                            (0.330, 0.0182, 0.0205, 0.0405), (0.345, 0.0176, 0.0200, 0.0405)):
        secs.append((zz, rrect_pts(0.0, cy, hw, hh, 0.0100, 3)))
    fore.sections(V(0.0, 0.0, 0.0), V(0, 0, 1), secs, P)
    fore_obj = fore.commit("fore", mats)
    vents = Builder("vents")
    for i in range(4):
        zc = 0.262 + i * 0.0230
        prof = [(a_, 0.042 + b_) for (a_, b_) in stadium(zc, 0.0, 0.0140, 0.0050, 4)]
        vents.prism_x(prof, 0.012, 0.040, P)
        vents.prism_x(prof, -0.040, -0.012, P)
    for i in range(3):
        zc = 0.272 + i * 0.0300
        vents.prism_y([(b_, a_) for (a_, b_) in stadium(zc, 0.0, 0.0200, 0.0070, 4)], 0.000, 0.0300, P)
    for i in range(6):                                                                         # top cooling grooves
        vents.box((0.0, 0.0645, 0.2520 + i * 0.0145), (0.0300, 0.0040, 0.0050), P)
    boolean(fore_obj, vents.commit("vents", mats))
    bevel_angle(fore_obj, 0.0005, 1, 40.0)

    fore2 = Builder("fore2")
    for i in range(6):                                                                         # finger ridges under the forend
        fore2.box((0.0, 0.0177 + 0.0, 0.2600 + i * 0.0120), (0.0260, 0.0030, 0.0050), P, bevel=0.0007, bseg=1)
    fore2.lathe(V(0.0, BY, 0.2300), V(0, 0, 1), [(0.0, 0.0215), (0.0012, 0.0262), (0.0095, 0.0262), (0.0108, 0.0245), (0.0140, 0.0245)], 28, M)
    fore2.lathe(V(0.0, BY, 0.3400), V(0, 0, 1), [(0.0, 0.0195), (0.0012, 0.0205), (0.0060, 0.0205), (0.0075, 0.0165)], 24, M)     # muzzle collar
    for sx in (-1.0, 1.0):
        fore2.cyl(V(sx * 0.0190, 0.0420, 0.2480), V(sx * 0.0250, 0.0420, 0.2480), 0.0042, A, segs=12, bevel=0.0005)
    barrel = Builder("barrel")
    barrel.lathe(V(0.0, BY, 0.230), V(0, 0, 1), [(0.0, 0.0105), (0.060, 0.0105), (0.061, 0.0095), (0.118, 0.0095)], 20, M)
    fh = Builder("fh")
    fh.lathe(V(0.0, BY, FH_Z0), V(0, 0, 1), [(0.0, 0.0095), (0.0040, 0.0110), (0.0480, 0.0110), (0.0540, 0.0106), (0.0575, 0.0092), (0.0580, 0.0086)], 20, M)
    fh_obj = fh.commit("fh", mats)
    fhcut = Builder("fhcut")
    for k in range(4):
        fhcut.box((0.0, BY, FH_Z0 + 0.0340), (0.0030, 0.0400, 0.0220), M, rot=(0, 0, 45.0 + 90.0 * k), pivot=(0.0, BY, FH_Z0 + 0.0340))
    fhcut.cyl(V(0.0, BY, FH_Z0 + 0.0200), V(0.0, BY, FH_Z0 + 0.0640), 0.0056, M, segs=16)
    boolean(fh_obj, fhcut.commit("fhcut", mats))
    bevel_angle(fh_obj, 0.0004, 1, 40.0)

    sights = Builder("sights")
    fz = 0.2150                                                                                # front sight on the rail
    sights.box((0.0, 0.0780, fz), (0.0160, 0.0160, 0.0200), M, bevel=0.0007, bseg=1)
    sights.box((-0.0062, 0.0920, fz), (0.0020, 0.0140, 0.0130), M, bevel=0.0004, bseg=1)
    sights.box((0.0062, 0.0920, fz), (0.0020, 0.0140, 0.0130), M, bevel=0.0004, bseg=1)
    sights.cyl(V(0.0, 0.0820, fz), V(0.0, 0.0960, fz), 0.0013, M, segs=8)
    rz = -0.0500                                                                               # rear aperture
    sights.box((0.0, 0.0795, rz), (0.0240, 0.0110, 0.0240), M, bevel=0.0007, bseg=1)
    sights.box((0.0, 0.0880, rz - 0.003), (0.0130, 0.0080, 0.0100), M, bevel=0.0006, bseg=1)
    sights.box((-0.0092, 0.0950, rz), (0.0022, 0.0200, 0.0075), M, bevel=0.0005, bseg=1)
    sights.box((0.0092, 0.0950, rz), (0.0022, 0.0200, 0.0075), M, bevel=0.0005, bseg=1)
    sights.cyl(V(-0.0130, 0.0795, rz + 0.002), V(0.0130, 0.0795, rz + 0.002), 0.0024, A, segs=8)
    sights_obj = sights.commit("sights", mats)
    ap = Builder("ap")
    ap.cyl(V(0.0, 0.0950, rz - 0.010), V(0.0, 0.0950, rz + 0.010), 0.0020, M, segs=10)
    boolean(sights_obj, ap.commit("ap", mats))

    inner = Builder("inner")
    inner.box((0.0020, 0.0470, 0.080), (0.0070, 0.0200, 0.0780), M, bevel=0.0008, bseg=1)         # bolt visible in the port
    inner.box((0.0040, 0.0470, 0.078), (0.0050, 0.0050, 0.0800), A, bevel=0.0005, bseg=1)
    inner.cyl(V(0.0030, 0.0500, 0.040), V(0.0030, 0.0500, 0.105), 0.0034, M, segs=10)

    # ------------------------------------------------------------------------------------------
    # Magazine (25-round .45) and charging handle
    # ------------------------------------------------------------------------------------------
    mag_top = V(0.0, 0.0140, 0.0620)
    mag = Builder("mag")
    path = bezier((0.0140, 0.0620), (-0.0500, 0.0625), (-0.1100, 0.0660), (-0.1650, 0.0760), 24)
    n = len(path)
    sizes = [(lerp(0.0125, 0.0128, i / (n - 1)), lerp(0.0225, 0.0235, i / (n - 1))) for i in range(n)]
    mag.sweep(path, sizes, P, r=0.0040, cs=3, bevel=0.0)
    for i in range(3, n - 2):
        if i % 2 == 0:
            (py, pz) = path[i]
            (ny, nz) = path[i + 1]
            (my, mz) = path[i - 1]
            tang = Vector((0, ny - my, nz - mz)).normalized()
            for sx in (-1.0, 1.0):
                hw = sizes[i][0]
                mag.box((sx * (hw + 0.0003), py, pz), (0.0014, 0.0036, sizes[i][1] * 1.60), P, bevel=0.0005, bseg=1,
                        rot=(math.degrees(math.atan2(tang.z, -tang.y)), 0, 0), pivot=(sx * (hw + 0.0003), py, pz))
    fy, fz2 = path[-1]
    mag.box((0.0, fy - 0.0030, fz2 + 0.0020), (0.0270, 0.0090, 0.0500), P, bevel=0.0022, bseg=2, rot=(-30.0, 0, 0), pivot=(0.0, fy - 0.0030, fz2 + 0.0020))
    mag.box((0.0, fy - 0.0075, fz2 + 0.0030), (0.0210, 0.0030, 0.0400), A, bevel=0.0008, bseg=1, rot=(-30.0, 0, 0), pivot=(0.0, fy - 0.0075, fz2 + 0.0030))
    for i in range(6):                                                                         # round-count window ticks
        (py, pz) = path[8 + i * 3]
        mag.box((0.0, py, pz + 0.0215), (0.0100, 0.0020, 0.0014), A, bevel=0.0003, bseg=1)
    mag.box((0.0, 0.0150, 0.0620), (0.0230, 0.0090, 0.0460), M, bevel=0.0006, bseg=1)
    mag_obj = mag.commit("Magazine", mats)

    bolt = Builder("bolt")
    cy, cz = 0.0510, 0.0800
    bolt.box((-0.0232, cy, cz), (0.0042, 0.0100, 0.0480), M, bevel=0.0006, bseg=1)
    bolt.cyl(V(-0.0245, cy, cz), V(-0.0350, cy, cz), 0.0036, M, segs=12)
    bolt.box((-0.0372, cy, cz - 0.003), (0.0100, 0.0160, 0.0280), P, bevel=0.0018, bseg=2)
    for i in range(5):
        bolt.box((-0.0430, cy, cz - 0.014 + i * 0.0060), (0.0010, 0.0140, 0.0020), M)
    bolt.cyl(V(-0.0356, cy, cz - 0.003), V(-0.0432, cy, cz - 0.003), 0.0042, A, segs=10)
    bolt_obj = bolt.commit("Bolt", mats)

    committed = []
    for b_ in (grip, stock, rail, fore2, barrel, inner):
        committed.append(b_.commit(b_.name, mats))
    committed += [upper_obj, lower_obj, fore_obj, plates_obj, fh_obj, sights_obj]
    body_obj = join(committed, "Body")
    body_obj.data.name = "UMPBodyMesh"

    set_origin_unity(mag_obj, mag_top)
    set_origin_unity(bolt_obj, V(-0.0350, cy, cz))
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
    ("side", (1.25, 0.02, 0.02), (0.0, 0.02, 0.02), 50),
    ("side-left", (-1.25, 0.02, 0.02), (0.0, 0.02, 0.02), 50),
    ("threequarter", (0.75, 0.28, 0.85), (0.0, 0.01, 0.03), 48),
    ("threequarter-rear", (0.75, 0.28, -0.85), (0.0, 0.01, 0.00), 48),
    ("top", (0.38, 1.15, -0.30), (0.0, 0.03, 0.02), 46),
    ("detail-receiver", (0.28, 0.10, 0.10), (0.0, 0.03, 0.06), 55),
    ("detail-muzzle", (0.18, 0.10, 0.55), (0.0, 0.05, 0.30), 52),
    ("detail-stock", (0.34, 0.12, -0.55), (0.0, 0.02, -0.21), 52),
    ("detail-grip", (0.24, -0.06, 0.22), (0.0, -0.040, 0.02), 55),
]


def main():
    out = arg("--out", os.path.abspath("ump_out"))
    renders = arg("--renders", None)
    objs = build_gun()
    report(objs)
    if "--no-export" not in ARGS:
        fbx, glb = export_all(objs, out, "UMP")
        print("GUN_EXPORTED", fbx, glb)
    if renders:
        render_views(VIEWS, "ump", renders, int(arg("--samples", "48")), int(arg("--res", "100")),
                     (0.0, 0.01, 0.02), -0.20)


if __name__ == "__main__":
    main()
