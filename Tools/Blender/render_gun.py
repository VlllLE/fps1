"""Studio renders of a built gun. Imported by the build scripts (needs the objects in the scene)."""

import math
import os

import bpy
from mathutils import Vector

import gunlib as gl
from gunlib import U


def render_gun(prefix, out_dir, samples, res_pct, objects, details=None, width=1500, height=800,
               side_focal=60):
    os.makedirs(out_dir, exist_ok=True)
    lo = Vector((9, 9, 9))
    hi = Vector((-9, -9, -9))
    for o in objects:
        l, h = gl.unity_bounds(o)
        lo = Vector((min(lo.x, l.x), min(lo.y, l.y), min(lo.z, l.z)))
        hi = Vector((max(hi.x, h.x), max(hi.y, h.y), max(hi.z, h.z)))
    centre = (lo + hi) / 2.0
    size = hi - lo
    area = gl.setup_render(samples, res_pct, width, height, floor_z=lo.y - 0.004)
    # lights are placed around the gun in Blender space
    cb = U(centre)
    gl.standard_lights(area, (cb.x, cb.y, cb.z))
    cam, cam_data = gl.add_camera()

    def shoot(name, cam_unity, target_unity, focal):
        cam_data.lens = focal
        cam.location = U(cam_unity)
        gl.look_at(cam, U(target_unity))
        gl.render_to(os.path.join(out_dir, "%s-%s.png" % (prefix, name)))

    L = max(size.z, 0.6)
    views = [
        ("side", (centre.x - L * 2.0, centre.y, centre.z), centre, side_focal),
        ("threequarter", (centre.x - L * 1.15, centre.y + L * 0.50, centre.z + L * 1.30), centre, 55),
        ("threequarter-rear", (centre.x + L * 0.85, centre.y + L * 0.42, centre.z - L * 1.30), centre, 55),
        ("top", (centre.x, centre.y + L * 2.0, centre.z + 0.001), centre, side_focal),
        ("front", (centre.x - L * 0.12, centre.y + 0.03, centre.z + L * 1.25), centre, 55),
    ]
    for v in views:
        shoot(*v)
    for d in details or []:
        name, target, offset, focal = d
        t = Vector(target)
        shoot(name, t + Vector(offset), t, focal)
