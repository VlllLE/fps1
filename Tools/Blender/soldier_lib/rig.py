"""Armature (the 19 HumanoidBone names), automatic weights, region masks and weight transfer."""

import math

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

from .geometry import (BONES, BONE_INDEX, M_BASE, M_XFER, M_RIGID, M_NECK, R_TORSO, R_ARM_L, R_ARM_R, R_LEG_L,
                       R_LEG_R, R_HEAD, U2B, B2U, smoothstep)

# Unity-space rest positions, identical to HumanoidParts.GetRestLocalPosition accumulated.
REST = {
    "Hips": (0.0, 0.98, 0.0), "Spine": (0.0, 1.04, 0.0), "Chest": (0.0, 1.22, 0.0), "Neck": (0.0, 1.44, 0.0),
    "Head": (0.0, 1.52, 0.0),
    "ShoulderL": (-0.215, 1.41, 0.0), "ShoulderR": (0.215, 1.41, 0.0),
    "UpperArmL": (-0.215, 1.41, 0.0), "UpperArmR": (0.215, 1.41, 0.0),
    "ForearmL": (-0.215, 1.12, 0.0), "ForearmR": (0.215, 1.12, 0.0),
    "HandL": (-0.215, 0.85, 0.0), "HandR": (0.215, 0.85, 0.0),
    "ThighL": (-0.11, 0.94, 0.0), "ThighR": (0.11, 0.94, 0.0),
    "ShinL": (-0.11, 0.52, 0.0), "ShinR": (0.11, 0.52, 0.0),
    "FootL": (-0.11, 0.10, 0.0), "FootR": (0.11, 0.10, 0.0),
}
PARENT = {
    "Hips": None, "Spine": "Hips", "Chest": "Spine", "Neck": "Chest", "Head": "Neck",
    "ShoulderL": "Chest", "ShoulderR": "Chest", "UpperArmL": "ShoulderL", "UpperArmR": "ShoulderR",
    "ForearmL": "UpperArmL", "ForearmR": "UpperArmR", "HandL": "ForearmL", "HandR": "ForearmR",
    "ThighL": "Hips", "ThighR": "Hips", "ShinL": "ThighL", "ShinR": "ThighR", "FootL": "ShinL", "FootR": "ShinR",
}
TAILS = {
    "Hips": (0.0, 1.04, 0.0), "Spine": (0.0, 1.22, 0.0), "Chest": (0.0, 1.44, 0.0), "Neck": (0.0, 1.52, 0.0),
    "Head": (0.0, 1.80, 0.0),
    "ShoulderL": (-0.215, 1.45, 0.0), "ShoulderR": (0.215, 1.45, 0.0),
    "UpperArmL": (-0.215, 1.12, 0.0), "UpperArmR": (0.215, 1.12, 0.0),
    "ForearmL": (-0.215, 0.85, 0.0), "ForearmR": (0.215, 0.85, 0.0),
    "HandL": (-0.215, 0.74, 0.0), "HandR": (0.215, 0.74, 0.0),
    "ThighL": (-0.11, 0.52, 0.0), "ThighR": (0.11, 0.52, 0.0),
    "ShinL": (-0.11, 0.10, 0.0), "ShinR": (0.11, 0.10, 0.0),
    "FootL": (-0.11, 0.10, 0.18), "FootR": (0.11, 0.10, 0.18),
}

ALLOWED = {
    R_TORSO: ["Hips", "Spine", "Chest", "Neck", "Head", "ShoulderL", "ShoulderR"],
    R_ARM_L: ["Chest", "ShoulderL", "UpperArmL", "ForearmL", "HandL"],
    R_ARM_R: ["Chest", "ShoulderR", "UpperArmR", "ForearmR", "HandR"],
    R_LEG_L: ["Hips", "ThighL", "ShinL", "FootL"],
    R_LEG_R: ["Hips", "ThighR", "ShinR", "FootR"],
    R_HEAD: ["Neck", "Head"],
}


def build_armature(name="Soldier_Armature", only=None):
    bpy.ops.object.select_all(action="DESELECT")
    arm_data = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    use = [b for b in BONES if only is None or b in only]
    for b in use:
        e = eb.new(b)
        e.head = U2B(Vector(REST[b]))
        e.tail = U2B(Vector(TAILS[b]))
        e.roll = 0.0
    for b in use:
        p = PARENT[b]
        if p and p in use:
            eb[b].parent = eb[p]
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def vertex_tags(m, bm):
    bm.verts.ensure_lookup_table()
    tags = []
    for v in bm.verts:
        tags.append((v[m.l_mode], v[m.l_bone], v[m.l_grp], v[m.l_reg]))
    return tags


def _ss(y, y0, h):
    return smoothstep(y0 - h, y0 + h, y)


def analytic_weights(region, u):
    """Joint-blend skin weights for a base-skin vertex at Unity position ``u``.

    Blender's bone-heat solve was tried first (per region, with a temporary armature per region), but
    on this layered suit it assigned sleeves to the Chest bone and legs to the opposite side, so the
    weights are computed from the joint positions instead: smoothstep blends across each joint whose
    width follows the joint's range of motion. Bent poses are checked by the pose renders.
    """
    y = u.y
    if region in (R_ARM_L, R_ARM_R):
        sd = "R" if region == R_ARM_R else "L"
        up = _ss(y, 1.12, 0.055)
        hand_up = _ss(y, 0.85, 0.035)
        chest = 0.35 * smoothstep(1.36, 1.46, y)
        w = {"Hand" + sd: 1.0 - hand_up, "Forearm" + sd: hand_up * (1.0 - up),
             "UpperArm" + sd: up * (1.0 - chest), "Chest": up * chest}
    elif region in (R_LEG_L, R_LEG_R):
        sd = "R" if region == R_LEG_R else "L"
        hips = _ss(y, 0.94, 0.05)
        knee = _ss(y, 0.52, 0.065)
        w = {"Hips": hips, "Thigh" + sd: (1.0 - hips) * knee, "Shin" + sd: (1.0 - hips) * (1.0 - knee)}
    else:
        s1 = _ss(y, 1.04, 0.05)
        s2 = _ss(y, 1.20, 0.07)
        s3 = _ss(y, 1.465, 0.035)
        w = {"Hips": 1.0 - s1, "Spine": s1 * (1.0 - s2), "Chest": s1 * s2 * (1.0 - s3), "Neck": s1 * s2 * s3}
    return norm_top({k: v for k, v in w.items() if v > 1e-4})


def norm_top(w, limit=4):
    items = sorted(w.items(), key=lambda kv: -kv[1])[:limit]
    s = sum(v for _, v in items)
    if s <= 1e-9:
        return {}
    return {k: v / s for k, v in items}


def compute_weights(m, bm, arm=None, mats=None):
    """Final per-vertex weights for every vertex. Returns list of dicts indexed by vertex."""
    bm.verts.ensure_lookup_table()
    n = len(bm.verts)
    w = [None] * n
    base_idx = []
    for v in bm.verts:
        if v[m.l_mode] == M_BASE:
            w[v.index] = analytic_weights(v[m.l_reg], B2U(v.co))
            base_idx.append(v.index)

    # KD-trees of base vertices per region for transfer.
    trees = {}
    for reg in (R_TORSO, R_ARM_L, R_ARM_R, R_LEG_L, R_LEG_R, R_HEAD):
        ids = [vi for vi in base_idx if bm.verts[vi][m.l_reg] == reg]
        if not ids:
            continue
        t = KDTree(len(ids))
        for i, vi in enumerate(ids):
            t.insert(bm.verts[vi].co, i)
        t.balance()
        trees[reg] = (t, ids)
    for v in bm.verts:
        mode, bone, grp, reg = v[m.l_mode], v[m.l_bone], v[m.l_grp], v[m.l_reg]
        if mode == M_XFER:
            t, ids = trees.get(reg, trees[R_TORSO])
            acc = {}
            near = t.find_n(v.co, 3)
            tot = 0.0
            for co, i, d in near:
                k = 1.0 / (d + 1e-3)
                tot += k
                for b, val in w[ids[i]].items():
                    acc[b] = acc.get(b, 0.0) + val * k
            w[v.index] = norm_top({b: val / tot for b, val in acc.items()})
        elif mode == M_RIGID:
            w[v.index] = {BONES[bone]: 1.0}
        elif mode == M_NECK:
            y = v.co.z
            t = smoothstep(1.46, 1.58, y)
            w[v.index] = norm_top({"Neck": 1.0 - t, "Head": t})
    return w


def fp_weights(m, bm):
    """FP arms: the same joint blends, with the Chest influence folded into the upper arm."""
    bm.verts.ensure_lookup_table()
    out = [None] * len(bm.verts)
    for v in bm.verts:
        w = analytic_weights(v[m.l_reg], B2U(v.co))
        merged = {}
        for b, val in w.items():
            if b == "Chest":
                b = "UpperArmR" if v[m.l_reg] == R_ARM_R else "UpperArmL"
            merged[b] = merged.get(b, 0.0) + val
        out[v.index] = norm_top(merged)
    return out


def apply_weights(obj, weights, bones=BONES):
    for b in bones:
        if b not in obj.vertex_groups:
            obj.vertex_groups.new(name=b)
    for vi, wd in enumerate(weights):
        for b, val in wd.items():
            obj.vertex_groups[b].add([vi], val, "REPLACE")


_P = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))


def set_pose(arm, rotations):
    """rotations: {bone: (rx, ry, rz) degrees about Unity-space axes through the joint."""
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
    for b, r in rotations.items():
        pb = arm.pose.bones[b]
        Ru = (Matrix.Rotation(math.radians(r[2]), 3, "Z") @ Matrix.Rotation(math.radians(r[1]), 3, "Y")
              @ Matrix.Rotation(math.radians(r[0]), 3, "X"))
        Rb = _P @ Ru @ _P.inverted()
        rest = pb.bone.matrix_local.to_3x3()
        pb.rotation_quaternion = (rest.inverted() @ Rb @ rest).to_quaternion()
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()


def reset_pose(arm):
    set_pose(arm, {})
