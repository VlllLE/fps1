"""Builds the detailed DMR (designated marksman rifle) and exports it for Unity.

    blender -b --factory-startup --python Tools/Blender/build_dmr.py -- \
        --out Assets/Weapons/Models/DMRSniper --renders /path/to/dir [--samples 64] [--res 100]

Weapon space matches the prefab: +Z muzzle, +Y up, metres, origin near the receiver. The bore axis is at
y = 0.040 and the muzzle at z = 0.58, so the existing MuzzlePoint / FirePoint / ShellEjectPoint
transforms stay valid. Export objects (one renderer each): Body, Scope, Magazine, Stock, CheekRiser,
ChargingHandle. Material slots: GunMetal, GunPolymer, SniperGlass, SniperAccent.
"""

import json
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import gunlib as gl  # noqa: E402
from gunlib import ACCENT, GLASS, METAL, POLYMER, Assembly  # noqa: E402
from gunparts import DMR_SCOPE, build_scope, knurl, picatinny, socket_screw  # noqa: E402

arg = gl.parse_args()
OUT_DIR = arg("--out", os.path.abspath("dmr_out"))
RENDER_DIR = arg("--renders", None)
SAMPLES = int(arg("--samples", "48"))
RES = int(arg("--res", "100"))

BORE = 0.040
MUZZLE_Z = 0.58
REC_REAR, REC_FRONT = -0.085, 0.075
UPPER_BOTTOM, UPPER_TOP = 0.022, 0.066
RAIL_ORIGIN_Y = UPPER_TOP
RAIL_TOP = RAIL_ORIGIN_Y + 0.005 + 0.0065
HG_Z0, HG_Z1 = 0.062, 0.340
HG_R = 0.0305
SCOPE_Y = RAIL_TOP + 0.0142 + 0.0152
SCOPE_RING_REAR_Z = -0.030
MAG_TOP_Y = 0.006
ANCHORS = {
    "MuzzlePoint": (0.0, BORE, MUZZLE_Z),
    "FirePoint": (0.0, BORE, MUZZLE_Z),
    "ShellEjectPoint": (0.0240, 0.0420, 0.025),
}


def build_receivers(asm):
    up = asm.part("GunBody", "Body")
    up.prism_rounded([(REC_REAR, UPPER_BOTTOM), (REC_REAR, UPPER_TOP - 0.004), (REC_REAR + 0.004, UPPER_TOP),
                      (REC_FRONT - 0.004, UPPER_TOP), (REC_FRONT, UPPER_TOP - 0.004), (REC_FRONT, UPPER_BOTTOM)],
                     "zy", [(-0.0188, 0.93), (-0.0150, 0.985), (0.0, 1.0), (0.0150, 0.985), (0.0188, 0.93)],
                     METAL, bevel=0.0018, seg=2, bevel_angle=math.radians(35), pivot=(-0.005, 0.044))
    # barrel nut / delta ring flange where the handguard starts
    up.revolve((0, BORE, 0), (0, 0, 1), [(REC_FRONT - 0.020, 0.0), (REC_FRONT - 0.020, 0.0175), (REC_FRONT + 0.006, 0.0190),
                                         (REC_FRONT + 0.016, 0.0190), (REC_FRONT + 0.016, 0.0)], seg=40, mat=METAL)
    # lower receiver with magwell flare
    lo = asm.part("LowerReceiver", "Body")
    lo.prism_rounded([(REC_REAR, -0.030), (REC_REAR, 0.026), (REC_FRONT, 0.026), (REC_FRONT, -0.030)], "zy",
                     [(-0.0188, 0.93), (-0.0150, 0.985), (0.0, 1.0), (0.0150, 0.985), (0.0188, 0.93)], METAL,
                     bevel=0.0018, seg=2, bevel_angle=math.radians(35), pivot=(-0.005, -0.002))
    lo.box((0, -0.0190, 0.0390), (0.0390, 0.0080, 0.0780), POLYMER, bevel=0.0028, seg=2)
    lo.box((0, -0.0120, 0.0400), (0.0345, 0.0100, 0.0700), METAL, bevel=0.0020, seg=1)
    # rear takedown pin, pivot pin
    for z in (REC_REAR + 0.010, REC_FRONT - 0.010):
        lo.cyl((-0.0200, -0.0010 if z < 0 else 0.0000, z), (0.0200, -0.0010 if z < 0 else 0.0000, z), 0.0040, 0.0040,
               seg=12, mat=METAL)
        lo.cyl((0.0192, -0.0010 if z < 0 else 0.0000, z), (0.0238, -0.0010 if z < 0 else 0.0000, z), 0.0058, 0.0058,
               seg=14, mat=METAL)
    # selector (left), magazine release (right)
    lo.cyl((-0.0190, 0.0000, -0.0450), (-0.0240, 0.0000, -0.0450), 0.0052, 0.0052, seg=14, mat=ACCENT)
    lo.box((-0.0215, 0.0030, -0.0440), (0.0040, 0.0036, 0.0230), ACCENT, bevel=0.0008, seg=1,
           axes=(Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))))
    lo.cyl((0.0190, -0.0150, 0.0080), (0.0270, -0.0150, 0.0080), 0.0040, 0.0040, seg=12, mat=METAL)
    lo.cyl((0.0268, -0.0150, 0.0080), (0.0290, -0.0150, 0.0080), 0.0058, 0.0058, seg=14, mat=ACCENT)
    # bolt catch and forward assist
    lo.box((-0.0200, 0.0200, 0.0120), (0.0040, 0.0090, 0.0130), METAL, bevel=0.0010, seg=1)
    up.box((0.0215, 0.0340, REC_REAR + 0.0360), (0.0100, 0.0120, 0.0260), METAL, bevel=0.0020, seg=1)
    up.cyl((0.0260, 0.0340, REC_REAR + 0.0360), (0.0330, 0.0340, REC_REAR + 0.0360), 0.0050, 0.0050, seg=14, mat=METAL)
    up.cyl((0.0330, 0.0340, REC_REAR + 0.0360), (0.0345, 0.0340, REC_REAR + 0.0360), 0.0062, 0.0062, seg=14, mat=METAL)
    # ejection port cut and brass deflector
    cutter = gl.Builder("port")
    cutter.box((0.0160, 0.0430, 0.0250), (0.0260, 0.0200, 0.0560), POLYMER, bevel=0.0030, seg=2)
    up.cut(cutter)
    up.box((0.0192, 0.0400, 0.0610), (0.0070, 0.0170, 0.0100), METAL, bevel=0.0020, seg=1)
    # dust cover swung open below the port
    hinge = Vector((0.0185, 0.0295, 0.0250))
    d = Vector((0.80, -0.60, 0.0)).normalized()
    n = Vector((0.60, 0.80, 0.0))
    up.box(hinge + d * 0.0100, (0.0200, 0.0014, 0.0540), METAL, bevel=0.0004, seg=1, axes=(d, n, Vector((0, 0, 1))))
    up.cyl((0.0190, 0.0295, 0.0025), (0.0190, 0.0295, 0.0475), 0.0018, 0.0018, seg=8, mat=METAL)
    # rail (continuous flat-top, receiver and handguard)
    rail = asm.part("Rail", "Body")
    picatinny(rail, (0, RAIL_ORIGIN_Y, REC_REAR), (1, 0, 0), (0, 1, 0), (0, 0, 1), HG_Z1 - REC_REAR,
              width=0.0215, base_h=0.0050, tooth_h=0.0065, base_overlap=0.0030)
    # pistol grip and trigger guard
    g = asm.part("Grip", "Body")
    grip = [(-0.0200, -0.0200), (-0.0450, -0.1220), (-0.0860, -0.1220), (-0.0830, -0.0750), (-0.0780, -0.0400),
            (-0.0700, -0.0200)]
    g.prism_rounded(grip, "zy", [(-0.0185, 0.82), (-0.0150, 0.94), (0.0, 1.0), (0.0150, 0.94), (0.0185, 0.82)], POLYMER,
                    bevel=0.0028, seg=2, bevel_angle=math.radians(35), pivot=(-0.060, -0.075))
    for k in range(4):
        y = -0.045 - k * 0.019
        t = (y - (-0.0200)) / (-0.1220 + 0.0200)
        zf = -0.0200 + t * (-0.0450 + 0.0200)
        g.box((0, y, zf + 0.0010), (0.0350, 0.0040, 0.0032), POLYMER, bevel=0.0008, seg=1)
    g.prism([(-0.0455, -0.1180), (-0.0865, -0.1180), (-0.0865, -0.1260), (-0.0455, -0.1260)], "zy", -0.0190, 0.0190,
            ACCENT, bevel=0.0018, seg=1)
    tg = asm.part("TriggerGuard", "Body")
    path = [(-0.0240, -0.0260), (-0.0215, -0.0480), (-0.0100, -0.0640), (0.0080, -0.0630), (0.0230, -0.0500),
            (0.0310, -0.0230)]
    tg.sweep([Vector((0.0, y, z)) for (z, y) in path],
             [(-0.0058, -0.0020), (0.0058, -0.0020), (0.0058, 0.0020), (-0.0058, 0.0020)], METAL, up=(1, 0, 0))
    tr = asm.part("Trigger", "Body")
    tr.sweep([Vector((0, -0.0200, -0.0030)), Vector((0, -0.0300, -0.0036)), Vector((0, -0.0420, -0.0010)),
              Vector((0, -0.0490, 0.0030))],
             [(-0.0026, -0.0040), (0.0026, -0.0040), (0.0026, 0.0040), (-0.0026, 0.0040)], ACCENT, up=(1, 0, 0))
    # sling mount plate at the rear of the lower
    lo.box((0.0, -0.0010, REC_REAR - 0.0020), (0.0300, 0.0360, 0.0060), METAL, bevel=0.0012, seg=1)


def build_barrel_and_handguard(asm):
    b = asm.part("Barrel", "Body")
    b.revolve((0, BORE, 0), (0, 0, 1), [(REC_FRONT, 0.0), (REC_FRONT, 0.0125), (0.330, 0.0125), (0.336, 0.0130),
                                          (0.352, 0.0130), (0.360, 0.0120), (0.488, 0.0120), (0.494, 0.0132),
                                          (0.500, 0.0132), (0.500, 0.0)], seg=48, mat=METAL)
    # gas block, front sight tower and post
    gb = asm.part("GasBlock", "Body")
    gb.revolve((0, BORE, 0), (0, 0, 1), [(0.3340, 0.0), (0.3340, 0.0170), (0.3380, 0.0190), (0.3560, 0.0190),
                                           (0.3590, 0.0175), (0.3590, 0.0)], seg=40, mat=METAL)
    gb.box((0, BORE + 0.0230, 0.3465), (0.0120, 0.0300, 0.0145), METAL, bevel=0.0014, seg=1)
    gb.box((0, BORE + 0.0405, 0.3465), (0.0160, 0.0050, 0.0160), METAL, bevel=0.0010, seg=1)
    gb.cyl((0, BORE + 0.0420, 0.3465), (0, BORE + 0.0560, 0.3465), 0.0016, 0.0016, seg=10, mat=METAL)
    for sx in (-1, 1):
        gb.box((sx * 0.0068, BORE + 0.0450, 0.3465), (0.0016, 0.0120, 0.0100), METAL, bevel=0.0004, seg=1)
    socket_screw(gb, (0.0190, BORE - 0.0020, 0.3465), (1, 0, 0), r=0.0034, h=0.0032)
    socket_screw(gb, (-0.0190, BORE - 0.0020, 0.3465), (-1, 0, 0), r=0.0034, h=0.0032)
    # flash hider with slots
    fh = asm.part("MuzzleBrake", "Body")
    z0 = 0.498
    prof = [(z0, 0.0), (z0, 0.0150), (z0 + 0.0040, 0.0158), (z0 + 0.0060, 0.0158), (z0 + 0.0100, 0.0138),
            (MUZZLE_Z - 0.060, 0.0138), (MUZZLE_Z - 0.020, 0.0152), (MUZZLE_Z - 0.004, 0.0152), (MUZZLE_Z, 0.0132),
            (MUZZLE_Z, 0.0072), (MUZZLE_Z - 0.050, 0.0072), (MUZZLE_Z - 0.050, 0.0)]
    fh.revolve((0, BORE, 0), (0, 0, 1), prof, seg=40, mat=METAL)
    slots = gl.Builder("fhslots")
    for k in range(3):
        a = k * math.pi / 3.0
        ax = Vector((math.cos(a), math.sin(a), 0.0))
        ay = Vector((-math.sin(a), math.cos(a), 0.0))
        slots.box((0.0, BORE, MUZZLE_Z - 0.024), (0.0500, 0.0040, 0.0400), METAL, axes=(ax, ay, Vector((0, 0, 1))))
    fh.cut(slots)

    # hollow free-float quad rail handguard with M-LOK slots on the diagonals
    h = asm.part("Handguard", "Body")
    wall = 0.0030
    h.revolve((0, BORE, 0), (0, 0, 1),
              [(HG_Z0, HG_R - 0.0020), (HG_Z0, HG_R + 0.0030), (HG_Z0 + 0.0150, HG_R + 0.0030), (HG_Z0 + 0.0150, HG_R),
               (HG_Z1 - 0.0120, HG_R), (HG_Z1 - 0.0120, HG_R + 0.0030), (HG_Z1, HG_R + 0.0030), (HG_Z1, HG_R - wall),
               (HG_Z0, HG_R - wall)], seg=64, mat=POLYMER, closed_profile=True)
    # rounded-square look: keep a round tube but add the diagonal slots
    slots = gl.Builder("mlok")
    for k in range(4):
        a = math.radians(45 + 90 * k)
        radial = Vector((math.cos(a), math.sin(a), 0.0))
        tang = Vector((-math.sin(a), math.cos(a), 0.0))
        for i in range(7):
            z = 0.095 + i * 0.0355
            c = Vector((0.0, BORE, z)) + radial * (HG_R - 0.0010)
            slots.box(c, (0.0100, 0.0070, 0.0300), METAL, bevel=0.0030, seg=2, axes=(radial, tang, Vector((0, 0, 1))))
    h.cut(slots)
    # side and bottom rails
    rails = asm.part("HandguardRails", "Body")
    for ang, z0, z1 in ((0.0, 0.130, 0.300), (180.0, 0.130, 0.300), (270.0, 0.120, 0.250)):
        a = math.radians(ang)
        ex = Vector((0, 1, 0)) if ang in (0.0, 180.0) else Vector((1, 0, 0))
        out = Vector((math.cos(a), math.sin(a), 0.0))
        origin = Vector((0.0, BORE, z0)) + out * (HG_R - 0.0028)
        picatinny(rails, origin, ex, out, (0, 0, 1), z1 - z0, width=0.0215, base_h=0.0050, tooth_h=0.0065,
                  base_overlap=0.0030)
    # vent / lanyard end cap ring
    rails.revolve((0, BORE, 0), (0, 0, 1), [(HG_Z1 - 0.0040, HG_R - 0.0010), (HG_Z1 - 0.0040, HG_R + 0.0050),
                                            (HG_Z1 + 0.0050, HG_R + 0.0035), (HG_Z1 + 0.0050, HG_R - 0.0010)],
                  seg=64, mat=ACCENT, closed_profile=True)


def build_magazine(asm):
    m = asm.part("Magazine", "Magazine")
    pts = []
    n = 8
    for k in range(n + 1):
        f = k / n
        y = MAG_TOP_Y - f * 0.1550
        z = 0.0360 + 0.020 * f * f
        pts.append(Vector((0.0, y, z)))
    section = [(-0.0148, -0.0330), (0.0148, -0.0330), (0.0148, 0.0330), (-0.0148, 0.0330)]
    m.sweep(pts, section, POLYMER, up=(1, 0, 0))
    # feed lips and floor plate
    top = pts[0]
    m.box((0, MAG_TOP_Y + 0.0040, 0.0360), (0.0260, 0.0100, 0.0620), METAL, bevel=0.0012, seg=1)
    bot = pts[-1]
    m.box((0, bot.y - 0.0045, bot.z + 0.0030), (0.0330, 0.0090, 0.0760), ACCENT, bevel=0.0018, seg=1)
    for k in range(1, n):
        for sx in (-1, 1):
            f = k / n
            m.box((sx * 0.0150, MAG_TOP_Y - f * 0.1550, 0.0360 + 0.020 * f * f), (0.0030, 0.0052, 0.0540), POLYMER,
                  bevel=0.0010, seg=1)
    return m


def build_stock(asm):
    # buffer tube and castle nut
    t = asm.part("BufferTube", "Body")
    ty = 0.0340
    t.revolve((0, ty, 0), (0, 0, 1), [(REC_REAR + 0.004, 0.0), (REC_REAR + 0.004, 0.0170), (-0.150, 0.0170),
                                       (-0.215, 0.0160), (-0.230, 0.0160), (-0.230, 0.0)], seg=40, mat=METAL)
    t.revolve((0, ty, 0), (0, 0, 1), [(REC_REAR - 0.018, 0.0150), (REC_REAR - 0.018, 0.0215), (REC_REAR - 0.002, 0.0215),
                                       (REC_REAR - 0.002, 0.0150)], seg=12, mat=METAL, closed_profile=True)
    # telescoping stock: adjustable cheek-weld body
    s = asm.part("Stock", "Stock")
    top = [(-0.130, 0.0540), (-0.215, 0.0530), (-0.2800, 0.0600)]
    bottom = [(-0.2800, -0.0360), (-0.2450, -0.0120), (-0.2000, 0.0020), (-0.130, 0.0070)]
    s.prism_rounded(top + bottom, "zy", [(-0.0215, 0.80), (-0.0160, 0.94), (-0.006, 1.0), (0.006, 1.0), (0.0160, 0.94), (0.0215, 0.80)],
                    POLYMER, bevel=0.0020, seg=2, bevel_angle=math.radians(35), pivot=(-0.205, 0.016))
    lt = gl.Builder("lighten")
    lt.box((0.0, 0.0100, -0.2400), (0.0600, 0.0120, 0.0300), POLYMER, bevel=0.0055, seg=3)
    s.cut(lt)
    # butt pad with ribs, QD sockets
    pad = [(-0.2800, 0.0610), (-0.2860, 0.0590), (-0.2920, 0.0300), (-0.2930, 0.0), (-0.2920, -0.0200),
           (-0.2860, -0.0370), (-0.2800, -0.0385)]
    s.prism(pad, "zy", -0.0200, 0.0200, ACCENT, bevel=0.0018, seg=2)
    for sx in (-1, 1):
        s.cyl((sx * 0.0170, 0.0220, -0.2000), (sx * 0.0240, 0.0220, -0.2000), 0.0050, 0.0050, seg=14, mat=METAL)
        s.cyl((sx * 0.0240, 0.0220, -0.2000), (sx * 0.0262, 0.0220, -0.2000), 0.0070, 0.0070, seg=14, mat=ACCENT)
    # collapsing lever under the tube
    s.box((0, -0.0010, -0.1500), (0.0090, 0.0060, 0.0300), ACCENT, bevel=0.0012, seg=1)
    # adjustment notches (tube indexing) on the tube behind the receiver
    for k in range(4):
        s.revolve((0, ty, -0.1040 - k * 0.0030), (0, 0, -1), [(0, 0.0166), (0, 0.0172), (0.0014, 0.0172), (0.0014, 0.0166)],
                  seg=24, mat=METAL, closed_profile=True)
    # cheek riser
    c = asm.part("CheekRiser", "CheekRiser")
    poly = [(-0.165, 0.0640), (-0.1665, 0.0710), (-0.180, 0.0725), (-0.235, 0.0725), (-0.2480, 0.0710), (-0.2490, 0.0640)]
    c.prism_rounded(poly, "zy", [(-0.0165, 0.90), (0.0, 1.0), (0.0165, 0.90)], POLYMER, bevel=0.0018, seg=2,
                    bevel_angle=math.radians(35))
    for z in (-0.185, -0.230):
        c.cyl((0, 0.0480, z), (0, 0.0660, z), 0.0034, 0.0034, seg=12, mat=METAL)
    c.revolve((0.0165, 0.0680, -0.2075), (1, 0, 0), [(0, 0), (0, 0.0075), (0.0100, 0.0075), (0.0100, 0.0)], seg=28,
              mat=ACCENT, radmod=lambda i, tt, th: knurl(16, 0.07)(th) if i in (1, 2) else 1.0, up=(0, 1, 0))


def build_charging_handle(asm):
    c = asm.part("ChargingHandle", "ChargingHandle")
    c.box((0, 0.0575, REC_REAR - 0.0220), (0.0140, 0.0070, 0.0440), METAL, bevel=0.0012, seg=1)
    c.box((0, 0.0575, REC_REAR - 0.0450), (0.0400, 0.0070, 0.0090), METAL, bevel=0.0014, seg=1)
    for sx in (-1, 1):
        c.box((sx * 0.0190, 0.0575, REC_REAR - 0.0450), (0.0150, 0.0068, 0.0160), POLYMER, bevel=0.0018, seg=1)
        c.box((sx * 0.0095, 0.0575, REC_REAR - 0.0100), (0.0050, 0.0070, 0.0140), METAL, bevel=0.0010, seg=1)
    asm.pivot("ChargingHandle", (0.0, 0.0575, REC_REAR))


def build_assembly():
    asm = Assembly("DMRSniper")
    build_receivers(asm)
    build_barrel_and_handguard(asm)
    build_magazine(asm)
    build_stock(asm)
    build_charging_handle(asm)
    info = build_scope(asm, SCOPE_Y, SCOPE_RING_REAR_Z - DMR_SCOPE["rings"][0], RAIL_TOP, params=DMR_SCOPE)
    asm.pivot("Scope", (0.0, SCOPE_Y, info["z_rear"] + info["L"] / 2.0))
    asm.pivot("Magazine", (0.0, MAG_TOP_Y, 0.0360))
    asm.pivot("Stock", (0.0, 0.0340, -0.130))
    asm.pivot("CheekRiser", (0.0, 0.0640, -0.2070))
    return asm, info


def run_checks(asm, info):
    objs = asm.part_objects()
    detached, edges = gl.connectivity_report(objs, "GunBody", tol=0.0025)
    print("DETACHED_PARTS", detached)
    results = {}
    results["rail_receiver_gap"] = gl.mesh_gap(objs["Rail"], objs["GunBody"])
    for ring in ("ScopeRingRear", "ScopeRingFront"):
        results[ring + "_rail"] = gl.mesh_gap(objs[ring], objs["Rail"])
        results[ring + "_tube"] = gl.mesh_gap(objs[ring], objs["ScopeBody"])
    results["handguard_receiver"] = gl.mesh_gap(objs["Handguard"], objs["GunBody"])
    results["gasblock_barrel"] = gl.mesh_gap(objs["GasBlock"], objs["Barrel"])
    results["magazine_lower"] = gl.mesh_gap(objs["Magazine"], objs["LowerReceiver"])
    lo, hi = gl.unity_bounds(objs["ScopeBody"])
    rlo, rhi = gl.unity_bounds(objs["Rail"])
    results["scope_tube_above_rail"] = lo.y - rhi.y
    results["scope_axis_over_bore"] = SCOPE_Y - BORE
    for k, v in results.items():
        print("CHECK", k, round(v, 5))
    for o in objs.values():
        bpy.data.objects.remove(o)
    return detached, results


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    gl.make_materials()
    asm, info = build_assembly()
    detached, results = run_checks(asm, info)
    objects = asm.build_groups()
    for o in objects:
        gl.shade_and_unwrap(o)
        gl.prune_unused_slots(o)
    total = 0
    for o in objects:
        t = gl.tri_count(o)
        total += t
        ol, oh = gl.unity_bounds(o)
        print("OBJECT", o.name, "tris", t, "verts", len(o.data.vertices), "mats", [m.name for m in o.data.materials],
              "min", tuple(round(v, 3) for v in ol), "max", tuple(round(v, 3) for v in oh))
    print("TOTAL_TRIS", total)
    lo = Vector((9, 9, 9))
    hi = Vector((-9, -9, -9))
    for o in objects:
        l, h = gl.unity_bounds(o)
        lo = Vector((min(lo.x, l.x), min(lo.y, l.y), min(lo.z, l.z)))
        hi = Vector((max(hi.x, h.x), max(hi.y, h.y), max(hi.z, h.z)))
    print("BOUNDS_UNITY", tuple(round(v, 4) for v in lo), tuple(round(v, 4) for v in hi))
    if OUT_DIR:
        gl.export_models(objects, OUT_DIR, "DMRSniper")
        with open(os.path.join(OUT_DIR, "DMRSniper.anchors.json"), "w") as fh:
            json.dump({"anchors": ANCHORS, "scope": {"axis_y": SCOPE_Y, "rail_top": RAIL_TOP, "ring_z": info["ring_z"],
                                                      "z_rear": info["z_rear"], "front_z": info["front_z"]},
                       "triangles": total, "bounds_min": list(lo), "bounds_max": list(hi), "checks": results},
                      fh, indent=2)
    if RENDER_DIR:
        from render_gun import render_gun
        details = [
            ("detail-optic", (0.0, 0.100, 0.02), (-0.20, 0.10, 0.30), 62),
            ("detail-receiver", (0.02, 0.030, 0.00), (0.24, 0.06, 0.20), 60),
            ("detail-handguard", (0.0, 0.040, 0.24), (-0.28, 0.06, 0.20), 58),
            ("detail-muzzle", (0.0, 0.040, 0.52), (-0.22, 0.07, 0.18), 62),
            ("detail-stock", (0.0, 0.020, -0.19), (-0.32, 0.10, -0.22), 58),
        ]
        render_gun("dmr", RENDER_DIR, SAMPLES, RES, objects, details)


main()
