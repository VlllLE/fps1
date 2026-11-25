#!/usr/bin/env python3
"""Generates the Shotgun and LMG weapon prefabs + WeaponConfig assets as Unity YAML.

The Unity Editor is not needed. Prefabs follow the same primitive-composed
conventions as the other guns in Assets/Weapons/Prefabs (Cube / Cylinder
built-in meshes, Mat_Gun* materials, MuzzlePoint / FirePoint / ShellEjectPoint
empties, WeaponController + WeaponSkinApplier on the root).

Run from the repository root:  python3 Tools/WeaponGen/build_shotgun_lmg.py
Re-running overwrites the generated files but keeps the committed GUIDs, so
existing references (catalog, skins) stay valid.
"""
import math
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

CUBE = 10202
CYLINDER = 10206
MAT_METAL = "9c56cd1a248e4ea8bd9cd090ebfe1b88"
MAT_DARK = "0538ccd428f640b3a94da514757cf7f6"
MAT_POLYMER = "57976278de9a418096150c9efa43832c"
SCRIPT_WEAPON_CONTROLLER = "4f2264107291e63498444c8598db82e7"
SCRIPT_SKIN_APPLIER = "e72ded460720468694079be126375251"

SKIN_DAMASCUS = "11c23a9489c9491a86f044323ba8efee"
SKIN_NIGHT_VIPER = "697a6589dea646509016ef6459d266b0"


def quat_x(deg):
    a = math.radians(deg) / 2
    return (math.sin(a), 0.0, 0.0, math.cos(a))


def quat_z(deg):
    a = math.radians(deg) / 2
    return (0.0, 0.0, math.sin(a), math.cos(a))


def num(v):
    s = ("%.6f" % v).rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def vec(v):
    return "{x: %s, y: %s, z: %s}" % tuple(num(c) for c in v)


def quat(q):
    return "{x: %s, y: %s, z: %s, w: %s}" % tuple(num(c) for c in q)


class Node:
    def __init__(self, name, pos, scale=(1, 1, 1), rot=(0, 0, 0, 1), mesh=None, mat=None, children=None):
        self.name = name
        self.pos = pos
        self.scale = scale
        self.rot = rot
        self.mesh = mesh
        self.mat = mat
        self.children = children or []
        self.go = self.tr = self.mf = self.mr = 0
        self.parent = None


class Ids:
    def __init__(self, base):
        self.n = base

    def next(self):
        self.n += 1
        return self.n


def assign(node, ids, parent):
    node.parent = parent
    node.go = ids.next()
    node.tr = ids.next()
    if node.mesh:
        node.mf = ids.next()
        node.mr = ids.next()
    for c in node.children:
        assign(c, ids, node)


HEADER = """  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
"""


def emit_node(node, out):
    comps = [node.tr] + ([node.mf, node.mr] if node.mesh else [])
    out.append("--- !u!1 &%d\nGameObject:\n%s  serializedVersion: 6\n  m_Component:\n" % (node.go, HEADER))
    for c in comps:
        out.append("  - component: {fileID: %d}\n" % c)
    out.append("  m_Layer: 0\n  m_Name: %s\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n"
               "  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n" % node.name)
    out.append("--- !u!4 &%d\nTransform:\n%s  m_GameObject: {fileID: %d}\n  serializedVersion: 2\n"
               "  m_LocalRotation: %s\n  m_LocalPosition: %s\n  m_LocalScale: %s\n"
               "  m_ConstrainProportionsScale: 0\n" % (node.tr, HEADER, node.go, quat(node.rot), vec(node.pos), vec(node.scale)))
    if node.children:
        out.append("  m_Children:\n")
        for c in node.children:
            out.append("  - {fileID: %d}\n" % c.tr)
    else:
        out.append("  m_Children: []\n")
    out.append("  m_Father: {fileID: %d}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n" % node.parent.tr)
    if node.mesh:
        out.append("--- !u!33 &%d\nMeshFilter:\n%s  m_GameObject: {fileID: %d}\n"
                   "  m_Mesh: {fileID: %d, guid: 0000000000000000e000000000000000, type: 0}\n" % (node.mf, HEADER, node.go, node.mesh))
        out.append(MESH_RENDERER.format(header=HEADER, id=node.mr, go=node.go, mat=node.mat))
    for c in node.children:
        emit_node(c, out)


MESH_RENDERER = """--- !u!23 &{id}
MeshRenderer:
{header}  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_ForceMeshLod: -1
  m_MeshLodSelectionBias: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 2100000, guid: {mat}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 1
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
  m_AdditionalVertexStreams: {{fileID: 0}}
"""


def build_prefab(spec):
    name = spec["name"]
    root = Node(name, (0, 0, 0))
    root.children = spec["parts"]
    ids = Ids(spec["idBase"])
    root.go = ids.next()
    root.tr = ids.next()
    wc = ids.next()
    sk = ids.next()
    for c in root.children:
        assign(c, ids, root)

    def find(n, target):
        for c in n.children:
            if c.name == target:
                return c
            f = find(c, target)
            if f:
                return f
        return None

    muzzle, fire, eject = (find(root, n) for n in ("MuzzlePoint", "FirePoint", "ShellEjectPoint"))

    out = ["%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"]
    out.append("--- !u!1 &%d\nGameObject:\n%s  serializedVersion: 6\n  m_Component:\n"
               "  - component: {fileID: %d}\n  - component: {fileID: %d}\n  - component: {fileID: %d}\n"
               "  m_Layer: 0\n  m_Name: %s\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n"
               "  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n" % (root.go, HEADER, root.tr, wc, sk, name))
    out.append("--- !u!4 &%d\nTransform:\n%s  m_GameObject: {fileID: %d}\n  serializedVersion: 2\n"
               "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
               "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n" % (root.tr, HEADER, root.go))
    for c in root.children:
        out.append("  - {fileID: %d}\n" % c.tr)
    out.append("  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n")
    out.append("--- !u!114 &%d\nMonoBehaviour:\n%s  m_GameObject: {fileID: %d}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
               "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: \n"
               "  m_EditorClassIdentifier: Scripts::WeaponController\n"
               "  config: {fileID: 11400000, guid: %s, type: 2}\n"
               "  firePoint: {fileID: %d}\n  adsPosition: {fileID: 0}\n  hipPosition: {fileID: 0}\n"
               "  playerCamera: {fileID: 0}\n  recoilSystem: {fileID: 0}\n  hitDetection: {fileID: 0}\n"
               "  animator: {fileID: 0}\n  audioSource: {fileID: 0}\n  muzzlePoint: {fileID: %d}\n"
               "  shellEjectPoint: {fileID: %d}\n  overrideSwaySettings: 0\n  customSwayAmount: 0.02\n"
               "  customMaxSway: 0.06\n  isAuthoritative: 1\n"
               % (wc, HEADER, root.go, SCRIPT_WEAPON_CONTROLLER, spec["configGuid"], fire.tr, muzzle.tr, eject.tr))
    out.append("--- !u!114 &%d\nMonoBehaviour:\n%s  m_GameObject: {fileID: %d}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
               "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n"
               "  defaultSkin: {fileID: 11400000, guid: %s, type: 2}\n  applyDefaultOnAwake: 1\n"
               "  rendererRoot: {fileID: 0}\n  includeInactiveRenderers: 1\n"
               % (sk, HEADER, root.go, SCRIPT_SKIN_APPLIER, spec["skinGuid"]))
    for c in root.children:
        emit_node(c, out)
    return "".join(out), root.go


def box(name, pos, scale, mat, rot=(0, 0, 0, 1)):
    return Node(name, pos, scale, rot, CUBE, mat)


def cyl_z(name, pos, radius, length, mat):
    """Cylinder lying along +Z (the built-in cylinder is 2 m tall on Y, so half-length goes in scale.y)."""
    return Node(name, pos, (radius * 2, length / 2, radius * 2), quat_x(90), CYLINDER, mat)


def empty(name, pos):
    return Node(name, pos)


# ----------------------------------------------------------------------------- Shotgun
SHOTGUN_ID_BASE = 470000000
LMG_ID_BASE = 480000000

SHOTGUN_PREFAB_GUID = "5c0a1f3e7b2d4a6c9e81d4f7a3b65c21"
SHOTGUN_CONFIG_GUID = "a3d7e91c4b5f48e2b6c0198d7e2f4a53"
LMG_PREFAB_GUID = "8e4b2d6f1a9c47d3b5e7f0c2a1d89b64"
LMG_CONFIG_GUID = "c1f95a3d7e2b4c8890a6d4e3b7f1c258"


def shotgun_parts():
    return [
        box("GunBody", (0, 0.02, 0.0), (0.05, 0.085, 0.2), MAT_METAL),
        box("Barrel", (0, 0.045, 0.33), (0.03, 0.03, 0.46), MAT_DARK),
        cyl_z("ShellTube", (0, 0.0, 0.31), 0.013, 0.42, MAT_DARK),
        box("Pump", (0, 0.0, 0.25), (0.056, 0.05, 0.16), MAT_POLYMER),
        box("PumpGrooves", (0, -0.0, 0.25), (0.06, 0.02, 0.1), MAT_DARK),
        box("ShellCarrier", (-0.03, 0.02, 0.01), (0.012, 0.05, 0.1), MAT_DARK),
        box("Grip", (0, -0.07, -0.06), (0.035, 0.1, 0.05), MAT_POLYMER, quat_x(15)),
        box("Stock", (0, 0.01, -0.2), (0.04, 0.075, 0.22), MAT_POLYMER),
        box("StockPad", (0, 0.005, -0.32), (0.05, 0.09, 0.03), MAT_DARK),
        box("FrontSight", (0, 0.068, 0.55), (0.012, 0.02, 0.012), MAT_DARK),
        cyl_z("MuzzleRing", (0, 0.045, 0.55), 0.018, 0.02, MAT_METAL),
        empty("MuzzlePoint", (0, 0.045, 0.57)),
        empty("FirePoint", (0, 0.045, 0.57)),
        empty("ShellEjectPoint", (0.03, 0.04, 0.0)),
    ]


def lmg_parts():
    belt = []
    links = 7
    for i in range(links):
        t = i / (links - 1)
        x = -(0.045 + 0.014 * math.sin(math.pi * t))
        y = 0.05 + 0.02 * i
        belt.append(box("BeltLink%d" % i, (x, y, 0.0 + 0.004 * i), (0.012, 0.018, 0.034), MAT_METAL, quat_z(-10 + 4 * i)))
    magazine = Node("Magazine", (0, -0.1, 0.02), children=[
        box("AmmoBox", (0, 0.0, 0.0), (0.085, 0.09, 0.15), MAT_DARK),
        box("AmmoBoxLid", (0, 0.047, 0.0), (0.088, 0.012, 0.154), MAT_POLYMER),
        box("AmmoBoxLatch", (0.0, 0.0, 0.08), (0.03, 0.04, 0.008), MAT_METAL),
    ] + belt)
    return [
        box("GunBody", (0, 0.025, 0.02), (0.065, 0.1, 0.34), MAT_METAL),
        box("TopCover", (0, 0.08, 0.0), (0.052, 0.022, 0.22), MAT_DARK),
        box("FeedTray", (0, 0.075, 0.17), (0.05, 0.012, 0.07), MAT_METAL),
        box("ChargingHandle", (0.04, 0.05, 0.1), (0.014, 0.014, 0.05), MAT_DARK),
        box("Handguard", (0, 0.035, 0.3), (0.058, 0.06, 0.22), MAT_POLYMER),
        box("Barrel", (0, 0.04, 0.45), (0.03, 0.03, 0.5), MAT_DARK),
        box("GasTube", (0, 0.07, 0.3), (0.014, 0.01, 0.22), MAT_METAL),
        cyl_z("FlashHider", (0, 0.04, 0.7), 0.02, 0.07, MAT_DARK),
        box("CarryHandle", (0, 0.105, 0.17), (0.016, 0.03, 0.14), MAT_METAL),
        box("BipodLegLeft", (-0.028, 0.0, 0.5), (0.008, 0.008, 0.16), MAT_DARK),
        box("BipodLegRight", (0.028, 0.0, 0.5), (0.008, 0.008, 0.16), MAT_DARK),
        box("BipodMount", (0, 0.015, 0.43), (0.07, 0.014, 0.02), MAT_METAL),
        magazine,
        box("Grip", (0, -0.075, -0.09), (0.036, 0.1, 0.05), MAT_POLYMER, quat_x(12)),
        box("Stock", (0, 0.015, -0.28), (0.048, 0.085, 0.24), MAT_POLYMER),
        box("StockPad", (0, 0.01, -0.41), (0.055, 0.1, 0.03), MAT_DARK),
        box("RearSight", (0, 0.1, -0.06), (0.02, 0.03, 0.02), MAT_DARK),
        empty("MuzzlePoint", (0, 0.04, 0.74)),
        empty("FirePoint", (0, 0.04, 0.74)),
        empty("ShellEjectPoint", (0.04, 0.05, 0.05)),
    ]


# ----------------------------------------------------------------------------- Configs
def curve(keys):
    """Unity AnimationCurve YAML from (time, value) keys, with slopes from neighbouring keys."""
    lines = ["    serializedVersion: 2\n    m_Curve:\n"]
    for i, (t, v) in enumerate(keys):
        prev = keys[i - 1] if i > 0 else None
        nxt = keys[i + 1] if i < len(keys) - 1 else None
        in_slope = (v - prev[1]) / (t - prev[0]) if prev else 0.0
        out_slope = (nxt[1] - v) / (nxt[0] - t) if nxt else 0.0
        lines.append("    - serializedVersion: 3\n      time: %s\n      value: %s\n      inSlope: %s\n"
                     "      outSlope: %s\n      tangentMode: 0\n      weightedMode: 0\n      inWeight: 0\n      outWeight: 0\n"
                     % (num(t), num(v), num(in_slope), num(out_slope)))
    lines.append("    m_PreInfinity: 2\n    m_PostInfinity: 2\n    m_RotationOrder: 4\n")
    return "".join(lines)


def build_config(spec):
    template = open(os.path.join(ROOT, "Assets/Configs/Weapons/UMPConfig.asset")).read()
    text = template
    text = re.sub(r"m_Name: UMPConfig", "m_Name: %s" % spec["assetName"], text)

    def set_scalar(key, value):
        nonlocal text
        pattern = re.compile(r"^(  %s): .*$" % re.escape(key), re.M)
        assert pattern.search(text), key
        text = pattern.sub(lambda m: "%s: %s" % (m.group(1), value), text, count=1)

    prefab_ref = "{fileID: %d, guid: %s, type: 3}" % (spec["prefabRootId"], spec["prefabGuid"])
    set_scalar("weaponPrefab", prefab_ref)
    for key, value in spec["scalars"].items():
        set_scalar(key, value)

    def set_curve(key, keys):
        nonlocal text
        pattern = re.compile(r"^(  %s:\n)    serializedVersion: 2\n    m_Curve:\n.*?    m_RotationOrder: 4\n" % key, re.M | re.S)
        assert pattern.search(text), key
        text = pattern.sub(lambda m: m.group(1) + curve(keys), text, count=1)

    for key, keys in spec.get("curves", {}).items():
        set_curve(key, keys)

    extra = "".join("  %s: %s\n" % (k, v) for k, v in spec["extra"].items())
    text = re.sub(r"^(  pelletCount: .*\n)", lambda m: m.group(1) + extra, text, count=1, flags=re.M)
    return text


def meta(guid, kind):
    if kind == "prefab":
        return ("fileFormatVersion: 2\nguid: %s\nPrefabImporter:\n  externalObjects: {}\n  userData: \n"
                "  assetBundleName: \n  assetBundleVariant: \n" % guid)
    return ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
            "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)


SHOTGUN_CONFIG = {
    "assetName": "ShotgunConfig",
    "prefabGuid": SHOTGUN_PREFAB_GUID,
    "scalars": {
        "weaponName": "Pump Shotgun",
        "weaponType": 3,
        "damage": 12,
        "headshotMultiplier": 1.5,
        "limbMultiplier": 0.8,
        "fireRate": 75,
        "availableFireModes": "01000000i",
        "range": 30,
        "baseSpread": 2.2,
        "adsSpread": 1.4,
        "spreadPerShot": 0.4,
        "maxSpread": 3,
        "spreadRecoverySpeed": 6,
        "magazineSize": 6,
        "maxReserveAmmo": 36,
        "startingReserveAmmo": 30,
        "reloadTime": 3.4,
        "tacticalReloadTime": 3.0,
        "reloadAmmoTime": 0.35,
        "canCancelReload": 1,
        "adsSpeed": 0.18,
        "adsFOV": 55,
        "adsMovementMultiplier": 0.8,
        "verticalRecoil": 4.2,
        "verticalRandomness": 0.15,
        "horizontalRecoil": 1.0,
        "horizontalRandomness": 0.4,
        "recoilRecoverySpeed": 7,
        "recoilRecoveryDelay": 0.12,
        "gunKickback": 0.09,
        "gunRotationKick": "{x: -6, y: 1.5, z: 2.5}",
        "pelletCount": 9,
    },
    "extra": {"perShellReload": 1, "shellReloadTime": 0.5, "shellReloadFinishTime": 0.35},
    "curves": {
        "damageFalloff": [(0, 1), (0.3, 0.85), (0.7, 0.4), (1, 0.15)],
        "verticalRecoilCurve": [(0, 1), (1, 1.15)],
    },
}

LMG_CONFIG = {
    "assetName": "LMGConfig",
    "prefabGuid": LMG_PREFAB_GUID,
    "scalars": {
        "weaponName": "LMG",
        "weaponType": 2,
        "damage": 24,
        "headshotMultiplier": 1.5,
        "limbMultiplier": 0.8,
        "fireRate": 700,
        "availableFireModes": "00000000i",
        "range": 100,
        "baseSpread": 1.8,
        "adsSpread": 0.7,
        "spreadPerShot": 0.25,
        "maxSpread": 5,
        "spreadRecoverySpeed": 4,
        "magazineSize": 100,
        "maxReserveAmmo": 200,
        "startingReserveAmmo": 200,
        "reloadTime": 5.4,
        "tacticalReloadTime": 4.7,
        "reloadAmmoTime": 3.6,
        "canCancelReload": 1,
        "adsSpeed": 0.34,
        "adsFOV": 55,
        "adsMovementMultiplier": 0.55,
        "adsSwayReduction": 0.6,
        "verticalRecoil": 2.2,
        "verticalRandomness": 0.15,
        "horizontalRecoil": 1.1,
        "horizontalRandomness": 0.4,
        "recoilRecoverySpeed": 5.5,
        "recoilRecoveryDelay": 0.15,
        "gunKickback": 0.07,
        "gunRotationKick": "{x: -4.5, y: 1.5, z: 2.5}",
        "swayAmount": 0.03,
        "maxSwayAmount": 0.08,
        "swaySmooth": 5,
        "bobAmount": 0.035,
        "pelletCount": 1,
    },
    "extra": {"perShellReload": 0, "shellReloadTime": 0.45, "shellReloadFinishTime": 0.3},
    "curves": {
        "damageFalloff": [(0, 1), (0.6, 0.9), (1, 0.6)],
        "verticalRecoilCurve": [(0, 1), (1, 1.6)],
    },
}


def write(path, text):
    full = os.path.join(ROOT, path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", newline="\n") as f:
        f.write(text)
    print("wrote", path)


def main():
    for name, id_base, parts, prefab_guid, config_guid, skin_guid, config in (
        ("Shotgun", SHOTGUN_ID_BASE, shotgun_parts(), SHOTGUN_PREFAB_GUID, SHOTGUN_CONFIG_GUID, SKIN_DAMASCUS, SHOTGUN_CONFIG),
        ("LMG", LMG_ID_BASE, lmg_parts(), LMG_PREFAB_GUID, LMG_CONFIG_GUID, SKIN_NIGHT_VIPER, LMG_CONFIG),
    ):
        spec = {"name": name, "idBase": id_base, "parts": parts, "configGuid": config_guid, "skinGuid": skin_guid}
        prefab_text, root_id = build_prefab(spec)
        write("Assets/Weapons/Prefabs/%s.prefab" % name, prefab_text)
        write("Assets/Weapons/Prefabs/%s.prefab.meta" % name, meta(prefab_guid, "prefab"))
        config = dict(config)
        config["prefabRootId"] = root_id
        write("Assets/Configs/Weapons/%sConfig.asset" % name, build_config(config))
        write("Assets/Configs/Weapons/%sConfig.asset.meta" % name, meta(config_guid, "asset"))
        print("%s prefab root fileID: %d" % (name, root_id))


if __name__ == "__main__":
    main()
