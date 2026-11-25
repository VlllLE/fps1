"""Builds the detailed Bolt-Action Sniper and exports it for Unity.

    blender -b --factory-startup --python Tools/Blender/build_bolt_sniper.py -- \
        --out Assets/Weapons/Models/BoltActionSniper --renders /path/to/dir [--samples 64] [--res 100]

Weapon space matches the prefab: +Z is the muzzle direction, +Y up, metres, origin at the receiver /
grip area. The bore axis is at y = 0.035 and the muzzle at z = 0.66 so the existing MuzzlePoint,
FirePoint and ShellEjectPoint transforms stay valid. Export objects (one renderer each in Unity):
Body, Scope, Bolt, Magazine, CheekRiser, BipodLegLeft, BipodLegRight. Material slots: GunMetal,
GunPolymer, SniperGlass, SniperAccent.
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
from gunlib import ACCENT, GLASS, METAL, POLYMER, Assembly, U  # noqa: E402
from gunparts import build_scope, knurl, picatinny, socket_screw, BOLT_SCOPE  # noqa: E402

arg = gl.parse_args()
OUT_DIR = arg("--out", os.path.abspath("bolt_sniper_out"))
RENDER_DIR = arg("--renders", None)
SAMPLES = int(arg("--samples", "48"))
RES = int(arg("--res", "100"))

BORE = 0.035
MUZZLE_Z = 0.66
RECV_REAR, RECV_FRONT = -0.095, 0.150
RAIL_Z0, RAIL_Z1 = -0.080, 0.145
RAIL_ORIGIN_Y = 0.0545
RAIL_TOP = RAIL_ORIGIN_Y + 0.006 + 0.0065
SCOPE_Y = RAIL_TOP + 0.0142 + 0.0152
SCOPE_RING_REAR_Z = 0.005
ANCHORS = {
    "MuzzlePoint": (0.0, BORE, MUZZLE_Z),
    "FirePoint": (0.0, BORE, MUZZLE_Z),
    "ShellEjectPoint": (0.026, 0.040, 0.070),
}


def side_poly(points):
    return [(z, y) for (z, y) in points]


def build_receiver(asm):
    r = asm.part("GunBody", "Body")
    prof = [(RECV_REAR, 0.0), (RECV_REAR, 0.0160), (RECV_REAR + 0.004, 0.0188), (RECV_REAR + 0.012, 0.0195),
            (RECV_FRONT - 0.016, 0.0195), (RECV_FRONT - 0.014, 0.0222), (RECV_FRONT - 0.006, 0.0225),
            (RECV_FRONT, 0.0212), (RECV_FRONT, 0.0)]
    r.revolve((0, BORE, 0), (0, 0, 1), prof, seg=56, mat=METAL)
    # bottom block (trigger group / bedding)
    r.box((0, BORE - 0.0235, (RECV_REAR + RECV_FRONT) / 2 + 0.004), (0.034, 0.0205, RECV_FRONT - RECV_REAR - 0.012),
          METAL, bevel=0.0035, seg=2)
    # recoil lug
    r.box((0, 0.0045, RECV_FRONT - 0.012), (0.052, 0.012, 0.010), METAL, bevel=0.0015, seg=1)
    # rear tang screw heads on the receiver ring and on top of the rail base
    for z in (-0.050, 0.050, 0.120):
        socket_screw(r, (0.0, BORE - 0.0255, z), (0, -1, 0), r=0.0034, h=0.0035)
    # serial plate on the left side
    r.box((-0.0196, BORE + 0.001, 0.018), (0.0018, 0.0105, 0.062), ACCENT, bevel=0.0004, seg=1)
    # rail
    rail = asm.part("Rail", "Body")
    picatinny(rail, (0, RAIL_ORIGIN_Y, RAIL_Z0), (1, 0, 0), (0, 1, 0), (0, 0, 1), RAIL_Z1 - RAIL_Z0,
              width=0.0215, base_h=0.006, tooth_h=0.0065, base_overlap=0.0040)
    # ejection port and bolt handle notch (boolean)
    cut = gl.Builder("cutter")
    cut.box((0.014, BORE + 0.0045, 0.068), (0.044, 0.0150, 0.080), POLYMER, bevel=0.0030, seg=2)
    cut.box((0.016, BORE + 0.0005, -0.005), (0.040, 0.0130, 0.0125), POLYMER, bevel=0.0020, seg=1)
    r.cut(cut)
    # bolt stop / safety lever on the right rear
    r.box((0.0215, BORE - 0.002, RECV_REAR + 0.030), (0.004, 0.010, 0.020), METAL, bevel=0.001, seg=1)
    return r


def build_barrel(asm):
    b = asm.part("Barrel", "Body")
    flute_z0, flute_z1 = 0.398, 0.572
    zs = [0.145, 0.155, 0.160, 0.2, 0.30, 0.380]
    radii = {0.145: 0.0185, 0.155: 0.0185, 0.160: 0.0162, 0.2: 0.0158, 0.30: 0.0150, 0.380: 0.0143}
    prof = [(0.140, 0.0)] + [(z, radii[z]) for z in zs]
    ramp = [0.0] * len(prof)
    n = 14
    for k in range(n + 1):
        z = flute_z0 + (flute_z1 - flute_z0) * k / n
        r = 0.0143 - (z - 0.38) * (0.0143 - 0.0129) / (0.585 - 0.38)
        prof.append((z, r))
        f = min(k, n - k) / 2.0
        ramp.append(min(1.0, f))
    prof.append((0.585, 0.0129))
    ramp.append(0.0)
    prof.append((0.585, 0.0))
    ramp.append(0.0)

    def flutes(i, t, th):
        d = ramp[i]
        if d <= 0.0:
            return 1.0
        return 1.0 - 0.115 * d * (0.5 + 0.5 * math.cos(8 * th)) ** 2.0 * 2.0 * 0.5
    b.revolve((0, BORE, 0), (0, 0, 1), prof, seg=72, mat=METAL, radmod=flutes)
    # barrel band / bedding ring where the fore-end ends
    ring = asm.part("BarrelBand", "Body")
    ring.revolve((0, BORE, 0), (0, 0, 1), [(0.368, 0.0141), (0.368, 0.0192), (0.372, 0.0198), (0.384, 0.0198),
                                           (0.388, 0.0192), (0.388, 0.0141)], seg=40, mat=ACCENT, closed_profile=True)
    # muzzle brake: ported body with chamfers, an open bore and a lock nut
    mb = asm.part("MuzzleBrake", "Body")
    z0 = 0.585
    prof = [(z0 + 0.0151, 0.0), (z0 + 0.0151, 0.0062), (MUZZLE_Z, 0.0062), (MUZZLE_Z, 0.0160), (MUZZLE_Z - 0.0030, 0.0186),
            (MUZZLE_Z - 0.0100, 0.0190), (z0 + 0.0130, 0.0190), (z0 + 0.0100, 0.0172), (z0 + 0.0030, 0.0165),
            (z0, 0.0150), (z0, 0.0)]
    mb.revolve((0, BORE, 0), (0, 0, 1), prof, seg=48, mat=METAL)
    ports = gl.Builder("ports")
    for zc, w in ((0.6180, 0.0070), (0.6340, 0.0070), (0.6500, 0.0070)):
        ports.box((0.0, BORE, zc), (0.0500, 0.0130, w), METAL, bevel=0.0012, seg=1)
    ports.box((0.0, BORE + 0.0118, 0.6340), (0.0100, 0.0200, 0.0225), METAL, bevel=0.0010, seg=1)
    mb.cut(ports)
    mb.revolve((0, BORE, 0), (0, 0, 1), [(z0 + 0.0, 0.0150), (z0 + 0.0, 0.0176), (z0 + 0.0075, 0.0176),
                                          (z0 + 0.0075, 0.0150)], seg=40, mat=ACCENT, closed_profile=True)


def build_forend(asm):
    f = asm.part("Forend", "Body")
    cy = BORE
    a, bdepth, p = 0.0262, 0.0470, 0.78
    outer = []
    for k in range(0, 17):
        t = -math.pi * k / 16.0
        c, s = math.cos(t), math.sin(t)
        outer.append((a * math.copysign(abs(c) ** p, c), cy + bdepth * math.copysign(abs(s) ** p, s)))
    shoulders_l = [(-0.0250, cy + 0.0058), (-0.0192, cy + 0.0060)]
    shoulders_r = [(0.0192, cy + 0.0060), (0.0250, cy + 0.0058)]
    rad = 0.0180
    # arc runs from the left shoulder (angle 180) down through the bottom (270) to the right shoulder (360)
    arc = [(rad * math.cos(math.pi + math.pi * k / 12.0), cy + rad * math.sin(math.pi + math.pi * k / 12.0))
           for k in range(0, 13)]
    poly = outer + shoulders_l + arc + shoulders_r

    def ring(z, wscale):
        return [Vector((x * wscale, y, z)) for (x, y) in poly]
    zs = [(0.152, 1.0), (0.345, 1.0), (0.362, 0.975), (0.375, 0.93)]
    f.loft([ring(z, w) for z, w in zs], POLYMER)
    for sx in (-1, 1):
        f.box((sx * 0.0262, 0.0100, 0.258), (0.0016, 0.020, 0.150), ACCENT, bevel=0.0006, seg=1)
    slots = gl.Builder("mlok")
    for sx in (-1, 1):
        for k in range(5):
            slots.box((sx * 0.0264, 0.0100, 0.2180 + k * 0.0300), (0.0060, 0.0090, 0.0185),
                      POLYMER, bevel=0.0035, seg=2)
    f.cut(slots)
    # rail under the fore-end (ARCA / Picatinny) for the bipod
    picatinny(f, (0, -0.0120, 0.190), (1, 0, 0), (0, -1, 0), (0, 0, 1), 0.176, width=0.0215,
              base_h=0.0050, tooth_h=0.0065, base_overlap=0.0030)
    return f


def build_stock(asm):
    s = asm.part("Stock", "Body")
    top = [(-0.092, 0.0525), (-0.150, 0.0505), (-0.255, 0.0470), (-0.335, 0.0515), (-0.347, 0.0600)]
    bottom = [(-0.347, -0.0520), (-0.295, -0.0400), (-0.235, -0.0220), (-0.165, -0.0150), (-0.092, -0.0150)]
    s.prism_rounded(top + bottom, "zy", [(-0.0225, 0.90), (-0.0165, 0.985), (0.0, 1.0), (0.0165, 0.985), (0.0225, 0.90)],
                    POLYMER, bevel=0.0022, seg=2, bevel_angle=math.radians(35), pivot=(-0.22, 0.012))
    # lightening slot, cut through
    cutter = gl.Builder("slot")
    slot = []
    zc, yc, rz, ry = -0.265, 0.0140, 0.036, 0.0125
    for k in range(24):
        a = 2 * math.pi * k / 24
        slot.append((zc + rz * math.cos(a), yc + ry * math.sin(a)))
    cutter.prism(slot, "zy", -0.04, 0.04, POLYMER)
    s.cut(cutter)
    # monopod socket and rear sling stud
    s.cyl((0, -0.0380, -0.300), (0, -0.0500, -0.300), 0.0042, 0.0042, seg=12, mat=METAL)
    s.cyl((0, -0.0500, -0.300), (0, -0.0540, -0.300), 0.0058, 0.0058, seg=12, mat=METAL)
    # length-of-pull spacers and butt pad
    s.prism([(-0.347, 0.0590), (-0.353, 0.0585), (-0.353, -0.0510), (-0.347, -0.0515)], "zy", -0.0215, 0.0215,
            ACCENT, bevel=0.0012, seg=1)
    s.prism([(-0.353, 0.0585), (-0.359, 0.0580), (-0.359, -0.0505), (-0.353, -0.0510)], "zy", -0.0212, 0.0212,
            POLYMER, bevel=0.0012, seg=1)
    pad = [(-0.359, 0.0580), (-0.3695, 0.0585), (-0.3730, 0.0300), (-0.3745, 0.0), (-0.3730, -0.0300),
           (-0.3680, -0.0540), (-0.359, -0.0510)]
    s.prism(pad, "zy", -0.0225, 0.0225, POLYMER, bevel=0.0018, seg=2)
    for dy in (0.040, -0.040):
        socket_screw(s, (0.0, dy, -0.3748), (0, 0, -1), r=0.0036, h=0.0030)
    # adjustment screws on both sides of the spacer stack
    for sx in (-1, 1):
        s.cyl((sx * 0.0195, 0.0300, -0.350), (sx * 0.0255, 0.0300, -0.350), 0.0030, 0.0030, seg=10, mat=METAL)
        s.revolve((sx * 0.0255, 0.0300, -0.350), (sx, 0, 0), [(0, 0), (0, 0.0055), (0.0050, 0.0055), (0.0050, 0)],
                  seg=14, mat=METAL, up=(0, 1, 0))
    # pistol grip
    grip = [(-0.0120, 0.0040), (-0.0400, -0.1180), (-0.0830, -0.1180), (-0.0880, -0.0800), (-0.0930, -0.0400), (-0.0960, 0.0040)]
    g = asm.part("Grip", "Body")
    g.prism_rounded(grip, "zy", [(-0.0185, 0.80), (-0.0150, 0.93), (0.0, 1.0), (0.0150, 0.93), (0.0185, 0.80)], POLYMER,
                    bevel=0.0030, seg=2, bevel_angle=math.radians(35), pivot=(-0.060, -0.060))
    for k in range(4):
        y = -0.030 - k * 0.020
        zf = -0.0120 + (y - 0.0040) / (-0.1180 - 0.0040) * (-0.0400 + 0.0120)
        g.box((0, y, zf + 0.0012), (0.0356, 0.0042, 0.0034), POLYMER, bevel=0.0008, seg=1)
    g.prism([(-0.0405, -0.1170), (-0.0835, -0.1170), (-0.0835, -0.1250), (-0.0405, -0.1250)], "zy", -0.0185, 0.0185,
            ACCENT, bevel=0.0018, seg=1)
    # trigger guard
    tg = asm.part("TriggerGuard", "Body")
    path = [(-0.0130, 0.0020), (-0.0125, -0.0200), (-0.0060, -0.0420), (0.0070, -0.0520), (0.0210, -0.0440),
            (0.0290, -0.0240), (0.0310, 0.0020)]
    pts = [Vector((0.0, y, z)) for (z, y) in path]
    section = [(-0.0058, -0.0020), (0.0058, -0.0020), (0.0058, 0.0020), (-0.0058, 0.0020)]
    tg.sweep(pts, section, METAL, up=(1, 0, 0))
    # trigger
    tr = asm.part("Trigger", "Body")
    tpath = [Vector((0, 0.0010, 0.0075)), Vector((0, -0.0140, 0.0072)), Vector((0, -0.0260, 0.0100)),
             Vector((0, -0.0330, 0.0140))]
    tr.sweep(tpath, [(-0.0026, -0.0042), (0.0026, -0.0042), (0.0026, 0.0042), (-0.0026, 0.0042)], ACCENT, up=(1, 0, 0))
    # magazine well
    mw = asm.part("MagWell", "Body")
    mw.box((0, -0.0010, 0.062), (0.0420, 0.0120, 0.0760), POLYMER, bevel=0.0030, seg=2)
    mw.box((0, -0.0040, 0.062), (0.0450, 0.0030, 0.0800), POLYMER, bevel=0.0010, seg=1)
    for sx in (-1, 1):
        mw.box((sx * 0.0212, -0.0020, 0.024), (0.0040, 0.0080, 0.010), METAL, bevel=0.001, seg=1)  # mag catch housing
    mw.cyl((0.0215, -0.0020, 0.024), (0.0290, -0.0020, 0.024), 0.0035, 0.0035, seg=10, mat=ACCENT)
    return s


def build_cheek_riser(asm):
    c = asm.part("CheekRiser", "CheekRiser")
    poly = [(-0.145, 0.0560), (-0.1465, 0.0642), (-0.165, 0.0665), (-0.245, 0.0665), (-0.2595, 0.0642), (-0.2610, 0.0560)]
    c.prism(poly, "zy", -0.0165, 0.0165, POLYMER, bevel=0.0028, seg=2)
    for z in (-0.175, -0.235):
        c.cyl((0, 0.0400, z), (0, 0.0580, z), 0.0034, 0.0034, seg=12, mat=METAL)
        # knurled lock knob on the right side at the stock flank
        c.revolve((0.0215, 0.0420, z), (1, 0, 0), [(0, 0), (0, 0.0075), (0.0100, 0.0075), (0.0100, 0.0)],
                  seg=28, mat=ACCENT, radmod=lambda i, t, th: knurl(16, 0.07)(th) if i in (1, 2) else 1.0,
                  up=(0, 1, 0))
        c.cyl((0, 0.0300, z), (0.0225, 0.0420, z), 0.0020, 0.0020, seg=8, mat=METAL)
    return c


def build_magazine(asm):
    m = asm.part("Magazine", "Magazine")
    body = [(0.0325, 0.0040), (0.0895, 0.0040), (0.0915, -0.0880), (0.0345, -0.0880)]
    m.prism(body, "zy", -0.0146, 0.0146, POLYMER, bevel=0.0028, seg=2)
    m.prism([(0.0335, 0.0100), (0.0885, 0.0100), (0.0895, 0.0030), (0.0325, 0.0030)], "zy", -0.0120, 0.0120, METAL,
            bevel=0.0012, seg=1)
    m.prism([(0.0335, -0.0870), (0.0925, -0.0870), (0.0930, -0.0950), (0.0330, -0.0950)], "zy", -0.0160, 0.0160, ACCENT,
            bevel=0.0018, seg=1)
    for k in range(4):
        y = -0.020 - k * 0.0160
        for sx in (-1, 1):
            m.box((sx * 0.0148, y, 0.0620 + (y + 0.044) * -0.03), (0.0030, 0.0050, 0.0420), POLYMER, bevel=0.0010, seg=1)
    # witness holes strip
    for k in range(5):
        m.cyl((0.0149, -0.025 - k * 0.012, 0.0780), (0.0156, -0.025 - k * 0.012, 0.0780), 0.0022, 0.0022, seg=8,
              mat=METAL)
    return m


def build_bolt(asm):
    b = asm.part("Bolt", "Bolt")
    prof = [(-0.1100, 0.0), (-0.1100, 0.0078), (-0.1060, 0.0110), (-0.0780, 0.0114), (-0.0750, 0.0098),
            (-0.0690, 0.0088)]
    z = -0.0690
    while z < 0.1400:
        z += 0.012
        prof.append((min(z, 0.1400), 0.0088))
    prof.append((0.1400, 0.0))
    spiral = [i for i, p in enumerate(prof) if -0.0680 <= p[0] <= 0.0200]

    def body_mod(i, t, th):
        if i in spiral:
            return 1.0 - 0.10 * max(0.0, math.cos(3 * th + 38.0 * t)) ** 1.5
        return 1.0
    b.revolve((0, BORE, 0), (0, 0, 1), prof, seg=48, mat=METAL, radmod=body_mod)
    b.cyl((0, BORE, -0.1100), (0, BORE, -0.1190), 0.0042, 0.0042, seg=16, mat=ACCENT)
    b.sphere((0, BORE, -0.1200), 0.0050, seg=14, rings=8, mat=ACCENT)
    # handle collar and stem, swept down and back
    b.revolve((0, BORE, -0.0120), (0, 0, 1), [(0.0, 0.0), (0.0, 0.0102), (0.0100, 0.0102), (0.0100, 0.0)], seg=32,
              mat=METAL)
    path = [Vector((0.0070, BORE, -0.0070)), Vector((0.0190, BORE - 0.0005, -0.0090)),
            Vector((0.0300, BORE - 0.0040, -0.0130)), Vector((0.0390, BORE - 0.0110, -0.0190)),
            Vector((0.0440, BORE - 0.0170, -0.0235))]
    circle = [(0.0042 * math.cos(2 * math.pi * k / 14), 0.0042 * math.sin(2 * math.pi * k / 14)) for k in range(14)]
    b.sweep(path, circle, METAL, up=(0, 1, 0))
    d = (path[-1] - path[-2]).normalized()
    knob_origin = path[-1] - d * 0.0030
    kp = [(0.0, 0.0), (0.0, 0.0062), (0.0040, 0.0066), (0.0050, 0.0108), (0.0140, 0.0138), (0.0240, 0.0128),
          (0.0310, 0.0098), (0.0350, 0.0050), (0.0364, 0.0)]
    b.revolve(knob_origin, d, kp, seg=36, mat=POLYMER,
              radmod=lambda i, t, th: knurl(18, 0.045)(th) if i in (4, 5, 6) else 1.0, up=(0, 1, 0))
    b.revolve(knob_origin + d * 0.0042, d, [(0.0, 0.0072), (0.0, 0.0100), (0.0030, 0.0110), (0.0030, 0.0074)],
              seg=36, mat=ACCENT, closed_profile=True, up=(0, 1, 0))
    return b


def build_bipod(asm, mount_z=0.265):
    y_top = -0.0110
    mount = asm.part("BipodMount", "Body")
    ucl = [(-0.0195, y_top), (-0.0125, y_top), (-0.0125, -0.0225), (0.0125, -0.0225), (0.0125, y_top),
           (0.0195, y_top), (0.0195, -0.0330), (-0.0195, -0.0330)]
    ring_pts = [[Vector((x, y, z)) for (x, y) in ucl] for z in (mount_z - 0.027, mount_z + 0.027)]
    mount.loft(ring_pts, METAL)
    # clamp thumb screw with a knurled knob on the right
    mount.cyl((-0.0200, -0.0170, mount_z), (0.0215, -0.0170, mount_z), 0.0022, 0.0022, seg=10, mat=METAL)
    mount.revolve((0.0214, -0.0170, mount_z), (1, 0, 0), [(0, 0), (0, 0.0075), (0.0085, 0.0075), (0.0085, 0.0)],
                  seg=28, mat=ACCENT, radmod=lambda i, t, th: knurl(14, 0.08)(th) if i in (1, 2) else 1.0,
                  up=(0, 1, 0))
    hinge_y = -0.0410
    for sx in (-1, 1):
        mount.box((sx * 0.0170, -0.0372, mount_z), (0.0190, 0.0170, 0.0240), METAL, bevel=0.0018, seg=1)
        mount.cyl((sx * 0.0080, hinge_y, mount_z), (sx * 0.0262, hinge_y, mount_z), 0.0030, 0.0030, seg=10, mat=METAL)
        mount.cyl((sx * 0.0260, hinge_y, mount_z), (sx * 0.0278, hinge_y, mount_z), 0.0046, 0.0046, seg=14, mat=METAL)
    for sx, name in ((-1, "BipodLegLeft"), (1, "BipodLegRight")):
        leg = asm.part(name, name)
        hinge = Vector((sx * 0.0170, hinge_y, mount_z))
        asm.pivot(name, hinge)
        d = Vector((sx * 0.30, -1.0, 0.0)).normalized()
        leg.cyl(hinge - Vector((sx * 0.0070, 0, 0)), hinge + Vector((sx * 0.0070, 0, 0)), 0.0074, 0.0074, seg=18,
                mat=METAL)
        leg.cyl(hinge, hinge + d * 0.082, 0.0060, 0.0060, seg=16, mat=METAL)
        # spring-loaded extension latch collar
        c0 = hinge + d * 0.070
        leg.revolve(c0, d, [(0.0, 0.0), (0.0, 0.0074), (0.0140, 0.0074), (0.0140, 0.0)], seg=20, mat=ACCENT,
                    closed_profile=False, up=(0, 0, 1))
        leg.cyl(hinge + d * 0.082, hinge + d * 0.170, 0.0044, 0.0044, seg=14, mat=METAL)
        for k in range(4):
            c = hinge + d * (0.098 + k * 0.016)
            leg.revolve(c, d, [(0.0, 0.0044), (0.0, 0.0053), (0.0030, 0.0053), (0.0030, 0.0044)], seg=14, mat=METAL,
                        closed_profile=True, up=(0, 0, 1))
        foot = hinge + d * 0.170
        leg.revolve(foot - d * 0.002, d, [(0.0, 0.0), (0.0, 0.0072), (0.0080, 0.0082), (0.0180, 0.0090), (0.0230, 0.0070),
                                          (0.0250, 0.0)], seg=22, mat=POLYMER, up=(0, 0, 1))


def build_assembly():
    asm = Assembly("BoltActionSniper")
    build_receiver(asm)
    build_barrel(asm)
    build_forend(asm)
    build_stock(asm)
    build_cheek_riser(asm)
    build_magazine(asm)
    build_bolt(asm)
    build_bipod(asm)
    info = build_scope(asm, SCOPE_Y, SCOPE_RING_REAR_Z - 0.094, RAIL_TOP, params=BOLT_SCOPE)
    asm.pivot("Scope", (0.0, SCOPE_Y, info["z_rear"] + info["L"] / 2.0))
    asm.pivot("Magazine", (0.0, 0.004, 0.0610))
    asm.pivot("Bolt", (0.0, BORE, 0.0))
    asm.pivot("CheekRiser", (0.0, 0.0560, -0.2030))
    return asm, info


def run_checks(asm, info):
    objs = asm.part_objects()
    names = list(objs)
    detached, edges = gl.connectivity_report(objs, "GunBody", tol=0.0025)
    print("DETACHED_PARTS", detached)
    print("CONNECTION_EDGES", len(edges))
    results = {}
    results["rail_receiver_gap"] = gl.mesh_gap(objs["Rail"], objs["GunBody"])
    for ring in ("ScopeRingRear", "ScopeRingFront"):
        results[ring + "_rail"] = gl.mesh_gap(objs[ring], objs["Rail"])
        results[ring + "_tube"] = gl.mesh_gap(objs[ring], objs["ScopeBody"])
    results["forend_barrel"] = gl.mesh_gap(objs["Forend"], objs["BarrelBand"])
    results["bipod_mount_forend"] = gl.mesh_gap(objs["BipodMount"], objs["Forend"])
    for leg in ("BipodLegLeft", "BipodLegRight"):
        results[leg + "_mount"] = gl.mesh_gap(objs[leg], objs["BipodMount"])
    lo, hi = gl.unity_bounds(objs["ScopeBody"])
    rlo, rhi = gl.unity_bounds(objs["Rail"])
    results["scope_tube_above_rail"] = lo.y - rhi.y
    results["scope_axis_over_bore"] = SCOPE_Y - BORE
    for k, v in results.items():
        print("CHECK", k, round(v, 5))
    for o in objs.values():
        bpy.data.objects.remove(o)
    return detached, results


def report(objects):
    total = 0
    for o in objects:
        t = gl.tri_count(o)
        total += t
        ol, oh = gl.unity_bounds(o)
        print("OBJECT", o.name, "tris", t, "verts", len(o.data.vertices), "mats", [m.name for m in o.data.materials],
              "min", tuple(round(v, 3) for v in ol), "max", tuple(round(v, 3) for v in oh))
    print("TOTAL_TRIS", total)
    return total


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
    total = report(objects)
    lo = Vector((9, 9, 9))
    hi = Vector((-9, -9, -9))
    for o in objects:
        l, h = gl.unity_bounds(o)
        lo = Vector((min(lo.x, l.x), min(lo.y, l.y), min(lo.z, l.z)))
        hi = Vector((max(hi.x, h.x), max(hi.y, h.y), max(hi.z, h.z)))
    print("BOUNDS_UNITY", tuple(round(v, 4) for v in lo), tuple(round(v, 4) for v in hi))
    if OUT_DIR:
        gl.export_models(objects, OUT_DIR, "BoltActionSniper")
        with open(os.path.join(OUT_DIR, "BoltActionSniper.anchors.json"), "w") as fh:
            json.dump({"anchors": ANCHORS, "scope": {"axis_y": SCOPE_Y, "rail_top": RAIL_TOP, "ring_z": info["ring_z"],
                                                      "z_rear": info["z_rear"], "front_z": info["front_z"]},
                       "triangles": total,
                       "bounds_min": list(lo), "bounds_max": list(hi), "checks": results}, fh, indent=2)
    if RENDER_DIR:
        from render_gun import render_gun
        details = [
            ("detail-scope", (0.0, 0.092, 0.10), (-0.20, 0.10, 0.34), 62),
            ("detail-bolt", (0.025, 0.030, -0.030), (0.22, 0.09, -0.22), 62),
            ("detail-muzzle", (0.0, 0.035, 0.60), (-0.24, 0.08, 0.20), 62),
            ("detail-bipod", (0.0, -0.045, 0.27), (-0.30, -0.02, 0.28), 62),
            ("detail-stock", (0.0, 0.020, -0.24), (-0.34, 0.12, -0.24), 58),
        ]
        render_gun("bolt-sniper", RENDER_DIR, SAMPLES, RES, objects, details)


main()
