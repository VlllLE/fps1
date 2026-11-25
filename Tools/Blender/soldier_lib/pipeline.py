import math
import os

import bmesh
import bpy
from mathutils import Vector

from . import body, finish, fp, gear, head, materials, preview, renders, rig
from .geometry import G_ARM_L, G_ARM_R, SKIN, BONES, MATERIAL_NAMES, Model, GROUP_NAMES, B2U


def build_model():
    m = Model()
    body.build_shirt(m)
    for side in (1, -1):
        body.build_sleeve(m, side)
        body.build_trousers(m, side)
        body.build_cargo_pockets(m, side)
        body.build_boot(m, side)
        body.build_glove(m, side)
    head.build_head_group(m)
    gear.build_gear(m)
    scale_head(m, 1.12, dy=-0.02)
    return m


def scale_head(m, k, pivot=(0.0, 1.60, 0.0), dy=0.0):
    from .geometry import U2B, G_HEAD, M_NECK
    pv = U2B(Vector(pivot))
    for v in m.bm.verts:
        if v[m.l_grp] == G_HEAD and v[m.l_mode] != M_NECK:
            v.co = pv + (v.co - pv) * k + Vector((0.0, 0.0, dy))


def finish_bmesh(m):
    bm = m.bm
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    return bm


def object_from_bm(bm, name, mats):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    for mat in mats:
        mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for p in mesh.polygons:
        p.use_smooth = True
    return obj


def sharpen(bm, angle_deg=40.0):
    cos_t = math.cos(math.radians(angle_deg))
    for e in bm.edges:
        if len(e.link_faces) == 2:
            n0, n1 = e.link_faces[0].normal, e.link_faces[1].normal
            skin = e.link_faces[0].material_index == SKIN and e.link_faces[1].material_index == SKIN
            e.smooth = skin or n0.dot(n1) >= cos_t
        else:
            e.smooth = True


def count_tris(objs):
    return sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)


def build_character(m, label, bones, weight_fn, arm_only=None):
    """Combined object + armature + weights + UV, ready for AO bake and splitting."""
    bm = finish_bmesh(m)
    sharpen(bm)
    mats = [bpy.data.materials[n] for n in MATERIAL_NAMES]
    obj = object_from_bm(bm, label + "_All", mats)
    arm = rig.build_armature(label + "_Armature", only=arm_only)
    weights = weight_fn(m, bm)
    rig.apply_weights(obj, weights, bones)
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return bm, obj, arm, weights


def split_and_finish(m, bm, obj, arm, weights, label, groups, bones, out_dir, ao_name, ao_size, ao_samples):
    uv_bounds = finish.unwrap(obj)
    print("SOLDIER_UV_BOUNDS", label, uv_bounds)
    ao_path = os.path.join(out_dir, ao_name)
    os.makedirs(out_dir, exist_ok=True)
    finish.bake_ao(obj, ao_path, size=ao_size, samples=ao_samples)
    parts = []
    for g, name in groups:
        gs = g if isinstance(g, (tuple, list)) else (g,)
        ids = {v.index for v in bm.verts if v[m.l_grp] in gs}
        if ids:
            parts.append(finish.make_part(obj, ids, name, arm, weights, bones))
    bpy.data.objects.remove(obj, do_unlink=True)
    return parts


def run(out_dir, render_dir, samples, res_percent, skip_render, quick, stage):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    materials.make_materials()
    ao_size = 512 if quick else 2048
    ao_samples = 8 if quick else 48

    m = build_model()
    bm, obj, arm, weights = build_character(m, "Soldier", rig.BONES if False else BONES, rig.compute_weights)
    print("SOLDIER_TRIS", sum(len(p.vertices) - 2 for p in obj.data.polygons), "VERTS", len(obj.data.vertices))
    parts = split_and_finish(m, bm, obj, arm, weights, "Soldier", list(zip(range(4), GROUP_NAMES)), BONES, out_dir,
                             "Soldier_AO.png", ao_size, ao_samples)
    body_tris = count_tris(parts)
    print("SOLDIER_FINAL_TRIS", body_tris, {p.name: len(p.data.polygons) for p in parts})
    finish.export_fbx_glb(parts, arm, os.path.join(out_dir, "Soldier"))

    fp_m = fp.build_fp_model()
    fp_bm, fp_obj, fp_arm, fp_w = build_character(fp_m, "SoldierArmsFP", fp.FP_BONES, rig.fp_weights, arm_only=fp.FP_BONES)
    fp_parts = split_and_finish(fp_m, fp_bm, fp_obj, fp_arm, fp_w, "SoldierArmsFP", [((G_ARM_L, G_ARM_R), "SoldierArmsFP_Mesh")],
                                fp.FP_BONES, out_dir, "SoldierArmsFP_AO.png", ao_size // 2, ao_samples)
    print("SOLDIER_FP_TRIS", count_tris(fp_parts))
    finish.export_fbx_glb(fp_parts, fp_arm, os.path.join(out_dir, "SoldierArmsFP"))

    if render_dir and not skip_render:
        renders.run_renders(render_dir, parts, arm, fp_parts, fp_arm, samples, res_percent, quick)
