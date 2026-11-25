"""Builds the detailed Pump Shotgun and exports it for Unity.

Run headless (Blender 4.2):

    blender -b --factory-startup --python Tools/Blender/build_shotgun.py -- \
        --out Assets/Weapons/Models/Shotgun --renders <dir> [--samples 48] [--res 100] [--no-render]

Coordinates are Unity weapon space (s right, u up, f forward, metres); see weapon_lib.py.
Parts exported as separate mesh objects (FBX siblings):
  ShotgunBody  receiver, barrel, magazine tube, stock, pistol grip, side-saddle shells ...
  Pump         ribbed forend + action bars, slid along f by WeaponPartAnimator
Materials: Shotgun_Metal, Shotgun_Polymer, Shotgun_Accent (remapped to Mat_Gun* in Unity by name).
Muzzle (0, 0.045, 0.57) and shell eject point (0.03, 0.04, 0.0) match the prefab transforms.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import weapon_lib as W  # noqa: E402
from weapon_lib import MAT_ACCENT, MAT_METAL, MAT_POLYMER, Part, Region, frame, lerp, smoothstep  # noqa: E402

arg = W.parse_args()
OUT_DIR = arg("--out", os.path.abspath("shotgun_out"))
RENDER_DIR = arg("--renders", None)
SAMPLES = int(arg("--samples", "48"))
RES = int(arg("--res", "100"))

BORE_U = 0.045
TUBE_U = 0.0165
MUZZLE_F = 0.57
M, P, A = MAT_METAL, MAT_POLYMER, MAT_ACCENT


def build_receiver():
    rec = Part("rec")
    hw = 0.0165
    poly = [(-0.122, 0.052, 0.006), (-0.105, 0.0625, 0.003), (0.085, 0.0625, 0.003),
            (0.089, 0.056, 0.002), (0.089, -0.020, 0.003), (-0.02, -0.0255, 0.003), (-0.122, -0.002, 0.004)]
    rec.extrude(M, poly, -hw, hw, bevel=0.0016, seg=2)
    # Raised top tang rear and finger scallops are modelled as separate low blocks.
    rec.box(M, (0, 0.0635, -0.1), (0.022, 0.003, 0.03), bevel=0.0008)
    cutters = []
    # Ejection port on the right side, deep pocket.
    port = Part("port")
    port.box(M, (0.0125, 0.045, 0.025), (0.0225, 0.026, 0.074), bevel=0.0012)
    cutters.append(port)
    # Loading port in the belly for the shell lifter.
    belly = Part("belly")
    belly.box(M, (0, -0.021, 0.036), (0.020, 0.012, 0.040), bevel=0.001)
    cutters.append(belly)
    # Action bar slots in the receiver front.
    for sx in (-1, 1):
        slot = Part("slot")
        slot.box(M, (sx * 0.0148, 0.004, 0.08), (0.006, 0.010, 0.020), bevel=0.0005)
        cutters.append(slot)
    # Cross-bolt safety hole.
    rec.cut(cutters)
    return rec


def build_body():
    body = Part("ShotgunBody")
    body.absorb(build_receiver())

    # Bolt carrier visible in the ejection port, shell lifter in the belly.
    body.box(M, (0.0045, 0.045, 0.030), (0.0045, 0.016, 0.05), bevel=0.0007)
    body.box(A, (0.0072, 0.045, 0.049), (0.0012, 0.008, 0.012), bevel=0.0004)
    body.box(M, (0, -0.0185, 0.044), (0.0125, 0.0025, 0.03), bevel=0.0006)
    # Serial plate and shell stop, receiver pins.
    body.box(A, (0.0168, 0.016, -0.03), (0.0012, 0.018, 0.06), bevel=0.0004)
    for f, u in ((-0.098, 0.040), (-0.062, 0.012), (-0.012, 0.0), (0.052, -0.012), (0.062, 0.050)):
        body.cyl(M, (0, u, f), 0.0026, 0.0352, axis="s", n=10, bevel=0.0003)
    body.cyl(M, (0.0175, 0.018, 0.074), 0.0042, 0.003, axis="s", n=14, bevel=0.0004)
    body.cyl(M, (-0.0175, 0.018, 0.074), 0.0042, 0.003, axis="s", n=14, bevel=0.0004)
    # Grooved receiver top: shallow ribs for glare reduction.
    for i in range(9):
        body.box(M, (0, 0.0634, 0.0 + i * 0.0062), (0.0185, 0.0012, 0.0028), bevel=0.0004)

    # Trigger guard, trigger, safety button.
    guard = [(0, -0.024, -0.034), (0, -0.040, -0.037), (0, -0.056, -0.020), (0, -0.0585, 0.002),
             (0, -0.056, 0.022), (0, -0.040, 0.034), (0, -0.0245, 0.033)]
    path = W.bezier(guard[0], guard[1], guard[2], guard[3], 6) + W.bezier(guard[3], guard[4], guard[5], guard[6], 6)[1:]
    body.sweep(M, path, 0.0042, 0.0026, bevel=0.0006)
    body.sweep(M, W.bezier((0, -0.024, -0.004), (0, -0.034, -0.006), (0, -0.042, -0.012), (0, -0.050, -0.010), 6),
               0.0034, 0.0030, bevel=0.0004)
    body.cyl(A, (0, -0.0305, 0.0285), 0.0036, 0.040, axis="s", n=12, bevel=0.0004)
    body.cyl(M, (0, -0.026, -0.044), 0.0031, 0.034, axis="s", n=10, bevel=0.0003)

    # Barrel extension, barrel, vent rib and bead.
    prof = [(0.06, 0.0155), (0.121, 0.0155), (0.122, 0.0172), (0.145, 0.0172), (0.147, 0.0152), (0.22, 0.0147),
            (0.40, 0.0143), (0.50, 0.0141), (0.545, 0.0139), (0.548, 0.0147), (0.5575, 0.0147), (0.5585, 0.0142),
            (0.563, 0.0142), (0.5635, 0.0146), (0.570, 0.0146), (0.570, 0.0112), (0.535, 0.0112), (0.535, 0.0109)]
    body.revolve(M, prof, (0, BORE_U), n=32, bevel=0.0)
    # Choke tube grooves.
    for f in (0.5485, 0.5505, 0.5525):
        body.revolve(M, [(f, 0.0150), (f + 0.0009, 0.0150)], (0, BORE_U), n=32)
    body.box(M, (0, 0.0648, 0.347), (0.0075, 0.0014, 0.40), bevel=0.0004)
    for i in range(14):
        body.box(M, (0, 0.0615, 0.17 + i * 0.0285), (0.0026, 0.0066, 0.0065), bevel=0.0003)
    body.cyl(A, (0, 0.0678, 0.553), 0.0023, 0.0036, axis="u", n=10)
    body.sphere(A, (0, 0.0690, 0.553), 0.0026, n=8)
    body.sphere(M, (0, 0.0663, 0.34), 0.0016, n=6)

    # Magazine tube, end cap and sling swivel, barrel hanger.
    body.revolve(M, [(0.074, 0.0122), (0.50, 0.0122), (0.50, 0.0134), (0.535, 0.0134), (0.535, 0.0122), (0.545, 0.0122)],
                 (0, TUBE_U), n=28)
    for k in range(7):
        f = 0.503 + k * 0.0045
        body.revolve(M, [(f, 0.0140), (f + 0.002, 0.0140)], (0, TUBE_U), n=28)
    body.box(M, (0, (TUBE_U + BORE_U) * 0.5 - 0.0002, 0.49), (0.0145, 0.0305, 0.016), bevel=0.0012, seg=2)
    body.torus(M, (0, TUBE_U - 0.0213, 0.5), 0.0058, 0.0012, axis="s", n=18, m=6)
    body.box(M, (0, TUBE_U - 0.0156, 0.5), (0.0042, 0.0045, 0.01), bevel=0.0006)
    # Barrel clamp ring where the tube meets the receiver.
    body.box(M, (0, (TUBE_U + BORE_U) * 0.5, 0.098), (0.0125, 0.0295, 0.018), bevel=0.0010, seg=2)

    build_stock(body)
    build_grip(body)
    build_side_saddle(body)
    return body


def stock_top(f):
    pts = [(-0.10, 0.0575), (-0.16, 0.0545), (-0.215, 0.0520), (-0.27, 0.0545), (-0.322, 0.0640)]
    return _interp(pts, f)


def stock_bottom(f):
    pts = [(-0.10, 0.004), (-0.16, -0.014), (-0.215, -0.034), (-0.27, -0.056), (-0.322, -0.083)]
    return _interp(pts, f)


def stock_hw(f):
    pts = [(-0.10, 0.0165), (-0.13, 0.0188), (-0.30, 0.0188), (-0.322, 0.0215)]
    return _interp(pts, f)


def _interp(pts, f):
    if f <= pts[0][0]:
        return pts[0][1]
    for (f0, v0), (f1, v1) in zip(pts, pts[1:]):
        if f <= f1:
            return lerp(v0, v1, (f - f0) / (f1 - f0))
    return pts[-1][1]


def build_stock(body):
    fs = [-0.098 - i * 0.0124 for i in range(19)]
    fs[-1] = -0.322
    rings = []
    for f in fs:
        top, bot, hw = stock_top(f), stock_bottom(f), stock_hw(f)
        rings.append(W.rect_ring(0.0, (top + bot) * 0.5, f, hw, (top - bot) * 0.5, r=0.009, rseg=3))
    body.loft(P, rings, bevel=0.0)
    # Stippled cheek panels with raised borders on both sides, plus accent pinstripes along the comb.
    def panel(a, b):
        return stock_bottom(a) + 0.014 < b < stock_top(a) - 0.014

    for sx in (-1, 1):
        body.bumps(P, Region((-0.292, -0.152, -0.06, 0.05), panel), ("s", sx * 0.0188, sx),
                   pitch=0.0034, size=0.0026, height=0.0009)
        for f0, f1 in ((-0.296, -0.296), (-0.148, -0.148)):
            u0, u1 = stock_bottom(f0) + 0.012, stock_top(f0) - 0.012
            body.box(P, (sx * 0.0190, (u0 + u1) * 0.5, f0), (0.0018, u1 - u0, 0.0026), bevel=0.0006)
        body.box(A, (sx * 0.0190, 0.052, -0.222), (0.0014, 0.0016, 0.145), bevel=0.0004)
    # Butt pad (accent) with spacer, ribs and stock bolt cap.
    body.box(A, (0, (0.0615 - 0.0865) * 0.5, -0.3245), (0.0452, 0.150, 0.005), bevel=0.0012, seg=2, r=0.006)
    body.box(P, (0, (0.0615 - 0.0865) * 0.5 - 0.0, -0.3295), (0.0472, 0.1530, 0.0145), bevel=0.0018, seg=2, r=0.006)
    for i in range(9):
        body.box(A, (0, 0.050 - i * 0.0135, -0.3375), (0.0405, 0.0034, 0.0022), bevel=0.0006)
    body.cyl(M, (0, -0.0095, -0.3380), 0.0058, 0.0016, axis="f", n=16, bevel=0.0004)
    body.cyl(M, (0, -0.0095, -0.3392), 0.0021, 0.0016, axis="f", n=10)
    # Sling stud and a heel/toe line.
    body.cyl(M, (0, stock_bottom(-0.27) - 0.003, -0.27), 0.0033, 0.008, axis="u", n=10, bevel=0.0004)
    body.cyl(M, (0, stock_bottom(-0.27) - 0.0078, -0.27), 0.0050, 0.0022, axis="u", n=12)
    # Recoil pad bolt holes and cheek detail.
    for sx in (-1, 1):
        body.cyl(M, (sx * 0.0155, 0.030, -0.3378), 0.0030, 0.0012, axis="f", n=10)
        body.cyl(M, (sx * 0.0155, -0.052, -0.3378), 0.0030, 0.0012, axis="f", n=10)


def build_grip(body):
    xf = frame((0, -0.015, -0.045), pitch=16.0)
    poly = [(0.026, 0.0, 0.006), (-0.030, 0.0, 0.006), (-0.034, -0.060, 0.004), (-0.036, -0.112, 0.010),
            (0.021, -0.118, 0.012), (0.024, -0.085, 0.004)]
    hw = 0.0175
    body.extrude(P, poly, -hw, hw, xf=xf, bevel=0.0042, seg=3)
    # Grip cap (accent) and finger rest ridge.
    body.extrude(A, [(0.0232, -0.1175, 0.004), (-0.0372, -0.1135, 0.004), (-0.0365, -0.1225, 0.004), (0.0222, -0.1265, 0.004)],
                 -0.0185, 0.0185, xf=xf, bevel=0.0012, seg=2)
    # Stipple on both sides of the grip (diamond pyramids).
    def inside(a, b):
        # a = local f, b = local u
        return -0.027 < a < 0.019 and -0.108 < b < -0.010

    region = Region((-0.028, 0.02, -0.109, -0.01), inside)
    for sx in (-1, 1):
        body.bumps(P, region, ("s", sx * hw, sx), pitch=0.0034, size=0.0026, height=0.0009, xf=xf)


def build_side_saddle(body):
    # Polymer carrier plate on the left of the receiver with six shells (hull accent, brass head metal).
    body.extrude(P, [(-0.100, 0.052, 0.004), (0.004, 0.052, 0.004), (0.004, -0.004, 0.004), (-0.100, -0.004, 0.004)],
                 -0.0215, -0.0165, bevel=0.0008, seg=1)
    for row_u in (0.037, 0.0115):
        for col_f in (-0.077, -0.0475, -0.018):
            x = -0.0215
            xf = frame((x, row_u, col_f - 0.0), yaw=-90.0)
            body.revolve(M, [(-0.0006, 0.0102), (0.0100, 0.0102)], (0, 0), n=14, xf=xf)
            body.revolve(A, [(0.0100, 0.0096), (0.0345, 0.0096), (0.0355, 0.0086), (0.0360, 0.0056)], (0, 0), n=14, xf=xf)
        # Retaining strap on the plate
    for f in (-0.092, -0.0625, -0.033, -0.004):
        body.box(A, (-0.0222, 0.024, f), (0.0014, 0.050, 0.0034), bevel=0.0004)


def build_pump():
    pump = Part("Pump", pivot=(0, TUBE_U, 0.25))
    u0 = 0.0065
    sections = [(0.155, 0.86), (0.1585, 0.95), (0.166, 1.0), (0.196, 1.0)]
    f = 0.196
    for k in range(13):
        f0 = 0.196 + 0.0085 * k
        sections += [(f0, 1.0), (f0 + 0.0013, 1.045), (f0 + 0.0040, 1.045), (f0 + 0.0053, 1.0)]
    sections += [(0.325, 1.0), (0.337, 1.0), (0.3425, 0.955), (0.345, 0.86)]
    rings = []
    for f, sc in sections:
        rings.append(W.superellipse_ring(0, u0, f, 0.0245 * sc, 0.0235 * sc, n=30, power=0.55))
    pump.loft(P, rings)
    # Trim collars (accent) at each end, and a finger lip.
    for f, w in ((0.1565, 0.004), (0.3415, 0.004)):
        pump.loft(A, [W.superellipse_ring(0, u0, f - w / 2, 0.0236, 0.0226, n=30, power=0.55),
                      W.superellipse_ring(0, u0, f + w / 2, 0.0236, 0.0226, n=30, power=0.55)])
    # Action bars run back into the receiver.
    for sx in (-1, 1):
        pump.cyl(M, (sx * 0.0150, 0.004, 0.115), 0.0026, 0.092, axis="f", n=10, bevel=0.0003)
        pump.box(M, (sx * 0.0150, 0.004, 0.163), (0.007, 0.012, 0.014), bevel=0.0006)
    pump.box(M, (0, 0.004, 0.072), (0.030, 0.012, 0.006), bevel=0.0006)
    return pump


def build():
    W.reset_scene()
    mats = W.make_materials(
        "Shotgun",
        ((0.075, 0.08, 0.09), 0.9, 0.34),
        ((0.045, 0.047, 0.05), 0.0, 0.55, 0.25),
        ((0.62, 0.12, 0.07), 0.15, 0.4))
    body = build_body()
    pump = build_pump()
    return W.build_objects([body, pump], mats)


def render(objs):
    if not RENDER_DIR:
        return []
    paths = W.render_views("shotgun", RENDER_DIR, [
        ("side", (-2.0, 0.02, 0.12), (0, 0.0, 0.12), 62),
        ("right-eject", (2.0, 0.02, 0.12), (0, 0.0, 0.12), 62),
        ("threequarter-front", (1.35, 0.55, 1.15), (0, 0.0, 0.12), 55),
        ("threequarter-rear", (-1.2, 0.55, -1.2), (0, 0.0, 0.12), 55),
        ("top", (0.0, 2.0, 0.121), (0, 0.0, 0.12), 62),
        ("detail-receiver", (0.9, 0.35, -0.25), (0, 0.02, 0.0), 60),
        ("detail-forend", (0.7, 0.2, 0.6), (0, 0.0, 0.26), 60),
        ("detail-muzzle", (0.35, 0.25, 0.9), (0, 0.04, 0.5), 60),
        ("detail-grip", (-0.7, 0.1, -0.55), (0, -0.03, -0.15), 60),
    ], samples=SAMPLES, floor_z=-0.14, scale=1.4, res=RES)
    return paths


def main():
    objs = build()
    W.stats("SHOTGUN", objs)
    W.export(objs, OUT_DIR, "Shotgun")
    if RENDER_DIR:
        paths = render(objs)
        W.check_renders(paths)
        W.reset_scene()
        # Rebuild for the held mock so the render scene is clean.
        objs = build()
        paths = W.render_held("shotgun", RENDER_DIR, objs, samples=SAMPLES, res=RES)
        W.check_renders(paths)


main()
