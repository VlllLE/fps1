#!/usr/bin/env python3
"""Wires the Blender-built Shotgun / LMG / Pistol models into the Unity assets.

Idempotent, run from the repository root with plain python3 (no Unity, no Blender needed):

    python3 Tools/Blender/wire_weapon_models.py

What it writes:
  * Assets/Weapons/Materials/Mat_GunAccent.mat (+ .meta) when missing.
  * <Gun>.fbx.meta / <Gun>.glb.meta next to the exported models, with stable GUIDs and an
    externalObjects material remap (<Gun>_Metal/_Polymer/_Accent -> Mat_GunMetal/Mat_GunPolymer/Mat_GunAccent).
  * A WeaponModelLoader (and WeaponPartAnimator for the shotgun pump and the pistol slide) on
    Assets/Weapons/Prefabs/Shotgun.prefab, Assets/Weapons/Prefabs/LMG.prefab and the TestPistol object of
    Assets/Prefabs/Player.prefab and Assets/Characters/Player/PlayerCharacter.prefab.

The primitive meshes stay in the prefabs as the fallback; WeaponModelLoader hides them only once the FBX has
been instantiated and validated at runtime. Re-run this script after Tools/WeaponGen/build_shotgun_lmg.py,
which regenerates Shotgun.prefab and LMG.prefab from scratch.
"""

import os
import re
import sys
import uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS = "Assets/Weapons/Models"
MAT_DIR = "Assets/Weapons/Materials"

LOADER_GUID = "080aafebec364137af2d212e569fe61b"
ANIMATOR_GUID = "c093d6d080704e259d34e3ca646d86ee"
MATS = {
    "Metal": "9c56cd1a248e4ea8bd9cd090ebfe1b88",
    "Polymer": "57976278de9a418096150c9efa43832c",
    "Accent": uuid.uuid5(uuid.NAMESPACE_URL, "fps1/Mat_GunAccent").hex,
}

GUNS = {
    "Shotgun": {
        "required": ["ShotgunBody", "Pump"],
        "fallback": ["GunBody", "Barrel", "ShellTube", "Pump", "PumpGrooves", "ShellCarrier", "Grip", "Stock",
                     "StockPad", "FrontSight", "MuzzleRing"],
        "animators": [dict(part="Pump", travel=(0, 0, -0.05), fire=1, reload=1, delay=0.12, back=0.12, ret=0.14, hold=0)],
        "prefabs": [("Assets/Weapons/Prefabs/Shotgun.prefab", "Shotgun")],
    },
    "LMG": {
        "required": ["LMGBody", "Magazine", "Bolt"],
        "fallback": ["GunBody", "TopCover", "FeedTray", "ChargingHandle", "Handguard", "Barrel", "GasTube",
                     "FlashHider", "CarryHandle", "BipodLegLeft", "BipodLegRight", "BipodMount", "Magazine", "Grip",
                     "Stock", "StockPad", "RearSight"],
        "animators": [],
        "prefabs": [("Assets/Weapons/Prefabs/LMG.prefab", "LMG")],
    },
    "Pistol": {
        "required": ["PistolBody", "Slide", "Magazine"],
        "fallback": ["Barrel", "GunBody", "Grip"],
        "animators": [dict(part="Slide", travel=(0, 0, -0.024), fire=1, reload=0, delay=0.0, back=0.03, ret=0.08, hold=1)],
        "prefabs": [("Assets/Prefabs/Player.prefab", "TestPistol"),
                    ("Assets/Characters/Player/PlayerCharacter.prefab", "TestPistol")],
    },
}


def path(rel):
    return os.path.join(ROOT, rel)


def stable_guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL, "fps1/weapon-model/" + name).hex


def write_if_changed(rel, text):
    full = path(rel)
    if os.path.exists(full) and open(full).read() == text:
        return False
    with open(full, "w", newline="\n") as f:
        f.write(text)
    return True


ACCENT_MAT = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Mat_GunAccent
  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _BumpMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _EmissionMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _Glossiness: 0.5
    - _Metallic: 0.85
    m_Colors:
    - _Color: {r: 0.72, g: 0.52, b: 0.16, a: 1}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""

MAT_META = """fileFormatVersion: 2
guid: %s
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 2100000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

DEFAULT_META = """fileFormatVersion: 2
guid: %s
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: %s
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FBX_META = """fileFormatVersion: 2
guid: %(guid)s
ModelImporter:
  serializedVersion: 22200
  internalIDToNameTable: []
  externalObjects:
%(remap)s  materials:
    materialImportMode: 1
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    removeConstantScaleCurves: 0
    motionNodeName: 
    rigImportErrors: 
    rigImportWarnings: 
    animationImportErrors: 
    animationImportWarnings: 
    animationRetargetingWarnings: 
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    importConstraints: 0
    animationCompression: 0
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 0
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useSRGBMaterialColor: 1
    sortHierarchyByName: 1
    importPhysicalCameras: 0
    importVisibility: 0
    importBlendShapes: 0
    importCameras: 0
    importLights: 0
    nodeNameCollisionStrategy: 1
    fileIdsGeneration: 2
    swapUVChannels: 0
    generateSecondaryUV: 0
    useFileUnits: 1
    keepQuads: 0
    weldVertices: 1
    bakeAxisConversion: 0
    preserveHierarchy: 0
    skinWeightsMode: 0
    maxBonesPerVertex: 4
    minBoneWeight: 0.001
    optimizeBones: 1
    generateMeshLods: 0
    meshLodGenerationFlags: 0
    maximumMeshLod: -1
    meshOptimizationFlags: -1
    indexFormat: 0
    secondaryUVAngleDistortion: 8
    secondaryUVAreaDistortion: 15.000001
    secondaryUVHardAngle: 88
    secondaryUVMarginMethod: 1
    secondaryUVMinLightmapResolution: 40
    secondaryUVMinObjectScale: 1
    secondaryUVPackMargin: 4
    useFileScale: 1
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
    legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes: 0
    blendShapeNormalImportMode: 1
    normalSmoothingSource: 0
  referencedClips: []
  importAnimation: 0
  humanDescription:
    serializedVersion: 3
    human: []
    skeleton: []
    armTwist: 0.5
    foreArmTwist: 0.5
    upperLegTwist: 0.5
    legTwist: 0.5
    armStretch: 0.05
    legStretch: 0.05
    feetSpacing: 0
    globalScale: 1
    rootMotionBoneName: 
    hasTranslationDoF: 0
    hasExtraRoot: 0
    skeletonHasParents: 1
  lastHumanDescriptionAvatarSource: {fileID: 0}
  autoGenerateAvatarMappingIfUnspecified: 1
  animationType: 0
  humanoidOversampling: 1
  avatarSetup: 0
  addHumanoidExtraRootOnlyWhenUsingAvatar: 1
  importBlendShapeDeformPercent: 1
  remapMaterialsIfMaterialImportModeIsNone: 0
  additionalBone: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def fbx_meta(gun):
    remap = ""
    for slot in ("Metal", "Polymer", "Accent"):
        remap += ("  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n"
                  "      name: %s_%s\n    second: {fileID: 2100000, guid: %s, type: 2}\n" % (gun, slot, MATS[slot]))
    return FBX_META % {"guid": stable_guid(gun + ".fbx"), "remap": remap}


def write_assets():
    changed = []
    if write_if_changed(MAT_DIR + "/Mat_GunAccent.mat", ACCENT_MAT):
        changed.append("Mat_GunAccent.mat")
    if write_if_changed(MAT_DIR + "/Mat_GunAccent.mat.meta", MAT_META % MATS["Accent"]):
        changed.append("Mat_GunAccent.mat.meta")
    for gun in GUNS:
        folder = "%s/%s" % (MODELS, gun)
        if not os.path.exists(path(folder + "/%s.fbx" % gun)):
            sys.exit("missing %s/%s.fbx - run Tools/Blender/build_%s.py first" % (folder, gun, gun.lower()))
        if write_if_changed(folder + ".meta", FOLDER_META % stable_guid(gun + "/folder")):
            changed.append(folder + ".meta")
        if write_if_changed(folder + "/%s.fbx.meta" % gun, fbx_meta(gun)):
            changed.append("%s.fbx.meta" % gun)
        if os.path.exists(path(folder + "/%s.glb" % gun)):
            if write_if_changed(folder + "/%s.glb.meta" % gun, DEFAULT_META % stable_guid(gun + ".glb")):
                changed.append("%s.glb.meta" % gun)
    return changed


def split_docs(text):
    header, *rest = re.split(r"(?m)^(?=--- !u!)", text)
    return header, rest


def doc_id(doc):
    return re.match(r"--- !u!(\d+) &(\d+)", doc).group(2)


def find_gameobject(docs, name):
    for i, d in enumerate(docs):
        if d.startswith("--- !u!1 &") and re.search(r"(?m)^  m_Name: %s$" % re.escape(name), d):
            return i
    return None


def mono_doc(file_id, go_id, script_guid, editor_class, body):
    return ("--- !u!114 &%s\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %s}\n"
            "  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
            "  m_Name: \n  m_EditorClassIdentifier: %s\n%s") % (file_id, go_id, script_guid, editor_class, body)


def loader_body(gun, cfg):
    lines = ["  modelPrefab: {fileID: 100100000, guid: %s, type: 3}" % stable_guid(gun + ".fbx"),
             "  visualRoot: {fileID: 0}", "  instanceName: DetailedModel", "  requiredParts:"]
    lines += ["  - %s" % n for n in cfg["required"]]
    for slot in ("Metal", "Polymer", "Accent"):
        lines.append("  %sMaterial: {fileID: 2100000, guid: %s, type: 2}" % (slot.lower(), MATS[slot]))
    lines.append("  fallbackChildren:")
    lines += ["  - %s" % n for n in cfg["fallback"]]
    return "\n".join(lines) + "\n"


def animator_body(a):
    return ("  partName: %s\n  travel: {x: %s, y: %s, z: %s}\n  animateOnFire: %d\n  animateOnReloadComplete: %d\n"
            "  delay: %s\n  backTime: %s\n  returnTime: %s\n  holdOpenWhenEmpty: %d\n  holdOpenFraction: 0.75\n"
            % (a["part"], *a["travel"], a["fire"], a["reload"], a["delay"], a["back"], a["ret"], a["hold"]))


def wire_prefab(rel, root_name, gun, cfg):
    full = path(rel)
    text = open(full).read()
    header, docs = split_docs(text)
    gi = find_gameobject(docs, root_name)
    if gi is None:
        sys.exit("%s: no GameObject named %s" % (rel, root_name))
    go_id = doc_id(docs[gi])
    used = set(doc_id(d) for d in docs)

    def new_id(seed):
        n = int(uuid.uuid5(uuid.NAMESPACE_URL, "fps1/wire/%s/%s/%s" % (rel, go_id, seed)).int % (10 ** 18)) + 10 ** 18
        while str(n) in used:
            n += 1
        used.add(str(n))
        return str(n)

    loader_marker = "guid: %s, type: 3}" % LOADER_GUID
    animator_marker = "guid: %s, type: 3}" % ANIMATOR_GUID
    wanted = [(loader_marker, LOADER_GUID, "Scripts::WeaponModelLoader", loader_body(gun, cfg), "loader")]
    for a in cfg["animators"]:
        wanted.append((animator_marker + "|" + a["part"], ANIMATOR_GUID, "Scripts::WeaponPartAnimator", animator_body(a),
                       "animator-" + a["part"]))

    # Replace previously wired components with fresh ones so edits to this script always win.
    drop = set()
    comp_ids = re.findall(r"(?m)^  - component: \{fileID: (\d+)\}$", docs[gi])
    for i, d in enumerate(docs):
        if d.startswith("--- !u!114 &") and "m_GameObject: {fileID: %s}" % go_id in d and (
                loader_marker in d or animator_marker in d):
            drop.add(i)
            cid = doc_id(d)
            docs[gi] = re.sub(r"(?m)^  - component: \{fileID: %s\}\n" % cid, "", docs[gi])
    docs = [d for i, d in enumerate(docs) if i not in drop]
    gi = find_gameobject(docs, root_name)

    new_docs = []
    comp_lines = ""
    for _, guid, klass, body, seed in wanted:
        fid = new_id(seed)
        comp_lines += "  - component: {fileID: %s}\n" % fid
        new_docs.append(mono_doc(fid, go_id, guid, klass, body))
    docs[gi] = re.sub(r"((?m:^  - component: \{fileID: \d+\}\n)+)", lambda m: m.group(1) + comp_lines, docs[gi], count=1)
    if not docs[-1].endswith("\n"):
        docs[-1] += "\n"
    out = header + "".join(docs) + "".join(new_docs)
    with open(full, "w", newline="\n") as f:
        f.write(out)


def main():
    changed = write_assets()
    for gun, cfg in GUNS.items():
        for rel, root in cfg["prefabs"]:
            wire_prefab(rel, root, gun, cfg)
            print("wired %s -> %s (%s)" % (gun, rel, root))
    print("wrote:", ", ".join(changed) if changed else "no asset changes")


if __name__ == "__main__":
    main()
