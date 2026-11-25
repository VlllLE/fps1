#!/usr/bin/env python3
"""Wires the Blender-generated models into BoltActionSniper.prefab and DMRSniper.prefab (idempotent).

For each prefab it:
  * moves every primitive-built child (everything except MuzzlePoint / FirePoint / ShellEjectPoint) under a new
    identity-transform child `ProceduralFallback`, so the old model keeps working unchanged,
  * adds an empty `Model` child (identity transform, listed first so name lookups find the model's parts first),
  * adds an `ImportedGunModel` component on the root that instantiates the FBX under `Model` at runtime, remaps the
    materials, and hides `ProceduralFallback` when that works.

The WeaponController / WeaponSkinApplier components and the three anchor transforms are left exactly as they were.

    python3 Tools/Weapons/wire_imported_gun_models.py
"""
import re

PREFABS = "Assets/Weapons/Prefabs"
SCRIPT_GUID = "a0c2f15b41d54878987f0b4a5132ba77"
MATERIALS = {
    "metalMaterial": "9c56cd1a248e4ea8bd9cd090ebfe1b88",
    "polymerMaterial": "57976278de9a418096150c9efa43832c",
    "glassMaterial": "5b0c1b7a9e4d4f2c8a3d6e1f7c2b9a40",
    "accentMaterial": "a17c3e5d2b9f4086b4d1c8e7f6a30b52",
}
ANCHORS = {"MuzzlePoint", "FirePoint", "ShellEjectPoint"}

GUNS = [
    {
        "prefab": "BoltActionSniper.prefab",
        "name": "BoltActionSniper",
        "root_id": 4108250301144107001,
        "id_base": 440009000,
    },
    {
        "prefab": "DMRSniper.prefab",
        "name": "DMRSniper",
        "root_id": 4108250301144107002,
        "id_base": 450009000,
    },
]


def meta_guid(path):
    return re.search(r"^guid: ([0-9a-f]{32})", open(path).read(), re.M).group(1)


def go_doc(go_id, name, component_ids):
    comps = "".join("  - component: {fileID: %d}\n" % c for c in component_ids)
    return (
        "--- !u!1 &%d\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n%s"
        "  m_Layer: 0\n  m_Name: %s\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n"
        "  m_StaticEditorFlags: 0\n  m_IsActive: 1\n" % (go_id, comps, name))


def transform_doc(tr_id, go_id, father_id):
    return (
        "--- !u!4 &%d\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n"
        "  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n"
        "  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: %d}\n"
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n" % (tr_id, go_id, father_id))


def component_doc(comp_id, go_id, model_guid, root_id, model_path, model_tr, fallback_go):
    mats = "".join("  %s: {fileID: 2100000, guid: %s, type: 2}\n" % (k, v) for k, v in MATERIALS.items())
    return (
        "--- !u!114 &%d\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n"
        "  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
        "  m_Name: \n  m_EditorClassIdentifier: Scripts::ImportedGunModel\n"
        "  importedModelPrefab: {fileID: %d, guid: %s, type: 3}\n  editorAssetPath: %s\n"
        "  visualRoot: {fileID: %d}\n%s  proceduralFallback: {fileID: %d}\n"
        % (comp_id, go_id, SCRIPT_GUID, root_id, model_guid, model_path, model_tr, mats, fallback_go))


def wire(gun):
    path = "%s/%s" % (PREFABS, gun["prefab"])
    text = open(path).read()
    if "ImportedGunModel" in text or "guid: %s" % SCRIPT_GUID in text:
        print("already wired:", path)
        return
    model_path = "Assets/Weapons/Models/%s/%s.fbx" % (gun["name"], gun["name"])
    model_guid = meta_guid(model_path + ".meta")

    head, *docs = re.split(r"(?m)^(?=--- !u!)", text)
    parsed = []
    for d in docs:
        m = re.match(r"--- !u!(\d+) &(\d+)", d)
        parsed.append([int(m.group(1)), int(m.group(2)), d])

    go_names = {}
    for cls, fid, d in parsed:
        if cls == 1:
            go_names[fid] = re.search(r"m_Name: (.*)", d).group(1).strip()

    root_tr = None
    for entry in parsed:
        cls, fid, d = entry
        if cls == 4 and re.search(r"m_Father: \{fileID: 0\}", d):
            root_tr = entry
    root_go = int(re.search(r"m_GameObject: \{fileID: (\d+)", root_tr[2]).group(1))

    base = gun["id_base"]
    fb_go, fb_tr, model_go, model_tr, comp = base + 1, base + 2, base + 3, base + 4, base + 5

    children = [int(c) for c in re.findall(r"- \{fileID: (\d+)\}", root_tr[2].split("m_Children:")[1].split("m_Father")[0])]
    child_go = {}
    for cls, fid, d in parsed:
        if cls == 4 and fid in children:
            child_go[fid] = go_names[int(re.search(r"m_GameObject: \{fileID: (\d+)", d).group(1))]
    moved = [c for c in children if child_go[c] not in ANCHORS]
    kept = [c for c in children if child_go[c] in ANCHORS]

    for entry in parsed:
        cls, fid, d = entry
        if cls == 4 and fid in moved:
            entry[2] = d.replace("m_Father: {fileID: %d}" % root_tr[1], "m_Father: {fileID: %d}" % fb_tr)
    new_children = "m_Children:\n" + "".join("  - {fileID: %d}\n" % c for c in [model_tr, fb_tr] + kept)
    root_tr[2] = re.sub(r"m_Children:\n(?:  - \{fileID: \d+\}\n)+", new_children, root_tr[2])

    for entry in parsed:
        cls, fid, d = entry
        if cls == 1 and fid == root_go:
            entry[2] = d.replace("  m_Layer: 0", "  - component: {fileID: %d}\n  m_Layer: 0" % comp, 1)

    fb_children = "m_Children:\n" + "".join("  - {fileID: %d}\n" % c for c in moved)
    fb_tr_doc = transform_doc(fb_tr, fb_go, root_tr[1]).replace("m_Children: []\n", fb_children)
    parsed.append([1, fb_go, go_doc(fb_go, "ProceduralFallback", [fb_tr])])
    parsed.append([4, fb_tr, fb_tr_doc])
    parsed.append([1, model_go, go_doc(model_go, "Model", [model_tr])])
    parsed.append([4, model_tr, transform_doc(model_tr, model_go, root_tr[1])])
    parsed.append([114, comp, component_doc(comp, root_go, model_guid, gun["root_id"], model_path, model_tr, fb_go)])

    with open(path, "w", newline="\n") as fh:
        fh.write(head + "".join(e[2] for e in parsed))
    print("wired:", path, "moved", len(moved), "primitives under ProceduralFallback")


if __name__ == "__main__":
    for gun in GUNS:
        wire(gun)
