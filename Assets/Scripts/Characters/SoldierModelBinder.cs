using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Everything created while binding the imported soldier meshes to a runtime skeleton.</summary>
public sealed class SoldierBuildResult
{
    public readonly List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
    public readonly List<Object> ownedObjects = new List<Object>();
    public Material[] soldierMaterials;
    /// <summary>Factor applied to the imported vertices so the model has its authored height (1 when the import scale is right).</summary>
    public float importScale = 1f;
    /// <summary>True when the model was imported facing backwards and was turned around.</summary>
    public bool turnedAround;
    public int triangleCount;
    public Bounds restBounds;

    public void Destroy()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
                DestroyObject(renderers[i].gameObject);
        }

        for (int i = 0; i < ownedObjects.Count; i++)
        {
            if (ownedObjects[i] != null)
                DestroyObject(ownedObjects[i]);
        }

        renderers.Clear();
        ownedObjects.Clear();
        soldierMaterials = null;
    }

    private static void DestroyObject(Object obj)
    {
        if (Application.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj);
    }
}

/// <summary>
/// Turns the imported Blender soldier into skinned meshes driven by the game's own bone transforms.
/// The FBX is instantiated off-screen, its meshes are skinned to the authored rest pose, validated
/// (readable, known bones/materials, upright, correct height and facing) and re-bound to the
/// <see cref="HumanoidBone"/> transforms of the body with bindposes built from <see cref="HumanoidParts"/> rest positions.
/// Any problem is reported through the <c>error</c> out parameter so callers can fall back to the procedural body.
/// </summary>
public static class SoldierModelBinder
{
    private const float MinimumImportScale = 0.001f;
    private const float MaximumImportScale = 1000f;
    private const float BoundsMargin = 0.45f;

    private sealed class RestMesh
    {
        public string name;
        public Vector3[] positions;
        public Vector3[] normals;
        public Vector2[] uv;
        public BoneWeight[] weights;
        public int[][] triangles;
        public SoldierMaterialSlot[] slots;
        public bool hasNormals;
    }

    /// <summary>Binds the third-person body to the 19 humanoid bones (indexed by <see cref="HumanoidBone"/>).</summary>
    public static bool TryBuildBody(SoldierModelSet set, Transform[] bones, Transform bodyRoot, float scale,
        HumanoidPalette palette, int layer, out SoldierBuildResult result, out string error)
    {
        var skeleton = new List<HumanoidBone>(HumanoidParts.AllBones);
        return TryBuild(set != null ? set.bodyModel : null, set, skeleton, bones, bodyRoot, scale, palette, layer,
            set != null ? set.bodyHeight : 0f, true, false, out result, out error);
    }

    /// <summary>
    /// Binds the first-person arms to six sibling transforms (UpperArm/Forearm/Hand L+R) that are posed by IK in
    /// <paramref name="armsRoot"/> space. <paramref name="bones"/> is indexed by <see cref="HumanoidBone"/>.
    /// </summary>
    public static bool TryBuildArms(SoldierModelSet set, Transform[] bones, Transform armsRoot, float scale,
        HumanoidPalette palette, int layer, out SoldierBuildResult result, out string error)
    {
        var skeleton = new List<HumanoidBone>
        {
            HumanoidBone.UpperArmL, HumanoidBone.UpperArmR, HumanoidBone.ForearmL, HumanoidBone.ForearmR,
            HumanoidBone.HandL, HumanoidBone.HandR
        };
        return TryBuild(set != null ? set.armsModel : null, set, skeleton, bones, armsRoot, scale, palette, layer,
            set != null ? set.armsHeight : 0f, false, true, out result, out error);
    }

    /// <summary>Rest position of a bone relative to the body root, unscaled.</summary>
    public static Vector3 RestWorldPosition(HumanoidBone bone)
    {
        Vector3 position = Vector3.zero;
        HumanoidBone? current = bone;
        while (current.HasValue)
        {
            position += HumanoidParts.GetRestLocalPosition(current.Value);
            current = HumanoidParts.GetParent(current.Value);
        }

        return position;
    }

    private static bool TryBuild(GameObject model, SoldierModelSet set, List<HumanoidBone> skeleton, Transform[] bones,
        Transform root, float scale, HumanoidPalette palette, int layer, float expectedHeight, bool upright,
        bool isArms, out SoldierBuildResult result, out string error)
    {
        result = null;
        error = null;
        var build = new SoldierBuildResult();
        GameObject scratch = null;
        try
        {
            if (set == null)
                throw new InvalidOperationException("no SoldierModelSet");
            if (model == null)
                throw new InvalidOperationException("model prefab is not assigned");
            if (bones == null || root == null)
                throw new InvalidOperationException("no target skeleton");
            if (expectedHeight <= 0.01f)
                throw new InvalidOperationException("model height is not configured");

            scratch = new GameObject("SoldierImportScratch") { hideFlags = HideFlags.HideAndDontSave };
            scratch.SetActive(false);
            GameObject instance = Object.Instantiate(model, scratch.transform);
            if (instance == null)
                throw new InvalidOperationException("model could not be instantiated");

            List<RestMesh> meshes = Harvest(instance);
            if (meshes.Count == 0)
                throw new InvalidOperationException("model has no skinned meshes");

            Normalise(meshes, expectedHeight, upright, isArms, build);

            int[] skeletonIndex = new int[HumanoidParts.BoneCount];
            for (int i = 0; i < skeletonIndex.Length; i++)
                skeletonIndex[i] = -1;
            var skeletonTransforms = new Transform[skeleton.Count];
            var bindPoses = new Matrix4x4[skeleton.Count];
            for (int i = 0; i < skeleton.Count; i++)
            {
                HumanoidBone bone = skeleton[i];
                skeletonIndex[(int)bone] = i;
                skeletonTransforms[i] = bones[(int)bone];
                if (skeletonTransforms[i] == null)
                    throw new InvalidOperationException("missing bone " + bone);
                bindPoses[i] = Matrix4x4.Translate(-RestWorldPosition(bone) * scale);
            }

            build.soldierMaterials = SoldierMaterials.Create(set, palette, isArms ? set.armsAmbientOcclusion : set.ambientOcclusion);
            for (int i = 0; i < build.soldierMaterials.Length; i++)
                build.ownedObjects.Add(build.soldierMaterials[i]);

            Vector3 hipsRest = RestWorldPosition(HumanoidBone.Hips) * scale;
            Bounds total = new Bounds();
            bool hasBounds = false;
            for (int i = 0; i < meshes.Count; i++)
            {
                RestMesh rest = meshes[i];
                Mesh mesh = CreateMesh(rest, skeletonIndex, bindPoses, scale);
                build.ownedObjects.Add(mesh);
                build.triangleCount += CountTriangles(rest);

                Transform parent = isArms ? root : ResolveParent(rest.name, bones, root);
                var go = new GameObject(rest.name);
                go.layer = layer;
                go.transform.SetParent(parent, false);

                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = skeletonTransforms;
                smr.rootBone = isArms ? skeletonTransforms[0] : bones[(int)HumanoidBone.Hips];
                smr.quality = SkinQuality.Bone4;
                smr.shadowCastingMode = isArms
                    ? UnityEngine.Rendering.ShadowCastingMode.Off
                    : UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = !isArms;
                smr.updateWhenOffscreen = isArms;

                var slotMaterials = new Material[rest.slots.Length];
                for (int s = 0; s < slotMaterials.Length; s++)
                    slotMaterials[s] = build.soldierMaterials[(int)rest.slots[s]];
                smr.sharedMaterials = slotMaterials;

                Bounds meshBounds = mesh.bounds;
                Vector3 rootRest = isArms ? RestWorldPosition(skeleton[0]) * scale : hipsRest;
                smr.localBounds = new Bounds(meshBounds.center - rootRest, meshBounds.size + Vector3.one * (BoundsMargin * 2f * scale));
                build.renderers.Add(smr);

                if (!hasBounds)
                {
                    total = meshBounds;
                    hasBounds = true;
                }
                else
                {
                    total.Encapsulate(meshBounds);
                }
            }

            build.restBounds = total;
            result = build;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.GetType().Name + ": " + exception.Message;
            build.Destroy();
            result = null;
            return false;
        }
        finally
        {
            if (scratch != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(scratch);
                else
                    Object.DestroyImmediate(scratch);
            }
        }
    }

    private static Transform ResolveParent(string meshName, Transform[] bones, Transform root)
    {
        Transform parent = null;
        if (meshName.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
            parent = bones[(int)HumanoidBone.Head];
        else if (meshName.IndexOf("ArmL", StringComparison.OrdinalIgnoreCase) >= 0)
            parent = bones[(int)HumanoidBone.ShoulderL];
        else if (meshName.IndexOf("ArmR", StringComparison.OrdinalIgnoreCase) >= 0)
            parent = bones[(int)HumanoidBone.ShoulderR];
        return parent != null ? parent : root;
    }

    private static List<RestMesh> Harvest(GameObject instance)
    {
        var list = new List<RestMesh>();
        Matrix4x4 toRoot = instance.transform.worldToLocalMatrix;
        SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer smr = renderers[r];
            Mesh mesh = smr.sharedMesh;
            if (mesh == null)
                continue;
            if (!mesh.isReadable)
                throw new InvalidOperationException(mesh.name + " is not readable (enable Read/Write on the model importer)");

            Transform[] smrBones = smr.bones;
            Matrix4x4[] bindposes = mesh.bindposes;
            if (smrBones.Length == 0 || smrBones.Length != bindposes.Length)
                throw new InvalidOperationException(mesh.name + " has no usable skin (" + smrBones.Length + " bones, " + bindposes.Length + " bindposes)");

            var boneIds = new int[smrBones.Length];
            var skin = new Matrix4x4[smrBones.Length];
            for (int i = 0; i < smrBones.Length; i++)
            {
                boneIds[i] = -1;
                skin[i] = Matrix4x4.identity;
                if (smrBones[i] == null)
                    continue;
                if (Enum.TryParse(smrBones[i].name, out HumanoidBone parsed) && Enum.IsDefined(typeof(HumanoidBone), parsed))
                    boneIds[i] = (int)parsed;
                skin[i] = toRoot * smrBones[i].localToWorldMatrix * bindposes[i];
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            BoneWeight[] weights = mesh.boneWeights;
            if (vertices.Length == 0 || weights.Length != vertices.Length)
                throw new InvalidOperationException(mesh.name + " is not skinned (" + weights.Length + " weights for " + vertices.Length + " vertices)");

            bool hasNormals = normals.Length == vertices.Length;
            var rest = new RestMesh
            {
                name = smr.gameObject.name,
                positions = new Vector3[vertices.Length],
                normals = new Vector3[vertices.Length],
                uv = mesh.uv,
                weights = new BoneWeight[vertices.Length],
                hasNormals = hasNormals
            };

            for (int v = 0; v < vertices.Length; v++)
            {
                BoneWeight w = weights[v];
                int[] index = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                float[] weight = { w.weight0, w.weight1, w.weight2, w.weight3 };
                Vector3 position = Vector3.zero;
                Vector3 normal = Vector3.zero;
                float sum = 0f;
                var outIndex = new int[4];
                var outWeight = new float[4];
                for (int k = 0; k < 4; k++)
                {
                    if (weight[k] <= 0f)
                        continue;
                    int boneId = index[k] >= 0 && index[k] < boneIds.Length ? boneIds[index[k]] : -1;
                    if (boneId < 0)
                        throw new InvalidOperationException(mesh.name + " is weighted to a bone that is not part of the humanoid skeleton");
                    Matrix4x4 m = skin[index[k]];
                    position += m.MultiplyPoint3x4(vertices[v]) * weight[k];
                    if (hasNormals)
                        normal += m.MultiplyVector(normals[v]) * weight[k];
                    outIndex[k] = boneId;
                    outWeight[k] = weight[k];
                    sum += weight[k];
                }

                if (sum <= 1e-5f)
                    throw new InvalidOperationException(mesh.name + " has an unweighted vertex");

                rest.positions[v] = position / sum;
                rest.normals[v] = hasNormals && normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
                rest.weights[v] = new BoneWeight
                {
                    boneIndex0 = outIndex[0], weight0 = outWeight[0] / sum,
                    boneIndex1 = outIndex[1], weight1 = outWeight[1] / sum,
                    boneIndex2 = outIndex[2], weight2 = outWeight[2] / sum,
                    boneIndex3 = outIndex[3], weight3 = outWeight[3] / sum
                };
            }

            Material[] sharedMaterials = smr.sharedMaterials;
            rest.triangles = new int[mesh.subMeshCount][];
            rest.slots = new SoldierMaterialSlot[mesh.subMeshCount];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                rest.triangles[s] = mesh.GetTriangles(s);
                Material source = s < sharedMaterials.Length ? sharedMaterials[s] : null;
                if (!SoldierMaterials.TryGetSlot(source, out rest.slots[s]))
                    throw new InvalidOperationException(mesh.name + " submesh " + s + " uses unknown material '" + (source != null ? source.name : "null") + "'");
            }

            list.Add(rest);
        }

        return list;
    }

    private static void Normalise(List<RestMesh> meshes, float expectedHeight, bool upright, bool isArms, SoldierBuildResult build)
    {
        Bounds bounds = ComputeBounds(meshes);
        float height = bounds.size.y;
        if (height < 1e-5f)
            throw new InvalidOperationException("model has no height");

        float k = expectedHeight / height;
        if (k < MinimumImportScale || k > MaximumImportScale)
            throw new InvalidOperationException("model height " + height + " is far from the expected " + expectedHeight);
        if (Mathf.Abs(k - 1f) > 0.02f)
            ScaleMeshes(meshes, k);
        else
            k = 1f;
        build.importScale = k;

        bounds = ComputeBounds(meshes);
        if (upright)
        {
            if (bounds.size.y < bounds.size.x * 2f || bounds.size.y < bounds.size.z * 2f)
                throw new InvalidOperationException("model is not upright (size " + bounds.size + ")");
            if (Mathf.Abs(bounds.min.y) > 0.15f)
                throw new InvalidOperationException("model does not stand on the origin (min y " + bounds.min.y + ")");
        }
        else if (bounds.size.y < bounds.size.x)
        {
            throw new InvalidOperationException("arm model is not hanging along -Y (size " + bounds.size + ")");
        }

        float leftSide = 0f;
        float leftWeight = 0f;
        for (int m = 0; m < meshes.Count; m++)
        {
            RestMesh mesh = meshes[m];
            for (int v = 0; v < mesh.positions.Length; v++)
            {
                if (HumanoidParts.IsLeft((HumanoidBone)mesh.weights[v].boneIndex0))
                {
                    leftSide += mesh.positions[v].x;
                    leftWeight += 1f;
                }
            }
        }

        if (leftWeight < 1f)
            throw new InvalidOperationException("model has no left-side vertices");
        float leftCentre = leftSide / leftWeight;
        if (Mathf.Abs(leftCentre) < 0.02f)
            throw new InvalidOperationException("cannot determine model facing");
        if (leftCentre > 0f)
        {
            TurnAround(meshes);
            build.turnedAround = true;
        }
    }

    private static Bounds ComputeBounds(List<RestMesh> meshes)
    {
        Bounds bounds = new Bounds(meshes[0].positions[0], Vector3.zero);
        for (int m = 0; m < meshes.Count; m++)
        {
            Vector3[] p = meshes[m].positions;
            for (int v = 0; v < p.Length; v++)
                bounds.Encapsulate(p[v]);
        }

        return bounds;
    }

    private static void ScaleMeshes(List<RestMesh> meshes, float k)
    {
        for (int m = 0; m < meshes.Count; m++)
        {
            Vector3[] p = meshes[m].positions;
            for (int v = 0; v < p.Length; v++)
                p[v] *= k;
        }
    }

    private static void TurnAround(List<RestMesh> meshes)
    {
        for (int m = 0; m < meshes.Count; m++)
        {
            Vector3[] p = meshes[m].positions;
            Vector3[] n = meshes[m].normals;
            for (int v = 0; v < p.Length; v++)
            {
                p[v] = new Vector3(-p[v].x, p[v].y, -p[v].z);
                n[v] = new Vector3(-n[v].x, n[v].y, -n[v].z);
            }
        }
    }

    private static Mesh CreateMesh(RestMesh rest, int[] skeletonIndex, Matrix4x4[] bindPoses, float scale)
    {
        int count = rest.positions.Length;
        var mesh = new Mesh { name = rest.name };
        if (count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var vertices = new Vector3[count];
        var weights = new BoneWeight[count];
        for (int v = 0; v < count; v++)
        {
            vertices[v] = rest.positions[v] * scale;
            BoneWeight w = rest.weights[v];
            weights[v] = new BoneWeight
            {
                boneIndex0 = Remap(skeletonIndex, w.boneIndex0, w.weight0), weight0 = w.weight0,
                boneIndex1 = Remap(skeletonIndex, w.boneIndex1, w.weight1), weight1 = w.weight1,
                boneIndex2 = Remap(skeletonIndex, w.boneIndex2, w.weight2), weight2 = w.weight2,
                boneIndex3 = Remap(skeletonIndex, w.boneIndex3, w.weight3), weight3 = w.weight3
            };
        }

        mesh.vertices = vertices;
        if (rest.uv != null && rest.uv.Length == count)
            mesh.uv = rest.uv;
        mesh.subMeshCount = rest.triangles.Length;
        for (int s = 0; s < rest.triangles.Length; s++)
            mesh.SetTriangles(rest.triangles[s], s, false);

        if (rest.hasNormals)
            mesh.normals = rest.normals;
        else
            mesh.RecalculateNormals();

        mesh.boneWeights = weights;
        mesh.bindposes = bindPoses;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static int Remap(int[] skeletonIndex, int boneId, float weight)
    {
        if (weight <= 0f)
            return 0;
        int index = skeletonIndex[boneId];
        if (index < 0)
            throw new InvalidOperationException("mesh is weighted to " + (HumanoidBone)boneId + ", which is not part of this skeleton");
        return index;
    }

    private static int CountTriangles(RestMesh rest)
    {
        int count = 0;
        for (int s = 0; s < rest.triangles.Length; s++)
            count += rest.triangles[s].Length / 3;
        return count;
    }
}
