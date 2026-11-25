#!/usr/bin/env python3
"""Wires the imported Blender models into the M4A1, SCAR and UMP prefabs (Unity YAML, no Editor needed).

For each prefab it
  * adds a "Model" child (first child, so the imported Magazine/Bolt are found first by WeaponController),
  * moves the primitive-built parts into a "Procedural" child (kept as the fallback),
  * adds an AssaultWeaponModel component (importedModelPrefab -> Assets/Weapons/Models/<Gun>/<Gun>.fbx),
  * moves MuzzlePoint / FirePoint / ShellEjectPoint to the model's anchors and stores the old positions as the
    component's fallback positions.

Re-runnable: a prefab that already has the component is left alone.

    python3 Tools/Weapons/wire_assault_models.py
"""

import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCRIPT_GUID = "16a157e73b495da4b431bd00034113a2"
FBX_ROOT_FILE_ID = 919132149155446097
MATERIALS = {
    "metal": "9c56cd1a248e4ea8bd9cd090ebfe1b88",
    "polymer": "57976278de9a418096150c9efa43832c",
    "accent": "00a55a17c0de5eed0000000000000001",
}
POINT_NAMES = ("MuzzlePoint", "FirePoint", "ShellEjectPoint")

# Anchors printed by Tools/Blender/build_<gun>.py (ANCHORS), in weapon-root space.
GUNS = {
    "M4A1": {"fbx_guid": None, "muzzle": (0.0, 0.045, 0.544), "eject": (0.024, 0.047, 0.060), "base": 8100000000},
    "SCAR": {"fbx_guid": None, "muzzle": (0.0, 0.050, 0.6045), "eject": (0.026, 0.056, 0.062), "base": 8200000000},
    "UMP": {"fbx_guid": None, "muzzle": (0.0, 0.042, 0.410), "eject": (0.024, 0.046, 0.075), "base": 8300000000},
}


def read_guid(path):
    with open(path) as f:
        return re.search(r"^guid: (\w+)", f.read(), re.M).group(1)


def split_docs(text):
    header, *rest = re.split(r"(?m)^(?=--- !u!)", text)
    return header, rest


def doc_info(doc):
    m = re.match(r"--- !u!(\d+) &(\d+)\n", doc)
    return int(m.group(1)), int(m.group(2))


def vec(v):
    return "{x: %s, y: %s, z: %s}" % tuple(("%.6g" % c) for c in v)


def game_object(fid, name, components):
    comps = "".join("  - component: {fileID: %d}\n" % c for c in components)
    return (
        "--- !u!1 &%d\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n%s"
        "  m_Layer: 0\n  m_Name: %s\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n"
        "  m_StaticEditorFlags: 0\n  m_IsActive: 1\n" % (fid, comps, name)
    )


def transform(fid, go, father, children):
    kids = "".join("  - {fileID: %d}\n" % c for c in children)
    kids = "  m_Children:\n" + kids if children else "  m_Children: []\n"
    return (
        "--- !u!4 &%d\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n"
        "  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n%s  m_Father: {fileID: %d}\n"
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n" % (fid, go, kids, father)
    )


def local_position(doc):
    m = re.search(r"m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", doc)
    return tuple(float(g) for g in m.groups())


def set_local_position(doc, pos):
    return re.sub(r"m_LocalPosition: \{[^}]*\}", "m_LocalPosition: " + vec(pos), doc, count=1)


def wire(gun, info):
    prefab_path = os.path.join(ROOT, "Assets/Weapons/Prefabs/%s.prefab" % gun)
    fbx_guid = read_guid(os.path.join(ROOT, "Assets/Weapons/Models/%s/%s.fbx.meta" % (gun, gun)))
    with open(prefab_path) as f:
        text = f.read()
    if SCRIPT_GUID in text:
        print("%s: already wired" % gun)
        return
    header, docs = split_docs(text)
    by_id = {}
    for d in docs:
        cls, fid = doc_info(d)
        by_id[fid] = (cls, d)

    go_name = {}
    for fid, (cls, d) in by_id.items():
        if cls == 1:
            go_name[fid] = re.search(r"m_Name: (.*)\n", d).group(1)
    tr_go = {}
    for fid, (cls, d) in by_id.items():
        if cls == 4:
            tr_go[fid] = int(re.search(r"m_GameObject: \{fileID: (\d+)\}", d).group(1))

    root_tr = next(fid for fid, (cls, d) in by_id.items() if cls == 4 and "m_Father: {fileID: 0}" in d)
    root_go = tr_go[root_tr]
    children = [int(c) for c in re.findall(r"  - \{fileID: (\d+)\}", re.search(r"m_Children:\n((?:  - \{fileID: \d+\}\n)+)", by_id[root_tr][1]).group(1))]
    points = {go_name[tr_go[c]]: c for c in children if go_name[tr_go[c]] in POINT_NAMES}
    procedural = [c for c in children if go_name[tr_go[c]] not in POINT_NAMES]

    base = info["base"]
    model_go, model_tr, proc_go, proc_tr, comp = base + 1, base + 2, base + 3, base + 4, base + 5

    muzzle_old = local_position(by_id[points["MuzzlePoint"]][1])
    eject_old = local_position(by_id[points["ShellEjectPoint"]][1])
    new_docs = []
    for d in docs:
        cls, fid = doc_info(d)
        if fid == root_tr:
            kids = [model_tr, proc_tr] + [points[n] for n in POINT_NAMES]
            d = re.sub(r"  m_Children:\n(?:  - \{fileID: \d+\}\n)+",
                       "  m_Children:\n" + "".join("  - {fileID: %d}\n" % c for c in kids), d, count=1)
        elif fid == root_go:
            d = re.sub(r"(  m_Component:\n(?:  - component: \{fileID: \d+\}\n)+)", lambda m: m.group(1) + "  - component: {fileID: %d}\n" % comp, d, count=1)
        elif fid in procedural:
            d = d.replace("m_Father: {fileID: %d}" % root_tr, "m_Father: {fileID: %d}" % proc_tr)
        elif fid in (points["MuzzlePoint"], points["FirePoint"]):
            d = set_local_position(d, info["muzzle"])
        elif fid == points["ShellEjectPoint"]:
            d = set_local_position(d, info["eject"])
        new_docs.append(d)

    new_docs.append(game_object(model_go, "Model", [model_tr]))
    new_docs.append(transform(model_tr, model_go, root_tr, []))
    new_docs.append(game_object(proc_go, "Procedural", [proc_tr]))
    new_docs.append(transform(proc_tr, proc_go, root_tr, procedural))
    new_docs.append(
        "--- !u!114 &%d\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n"
        "  importedModelPrefab: {fileID: %d, guid: %s, type: 3}\n  visualRoot: {fileID: %d}\n"
        "  metalMaterial: {fileID: 2100000, guid: %s, type: 2}\n  polymerMaterial: {fileID: 2100000, guid: %s, type: 2}\n"
        "  accentMaterial: {fileID: 2100000, guid: %s, type: 2}\n  proceduralRoot: {fileID: %d}\n"
        "  muzzlePoint: {fileID: %d}\n  firePoint: {fileID: %d}\n  shellEjectPoint: {fileID: %d}\n"
        "  fallbackMuzzleLocal: %s\n  fallbackEjectLocal: %s\n"
        % (comp, root_go, SCRIPT_GUID, FBX_ROOT_FILE_ID, fbx_guid, model_tr, MATERIALS["metal"], MATERIALS["polymer"],
           MATERIALS["accent"], proc_go, points["MuzzlePoint"], points["FirePoint"], points["ShellEjectPoint"],
           vec(muzzle_old), vec(eject_old))
    )
    with open(prefab_path, "w") as f:
        f.write(header + "".join(new_docs))
    print("%s: wired (%d procedural parts, fallback muzzle %s)" % (gun, len(procedural), muzzle_old))


def main():
    for gun, info in GUNS.items():
        wire(gun, info)


if __name__ == "__main__":
    sys.exit(main())
