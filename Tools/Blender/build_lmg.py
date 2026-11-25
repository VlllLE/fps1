"""Builds the detailed belt-fed LMG and exports it for Unity.

Run headless (Blender 4.2):

    blender -b --factory-startup --python Tools/Blender/build_lmg.py -- \
        --out Assets/Weapons/Models/LMG --renders <dir> [--samples 48] [--res 100]

Coordinates are Unity weapon space (s right, u up, f forward, metres); see weapon_lib.py.
Mesh objects exported as FBX siblings:
  LMGBody   receiver, feed cover, heavy finned barrel, vented handguard, flash hider, carry handle,
            stock, pistol grip, trigger guard, iron sights
  Magazine  ammo box + visible cartridge belt (the node WeaponController drops during reload)
  Bolt      charging handle (racked back by WeaponController at the end of the reload)
  Bipod     folded bipod under the handguard
Muzzle (0, 0.04, 0.74) and shell eject point (0.04, 0.05, 0.05) match the prefab transforms.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import weapon_lib as W  # noqa: E402
from weapon_lib import MAT_ACCENT, MAT_METAL, MAT_POLYMER, Part, Region, frame, lerp  # noqa: E402

arg = W.parse_args()
OUT_DIR = arg("--out", os.path.abspath("lmg_out"))
RENDER_DIR = arg("--renders", None)
SAMPLES = int(arg("--samples", "48"))
RES = int(arg("--res", "100"))

BORE_U = 0.04
M, P, A = MAT_METAL, MAT_POLYMER, MAT_ACCENT
BOX_PIVOT = (0.0, -0.09, 0.085)
BOLT_PIVOT = (0.032, 0.035, 0.12)
BIPOD_PIVOT = (0.0, 0.012, 0.455)


def build_receiver():
    rec = Part("rec")
    hw = 0.0315
    secs = [(-0.152, 0.93), (-0.147, 1.0), (0.185, 1.0), (0.19, 0.95)]
    rings = [W.rect_ring(0, 0.0235, f, hw * sc, 0.0485 * sc, r=0.010, rseg=3) for f, sc in secs]
    rec.loft(M, rings)
    cutters = []

    def cut_box(c, size, bevel=0.0006):
        p = Part("c")
        p.box(M, c, size, bevel=bevel)
        cutters.append(p)

    cut_box((0.030, 0.056, 0.045), (0.016, 0.022, 0.090))          # ejection port (right)
    cut_box((0.030, 0.035, 0.080), (0.016, 0.0055, 0.115))         # charging handle slot (right)
    cut_box((-0.030, 0.050, 0.085), (0.016, 0.038, 0.072))         # belt feed slot (left)
    for sx in (-1, 1):
        for k in range(6):
            cut_box((sx * 0.030, 0.016, -0.135 + k * 0.0125), (0.012, 0.030, 0.0055), bevel=0.0004)
    # Bottom feed opening.
    cut_box((0, -0.020, 0.085), (0.040, 0.010, 0.090))
    rec.cut(cutters)
    return rec


def build_body():
    body = Part("LMGBody")
    body.absorb(build_receiver())

    # Internals visible through the ejection port and feed slot.
    body.box(M, (0.0, 0.050, 0.085), (0.040, 0.020, 0.070), bevel=0.0008)
    body.box(A, (0.0155, 0.054, 0.040), (0.0012, 0.010, 0.022), bevel=0.0004)

    # Feed well (ammo box mount), bottom rail and feed cover.
    body.box(M, (0, -0.0375, 0.085), (0.060, 0.026, 0.100), bevel=0.0018, seg=2, taper=(0.92, 1.0))
    body.box(M, (0, -0.0255, 0.085), (0.068, 0.006, 0.110), bevel=0.0012)
    cover = [W.rect_ring(0, 0.0845, f, 0.0275 * sc, 0.0125 * sc, r=0.011, rseg=3)
             for f, sc in ((-0.052, 0.90), (-0.046, 1.0), (0.162, 1.0), (0.170, 0.92))]
    body.loft(M, cover)
    for f in (-0.026, 0.0, 0.026, 0.052, 0.078, 0.104, 0.130):
        body.box(M, (0, 0.0962, f), (0.026, 0.0016, 0.006), bevel=0.0005)
    for sx in (-1, 1):
        body.cyl(M, (sx * 0.0285, 0.0755, -0.047), 0.0044, 0.008, axis="s", n=12, bevel=0.0003)
        body.box(A, (sx * 0.0285, 0.0765, 0.160), (0.0036, 0.016, 0.009), bevel=0.0007)
    # Picatinny rail on the cover with a flip-up rear aperture and front-sight post.
    body.box(M, (0, 0.1000, 0.045), (0.0195, 0.0035, 0.19), bevel=0.0005)
    for i in range(15):
        body.box(M, (0, 0.1045, -0.03 + i * 0.0105), (0.0215, 0.0065, 0.0058), bevel=0.0007)
    body.box(M, (0, 0.1105, -0.034), (0.020, 0.010, 0.012), bevel=0.0008)
    body.torus(M, (0, 0.1185, -0.034), 0.0042, 0.0012, axis="f", n=16, m=6)

    # Carry-handle style barrel mount comes later; receiver details first.
    # Rear receiver plate, buffer tube and trigger group.
    body.box(M, (0, 0.0235, -0.1555), (0.060, 0.092, 0.010), bevel=0.0014, seg=2, r=0.006)
    body.cyl(M, (0, 0.030, -0.170), 0.0155, 0.040, axis="f", n=20, bevel=0.0008)
    body.revolve(M, [(-0.19, 0.0165), (-0.172, 0.0165)], (0, 0.030), n=20)
    body.box(M, (0, -0.040, -0.030), (0.034, 0.030, 0.100), bevel=0.0015, seg=2)

    # Trigger guard (large enough for a gloved hand), trigger, safety.
    pts = W.bezier((0, -0.058, -0.024), (0, -0.092, -0.026), (0, -0.092, 0.014), (0, -0.074, 0.018), 8)
    pts += W.bezier((0, -0.074, 0.018), (0, -0.056, 0.022), (0, -0.040, 0.020), (0, -0.0255, 0.018), 8)[1:]
    body.sweep(M, pts, 0.0046, 0.0028, bevel=0.0006)
    body.sweep(M, W.bezier((0, -0.027, -0.002), (0, -0.040, -0.004), (0, -0.052, -0.008), (0, -0.064, -0.002), 6),
               0.0036, 0.0030, bevel=0.0005)
    body.cyl(A, (0, -0.052, -0.050), 0.0042, 0.070, axis="s", n=12, bevel=0.0004)

    build_barrel(body)
    build_handguard(body)
    build_stock(body)
    build_grip(body)
    return body


def build_barrel(body):
    prof = [(0.17, 0.0185), (0.215, 0.0185), (0.216, 0.0175), (0.46, 0.0172), (0.485, 0.0172)]
    body.revolve(M, prof, (0, BORE_U), n=32)
    # Heavy barrel with cooling fins and a chamber collar.
    body.revolve(M, [(0.485, 0.0168), (0.678, 0.0150)], (0, BORE_U), n=32)
    for k in range(15):
        f = 0.50 + k * 0.0115
        body.revolve(M, [(f, 0.0172), (f, 0.0196), (f + 0.0050, 0.0196), (f + 0.0050, 0.0172)], (0, BORE_U), n=28, bevel=0.0003)
    # Gas block, gas tube and front sight with protective ears.
    body.box(M, (0, BORE_U + 0.011, 0.472), (0.026, 0.040, 0.032), bevel=0.0018, seg=2)
    body.cyl(M, (0, 0.0795, 0.33), 0.0068, 0.27, axis="f", n=16, bevel=0.0002)
    body.cyl(M, (0, 0.0795, 0.455), 0.0075, 0.030, axis="f", n=16)
    body.box(M, (0, 0.0905, 0.472), (0.0048, 0.020, 0.0064), bevel=0.0005)
    body.box(A, (0, 0.1025, 0.472), (0.0018, 0.0065, 0.0050), bevel=0.0003)
    for sx in (-1, 1):
        body.box(M, (sx * 0.0072, 0.0970, 0.472), (0.0022, 0.016, 0.008), bevel=0.0004)
    for f in (0.25, 0.34, 0.41):
        body.box(M, (0, 0.0725, f), (0.024, 0.0035, 0.012), bevel=0.0008)
    # Muzzle: slotted flash hider.
    hider = Part("hider")
    hider.revolve(M, [(0.672, 0.0172), (0.675, 0.0212), (0.738, 0.0212), (0.740, 0.0192), (0.740, 0.0110), (0.690, 0.0110), (0.690, 0.0095)],
                  (0, BORE_U), n=32, bevel=0.0)
    slots = []
    for k in range(3):
        s = Part("slot")
        s.box(M, (0, 0, 0), (0.0042, 0.060, 0.040), xf=frame((0, BORE_U, 0.71), roll=60.0 * k), bevel=0.0)
        slots.append(s)
    hider.cut(slots)
    body.absorb(hider)
    # Quick-change barrel carry handle (arched, with a polymer grip sleeve).
    body.revolve(M, [(0.52, 0.0215), (0.54, 0.0215)], (0, BORE_U), n=24, bevel=0.0004)
    body.revolve(M, [(0.605, 0.0215), (0.625, 0.0215)], (0, BORE_U), n=24, bevel=0.0004)
    arch = W.bezier((0, 0.055, 0.5305), (0, 0.140, 0.5305), (0, 0.140, 0.6145), (0, 0.055, 0.6145), 16)
    body.sweep(M, arch, 0.0045, 0.0050, n=12)
    sleeve = W.bezier((0, 0.113, 0.545), (0, 0.126, 0.552), (0, 0.126, 0.593), (0, 0.113, 0.600), 8)
    body.sweep(P, sleeve, 0.0092, 0.0098, n=14, bevel=0.0)


def build_handguard(body):
    hg = Part("hg")
    secs = [(0.212, 0.90), (0.218, 1.0), (0.428, 1.0), (0.434, 0.92)]
    rings = [W.superellipse_ring(0, 0.0345, f, 0.0360 * sc, 0.0350 * sc, n=32, power=0.50) for f, sc in secs]
    hg.loft(P, rings)
    cutters = []
    for sx in (-1, 1):
        for row_u, count in ((0.050, 9), (0.0355, 9), (0.021, 9)):
            for k in range(count):
                c = Part("v")
                c.box(M, (sx * 0.036, row_u, 0.245 + k * 0.0215), (0.016, 0.0072, 0.0125), bevel=0.0010)
                cutters.append(c)
    for k in range(7):
        c = Part("v")
        c.box(M, (0, 0.0695, 0.250 + k * 0.026), (0.020, 0.016, 0.0125), bevel=0.0010)
        cutters.append(c)
    hg.cut(cutters)
    body.absorb(hg)
    # Handguard retaining rings and the forward heat-shield cap.
    for f, w in ((0.2125, 0.006), (0.432, 0.007)):
        body.loft(M, [W.superellipse_ring(0, 0.0345, f - w / 2, 0.0362, 0.0352, n=32, power=0.50),
                      W.superellipse_ring(0, 0.0345, f + w / 2, 0.0362, 0.0352, n=32, power=0.50)])


def stock_top(f):
    return lerp(0.052, 0.060, (f + 0.17) / -0.235)


def build_stock(body):
    # Skeletal stock: top and bottom bars joined by uprights, ribbed butt pad.
    body.extrude(P, [(-0.17, 0.046), (-0.41, 0.060), (-0.41, 0.040), (-0.17, 0.030)], -0.0165, 0.0165, bevel=0.0018, seg=2)
    body.extrude(P, [(-0.17, 0.000), (-0.41, -0.048), (-0.41, -0.076), (-0.17, -0.026)], -0.0165, 0.0165, bevel=0.0018, seg=2)
    body.extrude(P, [(-0.170, 0.036), (-0.200, 0.036), (-0.200, -0.010), (-0.170, -0.010)], -0.0175, 0.0175, bevel=0.0018, seg=2)
    body.extrude(P, [(-0.330, 0.054), (-0.358, 0.056), (-0.358, -0.054), (-0.330, -0.040)], -0.0175, 0.0175, bevel=0.0018, seg=2)
    body.box(M, (0, 0.017, -0.30), (0.012, 0.062, 0.006), bevel=0.0007)
    # Cheek riser and sling stud.
    body.box(P, (0, 0.064, -0.30), (0.030, 0.008, 0.090), bevel=0.0025, seg=2, r=0.003)
    body.cyl(M, (0, -0.0685, -0.30), 0.0042, 0.010, axis="u", n=10)
    # Butt plate (accent rubber) with ribs.
    body.box(A, (0, -0.012, -0.4145), (0.052, 0.150, 0.014), bevel=0.0022, seg=2, r=0.006)
    for i in range(10):
        body.box(P, (0, 0.050 - i * 0.0125, -0.4225), (0.044, 0.0035, 0.0022), bevel=0.0006)
    body.cyl(M, (0.0, -0.045, -0.4235), 0.0035, 0.0016, axis="f", n=10)
    for sx in (-1, 1):
        body.cyl(M, (sx * 0.019, 0.0, -0.424), 0.0028, 0.0016, axis="f", n=10)


def build_grip(body):
    xf = frame((0, -0.020, -0.030), pitch=14.0)
    poly = [(0.028, 0.0, 0.006), (-0.031, 0.0, 0.006), (-0.035, -0.060, 0.004), (-0.037, -0.115, 0.010),
            (0.022, -0.120, 0.012), (0.026, -0.085, 0.004)]
    hw = 0.0185
    body.extrude(P, poly, -hw, hw, xf=xf, bevel=0.0045, seg=3)
    body.extrude(A, [(0.0242, -0.1195, 0.004), (-0.0382, -0.1155, 0.004), (-0.0375, -0.1255, 0.004), (0.0232, -0.1295, 0.004)],
                 -0.0195, 0.0195, xf=xf, bevel=0.0012, seg=2)

    def inside(a, b):
        return -0.028 < a < 0.021 and -0.110 < b < -0.012

    region = Region((-0.03, 0.022, -0.111, -0.01), inside)
    for sx in (-1, 1):
        body.bumps(P, region, ("s", sx * hw, sx), pitch=0.0034, size=0.0026, height=0.0009, xf=xf)


def build_magazine():
    mag = Part("Magazine", pivot=BOX_PIVOT)
    cx, cy, cz = BOX_PIVOT
    # Ammo box with stamped ribs, hinged lid, front latch and carry handle.
    mag.box(P, (0, cy, cz), (0.085, 0.092, 0.120), bevel=0.0028, seg=2, r=0.004)
    mag.box(M, (0, cy + 0.0495, cz), (0.088, 0.008, 0.124), bevel=0.0018, seg=2)
    mag.box(M, (0, cy + 0.0445, cz - 0.0615), (0.050, 0.014, 0.004), bevel=0.0008)
    mag.box(A, (0, cy + 0.003, cz + 0.0635), (0.030, 0.046, 0.0045), bevel=0.0012)
    for sx in (-1, 1):
        for k in range(3):
            mag.box(P, (sx * 0.0432, cy - 0.028 + k * 0.026, cz), (0.0022, 0.0075, 0.090), bevel=0.0006)
        for k in range(5):
            mag.box(P, (sx * 0.0432, cy + 0.0, cz - 0.045 + k * 0.0225), (0.0020, 0.074, 0.0050), bevel=0.0005)
        mag.box(M, (sx * 0.0422, cy - 0.0445, cz - 0.0615), (0.0100, 0.0060, 0.0100), bevel=0.0008)
        mag.box(M, (sx * 0.0422, cy - 0.0445, cz + 0.0615), (0.0100, 0.0060, 0.0100), bevel=0.0008)
    handle = W.bezier((0.0452, cy + 0.03, cz - 0.032), (0.062, cy + 0.04, cz - 0.032), (0.062, cy + 0.04, cz + 0.032),
                      (0.0452, cy + 0.03, cz + 0.032), 10)
    mag.sweep(M, handle, 0.0042, 0.0042, side=(0, 1, 0), n=10)
    # Visible belt: cartridges on links from the box up to the feed slot on the left of the receiver.
    spine = (W.bezier((-0.006, -0.0425, 0.085), (-0.014, -0.030, 0.085), (-0.046, -0.026, 0.085), (-0.050, -0.004, 0.085), 12)
             + W.bezier((-0.050, -0.004, 0.085), (-0.054, 0.016, 0.085), (-0.050, 0.036, 0.085), (-0.036, 0.050, 0.085), 12)[1:])
    pitch = 0.0128
    pts = [W.V(p) for p in spine]
    # Resample the spine at link pitch.
    out = [pts[0]]
    acc = 0.0
    for a, b in zip(pts, pts[1:]):
        seg = (b - a).length
        while acc + seg >= pitch:
            t = (pitch - acc) / seg
            a = a + (b - a) * t
            out.append(a.copy())
            seg = (b - a).length
            acc = 0.0
        acc += seg
    prev = None
    for i, p in enumerate(out):
        tangent = (out[min(i + 1, len(out) - 1)] - out[max(i - 1, 0)])
        roll = math.degrees(math.atan2(tangent.y, tangent.x))
        xf = frame((p.x, p.y, p.z), roll=roll)
        # Brass case with neck, copper-tipped bullet (accent), steel link rings.
        mag.revolve(A, [(-0.0235, 0.0048), (-0.0230, 0.0050), (-0.020, 0.0050), (0.005, 0.0050), (0.0075, 0.0038), (0.0095, 0.0033),
                        (0.0195, 0.0033), (0.0235, 0.0026), (0.0270, 0.0012), (0.0275, 0.0006)], (0, 0), n=12, xf=xf)
        mag.revolve(M, [(-0.0118, 0.0059), (-0.0082, 0.0059)], (0, 0), n=12, xf=xf, bevel=0.0)
        mag.revolve(M, [(0.0010, 0.0057), (0.0046, 0.0057)], (0, 0), n=12, xf=xf, bevel=0.0)
        if prev is not None:
            mid = (prev + p) * 0.5
            mag.box(M, tuple(mid), (0.0034, 0.0034, 0.0024), xf=None, bevel=0.0)
        prev = p
    return mag


def build_bolt():
    bolt = Part("Bolt", pivot=BOLT_PIVOT)
    sx, su, sf = BOLT_PIVOT
    bolt.cyl(M, (sx + 0.0085, su, sf), 0.0050, 0.026, axis="s", n=14, bevel=0.0004)
    bolt.cyl(P, (sx + 0.0265, su, sf), 0.0072, 0.042, axis="f", n=16, bevel=0.0006)
    bolt.box(M, (sx - 0.0015, su, sf), (0.004, 0.0165, 0.036), bevel=0.0007)
    bolt.cyl(A, (sx + 0.0265, su, sf - 0.0235), 0.0074, 0.004, axis="f", n=16, bevel=0.0004)
    bolt.cyl(A, (sx + 0.0265, su, sf + 0.0235), 0.0074, 0.004, axis="f", n=16, bevel=0.0004)
    return bolt


def build_bipod():
    bp = Part("Bipod", pivot=BIPOD_PIVOT)
    px, pu, pf = BIPOD_PIVOT
    bp.box(M, (0, pu + 0.002, pf), (0.040, 0.014, 0.030), bevel=0.0016, seg=2)
    bp.cyl(M, (0, pu - 0.004, pf), 0.0045, 0.052, axis="s", n=12, bevel=0.0003)
    for sx in (-1, 1):
        x = sx * 0.0255
        bp.cyl(M, (x, pu - 0.004, pf), 0.0058, 0.008, axis="s", n=14, bevel=0.0004)
        bp.cyl(M, (x, pu - 0.006, pf + 0.083), 0.0035, 0.160, axis="f", n=10, bevel=0.0003)
        bp.cyl(M, (x, pu - 0.006, pf + 0.150), 0.0050, 0.030, axis="f", n=12, bevel=0.0003)
        bp.box(A, (x, pu - 0.0065, pf + 0.171), (0.0085, 0.0095, 0.022), bevel=0.0018, seg=2)
        bp.cyl(M, (x * 0.80, pu - 0.006, pf + 0.047), 0.0042, 0.012, axis="s", n=10)
    bp.cyl(P, (0, pu - 0.004, pf + 0.02), 0.0028, 0.050, axis="s", n=8)
    return bp


def build():
    W.reset_scene()
    mats = W.make_materials(
        "LMG",
        ((0.085, 0.09, 0.10), 0.9, 0.36),
        ((0.035, 0.038, 0.036), 0.0, 0.58, 0.25),
        ((0.78, 0.55, 0.16), 0.85, 0.30))
    parts = [build_body(), build_magazine(), build_bolt(), build_bipod()]
    return W.build_objects(parts, mats)


def render(objs):
    return W.render_views("lmg", RENDER_DIR, [
        ("side", (2.2, 0.02, 0.17), (0, 0.0, 0.17), 62),
        ("left-belt", (-2.2, 0.02, 0.17), (0, 0.0, 0.17), 62),
        ("threequarter-front", (1.4, 0.6, 1.4), (0, 0.0, 0.17), 55),
        ("threequarter-rear", (-1.3, 0.55, -1.2), (0, 0.0, 0.17), 55),
        ("top", (0.0, 2.2, 0.171), (0, 0.0, 0.17), 62),
        ("detail-receiver", (0.9, 0.35, -0.35), (0, 0.03, 0.06), 60),
        ("detail-belt", (-0.8, 0.12, 0.65), (0, -0.01, 0.04), 60),
        ("detail-handguard", (0.75, 0.25, 0.9), (0, 0.04, 0.42), 60),
        ("detail-muzzle-bipod", (-0.6, -0.05, 1.05), (0, 0.04, 0.58), 60),
    ], samples=SAMPLES, floor_z=-0.17, scale=1.6, res=RES)


def main():
    objs = build()
    W.stats("LMG", objs)
    W.export(objs, OUT_DIR, "LMG")
    if RENDER_DIR:
        W.check_renders(render(objs))
        W.reset_scene()
        objs = build()
        W.check_renders(W.render_held("lmg", RENDER_DIR, objs, samples=SAMPLES, res=RES))


main()
