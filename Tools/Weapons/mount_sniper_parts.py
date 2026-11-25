#!/usr/bin/env python3
"""Rebuild the scope / bipod hierarchy of BoltActionSniper.prefab and DMRSniper.prefab in place.

The prefabs were originally generated as flat lists of primitives, which left the scopes floating
above the receiver and the bolt-sniper bipod legs hanging in mid air. This script:

  * seats the bolt-sniper magazine against the receiver,
  * adds a scope `Rail` (bolt sniper) / lowers the DMR `Rail` onto the receiver,
  * groups the scope pieces under a `Scope` pivot (optical axis = pivot local +Z, x = 0) and adds two
    mounting rings that touch the rail,
  * adds a `Forend` under the bolt-sniper barrel and groups the bipod under a `Bipod` pivot attached
    to the underside of the forend (mount block plus two splayed legs whose tops sit on the mount).

Run from the repo root on an unmodified checkout of the original flat prefabs:

    python3 Tools/Weapons/mount_sniper_parts.py

Muzzle / fire / eject points and the WeaponController / WeaponSkinApplier wiring are not touched.
"""
import math
import re
import sys

PREFABS = "Assets/Weapons/Prefabs"
CUBE, CYL = 10202, 10206


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    )


def axis_quat(axis, deg):
    h = math.radians(deg) / 2
    s = math.sin(h)
    v = [0, 0, 0]
    v[axis] = s
    return (v[0], v[1], v[2], math.cos(h))


def rotate(q, v):
    x, y, z, w = q
    u = (x, y, z)
    cross = lambda a, b: (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
    t = cross(u, v)
    t = (2 * t[0], 2 * t[1], 2 * t[2])
    c = cross(u, t)
    return (v[0] + w * t[0] + c[0], v[1] + w * t[1] + c[1], v[2] + w * t[2] + c[2])


def fmt(v):
    s = f"{v:.6f}".rstrip("0").rstrip(".")
    return "0" if s in ("", "-0") else s


class Prefab:
    def __init__(self, path, first_new_id):
        self.path = path
        text = open(path).read()
        parts = re.split(r"(?m)^(--- !u!\d+ &\d+\n)", text)
        self.head = parts[0]
        self.blocks = []  # [classId, id, body]
        for i in range(1, len(parts), 2):
            m = re.match(r"--- !u!(\d+) &(\d+)\n", parts[i])
            self.blocks.append([m.group(1), m.group(2), parts[i + 1]])
        self.next_id = first_new_id

    def new_id(self):
        self.next_id += 17
        return str(self.next_id)

    def block(self, cid, fid):
        return next(b for b in self.blocks if b[0] == cid and b[1] == fid)

    def go_id(self, name):
        for cid, fid, body in self.blocks:
            if cid == "1" and re.search(rf"(?m)^  m_Name: {re.escape(name)}$", body):
                return fid
        raise KeyError(name)

    def tr_of(self, name):
        go = self.go_id(name)
        for b in self.blocks:
            if b[0] == "4" and f"m_GameObject: {{fileID: {go}}}" in b[2]:
                return b
        raise KeyError(name)

    def material_of(self, name):
        go = self.go_id(name)
        for b in self.blocks:
            if b[0] == "23" and f"m_GameObject: {{fileID: {go}}}" in b[2]:
                return re.search(r"m_Materials:\n  - (\{.*\})", b[2]).group(1)
        raise KeyError(name)

    def renderer_template(self, name):
        go = self.go_id(name)
        for b in self.blocks:
            if b[0] == "23" and f"m_GameObject: {{fileID: {go}}}" in b[2]:
                return b[2]
        raise KeyError(name)

    @staticmethod
    def set_transform(body, pos=None, rot=None, scale=None, father=None):
        if pos is not None:
            body = re.sub(r"m_LocalPosition: \{.*\}", "m_LocalPosition: {x: %s, y: %s, z: %s}" % tuple(fmt(c) for c in pos), body)
        if rot is not None:
            body = re.sub(r"m_LocalRotation: \{.*\}", "m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}" % tuple(fmt(c) for c in rot), body)
        if scale is not None:
            body = re.sub(r"m_LocalScale: \{.*\}", "m_LocalScale: {x: %s, y: %s, z: %s}" % tuple(fmt(c) for c in scale), body)
        if father is not None:
            body = re.sub(r"m_Father: \{fileID: \d+\}", f"m_Father: {{fileID: {father}}}", body)
        return body

    def children(self, tr_block):
        return re.findall(r"- \{fileID: (\d+)\}", tr_block[2].split("m_Children:")[1].split("m_Father")[0])

    def set_children(self, tr_block, ids):
        lines = "".join(f"  - {{fileID: {i}}}\n" for i in ids)
        body = tr_block[2]
        head, rest = body.split("  m_Children:")
        tail = "  m_Father" + rest.split("  m_Father", 1)[1]
        tr_block[2] = head + ("  m_Children:\n" + lines if ids else "  m_Children: []\n") + tail

    def reparent(self, name, parent_tr_id, pos, rot=None):
        tr = self.tr_of(name)
        old_father = re.search(r"m_Father: \{fileID: (\d+)\}", tr[2]).group(1)
        old = self.block("4", old_father)
        self.set_children(old, [c for c in self.children(old) if c != tr[1]])
        tr[2] = self.set_transform(tr[2], pos=pos, rot=rot, father=parent_tr_id)
        new_parent = self.block("4", parent_tr_id)
        self.set_children(new_parent, self.children(new_parent) + [tr[1]])

    def add_empty(self, name, parent_tr_id, pos):
        go, tr = self.new_id(), self.new_id()
        self.blocks.append(["1", go, (
            "GameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n"
            f"  m_Component:\n  - component: {{fileID: {tr}}}\n  m_Layer: 0\n  m_Name: {name}\n"
            "  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n")])
        self.blocks.append(["4", tr, (
            "Transform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            "  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
            "  m_LocalPosition: {x: %s, y: %s, z: %s}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n"
            "  m_ConstrainProportionsScale: 0\n  m_Children: []\n"
            f"  m_Father: {{fileID: {parent_tr_id}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n" % tuple(fmt(c) for c in pos))])
        parent = self.block("4", parent_tr_id)
        self.set_children(parent, self.children(parent) + [tr])
        return tr

    def add_mesh(self, name, mesh, material_from, parent_tr_id, pos, scale, rot=(0, 0, 0, 1)):
        go, tr, mf, mr = self.new_id(), self.new_id(), self.new_id(), self.new_id()
        self.blocks.append(["1", go, (
            "GameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n"
            f"  m_Component:\n  - component: {{fileID: {tr}}}\n  - component: {{fileID: {mf}}}\n  - component: {{fileID: {mr}}}\n"
            f"  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n"
            "  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n")])
        self.blocks.append(["4", tr, (
            "Transform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            "  serializedVersion: 2\n  m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}\n"
            "  m_LocalPosition: {x: %s, y: %s, z: %s}\n  m_LocalScale: {x: %s, y: %s, z: %s}\n"
            "  m_ConstrainProportionsScale: 0\n  m_Children: []\n"
            f"  m_Father: {{fileID: {parent_tr_id}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
            % tuple(fmt(c) for c in (*rot, *pos, *scale)))])
        self.blocks.append(["33", mf, (
            "MeshFilter:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            f"  m_Mesh: {{fileID: {mesh}, guid: 0000000000000000e000000000000000, type: 0}}\n")])
        template = self.renderer_template(material_from)
        body = re.sub(r"m_GameObject: \{fileID: \d+\}", f"m_GameObject: {{fileID: {go}}}", template, count=1)
        self.blocks.append(["23", mr, body])
        parent = self.block("4", parent_tr_id)
        self.set_children(parent, self.children(parent) + [tr])
        return tr

    def save(self):
        out = self.head + "".join(f"--- !u!{c} &{i}\n{b}" for c, i, b in self.blocks)
        open(self.path, "w").write(out)


Q_X90 = axis_quat(0, 90)
IDENT = (0, 0, 0, 1)


def add_scope(p, root_tr, axis_pos, body_scale, lens_front_z, lens_rear_z, ring_z, ring_scale, lens_scales):
    scope = p.add_empty("Scope", root_tr, axis_pos)
    p.reparent("ScopeBody", scope, (0, 0, 0))
    p.tr_of("ScopeBody")[2] = Prefab.set_transform(p.tr_of("ScopeBody")[2], scale=body_scale)
    p.reparent("ScopeLensFront", scope, (0, 0, lens_front_z))
    p.tr_of("ScopeLensFront")[2] = Prefab.set_transform(p.tr_of("ScopeLensFront")[2], scale=lens_scales[0])
    if lens_rear_z is not None:
        p.reparent("ScopeLensRear", scope, (0, 0, lens_rear_z))
        p.tr_of("ScopeLensRear")[2] = Prefab.set_transform(p.tr_of("ScopeLensRear")[2], scale=lens_scales[1])
    for name, z in (("ScopeRingFront", ring_z), ("ScopeRingRear", -ring_z)):
        p.add_mesh(name, CYL, "ScopeLensFront", scope, (0, 0, z), ring_scale, Q_X90)
    return scope


def bolt_sniper():
    p = Prefab(f"{PREFABS}/BoltActionSniper.prefab", 440001000)
    root = p.tr_of("BoltActionSniper")[1]
    # The magazine top (y = -0.025) stopped 5 mm short of the receiver bottom (y = -0.02); seat it.
    mag = p.tr_of("Magazine")
    mag[2] = Prefab.set_transform(mag[2], pos=(0, -0.053, 0.02))
    # Receiver top is y = 0.06. Rail sits on it (y 0.06..0.072); ring bottoms (axis 0.09 - 0.02) overlap the rail top.
    p.add_mesh("Rail", CUBE, "ScopeBody", root, (0, 0.066, 0.03), (0.026, 0.012, 0.26))
    add_scope(p, root, (0, 0.09, 0.05), (0.03, 0.1, 0.03), 0.11, -0.11, 0.06, (0.04, 0.008, 0.04),
              [(0.035, 0.01, 0.035), (0.032, 0.01, 0.032)])
    # Fore-end wraps the underside of the barrel (barrel bottom y = 0.023) and butts against the receiver (z = 0.16).
    p.add_mesh("Forend", CUBE, "Stock", root, (0, 0, 0.295), (0.04, 0.05, 0.29))
    bipod = p.add_empty("Bipod", root, (0, -0.025, 0.3))
    p.add_mesh("BipodMount", CUBE, "ScopeBody", bipod, (0, -0.006, 0), (0.03, 0.016, 0.05))
    leg_len = 0.1
    for name, side in (("BipodLeft", -1), ("BipodRight", 1)):
        pivot = (side * 0.014, -0.01, 0.0)
        q = qmul(axis_quat(0, -10), axis_quat(2, side * 15))
        d = rotate(q, (0, -leg_len / 2, 0))
        p.reparent(name, bipod, tuple(pivot[i] + d[i] for i in range(3)), rot=q)
    p.save()


def dmr():
    p = Prefab(f"{PREFABS}/DMRSniper.prefab", 450001000)
    root = p.tr_of("DMRSniper")[1]
    # Receiver top is y = 0.07; lower the rail so it rests on it (y 0.069..0.087).
    rail = p.tr_of("Rail")
    rail[2] = Prefab.set_transform(rail[2], pos=(0, 0.078, 0.04))
    add_scope(p, root, (0, 0.105, 0.06), (0.028, 0.09, 0.028), 0.1, None, 0.05, (0.04, 0.008, 0.04),
              [(0.032, 0.01, 0.032)])
    p.save()


if __name__ == "__main__":
    bolt_sniper()
    dmr()
    print("done", file=sys.stderr)
