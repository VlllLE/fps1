#!/usr/bin/env python3
"""Write batch-2 skin Unity YAML assets and primitive gun prefabs with stable GUIDs."""

from __future__ import annotations

import os
import textwrap

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
REPO = os.path.abspath(os.path.join(ROOT, "..", ".."))
WEAPONS = os.path.join(REPO, "Assets", "Weapons")

# Existing GUIDs reused from batch 1
EXISTING = {
    "shader_TriplanarStatic": "ad41480938c447dd9144143e52faceeb",
    "tex_SkinNoise": "fd6c145835a048ef8f174a1ac8f5fa0d",
    "tex_SkinCircuitMask": "0338dec1c5c54abc81253945edef99a2",
    "cs_WeaponSkinDefinition": "40c3bfe530754bafb16d4ffde8f0ed62",
    "cs_WeaponSkinApplier": "e72ded460720468694079be126375251",
    "cs_WeaponController": "4f2264107291e63498444c8598db82e7",
}

G = {
    **EXISTING,
    # textures
    "tex_DesertTiger": "afae0739ea144c5585baca92810e0323",
    "tex_UrbanHex": "cc1547ec2a5043cd8a2a69c1f17abc9d",
    "tex_CarbonFiber": "1e266625ffb04376b1a5b0e09682e2f2",
    "tex_CopperPatina": "74b488e8ac0b4a1fb9726f4ca35dfe8a",
    "tex_KryptekFracture": "76873a66baf149d1a77c3a3b17c95afd",
    "tex_NightViper": "550a5235863d4f94a313e83f50c45349",
    # materials static
    "mat_DesertTiger": "c79d13dce398491a9fc277fd584922bb",
    "mat_UrbanHex": "2b38f6303d1748a4a34512152eb3ee1a",
    "mat_CarbonFiber": "bc656cd18d464adcb510e4bd229f0793",
    "mat_CopperPatina": "a02837feccdd4526adebcc91835c2f7c",
    "mat_KryptekFracture": "77ee81404c0f40c7b6c648a84c0dedd2",
    "mat_NightViper": "11fe21f0b52b4f01898d66886113ea31",
    # materials animated
    "mat_NeonRain": "b552039f18034582aa0f9205a6dfbd7d",
    "mat_ChromaticShift": "ba0e38ac1eec4034af9c4ba1079d8561",
    "mat_StormArc": "99febc05ce494cc58764f8df702b1b9e",
    "mat_EmberAsh": "99fa15b1aeb84f13b25fa171b5bc7240",
    # shaders
    "shader_NeonRain": "fec9e3319111458eb11b3bf5dc81fb56",
    "shader_ChromaticShift": "6cce099b8d0743df8557ceb31ab5cf80",
    "shader_StormArc": "7f3753d622d741699ec112a116e9ff14",
    "shader_EmberAsh": "aa757e68b0bb4a4580e7590dddb8d3cb",
    # definitions
    "so_DesertTiger": "25ece70f919d4ad78599b88db4eba6a2",
    "so_UrbanHex": "e3e398d383e84eff8519853f0ca03561",
    "so_CarbonFiber": "40004db994d741298e468f393526aee5",
    "so_CopperPatina": "e37ec62055564acca64a2098f869dde0",
    "so_KryptekFracture": "f8ff666595f84e1eb66cfd719752e8c1",
    "so_NightViper": "697a6589dea646509016ef6459d266b0",
    "so_NeonRain": "f6c9f54ae93e463f9d6bafe226fb7bda",
    "so_ChromaticShift": "45a68f581fdc4b89a7523f43386dbe31",
    "so_StormArc": "d8bd663c5317466fa14f370e0f30f7ad",
    "so_EmberAsh": "466f059384294303bbc050f72324a898",
    # weapons folders / prefabs / materials
    "folder_Weapons": "f195b4f5728441e2a80ded3be1ac6ff3",
    "folder_WeaponsPrefabs": "1a96df9804f94c298f95e3c9294c5bc7",
    "folder_WeaponsMaterials": "4cf4cd0481a4421584a406782784b088",
    "mat_GunMetal": "9c56cd1a248e4ea8bd9cd090ebfe1b88",
    "mat_GunDark": "0538ccd428f640b3a94da514757cf7f6",
    "mat_GunPolymer": "57976278de9a418096150c9efa43832c",
    "prefab_M4A1": "eabc44e128d54d26a4a7af49269f1c9c",
    "prefab_SCAR": "e3a553387eb74606b410bdcbee6efd99",
    "prefab_UMP": "475bfb7dc087434ea682ff192c2580e0",
    "prefab_BoltSniper": "7f85a43c3cd6431ba5d3b3c69f4d36e6",
    "prefab_DMRSniper": "e4b8dde66e484f53a3b6ba5e2588d97d",
    "readme_weapons": "462e2a7769c74f0da8de66b38cca9bd2",
    "readme_skins": "0cd98a35758244cb8f16306c07da4371",  # existing; rewrite contents only
}

RARITY = {"Common": 0, "Uncommon": 1, "Rare": 2, "Epic": 3, "Legendary": 4}

# Built-in mesh fileIDs
MESH = {
    "Cube": 10202,
    "Capsule": 10208,
    "Cylinder": 10206,
    "Sphere": 10207,
}

# Built-in Standard shader
STANDARD_SHADER = "{fileID: 46, guid: 0000000000000000f000000000000000, type: 0}"


def write(path: str, contents: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(contents)


def folder_meta(guid: str) -> str:
    return textwrap.dedent(
        f"""\
        fileFormatVersion: 2
        guid: {guid}
        folderAsset: yes
        DefaultImporter:
          externalObjects: {{}}
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """
    )


def shader_meta(guid: str) -> str:
    return textwrap.dedent(
        f"""\
        fileFormatVersion: 2
        guid: {guid}
        ShaderImporter:
          externalObjects: {{}}
          defaultTextures: []
          nonModifiableTextures: []
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """
    )


def default_meta(guid: str) -> str:
    return textwrap.dedent(
        f"""\
        fileFormatVersion: 2
        guid: {guid}
        DefaultImporter:
          externalObjects: {{}}
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """
    )


def native_meta(guid: str, main_id: int = 2100000) -> str:
    return textwrap.dedent(
        f"""\
        fileFormatVersion: 2
        guid: {guid}
        NativeFormatImporter:
          externalObjects: {{}}
          mainObjectFileID: {main_id}
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """
    )


def scriptable_meta(guid: str) -> str:
    return native_meta(guid, 11400000)


def prefab_meta(guid: str) -> str:
    return textwrap.dedent(
        f"""\
        fileFormatVersion: 2
        guid: {guid}
        PrefabImporter:
          externalObjects: {{}}
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """
    )


def texture_meta(guid: str, *, srgb: bool = True) -> str:
    color_space = 1 if srgb else 0
    return textwrap.dedent(
        f"""\
        fileFormatVersion: 2
        guid: {guid}
        TextureImporter:
          internalIDToNameTable: []
          externalObjects: {{}}
          serializedVersion: 13
          mipmaps:
            mipMapMode: 0
            enableMipMap: 1
            sRGBTexture: {color_space}
            linearTexture: 0
            fadeOut: 0
            borderMipMap: 0
            mipMapsPreserveCoverage: 0
            alphaTestReferenceValue: 0.5
            mipMapFadeDistanceStart: 1
            mipMapFadeDistanceEnd: 3
          bumpmap:
            convertToNormalMap: 0
            externalNormalMap: 0
            heightScale: 0.25
            normalMapFilter: 0
            flipGreenChannel: 0
          isReadable: 0
          streamingMipmaps: 0
          streamingMipmapsPriority: 0
          vTOnly: 0
          ignoreMipmapLimit: 0
          grayScaleToAlpha: 0
          generateCubemap: 6
          cubemapConvolution: 0
          seamlessCubemap: 0
          textureFormat: 1
          maxTextureSize: 2048
          textureSettings:
            serializedVersion: 2
            filterMode: 1
            aniso: 4
            mipBias: 0
            wrapU: 0
            wrapV: 0
            wrapW: 0
          nPOTScale: 1
          lightmap: 0
          compressionQuality: 50
          spriteMode: 0
          spriteExtrude: 1
          spriteMeshType: 1
          alignment: 0
          spritePivot: {{x: 0.5, y: 0.5}}
          spritePixelsToUnits: 100
          spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
          spriteGenerateFallbackPhysicsShape: 1
          alphaUsage: 1
          alphaIsTransparency: 0
          spriteTessellationDetail: -1
          textureType: 0
          textureShape: 1
          singleChannelComponent: 0
          flipbookRows: 1
          flipbookColumns: 1
          maxTextureSizeSet: 0
          compressionQualitySet: 0
          textureFormatSet: 0
          ignorePngGamma: 0
          applyGammaDecoding: 0
          swizzle: 50462976
          cookieLightType: 0
          platformSettings:
          - serializedVersion: 3
            buildTarget: DefaultTexturePlatform
            maxTextureSize: 2048
            resizeAlgorithm: 0
            textureFormat: -1
            textureCompression: 1
            compressionQuality: 50
            crunchedCompression: 0
            allowsAlphaSplitting: 0
            overridden: 0
            ignorePlatformSupport: 0
            androidETC2FallbackOverride: 0
            forceMaximumCompressionQuality_BC7: 0
          spriteSheet:
            serializedVersion: 2
            sprites: []
            outline: []
            physicsShape: []
            bones: []
            spriteID: 
            internalID: 0
            vertices: []
            indices: 
            edges: []
            weights: []
            secondaryTextures: []
            nameFileIdTable: {{}}
          mipmapLimitGroupName: 
          pSDRemoveMatte: 0
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """
    )


def material_yaml(
    name: str,
    shader_guid: str | None,
    floats: dict[str, float],
    colors: dict[str, tuple],
    textures: dict[str, str | None],
    *,
    builtin_standard: bool = False,
) -> str:
    if builtin_standard:
        shader_line = f"  m_Shader: {STANDARD_SHADER}\n"
    else:
        shader_line = f"  m_Shader: {{fileID: 4800000, guid: {shader_guid}, type: 3}}\n"

    tex_lines = []
    for prop, tex_guid in textures.items():
        if tex_guid:
            tex_ref = f"{{fileID: 2800000, guid: {tex_guid}, type: 3}}"
        else:
            tex_ref = "{fileID: 0}"
        tex_lines.append(
            f"    - {prop}:\n"
            f"        m_Texture: {tex_ref}\n"
            f"        m_Scale: {{x: 1, y: 1}}\n"
            f"        m_Offset: {{x: 0, y: 0}}"
        )
    float_lines = [f"    - {k}: {v}" for k, v in floats.items()]
    color_lines = [f"    - {k}: {{r: {r}, g: {g}, b: {b}, a: {a}}}" for k, (r, g, b, a) in colors.items()]
    return (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!21 &2100000\n"
        "Material:\n"
        "  serializedVersion: 8\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_Name: {name}\n"
        + shader_line
        + "  m_Parent: {fileID: 0}\n"
        "  m_ModifiedSerializedProperties: 0\n"
        "  m_ValidKeywords: []\n"
        "  m_InvalidKeywords: []\n"
        "  m_LightmapFlags: 4\n"
        "  m_EnableInstancingVariants: 0\n"
        "  m_DoubleSidedGI: 0\n"
        "  m_CustomRenderQueue: -1\n"
        "  stringTagMap: {}\n"
        "  disabledShaderPasses: []\n"
        "  m_LockedProperties: \n"
        "  m_SavedProperties:\n"
        "    serializedVersion: 3\n"
        "    m_TexEnvs:\n"
        + "\n".join(tex_lines)
        + "\n"
        "    m_Ints: []\n"
        "    m_Floats:\n"
        + "\n".join(float_lines)
        + "\n"
        "    m_Colors:\n"
        + "\n".join(color_lines)
        + "\n"
        "  m_BuildTextureStacks: []\n"
        "  m_AllowLocking: 1\n"
    )


def skin_asset(
    name: str,
    display: str,
    description: str,
    rarity: str,
    material_guid: str,
    *,
    overrides: bool,
    skin_color=(1, 1, 1, 1),
    accent=(1, 1, 1, 1),
    speed=1.0,
    intensity=1.0,
    scale=1.0,
) -> str:
    sc, ac = skin_color, accent
    return (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {G['cs_WeaponSkinDefinition']}, type: 3}}\n"
        f"  m_Name: {name}\n"
        "  m_EditorClassIdentifier: \n"
        f"  skinName: {display}\n"
        f"  description: {description}\n"
        f"  rarity: {RARITY[rarity]}\n"
        "  icon: {fileID: 0}\n"
        f"  material: {{fileID: 2100000, guid: {material_guid}, type: 2}}\n"
        f"  applyParameterOverrides: {1 if overrides else 0}\n"
        f"  skinColor: {{r: {sc[0]}, g: {sc[1]}, b: {sc[2]}, a: {sc[3]}}}\n"
        f"  accentColor: {{r: {ac[0]}, g: {ac[1]}, b: {ac[2]}, a: {ac[3]}}}\n"
        f"  speed: {speed}\n"
        f"  intensity: {intensity}\n"
        f"  scale: {scale}\n"
    )


class IdGen:
    def __init__(self, seed: int):
        self._n = seed

    def next(self) -> int:
        self._n += 17
        return self._n


def quat_from_euler(x_deg: float, y_deg: float, z_deg: float) -> tuple[float, float, float, float]:
    import math

    x, y, z = map(math.radians, (x_deg, y_deg, z_deg))
    cx, sx = math.cos(x * 0.5), math.sin(x * 0.5)
    cy, sy = math.cos(y * 0.5), math.sin(y * 0.5)
    cz, sz = math.cos(z * 0.5), math.sin(z * 0.5)
    qw = cx * cy * cz + sx * sy * sz
    qx = sx * cy * cz - cx * sy * sz
    qy = cx * sy * cz + sx * cy * sz
    qz = cx * cy * sz - sx * sy * cz
    return qx, qy, qz, qw


def mesh_renderer_block(rid: int, go_id: int, mat_guid: str) -> str:
    return textwrap.dedent(
        f"""\
        --- !u!23 &{rid}
        MeshRenderer:
          m_ObjectHideFlags: 0
          m_CorrespondingSourceObject: {{fileID: 0}}
          m_PrefabInstance: {{fileID: 0}}
          m_PrefabAsset: {{fileID: 0}}
          m_GameObject: {{fileID: {go_id}}}
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
          - {{fileID: 2100000, guid: {mat_guid}, type: 2}}
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
    )


def build_prefab(
    name: str,
    parts: list[dict],
    empties: list[dict],
    *,
    seed: int,
) -> str:
    """parts: name, mesh, pos, scale, rot_euler, mat_key
    empties: name, pos (MuzzlePoint / FirePoint / ShellEjectPoint)
    """
    ids = IdGen(seed)
    root_go = ids.next()
    root_tr = ids.next()
    root_wc = ids.next()
    root_sa = ids.next()

    child_blocks = []
    child_tr_ids = []
    muzzle_tr = None
    fire_tr = None
    shell_tr = None

    for part in parts:
        go = ids.next()
        tr = ids.next()
        mf = ids.next()
        mr = ids.next()
        child_tr_ids.append(tr)
        qx, qy, qz, qw = quat_from_euler(*part.get("rot", (0, 0, 0)))
        px, py, pz = part["pos"]
        sx, sy, sz = part["scale"]
        mesh_id = MESH[part["mesh"]]
        mat_guid = G[part["mat"]]
        child_blocks.append(
            textwrap.dedent(
                f"""\
                --- !u!1 &{go}
                GameObject:
                  m_ObjectHideFlags: 0
                  m_CorrespondingSourceObject: {{fileID: 0}}
                  m_PrefabInstance: {{fileID: 0}}
                  m_PrefabAsset: {{fileID: 0}}
                  serializedVersion: 6
                  m_Component:
                  - component: {{fileID: {tr}}}
                  - component: {{fileID: {mf}}}
                  - component: {{fileID: {mr}}}
                  m_Layer: 0
                  m_Name: {part['name']}
                  m_TagString: Untagged
                  m_Icon: {{fileID: 0}}
                  m_NavMeshLayer: 0
                  m_StaticEditorFlags: 0
                  m_IsActive: 1
                --- !u!4 &{tr}
                Transform:
                  m_ObjectHideFlags: 0
                  m_CorrespondingSourceObject: {{fileID: 0}}
                  m_PrefabInstance: {{fileID: 0}}
                  m_PrefabAsset: {{fileID: 0}}
                  m_GameObject: {{fileID: {go}}}
                  serializedVersion: 2
                  m_LocalRotation: {{x: {qx}, y: {qy}, z: {qz}, w: {qw}}}
                  m_LocalPosition: {{x: {px}, y: {py}, z: {pz}}}
                  m_LocalScale: {{x: {sx}, y: {sy}, z: {sz}}}
                  m_ConstrainProportionsScale: 0
                  m_Children: []
                  m_Father: {{fileID: {root_tr}}}
                  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
                --- !u!33 &{mf}
                MeshFilter:
                  m_ObjectHideFlags: 0
                  m_CorrespondingSourceObject: {{fileID: 0}}
                  m_PrefabInstance: {{fileID: 0}}
                  m_PrefabAsset: {{fileID: 0}}
                  m_GameObject: {{fileID: {go}}}
                  m_Mesh: {{fileID: {mesh_id}, guid: 0000000000000000e000000000000000, type: 0}}
                """
            )
            + mesh_renderer_block(mr, go, mat_guid)
        )

    for empty in empties:
        go = ids.next()
        tr = ids.next()
        child_tr_ids.append(tr)
        px, py, pz = empty["pos"]
        if empty["name"] == "MuzzlePoint":
            muzzle_tr = tr
        elif empty["name"] == "FirePoint":
            fire_tr = tr
        elif empty["name"] == "ShellEjectPoint":
            shell_tr = tr
        child_blocks.append(
            textwrap.dedent(
                f"""\
                --- !u!1 &{go}
                GameObject:
                  m_ObjectHideFlags: 0
                  m_CorrespondingSourceObject: {{fileID: 0}}
                  m_PrefabInstance: {{fileID: 0}}
                  m_PrefabAsset: {{fileID: 0}}
                  serializedVersion: 6
                  m_Component:
                  - component: {{fileID: {tr}}}
                  m_Layer: 0
                  m_Name: {empty['name']}
                  m_TagString: Untagged
                  m_Icon: {{fileID: 0}}
                  m_NavMeshLayer: 0
                  m_StaticEditorFlags: 0
                  m_IsActive: 1
                --- !u!4 &{tr}
                Transform:
                  m_ObjectHideFlags: 0
                  m_CorrespondingSourceObject: {{fileID: 0}}
                  m_PrefabInstance: {{fileID: 0}}
                  m_PrefabAsset: {{fileID: 0}}
                  m_GameObject: {{fileID: {go}}}
                  serializedVersion: 2
                  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
                  m_LocalPosition: {{x: {px}, y: {py}, z: {pz}}}
                  m_LocalScale: {{x: 1, y: 1, z: 1}}
                  m_ConstrainProportionsScale: 0
                  m_Children: []
                  m_Father: {{fileID: {root_tr}}}
                  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
                """
            )
        )

    children_yaml = "\n".join(f"  - {{fileID: {cid}}}" for cid in child_tr_ids)
    fire_ref = f"{{fileID: {fire_tr}}}" if fire_tr else "{fileID: 0}"
    muzzle_ref = f"{{fileID: {muzzle_tr}}}" if muzzle_tr else "{fileID: 0}"
    shell_ref = f"{{fileID: {shell_tr}}}" if shell_tr else "{fileID: 0}"

    root = (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        f"--- !u!1 &{root_go}\n"
        "GameObject:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  serializedVersion: 6\n"
        "  m_Component:\n"
        f"  - component: {{fileID: {root_tr}}}\n"
        f"  - component: {{fileID: {root_wc}}}\n"
        f"  - component: {{fileID: {root_sa}}}\n"
        "  m_Layer: 0\n"
        f"  m_Name: {name}\n"
        "  m_TagString: Untagged\n"
        "  m_Icon: {fileID: 0}\n"
        "  m_NavMeshLayer: 0\n"
        "  m_StaticEditorFlags: 0\n"
        "  m_IsActive: 1\n"
        f"--- !u!4 &{root_tr}\n"
        "Transform:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {root_go}}}\n"
        "  serializedVersion: 2\n"
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
        "  m_ConstrainProportionsScale: 0\n"
        "  m_Children:\n"
        f"{children_yaml}\n"
        "  m_Father: {fileID: 0}\n"
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n"
        f"--- !u!114 &{root_wc}\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {root_go}}}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {G['cs_WeaponController']}, type: 3}}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: Scripts::WeaponController\n"
        "  config: {fileID: 0}\n"
        f"  firePoint: {fire_ref}\n"
        "  adsPosition: {fileID: 0}\n"
        "  hipPosition: {fileID: 0}\n"
        "  playerCamera: {fileID: 0}\n"
        "  recoilSystem: {fileID: 0}\n"
        "  hitDetection: {fileID: 0}\n"
        "  animator: {fileID: 0}\n"
        "  audioSource: {fileID: 0}\n"
        f"  muzzlePoint: {muzzle_ref}\n"
        f"  shellEjectPoint: {shell_ref}\n"
        "  overrideSwaySettings: 0\n"
        "  customSwayAmount: 0.02\n"
        "  customMaxSway: 0.06\n"
        "  isAuthoritative: 1\n"
        f"--- !u!114 &{root_sa}\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {root_go}}}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {G['cs_WeaponSkinApplier']}, type: 3}}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        "  defaultSkin: {fileID: 0}\n"
        "  applyDefaultOnAwake: 0\n"
        "  rendererRoot: {fileID: 0}\n"
        "  includeInactiveRenderers: 1\n"
    )
    return root + "".join(child_blocks)


def write_skins() -> None:
    # Texture metas
    for key, filename in [
        ("tex_DesertTiger", "Skin_DesertTiger.png"),
        ("tex_UrbanHex", "Skin_UrbanHex.png"),
        ("tex_CarbonFiber", "Skin_CarbonFiber.png"),
        ("tex_CopperPatina", "Skin_CopperPatina.png"),
        ("tex_KryptekFracture", "Skin_KryptekFracture.png"),
        ("tex_NightViper", "Skin_NightViper.png"),
    ]:
        write(os.path.join(ROOT, "Textures", filename + ".meta"), texture_meta(G[key]))

    # Shader metas
    for key, filename in [
        ("shader_NeonRain", "NeonRain.shader"),
        ("shader_ChromaticShift", "ChromaticShift.shader"),
        ("shader_StormArc", "StormArc.shader"),
        ("shader_EmberAsh", "EmberAsh.shader"),
    ]:
        write(os.path.join(ROOT, "Shaders", filename + ".meta"), shader_meta(G[key]))

    static_mats = [
        ("Mat_Skin_DesertTiger", "mat_DesertTiger", "tex_DesertTiger", 1.05, 0.1, 0.32),
        ("Mat_Skin_UrbanHex", "mat_UrbanHex", "tex_UrbanHex", 1.2, 0.2, 0.38),
        ("Mat_Skin_CarbonFiber", "mat_CarbonFiber", "tex_CarbonFiber", 1.5, 0.7, 0.65),
        ("Mat_Skin_CopperPatina", "mat_CopperPatina", "tex_CopperPatina", 1.1, 0.75, 0.55),
        ("Mat_Skin_KryptekFracture", "mat_KryptekFracture", "tex_KryptekFracture", 1.0, 0.12, 0.3),
        ("Mat_Skin_NightViper", "mat_NightViper", "tex_NightViper", 1.15, 0.25, 0.42),
    ]
    for name, mat_key, tex_key, scale, metallic, gloss in static_mats:
        yaml = material_yaml(
            name,
            G["shader_TriplanarStatic"],
            {
                "_BlendSharpness": 4.0,
                "_Glossiness": gloss,
                "_Metallic": metallic,
                "_Scale": scale,
                "_Speed": 0.0,
                "_Intensity": 1.0,
            },
            {
                "_Color": (1, 1, 1, 1),
                "_SkinColor": (1, 1, 1, 1),
                "_EnergyColor": (1, 1, 1, 1),
                "_EmissionColor": (0, 0, 0, 1),
                "_AccentColor": (1, 1, 1, 1),
            },
            {
                "_MainTex": G[tex_key],
                "_BumpMap": None,
                "_DetailAlbedoMap": None,
                "_DetailMask": None,
                "_DetailNormalMap": None,
                "_EmissionMap": None,
                "_MetallicGlossMap": None,
                "_OcclusionMap": None,
                "_ParallaxMap": None,
            },
        )
        path = os.path.join(ROOT, "Materials", name + ".mat")
        write(path, yaml)
        write(path + ".meta", native_meta(G[mat_key]))

    animated = [
        (
            "Mat_Skin_NeonRain",
            "mat_NeonRain",
            G["shader_NeonRain"],
            {
                "_BlendSharpness": 4.0,
                "_Glossiness": 0.55,
                "_Metallic": 0.5,
                "_Scale": 1.4,
                "_Speed": 1.75,
                "_Intensity": 2.25,
            },
            {
                "_Color": (0.04, 0.05, 0.08, 1),
                "_SkinColor": (0.04, 0.05, 0.08, 1),
                "_EnergyColor": (0.25, 0.95, 1.0, 1),
                "_AccentColor": (0.55, 0.2, 1.0, 1),
                "_EmissionColor": (0.35, 0.9, 1.0, 1),
            },
            {"_NoiseTex": G["tex_SkinNoise"], "_MainTex": None},
        ),
        (
            "Mat_Skin_ChromaticShift",
            "mat_ChromaticShift",
            G["shader_ChromaticShift"],
            {
                "_BlendSharpness": 4.0,
                "_Glossiness": 0.9,
                "_Metallic": 0.95,
                "_Scale": 1.2,
                "_Speed": 1.1,
                "_Intensity": 1.85,
            },
            {
                "_Color": (0.08, 0.08, 0.1, 1),
                "_SkinColor": (0.08, 0.08, 0.1, 1),
                "_EnergyColor": (1.0, 0.2, 0.45, 1),
                "_AccentColor": (0.2, 0.85, 1.0, 1),
                "_EmissionColor": (0.9, 0.9, 1.0, 1),
            },
            {"_NoiseTex": G["tex_SkinNoise"], "_MainTex": None},
        ),
        (
            "Mat_Skin_StormArc",
            "mat_StormArc",
            G["shader_StormArc"],
            {
                "_BlendSharpness": 4.0,
                "_Glossiness": 0.5,
                "_Metallic": 0.7,
                "_Scale": 1.15,
                "_Speed": 2.0,
                "_Intensity": 2.6,
            },
            {
                "_Color": (0.05, 0.06, 0.1, 1),
                "_SkinColor": (0.05, 0.06, 0.1, 1),
                "_EnergyColor": (0.55, 0.8, 1.0, 1),
                "_AccentColor": (0.9, 0.95, 1.0, 1),
                "_EmissionColor": (0.65, 0.85, 1.0, 1),
            },
            {
                "_NoiseTex": G["tex_SkinNoise"],
                "_CircuitTex": G["tex_SkinCircuitMask"],
                "_MainTex": None,
            },
        ),
        (
            "Mat_Skin_EmberAsh",
            "mat_EmberAsh",
            G["shader_EmberAsh"],
            {
                "_BlendSharpness": 4.0,
                "_Glossiness": 0.2,
                "_Metallic": 0.08,
                "_Scale": 0.95,
                "_Speed": 0.9,
                "_Intensity": 2.8,
            },
            {
                "_Color": (0.07, 0.06, 0.05, 1),
                "_SkinColor": (0.07, 0.06, 0.05, 1),
                "_EnergyColor": (1.0, 0.45, 0.08, 1),
                "_AccentColor": (0.25, 0.1, 0.05, 1),
                "_EmissionColor": (1.0, 0.55, 0.12, 1),
            },
            {"_NoiseTex": G["tex_SkinNoise"], "_MainTex": None},
        ),
    ]
    for name, mat_key, shader_guid, floats, colors, textures in animated:
        path = os.path.join(ROOT, "Materials", name + ".mat")
        write(path, material_yaml(name, shader_guid, floats, colors, textures))
        write(path + ".meta", native_meta(G[mat_key]))

    static_defs = [
        ("Skin_DesertTiger", "so_DesertTiger", "mat_DesertTiger", "Desert Tiger", "Sandy tiger-stripe desert camo.", "Uncommon", 1.05),
        ("Skin_UrbanHex", "so_UrbanHex", "mat_UrbanHex", "Urban Hex", "Cool grey urban hex lattice.", "Uncommon", 1.2),
        ("Skin_CarbonFiber", "so_CarbonFiber", "mat_CarbonFiber", "Carbon Fiber", "Interlaced carbon weave with a glossy finish.", "Rare", 1.5),
        ("Skin_CopperPatina", "so_CopperPatina", "mat_CopperPatina", "Copper Patina", "Oxidized copper with turquoise verdigris.", "Rare", 1.1),
        ("Skin_KryptekFracture", "so_KryptekFracture", "mat_KryptekFracture", "Kryptek Fracture", "Angular olive fracture camo.", "Epic", 1.0),
        ("Skin_NightViper", "so_NightViper", "mat_NightViper", "Night Viper", "Near-black digital camo with neon green ticks.", "Epic", 1.15),
    ]
    for asset, so_key, mat_key, display, desc, rarity, scale in static_defs:
        path = os.path.join(ROOT, "Definitions", asset + ".asset")
        write(path, skin_asset(asset, display, desc, rarity, G[mat_key], overrides=False, scale=scale))
        write(path + ".meta", scriptable_meta(G[so_key]))

    animated_defs = [
        (
            "Skin_NeonRain",
            "so_NeonRain",
            "mat_NeonRain",
            "Neon Rain",
            "Falling neon streaks over a dark chassis.",
            "Epic",
            (0.04, 0.05, 0.08, 1),
            (0.25, 0.95, 1.0, 1),
            1.75,
            2.25,
            1.4,
        ),
        (
            "Skin_ChromaticShift",
            "so_ChromaticShift",
            "mat_ChromaticShift",
            "Chromatic Shift",
            "Metallic chromatic colour bands that drift across the mesh.",
            "Legendary",
            (0.08, 0.08, 0.1, 1),
            (0.2, 0.85, 1.0, 1),
            1.1,
            1.85,
            1.2,
        ),
        (
            "Skin_StormArc",
            "so_StormArc",
            "mat_StormArc",
            "Storm Arc",
            "Electric arcs that flicker along circuit traces.",
            "Legendary",
            (0.05, 0.06, 0.1, 1),
            (0.55, 0.8, 1.0, 1),
            2.0,
            2.6,
            1.15,
        ),
        (
            "Skin_EmberAsh",
            "so_EmberAsh",
            "mat_EmberAsh",
            "Ember Ash",
            "Rising embers over hot ash and coal.",
            "Epic",
            (0.07, 0.06, 0.05, 1),
            (1.0, 0.45, 0.08, 1),
            0.9,
            2.8,
            0.95,
        ),
    ]
    for asset, so_key, mat_key, display, desc, rarity, skin_c, accent, speed, intensity, scale in animated_defs:
        path = os.path.join(ROOT, "Definitions", "Animated", asset + ".asset")
        write(
            path,
            skin_asset(
                asset,
                display,
                desc,
                rarity,
                G[mat_key],
                overrides=True,
                skin_color=skin_c,
                accent=accent,
                speed=speed,
                intensity=intensity,
                scale=scale,
            ),
        )
        write(path + ".meta", scriptable_meta(G[so_key]))


def write_readme_skins() -> None:
    path = os.path.join(ROOT, "README.md")
    text = open(path, encoding="utf-8").read()
    if "Batch 2" not in text:
        addition = textwrap.dedent(
            """

            ## Batch 2 skins

            **Static:** Desert Tiger, Urban Hex, Carbon Fiber, Copper Patina, Kryptek Fracture, Night Viper.

            **Animated:** Neon Rain, Chromatic Shift, Storm Arc, Ember Ash
            (`Skins/NeonRain`, `Skins/ChromaticShift`, `Skins/StormArc`, `Skins/EmberAsh`).

            Previews for batch-2 static textures live in the project store under `media/skins-batch2/`.
            """
        )
        # Insert before Notes
        if "## Notes" in text:
            text = text.replace("## Notes", addition + "\n## Notes")
        else:
            text += addition
        # Update animated skin list in "Add an animated skin"
        old_list = (
            "   - `ScrollingEnergy` — circuit traces scroll; uses `SkinCircuitMask` + `SkinNoise`\n"
            "   - `PulsingEmissive` — cell glow pulse; uses `SkinNoise`\n"
            "   - `Holographic` — fresnel + iridescent shift; uses `SkinNoise`\n"
            "   - `FlowingLava` — dual-layer flowing heat; uses `SkinNoise`"
        )
        new_list = (
            old_list
            + "\n"
            "   - `NeonRain` — falling neon streaks; uses `SkinNoise`\n"
            "   - `ChromaticShift` — metallic colour bands; uses `SkinNoise`\n"
            "   - `StormArc` — electric arcs on traces; uses `SkinCircuitMask` + `SkinNoise`\n"
            "   - `EmberAsh` — rising embers over ash; uses `SkinNoise`"
        )
        text = text.replace(old_list, new_list)
        write(path, text)


def write_weapons() -> None:
    write(os.path.join(REPO, "Assets", "Weapons.meta"), folder_meta(G["folder_Weapons"]))
    write(os.path.join(WEAPONS, "Prefabs.meta"), folder_meta(G["folder_WeaponsPrefabs"]))
    write(os.path.join(WEAPONS, "Materials.meta"), folder_meta(G["folder_WeaponsMaterials"]))

    # Default gun materials (Built-in Standard)
    gun_mats = [
        ("Mat_GunMetal", "mat_GunMetal", (0.18, 0.19, 0.21, 1), 0.75, 0.45),
        ("Mat_GunDark", "mat_GunDark", (0.08, 0.08, 0.09, 1), 0.55, 0.35),
        ("Mat_GunPolymer", "mat_GunPolymer", (0.12, 0.11, 0.1, 1), 0.05, 0.25),
    ]
    for name, key, colour, metallic, gloss in gun_mats:
        yaml = material_yaml(
            name,
            None,
            {"_Glossiness": gloss, "_Metallic": metallic},
            {"_Color": colour},
            {"_MainTex": None, "_BumpMap": None, "_EmissionMap": None},
            builtin_standard=True,
        )
        path = os.path.join(WEAPONS, "Materials", name + ".mat")
        write(path, yaml)
        write(path + ".meta", native_meta(G[key]))

    # --- Gun definitions (meters, FPS viewmodel scale; pivot at grip/receiver) ---
    # Convention mirrors TestPistol: children Barrel/GunBody/Grip + MuzzlePoint empty.
    # Also FirePoint (same as muzzle) and ShellEjectPoint for WeaponController wiring.

    m4 = build_prefab(
        "M4A1",
        [
            {"name": "GunBody", "mesh": "Cube", "pos": (0, 0.02, 0.02), "scale": (0.055, 0.09, 0.28), "mat": "mat_GunMetal"},
            {"name": "Barrel", "mesh": "Cube", "pos": (0, 0.035, 0.32), "scale": (0.028, 0.028, 0.34), "mat": "mat_GunDark"},
            {"name": "Handguard", "mesh": "Cube", "pos": (0, 0.03, 0.2), "scale": (0.05, 0.055, 0.2), "mat": "mat_GunPolymer"},
            {"name": "Magazine", "mesh": "Cube", "pos": (0, -0.07, 0.04), "scale": (0.035, 0.12, 0.06), "mat": "mat_GunDark"},
            {"name": "Grip", "mesh": "Cube", "pos": (0, -0.07, -0.06), "scale": (0.035, 0.1, 0.05), "rot": (12, 0, 0), "mat": "mat_GunPolymer"},
            {"name": "Stock", "mesh": "Cube", "pos": (0, 0.01, -0.22), "scale": (0.04, 0.07, 0.22), "mat": "mat_GunPolymer"},
            {"name": "CarryHandle", "mesh": "Cube", "pos": (0, 0.085, -0.02), "scale": (0.03, 0.04, 0.1), "mat": "mat_GunMetal"},
            {"name": "FrontSight", "mesh": "Cube", "pos": (0, 0.07, 0.38), "scale": (0.02, 0.04, 0.02), "mat": "mat_GunDark"},
            {"name": "MuzzleBrake", "mesh": "Cylinder", "pos": (0, 0.035, 0.5), "scale": (0.02, 0.02, 0.02), "rot": (90, 0, 0), "mat": "mat_GunDark"},
        ],
        [
            {"name": "MuzzlePoint", "pos": (0, 0.035, 0.54)},
            {"name": "FirePoint", "pos": (0, 0.035, 0.54)},
            {"name": "ShellEjectPoint", "pos": (0.04, 0.04, 0.05)},
        ],
        seed=410000000,
    )
    scar = build_prefab(
        "SCAR",
        [
            {"name": "GunBody", "mesh": "Cube", "pos": (0, 0.025, 0.0), "scale": (0.065, 0.1, 0.3), "mat": "mat_GunMetal"},
            {"name": "Barrel", "mesh": "Cube", "pos": (0, 0.04, 0.28), "scale": (0.03, 0.03, 0.28), "mat": "mat_GunDark"},
            {"name": "Handguard", "mesh": "Cube", "pos": (0, 0.03, 0.16), "scale": (0.06, 0.06, 0.18), "mat": "mat_GunPolymer"},
            {"name": "Magazine", "mesh": "Cube", "pos": (0, -0.08, 0.02), "scale": (0.04, 0.13, 0.065), "mat": "mat_GunDark"},
            {"name": "Grip", "mesh": "Cube", "pos": (0, -0.075, -0.08), "scale": (0.038, 0.11, 0.055), "rot": (10, 0, 0), "mat": "mat_GunPolymer"},
            {"name": "Stock", "mesh": "Cube", "pos": (0, 0.02, -0.24), "scale": (0.05, 0.08, 0.2), "mat": "mat_GunPolymer"},
            {"name": "StockPad", "mesh": "Cube", "pos": (0, 0.0, -0.35), "scale": (0.055, 0.1, 0.04), "mat": "mat_GunPolymer"},
            {"name": "Rail", "mesh": "Cube", "pos": (0, 0.085, 0.02), "scale": (0.035, 0.02, 0.28), "mat": "mat_GunDark"},
            {"name": "MuzzleBrake", "mesh": "Cylinder", "pos": (0, 0.04, 0.44), "scale": (0.022, 0.022, 0.022), "rot": (90, 0, 0), "mat": "mat_GunDark"},
        ],
        [
            {"name": "MuzzlePoint", "pos": (0, 0.04, 0.48)},
            {"name": "FirePoint", "pos": (0, 0.04, 0.48)},
            {"name": "ShellEjectPoint", "pos": (0.045, 0.05, 0.02)},
        ],
        seed=420000000,
    )
    ump = build_prefab(
        "UMP",
        [
            {"name": "GunBody", "mesh": "Cube", "pos": (0, 0.02, 0.0), "scale": (0.06, 0.1, 0.22), "mat": "mat_GunMetal"},
            {"name": "Barrel", "mesh": "Cube", "pos": (0, 0.035, 0.2), "scale": (0.028, 0.028, 0.18), "mat": "mat_GunDark"},
            {"name": "Handguard", "mesh": "Cube", "pos": (0, 0.02, 0.12), "scale": (0.055, 0.07, 0.12), "mat": "mat_GunPolymer"},
            {"name": "Magazine", "mesh": "Cube", "pos": (0, -0.09, 0.02), "scale": (0.04, 0.14, 0.05), "rot": (8, 0, 0), "mat": "mat_GunDark"},
            {"name": "Grip", "mesh": "Cube", "pos": (0, -0.07, -0.05), "scale": (0.035, 0.1, 0.05), "rot": (15, 0, 0), "mat": "mat_GunPolymer"},
            {"name": "Stock", "mesh": "Cube", "pos": (0, 0.03, -0.18), "scale": (0.03, 0.04, 0.2), "mat": "mat_GunPolymer"},
            {"name": "StockPad", "mesh": "Cube", "pos": (0, 0.0, -0.28), "scale": (0.05, 0.09, 0.03), "mat": "mat_GunPolymer"},
            {"name": "RearSight", "mesh": "Cube", "pos": (0, 0.08, -0.04), "scale": (0.025, 0.03, 0.03), "mat": "mat_GunDark"},
        ],
        [
            {"name": "MuzzlePoint", "pos": (0, 0.035, 0.3)},
            {"name": "FirePoint", "pos": (0, 0.035, 0.3)},
            {"name": "ShellEjectPoint", "pos": (0.04, 0.04, 0.0)},
        ],
        seed=430000000,
    )
    bolt = build_prefab(
        "BoltActionSniper",
        [
            {"name": "GunBody", "mesh": "Cube", "pos": (0, 0.02, 0.0), "scale": (0.05, 0.08, 0.32), "mat": "mat_GunMetal"},
            {"name": "Barrel", "mesh": "Cube", "pos": (0, 0.035, 0.4), "scale": (0.024, 0.024, 0.5), "mat": "mat_GunDark"},
            {"name": "Stock", "mesh": "Cube", "pos": (0, -0.01, -0.28), "scale": (0.05, 0.09, 0.28), "mat": "mat_GunPolymer"},
            {"name": "CheekRest", "mesh": "Cube", "pos": (0, 0.05, -0.22), "scale": (0.045, 0.04, 0.14), "mat": "mat_GunPolymer"},
            {"name": "Grip", "mesh": "Cube", "pos": (0, -0.06, -0.08), "scale": (0.032, 0.09, 0.05), "rot": (8, 0, 0), "mat": "mat_GunPolymer"},
            {"name": "Magazine", "mesh": "Cube", "pos": (0, -0.06, 0.02), "scale": (0.03, 0.07, 0.05), "mat": "mat_GunDark"},
            {"name": "Bolt", "mesh": "Cylinder", "pos": (0.04, 0.04, -0.02), "scale": (0.015, 0.04, 0.015), "rot": (0, 0, 90), "mat": "mat_GunMetal"},
            {"name": "ScopeBody", "mesh": "Cylinder", "pos": (0, 0.1, 0.05), "scale": (0.03, 0.1, 0.03), "rot": (90, 0, 0), "mat": "mat_GunDark"},
            {"name": "ScopeLensFront", "mesh": "Cylinder", "pos": (0, 0.1, 0.16), "scale": (0.035, 0.01, 0.035), "rot": (90, 0, 0), "mat": "mat_GunMetal"},
            {"name": "ScopeLensRear", "mesh": "Cylinder", "pos": (0, 0.1, -0.06), "scale": (0.032, 0.01, 0.032), "rot": (90, 0, 0), "mat": "mat_GunMetal"},
            {"name": "BipodLeft", "mesh": "Cube", "pos": (-0.04, -0.05, 0.22), "scale": (0.012, 0.1, 0.012), "rot": (15, 0, 12), "mat": "mat_GunDark"},
            {"name": "BipodRight", "mesh": "Cube", "pos": (0.04, -0.05, 0.22), "scale": (0.012, 0.1, 0.012), "rot": (15, 0, -12), "mat": "mat_GunDark"},
        ],
        [
            {"name": "MuzzlePoint", "pos": (0, 0.035, 0.66)},
            {"name": "FirePoint", "pos": (0, 0.035, 0.66)},
            {"name": "ShellEjectPoint", "pos": (0.05, 0.05, 0.0)},
        ],
        seed=440000000,
    )
    dmr = build_prefab(
        "DMRSniper",
        [
            {"name": "GunBody", "mesh": "Cube", "pos": (0, 0.025, 0.02), "scale": (0.055, 0.09, 0.3), "mat": "mat_GunMetal"},
            {"name": "Barrel", "mesh": "Cube", "pos": (0, 0.04, 0.34), "scale": (0.026, 0.026, 0.38), "mat": "mat_GunDark"},
            {"name": "Handguard", "mesh": "Cube", "pos": (0, 0.03, 0.18), "scale": (0.05, 0.05, 0.2), "mat": "mat_GunPolymer"},
            {"name": "Magazine", "mesh": "Cube", "pos": (0, -0.08, 0.03), "scale": (0.035, 0.12, 0.055), "mat": "mat_GunDark"},
            {"name": "Grip", "mesh": "Cube", "pos": (0, -0.07, -0.07), "scale": (0.035, 0.1, 0.05), "rot": (12, 0, 0), "mat": "mat_GunPolymer"},
            {"name": "Stock", "mesh": "Cube", "pos": (0, 0.015, -0.24), "scale": (0.045, 0.075, 0.22), "mat": "mat_GunPolymer"},
            {"name": "Rail", "mesh": "Cube", "pos": (0, 0.08, 0.04), "scale": (0.03, 0.018, 0.3), "mat": "mat_GunDark"},
            {"name": "ScopeBody", "mesh": "Cylinder", "pos": (0, 0.12, 0.06), "scale": (0.028, 0.09, 0.028), "rot": (90, 0, 0), "mat": "mat_GunDark"},
            {"name": "ScopeLensFront", "mesh": "Cylinder", "pos": (0, 0.12, 0.16), "scale": (0.032, 0.01, 0.032), "rot": (90, 0, 0), "mat": "mat_GunMetal"},
            {"name": "MuzzleBrake", "mesh": "Cylinder", "pos": (0, 0.04, 0.55), "scale": (0.02, 0.02, 0.02), "rot": (90, 0, 0), "mat": "mat_GunDark"},
        ],
        [
            {"name": "MuzzlePoint", "pos": (0, 0.04, 0.58)},
            {"name": "FirePoint", "pos": (0, 0.04, 0.58)},
            {"name": "ShellEjectPoint", "pos": (0.04, 0.05, 0.02)},
        ],
        seed=450000000,
    )

    prefabs = [
        ("M4A1", "prefab_M4A1", m4),
        ("SCAR", "prefab_SCAR", scar),
        ("UMP", "prefab_UMP", ump),
        ("BoltActionSniper", "prefab_BoltSniper", bolt),
        ("DMRSniper", "prefab_DMRSniper", dmr),
    ]
    for filename, key, content in prefabs:
        path = os.path.join(WEAPONS, "Prefabs", filename + ".prefab")
        write(path, content)
        write(path + ".meta", prefab_meta(G[key]))

    readme = textwrap.dedent(
        """\
        # Weapon models

        Primitive-composed gun prefabs for first-person use. Built without DCC tools
        from Unity built-in Cube / Cylinder meshes so they work out of the box.

        ## Prefabs (`Prefabs/`)

        | Prefab | Class | Approx. length (local Z) | Notes |
        | --- | --- | --- | --- |
        | `M4A1` | Assault rifle | ~0.54 m muzzle | Carry handle + handguard silhouette |
        | `SCAR` | Assault rifle | ~0.48 m muzzle | Bulkier receiver, stock pad |
        | `UMP` | SMG | ~0.30 m muzzle | Compact; angled mag |
        | `BoltActionSniper` | Bolt sniper | ~0.66 m muzzle | Scope + bipod + bolt handle |
        | `DMRSniper` | Semi DMR | ~0.58 m muzzle | Rail optic, shorter than bolt |

        ## Scale & pivot

        - Units are **meters**. Pivot is at the **receiver / grip** (local origin),
          matching the existing `TestPistol` viewmodel convention in `Player.prefab`.
        - Intended as FPS viewmodels (same ballpark as the test pistol ~0.3 m).
          For world/third-person, parent under a socket and scale ~1.0, or duplicate
          and stretch to taste.
        - Forward is **+Z**. Muzzle empties sit at the barrel tip.

        ## Hierarchy conventions (`WeaponController`)

        Root has `WeaponController` + `WeaponSkinApplier`. Child empties:

        - `MuzzlePoint` — muzzle flash / projectile spawn (wired to `muzzlePoint`)
        - `FirePoint` — same tip; wired to `firePoint`
        - `ShellEjectPoint` — right-side ejector; wired to `shellEjectPoint`

        Mesh children use readable names (`GunBody`, `Barrel`, `Grip`, `Magazine`,
        `Stock`, …). All mesh parts have `MeshRenderer`s so `WeaponSkinApplier`
        can replace materials with triplanar skins.

        Assign a `WeaponConfig` on the root `WeaponController` before play.
        Default body materials live in `Materials/` (Built-in Standard).

        ## Not verified

        Prefabs were authored as YAML outside the Unity editor. Open the project
        locally to confirm mesh pivots, materials, and component wiring.
        """
    )
    write(os.path.join(WEAPONS, "README.md"), readme)
    write(os.path.join(WEAPONS, "README.md.meta"), default_meta(G["readme_weapons"]))


def main() -> None:
    write_skins()
    write_readme_skins()
    write_weapons()
    print("Batch-2 skins + weapon prefabs written.")


if __name__ == "__main__":
    main()
