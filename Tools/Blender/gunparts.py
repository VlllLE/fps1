"""Reusable gun sub-assemblies: Picatinny rails, scopes with rings, knurling, screws.

All coordinates are Unity weapon space (see gunlib.py).
"""

import math

from mathutils import Vector

from gunlib import ACCENT, GLASS, METAL, POLYMER


def knurl(n, depth, power=0.7):
    """Radius multiplier giving n sharp ridges around the circumference."""
    def f(th):
        c = max(0.0, math.cos(n * th))
        return 1.0 - depth * (1.0 - c ** power)
    return f


def frame(origin, ex, ey, ez):
    o = Vector(origin)
    ex, ey, ez = Vector(ex).normalized(), Vector(ey).normalized(), Vector(ez).normalized()

    def to_world(x, y, z):
        return o + ex * x + ey * y + ez * z
    return to_world


def picatinny(b, origin, ex, ey, ez, length, width=0.0215, base_h=0.006, tooth_h=0.0065,
              pitch=0.0101, slot=0.0052, mat=METAL, base_overlap=0.003, end_chamfer=0.002):
    """Rail on the +ey side of `origin`: local x across, y out, z along `length`.

    A solid base plate runs the whole length and a row of chamfered Picatinny teeth sits on it with
    real cross slots between them.
    """
    w = frame(origin, ex, ey, ez)
    hw = width / 2.0
    # base plate (extends below origin by base_overlap so it buries into the receiver)
    base = [(-hw * 0.96, -base_overlap), (hw * 0.96, -base_overlap), (hw * 0.96, base_h), (-hw * 0.96, base_h)]
    b.loft([[w(x, y, 0.0) for (x, y) in base], [w(x, y, length) for (x, y) in base]], mat)
    # teeth with 45-degree chamfers
    ch = 0.0032
    profile = [(-hw, base_h - 0.0004), (hw, base_h - 0.0004), (hw, base_h + tooth_h * 0.55),
               (hw - ch, base_h + tooth_h), (-hw + ch, base_h + tooth_h), (-hw, base_h + tooth_h * 0.55)]
    tooth_len = pitch - slot
    count = int((length - slot) / pitch)
    for i in range(count):
        z0 = slot * 0.5 + i * pitch
        b.loft([[w(x, y, z0) for (x, y) in profile], [w(x, y, z0 + tooth_len) for (x, y) in profile]], mat)
    return base_h + tooth_h


def hex_screw(b, p, direction, r=0.0032, h=0.0035, mat=METAL):
    p = Vector(p)
    d = Vector(direction).normalized()
    b.cyl(p, p + d * h, r, r, seg=6, mat=mat, up=None)


def socket_screw(b, p, direction, r=0.0030, h=0.0030, mat=METAL):
    """Round socket-head screw with a recessed hex."""
    p = Vector(p)
    d = Vector(direction).normalized()
    b.revolve(p, d, [(0.0, 0.0), (0.0, r * 0.55), (-h * 0.35, r * 0.55), (-h * 0.35, 0.0)],
              seg=6, mat=mat)
    b.revolve(p, d, [(0.0, r * 0.55), (0.0, r * 0.95), (h * 0.8, r), (h, r * 0.8), (h, 0.0)], seg=14, mat=mat)


# ---------------------------------------------------------------------------------------------
# Scope
# ---------------------------------------------------------------------------------------------
BOLT_SCOPE = dict(L=0.38, tube_r=0.0152, eye_r=0.0225, obj_r=0.0275, e_cone=(0.028, 0.080),
                  e_house=(0.120, 0.170), e_obj=(0.236, 0.288), rings=(0.094, 0.205), turret_e=0.145,
                  ring_w=0.020, ring_r=0.0215, turret_h=0.027, side_turrets=True, focus_knob=True)
DMR_SCOPE = dict(L=0.300, tube_r=0.0152, eye_r=0.0215, obj_r=0.0245, e_cone=(0.024, 0.070),
                 e_house=(0.098, 0.140), e_obj=(0.190, 0.236), rings=(0.084, 0.170), turret_e=0.119,
                 ring_w=0.020, ring_r=0.0215, turret_h=0.022, side_turrets=True, focus_knob=False)


def build_scope(asm, axis_y, z_rear, rail_top, group="Scope", params=None, rail_half_width=0.0105,
                ring_names=("ScopeRingRear", "ScopeRingFront"), seg=40):
    """Adds the scope parts to the assembly. Returns a dict of useful points."""
    P = dict(BOLT_SCOPE)
    if params:
        P.update(params)
    L = P["L"]
    tr = P["tube_r"]
    ax = Vector((0, 0, 1))
    origin = Vector((0.0, axis_y, z_rear))

    # ---- main body: one closed revolve from the ocular lens plate to the objective lens plate ----
    eye_depth = 0.014
    obj_depth = 0.030
    e0, e1 = P["e_cone"]
    h0, h1 = P["e_house"]
    o0, o1 = P["e_obj"]
    prof = []
    mats = []

    def add(t, r, m=METAL):
        prof.append((t, r))
        mats.append(m)

    add(eye_depth, 0.0)
    add(eye_depth, 0.0185, METAL)           # back plate of the ocular
    add(0.0, 0.0185, METAL)                 # inner wall
    add(0.0, P["eye_r"] - 0.0005, POLYMER)  # eye cup lip
    add(0.004, P["eye_r"], POLYMER)
    add(e0 - 0.006, P["eye_r"], POLYMER)    # rubber eye cup
    add(e0 - 0.003, P["eye_r"] - 0.0020, POLYMER)
    ring_idx_start = len(prof)
    add(e0, P["eye_r"] - 0.0035, METAL)
    add(e0 + 0.020, P["eye_r"] - 0.0035, METAL)  # magnification ring (knurled below)
    ring_idx_end = len(prof) - 1
    add(e0 + 0.022, P["eye_r"] - 0.0060, ACCENT)
    add(e0 + 0.026, P["eye_r"] - 0.0060, ACCENT)
    add(e0 + 0.028, P["eye_r"] - 0.0050, METAL)
    add(e1 - 0.014, tr + 0.0030, METAL)
    add(e1, tr, METAL)
    add(h0 - 0.004, tr, METAL)
    add(h0, tr + 0.0030, METAL)
    add(h0 + 0.004, tr + 0.0053, METAL)
    add(h1 - 0.004, tr + 0.0053, METAL)
    add(h1, tr + 0.0030, METAL)
    add(h1 + 0.004, tr, METAL)
    add(o0, tr, METAL)
    add(o0 + 0.016, tr + 0.0050, METAL)
    add(o1 - 0.010, P["obj_r"] - 0.0020, METAL)
    add(o1, P["obj_r"], METAL)
    add(L - 0.008, P["obj_r"], METAL)
    add(L - 0.002, P["obj_r"] + 0.0008, ACCENT)   # anti-glare lip ring
    add(L, P["obj_r"] - 0.0007, ACCENT)
    add(L, P["obj_r"] - 0.0020, METAL)
    add(L - obj_depth, P["obj_r"] - 0.0020, METAL)
    add(L - obj_depth, 0.0, METAL)
    mats = mats[1:]  # segment i uses the material of its end point

    def radmod(i, t, th):
        if ring_idx_start <= i <= ring_idx_end:
            return knurl(48, 0.035)(th)
        return 1.0

    body = asm.part("ScopeBody", group)
    body.revolve(origin, ax, prof, seg=seg, mats=mats, radmod=radmod)

    # ---- lenses (glass) ----
    def lens(t_center, rr, depth_dir, bulge):
        pts = [(t_center - depth_dir * bulge, 0.0)]
        n = 5
        for k in range(1, n + 1):
            f = k / n
            pts.append((t_center - depth_dir * bulge * (1 - f * f), rr * f))
        pts.append((t_center + depth_dir * 0.004, rr))
        pts.append((t_center + depth_dir * 0.004, 0.0))
        return pts
    lf = asm.part("ScopeLensFront", group)
    lf.revolve(origin, ax, lens(L - obj_depth + 0.0005, P["obj_r"] - 0.0024, -1, 0.0045), seg=seg, mat=GLASS)
    lr = asm.part("ScopeLensRear", group)
    lr.revolve(origin, ax, lens(0.0095, 0.0183, 1, 0.0035), seg=28, mat=GLASS)

    # ---- turrets ----
    te = P["turret_e"]
    house_r = tr + 0.0053
    tz = z_rear + te
    elev = asm.part("ScopeTurretElevation", group)
    th = P["turret_h"]
    ey = Vector((0, 1, 0))
    elev_origin = Vector((0.0, axis_y, tz))
    prof_e = [(0.0, 0.0), (0.0, 0.0140), (house_r + 0.004, 0.0140), (house_r + 0.004, 0.0120),
              (house_r + 0.0085, 0.0120), (house_r + 0.0085, 0.0145)]
    # knurled cap
    cap0 = house_r + 0.0085
    prof_e += [(cap0 + 0.0010, 0.0148), (cap0 + th - 0.003, 0.0148), (cap0 + th - 0.001, 0.0138),
               (cap0 + th, 0.0125), (cap0 + th, 0.0)]
    cap_first = 5
    cap_last = len(prof_e) - 4
    elev.revolve(elev_origin, ey, prof_e, seg=56, mat=METAL,
                 radmod=lambda i, t, thta: knurl(40, 0.045)(thta) if cap_first < i <= cap_last else 1.0,
                 up=(1, 0, 0))
    # numbered band cap lines (accent ring)
    elev.revolve(elev_origin, ey, [(cap0 + 0.0045, 0.0149), (cap0 + 0.0075, 0.0152),
                                   (cap0 + 0.0075, 0.0144), (cap0 + 0.0045, 0.0142)],
                 seg=56, mat=ACCENT, closed_profile=True, up=(1, 0, 0))
    if P["side_turrets"]:
        ex = Vector((1, 0, 0))
        wind = asm.part("ScopeTurretWindage", group)
        w_origin = Vector((0.0, axis_y, tz))
        wr0 = house_r - 0.002
        wprof = [(0.0, 0.0), (0.0, 0.0105), (wr0 + 0.006, 0.0105), (wr0 + 0.006, 0.0090),
                 (wr0 + 0.010, 0.0090), (wr0 + 0.010, 0.0113), (wr0 + 0.0145, 0.0113),
                 (wr0 + 0.0145, 0.0095), (wr0 + 0.0158, 0.0090), (wr0 + 0.0158, 0.0)]
        wind.revolve(w_origin, ex, wprof, seg=44, mat=METAL,
                     radmod=lambda i, t, thta: knurl(32, 0.05)(thta) if 5 <= i <= 6 else 1.0, up=(0, 1, 0))
        if P["focus_knob"]:
            foc = asm.part("ScopeTurretFocus", group)
            fprof = [(0.0, 0.0), (0.0, 0.0110), (house_r + 0.003, 0.0110), (house_r + 0.003, 0.0150),
                     (house_r + 0.0155, 0.0150), (house_r + 0.0165, 0.0135), (house_r + 0.0165, 0.0)]
            foc.revolve(w_origin, Vector((-1, 0, 0)), fprof, seg=56, mat=METAL,
                        radmod=lambda i, t, thta: knurl(36, 0.05)(thta) if 3 <= i <= 4 else 1.0, up=(0, 1, 0))
            foc.revolve(w_origin, Vector((-1, 0, 0)), [(house_r + 0.0165, 0.0), (house_r + 0.0165, 0.0095),
                                                      (house_r + 0.0178, 0.0095), (house_r + 0.0178, 0.0)],
                        seg=24, mat=ACCENT, up=(0, 1, 0))
        else:
            # illumination / battery cap on the left
            cap = asm.part("ScopeTurretLeftCap", group)
            lprof = [(0.0, 0.0), (0.0, 0.0090), (house_r + 0.0045, 0.0090), (house_r + 0.0045, 0.0100),
                     (house_r + 0.0080, 0.0100), (house_r + 0.0090, 0.0085), (house_r + 0.0090, 0.0)]
            cap.revolve(w_origin, Vector((-1, 0, 0)), lprof, seg=32, mat=METAL, up=(0, 1, 0))

    # ---- rings on the rail ----
    ring_w = P["ring_w"]
    r_out = P["ring_r"]
    foot_top = axis_y - r_out + 0.003
    jaw_bottom = rail_top - 0.0075
    for name, e in zip(ring_names, P["rings"]):
        rz = z_rear + e
        r = asm.part(name, group)
        rprof = [(-ring_w / 2, tr - 0.0001), (-ring_w / 2, r_out - 0.0015), (-ring_w / 2 + 0.002, r_out),
                 (ring_w / 2 - 0.002, r_out), (ring_w / 2, r_out - 0.0015), (ring_w / 2, tr - 0.0001)]
        r.revolve(Vector((0, axis_y, rz)), ax, rprof, seg=48, mat=METAL, closed_profile=True)
        # clamp ears with socket screws
        for side in (-1, 1):
            ear_cx = side * (r_out + 0.0035)
            r.box((ear_cx, axis_y, rz), (0.012, 0.0100, ring_w), METAL, bevel=0.0012, seg=1)
            for dz in (-0.0055, 0.0055):
                socket_screw(r, (ear_cx, axis_y + 0.0050, rz + dz), (0, 1, 0), r=0.0028, h=0.0030)
        # foot: saddle from ring bottom down to the rail, with clamp jaws around the dovetail
        foot_len = 0.026
        r.box((0, (foot_top + jaw_bottom + 0.004) / 2, rz),
              (0.0345, foot_top - (jaw_bottom + 0.004), foot_len), METAL, bevel=0.0016, seg=1)
        jaw_h = rail_top - jaw_bottom
        for side in (-1, 1):
            r.box((side * (rail_half_width + 0.0030), jaw_bottom + jaw_h / 2 + 0.0005, rz),
                  (0.0042, jaw_h + 0.001, foot_len), METAL, bevel=0.0008, seg=1)
        # cross bolt with a thumb nut on the right
        by = rail_top - 0.0042
        r.cyl((-0.0200, by, rz), (0.0200, by, rz), 0.0020, 0.0020, seg=10, mat=METAL)
        r.revolve(Vector((0.0198, by, rz)), Vector((1, 0, 0)),
                  [(0.0, 0.0), (0.0, 0.0050), (0.0042, 0.0058), (0.0070, 0.0058), (0.0070, 0.0)],
                  seg=18, mat=METAL, up=(0, 1, 0))
        r.revolve(Vector((-0.0198, by, rz)), Vector((-1, 0, 0)),
                  [(0.0, 0.0), (0.0, 0.0044), (0.0030, 0.0044), (0.0030, 0.0)], seg=6, mat=METAL, up=(0, 1, 0))
    return dict(L=L, turret_z=tz, ring_z=[z_rear + e for e in P["rings"]], front_z=z_rear + L,
                axis_y=axis_y, z_rear=z_rear)
