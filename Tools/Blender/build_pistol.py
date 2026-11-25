"""Builds the detailed sidearm pistol and exports it for Unity.

Run headless (Blender 4.2):

    blender -b --factory-startup --python Tools/Blender/build_pistol.py -- \
        --out Assets/Weapons/Models/Pistol --renders <dir> [--samples 48] [--res 100]

Coordinates are Unity weapon space (s right, u up, f forward, metres); see weapon_lib.py.
Mesh objects exported as FBX siblings:
  PistolBody  polymer frame with rail, trigger guard, trigger, stippled grip, controls, fixed barrel
              with a ported compensator
  Slide       steel slide: serrations, ejection port, top slots, rear and front sights
              (slid back by WeaponPartAnimator when the weapon fires)
  Magazine    magazine stub and floor plate that WeaponController drops during the reload
The bore is on u = 0 and the muzzle at (0, 0, 0.30), the MuzzlePoint of the TestPistol prefab.
The grip is centred on the right-hand IK target (0, -0.09, -0.05).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import weapon_lib as W  # noqa: E402
from weapon_lib import MAT_ACCENT, MAT_METAL, MAT_POLYMER, Part, Region, frame  # noqa: E402

arg = W.parse_args()
OUT_DIR = arg("--out", os.path.abspath("pistol_out"))
RENDER_DIR = arg("--renders", None)
SAMPLES = int(arg("--samples", "48"))
RES = int(arg("--res", "100"))

M, P, A = MAT_METAL, MAT_POLYMER, MAT_ACCENT
SLIDE_PIVOT = (0.0, 0.010, 0.0)
MAG_PIVOT = (0.0, -0.152, -0.100)
GRIP_PITCH = 14.0
GRIP_ORIGIN = (0.0, -0.016, -0.028)
SLIDE_BACK, SLIDE_FRONT = -0.115, 0.275
SLIDE_BOTTOM, SLIDE_TOP = -0.022, 0.040
SLIDE_HW = 0.0170


def build_slide():
    sl = Part("Slide", pivot=SLIDE_PIVOT)
    mid = 0.5 * (SLIDE_BOTTOM + SLIDE_TOP)
    hh = 0.5 * (SLIDE_TOP - SLIDE_BOTTOM)
    secs = [(SLIDE_BACK, 0.93), (SLIDE_BACK + 0.005, 1.0), (SLIDE_FRONT - 0.012, 1.0), (SLIDE_FRONT - 0.004, 0.97),
            (SLIDE_FRONT, 0.90)]
    rings = [W.rect_ring(0, mid, f, SLIDE_HW * sc, hh * sc, r=0.0075, rseg=3) for f, sc in secs]
    body = Part("slide_solid")
    body.loft(M, rings, bevel=0.0006, seg=1)

    cutters = []

    def cut_box(c, size, bevel=0.0004):
        p = Part("c")
        p.box(M, c, size, bevel=bevel)
        cutters.append(p)

    # Serrations: rear block and a shorter front block on both sides, plus top rear cocking grooves.
    for sx in (-1, 1):
        for k in range(9):
            cut_box((sx * SLIDE_HW, 0.011, -0.104 + k * 0.0072), (0.0050, 0.040, 0.0029), bevel=0.0003)
        for k in range(5):
            cut_box((sx * SLIDE_HW, 0.011, 0.186 + k * 0.0072), (0.0050, 0.040, 0.0029), bevel=0.0003)
    for k in range(7):
        cut_box((0, SLIDE_TOP, -0.100 + k * 0.0070), (0.0165, 0.0030, 0.0026), bevel=0.0003)

    # Ejection port (right side, opens over the barrel hood) and lightening slots on top.
    cut_box((0.0135, 0.026, 0.040), (0.0120, 0.026, 0.082), bevel=0.0012)
    for k in range(4):
        cut_box((0, SLIDE_TOP, 0.100 + k * 0.021), (0.0110, 0.0070, 0.0130), bevel=0.0010)
    # Muzzle opening around the barrel.
    muzzle = Part("c")
    muzzle.cyl(M, (0, 0, SLIDE_FRONT - 0.004), 0.0118, 0.018, axis="f", n=24)
    cutters.append(muzzle)
    body.cut(cutters)
    sl.absorb(body)

    # Chambered round and barrel hood visible through the ejection port.
    sl.box(M, (0.0070, 0.019, 0.040), (0.0050, 0.018, 0.070), bevel=0.0006)
    sl.cyl(A, (0.0078, 0.022, 0.060), 0.0042, 0.026, axis="f", n=14, bevel=0.0003)
    sl.cyl(A, (0.0078, 0.022, 0.0735), 0.0028, 0.008, axis="f", n=14, r1=0.0016)

    # Rear sight (dovetailed block with a notch and two dots) and front blade with a dot.
    rear = Part("rear")
    rear.box(M, (0, SLIDE_TOP + 0.0045, -0.098), (0.0165, 0.0095, 0.0125), bevel=0.0007)
    notch = Part("n")
    notch.box(M, (0, SLIDE_TOP + 0.0060, -0.098), (0.0042, 0.0070, 0.0300), bevel=0.0002)
    rear.cut([notch])
    sl.absorb(rear)
    for sx in (-1, 1):
        sl.cyl(A, (sx * 0.0058, SLIDE_TOP + 0.0045, -0.1035), 0.0019, 0.0016, axis="f", n=12)
    sl.box(M, (0, SLIDE_TOP + 0.0050, 0.254), (0.0085, 0.0100, 0.0160), bevel=0.0008, taper=(0.5, 1.0))
    sl.cyl(A, (0, SLIDE_TOP + 0.0060, 0.2645), 0.0020, 0.0015, axis="f", n=12)

    # Rear face plate and firing pin retainer.
    sl.box(M, (0, 0.012, SLIDE_BACK - 0.0012), (0.026, 0.040, 0.0030), bevel=0.0006)
    sl.cyl(M, (0, 0.016, SLIDE_BACK - 0.0030), 0.0036, 0.0030, axis="f", n=14)
    # Extractor on the ejection port edge.
    sl.box(M, (0.0160, 0.0105, 0.0525), (0.0030, 0.0120, 0.0110), bevel=0.0004)
    return sl


def build_grip(fr):
    xf = frame(GRIP_ORIGIN, pitch=GRIP_PITCH)
    hw = 0.0170
    poly = [(0.040, 0.0, 0.004), (-0.054, 0.0, 0.006), (-0.056, -0.050, 0.004), (-0.050, -0.138, 0.007),
            (0.032, -0.138, 0.007), (0.034, -0.060, 0.012)]
    fr.extrude(P, poly, -hw, hw, xf=xf, bevel=0.0035, seg=2)

    # Finger grooves down the front strap and a flared magwell lip.
    for k in range(3):
        fr.box(P, (0, -0.062 - k * 0.020, 0.0345), (0.0345, 0.0085, 0.0030), xf=xf, bevel=0.0009)
    fr.box(P, (0, -0.1355, -0.010), (0.0375, 0.0100, 0.092), xf=xf, bevel=0.0018, seg=2)

    def inside_side(a, b):
        return -0.044 < a < 0.026 and -0.126 < b < -0.030

    for sx in (-1, 1):
        fr.bumps(P, Region((-0.046, 0.028, -0.128, -0.030), inside_side), ("s", sx * hw, sx), pitch=0.0030, size=0.0022,
                 height=0.0008, xf=xf)

    def inside_back(a, b):
        return -0.014 < a < 0.014 and -0.130 < b < -0.034

    fr.bumps(P, Region((-0.014, 0.014, -0.130, -0.034), inside_back), ("f", -0.0545, -1), pitch=0.0030, size=0.0022,
             height=0.0008, xf=xf)
    # Lanyard loop at the heel.
    fr.torus(M, (0.0, -0.1315, -0.0545), 0.0050, 0.0014, axis="s", n=14, m=6, xf=xf)


def build_frame():
    fr = Part("PistolBody")
    hw = 0.0165
    # Dust cover + frame rail section.
    prof = [(-0.098, -0.014, 0.006), (0.215, -0.014, 0.001), (0.215, -0.047, 0.002), (0.056, -0.047, 0.002),
            (0.046, -0.062, 0.006), (-0.020, -0.062, 0.006), (-0.098, -0.040, 0.010)]
    fr.extrude(P, prof, -hw, hw, bevel=0.0012, seg=2)
    # Accessory rail slots on the underside of the dust cover.
    for k in range(6):
        fr.box(M, (0, -0.0490, 0.074 + k * 0.0235), (0.0215, 0.0045, 0.0080), bevel=0.0006)
    fr.box(M, (0, -0.0455, 0.105), (0.0255, 0.0030, 0.1130), bevel=0.0005)
    # Beavertail behind the slide.
    fr.box(P, (0, -0.012, -0.108), (0.030, 0.014, 0.020), bevel=0.0018, seg=2, r=0.004)

    # Trigger guard, trigger and trigger bar.
    guard = (W.bezier((0, -0.0460, 0.052), (0, -0.0780, 0.060), (0, -0.0900, 0.020), (0, -0.0700, -0.004), 8)
             + W.bezier((0, -0.0700, -0.004), (0, -0.0640, -0.012), (0, -0.0540, -0.016), (0, -0.0470, -0.018), 6)[1:])
    fr.sweep(P, guard, 0.0125, 0.0040, bevel=0.0007)
    fr.sweep(M, W.bezier((0, -0.016, 0.014), (0, -0.030, 0.016), (0, -0.042, 0.010), (0, -0.054, 0.003), 7),
             0.0045, 0.0030, bevel=0.0005)
    fr.box(A, (0, -0.035, 0.0155), (0.0070, 0.0050, 0.0050), bevel=0.0004)

    # Controls: slide stop, takedown lever, magazine catch.
    for sx in (-1, 1):
        fr.cyl(M, (sx * 0.0170, -0.0215, 0.060), 0.0050, 0.0050, axis="s", n=16)
    fr.box(A, (-0.0188, -0.0170, 0.022), (0.0040, 0.0060, 0.0300), bevel=0.0008)
    fr.cyl(M, (-0.0176, -0.0170, 0.0060), 0.0034, 0.0040, axis="s", n=12)
    fr.cyl(A, (-0.0186, -0.044, -0.0150), 0.0052, 0.0044, axis="s", n=14, bevel=0.0004)

    # Fixed barrel and compensator.
    fr.cyl(M, (0, 0, 0.242), 0.0108, 0.052, axis="f", n=24, bevel=0.0003)
    comp = Part("comp")
    comp.cyl(M, (0, 0, 0.2905), 0.0134, 0.0215, axis="f", n=24, bevel=0.0004)
    comp.cyl(M, (0, 0, 0.2820), 0.0150, 0.0040, axis="f", n=24, bevel=0.0003)
    cutters = []
    bore = Part("c")
    bore.cyl(M, (0, 0, 0.2955), 0.0062, 0.0200, axis="f", n=20)
    cutters.append(bore)
    for k in range(3):
        c = Part("c")
        c.box(M, (0, 0.0128, 0.2870 + k * 0.0050), (0.0090, 0.0100, 0.0026), bevel=0.0002)
        cutters.append(c)
    for sx in (-1, 1):
        for k in range(2):
            c = Part("c")
            c.box(M, (sx * 0.0128, 0.0, 0.2895 + k * 0.0060), (0.0100, 0.0070, 0.0026), bevel=0.0002)
            cutters.append(c)
    comp.cut(cutters)
    fr.absorb(comp)
    fr.cyl(A, (0, 0, 0.2705), 0.0112, 0.0030, axis="f", n=24)
    build_grip(fr)
    return fr


def build_magazine():
    mg = Part("Magazine", pivot=MAG_PIVOT)
    xf = frame(GRIP_ORIGIN, pitch=GRIP_PITCH)
    # Visible follower stub, body inside the well, and the floor plate with finger ridges.
    mg.box(M, (0, -0.136, -0.010), (0.0270, 0.050, 0.0820), xf=xf, bevel=0.0012)
    mg.box(A, (0, -0.1475, -0.010), (0.0360, 0.0120, 0.0900), xf=xf, bevel=0.0018, seg=2, r=0.003)
    for k in range(3):
        mg.box(A, (0, -0.1535, 0.012 - k * 0.022), (0.0300, 0.0020, 0.0060), xf=xf, bevel=0.0005)
    for sx in (-1, 1):
        mg.box(M, (sx * 0.0128, -0.118, -0.010), (0.0022, 0.0240, 0.0040), xf=xf, bevel=0.0004)
    return mg


def build():
    W.reset_scene()
    mats = W.make_materials(
        "Pistol",
        ((0.085, 0.09, 0.10), 0.9, 0.36),
        ((0.035, 0.038, 0.036), 0.0, 0.58, 0.25),
        ((0.78, 0.55, 0.16), 0.85, 0.30))
    return W.build_objects([build_frame(), build_slide(), build_magazine()], mats)


def render(objs):
    return W.render_views("pistol", RENDER_DIR, [
        ("side", (1.0, -0.02, 0.09), (0, -0.05, 0.09), 62),
        ("left", (-1.0, -0.02, 0.09), (0, -0.05, 0.09), 62),
        ("threequarter-front", (0.55, 0.28, 0.75), (0, -0.04, 0.10), 55),
        ("threequarter-rear", (-0.50, 0.25, -0.50), (0, -0.04, 0.08), 55),
        ("top", (0.0, 0.9, 0.09), (0, 0.0, 0.09), 62),
        ("detail-slide", (0.38, 0.12, 0.30), (0, 0.015, 0.06), 60),
        ("detail-grip", (-0.38, -0.12, -0.28), (0, -0.09, -0.06), 60),
        ("detail-muzzle", (0.18, 0.06, 0.55), (0, 0.0, 0.25), 60),
        ("detail-rail-trigger", (0.30, -0.13, 0.28), (0, -0.05, 0.04), 60),
    ], samples=SAMPLES, floor_z=-0.17, scale=0.55, res=RES)


def main():
    objs = build()
    W.stats("Pistol", objs)
    W.export(objs, OUT_DIR, "Pistol")
    if RENDER_DIR:
        W.check_renders(render(objs))
        W.reset_scene()
        objs = build()
        W.check_renders(W.render_held("pistol", RENDER_DIR, objs, samples=SAMPLES, res=RES))


main()
