"""Procedural ghillie-suit cover: hanging strips of camouflage yarn grown over a retargeted, textured soldier.

No free-licensed ghillie character was available, so the Ghillie look is the CC-BY "boonie soldier" body with several
hundred tapered strips scattered over its torso, arms, legs and hat. Every strip copies the bone weights of the body
vertex it grows from, so it follows the skeleton.
"""

import math
import random

import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree

PALETTE = [
    ("Ghillie_Olive", (0.20, 0.24, 0.10)),
    ("Ghillie_Moss", (0.12, 0.17, 0.07)),
    ("Ghillie_Brown", (0.27, 0.19, 0.09)),
    ("Ghillie_Tan", (0.36, 0.30, 0.16)),
]

DENSITY = {
    "Head": 0.55, "Neck": 0.6, "Chest": 1.0, "Spine": 1.0, "Hips": 0.85, "ShoulderL": 1.0, "ShoulderR": 1.0,
    "UpperArmL": 1.2, "UpperArmR": 1.2, "ForearmL": 0.95, "ForearmR": 0.95, "ThighL": 0.9, "ThighR": 0.9,
    "ShinL": 0.55, "ShinR": 0.55, "HandL": 0.0, "HandR": 0.0, "FootL": 0.0, "FootR": 0.0,
}
STRIP_COUNT = 1500
SEGMENTS = 3
FRONT = Vector((0.0, -1.0, 0.0))


def make_material(name, color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    bsdf.inputs["Roughness"].default_value = 0.95
    mat.diffuse_color = (color[0], color[1], color[2], 1.0)
    return mat


def dominant(obj, vertex):
    best, weight = None, 0.0
    names = {g.index: g.name for g in obj.vertex_groups}
    for g in vertex.groups:
        if g.weight > weight:
            best, weight = names[g.group], g.weight
    return best


def vertex_weights(obj, vertex):
    names = {g.index: g.name for g in obj.vertex_groups}
    pairs = sorted(((names[g.group], g.weight) for g in vertex.groups), key=lambda kv: -kv[1])[:4]
    total = sum(w for _, w in pairs) or 1.0
    return {n: w / total for n, w in pairs}


def apply(base_objs, look, count=STRIP_COUNT, seed=7):
    rng = random.Random(seed)
    tree_points = []
    owners = []
    triangles = []
    for obj in base_objs:
        me = obj.data
        for v in me.vertices:
            tree_points.append((v.co.copy(), obj, v))
        me.calc_loop_triangles()
        for tri in me.loop_triangles:
            a, b, c = (me.vertices[i] for i in tri.vertices)
            area = tri.area
            triangles.append((area, obj, (a, b, c), tri.normal.copy()))
    tree = KDTree(len(tree_points))
    for i, (co, _, _) in enumerate(tree_points):
        tree.insert(co, i)
    tree.balance()

    total_area = sum(t[0] for t in triangles)
    cumulative = []
    acc = 0.0
    for t in triangles:
        acc += t[0]
        cumulative.append(acc)

    def pick():
        r = rng.random() * total_area
        lo, hi = 0, len(cumulative) - 1
        while lo < hi:
            mid = (lo + hi) // 2
            if cumulative[mid] < r:
                lo = mid + 1
            else:
                hi = mid
        return triangles[lo]

    verts = []
    faces = []
    face_mats = []
    weights = []
    attempts = 0
    made = 0
    while made < count and attempts < count * 60:
        attempts += 1
        area, obj, (a, b, c), normal = pick()
        u, w = rng.random(), rng.random()
        if u + w > 1.0:
            u, w = 1.0 - u, 1.0 - w
        p = a.co + (b.co - a.co) * u + (c.co - a.co) * w
        _, index, _ = tree.find(p)
        _, owner, src_vertex = tree_points[index]
        region = dominant(owner, src_vertex)
        if region is None or rng.random() > DENSITY.get(region, 0.5):
            continue
        if region == "Head" and (normal.dot(FRONT) > 0.15 or p.z < 1.45):
            continue
        if normal.z < -0.35:
            continue
        n = normal.normalized()
        down = Vector((0.0, 0.0, -1.0))
        length = rng.uniform(0.14, 0.30) * (0.65 if region == "Head" else 1.0)
        length = min(length, max(0.05, p.z - 0.07))
        width = rng.uniform(0.035, 0.065)
        drift = Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), 0.0))
        side = down.cross(n)
        if side.length < 1e-3:
            side = Vector((1.0, 0.0, 0.0))
        side.normalize()
        side = (side + drift * 0.3).normalized()
        base_index = len(verts)
        nodes = []
        for s in range(SEGMENTS + 1):
            t = s / SEGMENTS
            centre = p + n * (0.012 + 0.03 * math.sin(t * math.pi * 0.8)) + down * (length * t) + drift * (0.04 * t * t)
            half = width * 0.5 * (1.0 - 0.65 * t)
            nodes.append((centre - side * half, centre + side * half))
        for left, right in nodes:
            verts.append(tuple(left))
            verts.append(tuple(right))
        mat_index = rng.randrange(len(PALETTE))
        for s in range(SEGMENTS):
            i0 = base_index + s * 2
            quad = (i0, i0 + 1, i0 + 3, i0 + 2)
            faces.append(quad)
            faces.append(tuple(reversed(quad)))
            face_mats.extend([mat_index, mat_index])
        wd = vertex_weights(owner, src_vertex)
        weights.extend([wd] * ((SEGMENTS + 1) * 2))
        made += 1

    me = bpy.data.meshes.new(look + "_Strips")
    me.from_pydata(verts, [], faces)
    me.update()
    materials = [make_material(n, c) for n, c in PALETTE]
    for m in materials:
        me.materials.append(m)
    for poly, mi in zip(me.polygons, face_mats):
        poly.material_index = mi
        poly.use_smooth = False
    obj = bpy.data.objects.new(look + "_Strips", me)
    bpy.context.scene.collection.objects.link(obj)
    from soldier_lib.geometry import BONES
    for b in BONES:
        obj.vertex_groups.new(name=b)
    for vi, wd in enumerate(weights):
        for b, val in wd.items():
            obj.vertex_groups[b].add([vi], val, "REPLACE")
    print("RETARGET ghillie strips", made, "verts", len(verts), "tris", len(faces) * 2)
    return list(base_objs) + [obj]
