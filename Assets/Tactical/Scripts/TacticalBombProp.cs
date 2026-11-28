using UnityEngine;

namespace FpsTactical
{
    /// <summary>
    /// World object of the bomb: a small charge with a blinking light that lies where it was dropped or planted, beeps
    /// faster as the fuse burns down and flashes when it explodes. All geometry, light and sound are generated at runtime.
    /// </summary>
    public sealed class TacticalBombProp : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private static AudioClip beepClip;

        private Transform lamp;
        private Renderer lampRenderer;
        private Light lampLight;
        private AudioSource audioSource;
        private Material lampMaterial;
        private float fuse01;
        private bool planted;
        private bool exploded;
        private float nextBeep;
        private float blinkUntil;
        private float explodeTime;
        private Transform blast;
        private Light blastLight;

        public bool IsPlanted => planted;
        public bool HasExploded => exploded;

        public static TacticalBombProp Create(Vector3 position, bool plantedBomb)
        {
            var go = new GameObject(plantedBomb ? "TacticalBomb_Planted" : "TacticalBomb_Dropped");
            go.transform.position = position;
            var prop = go.AddComponent<TacticalBombProp>();
            prop.Build();
            prop.planted = plantedBomb;
            return prop;
        }

        private void Build()
        {
            Material body = TacticalVisuals.MakeMaterial(new Color(0.18f, 0.2f, 0.14f), 0.1f);
            Material strap = TacticalVisuals.MakeMaterial(new Color(0.08f, 0.08f, 0.08f), 0.1f);
            lampMaterial = TacticalVisuals.MakeMaterial(new Color(0.35f, 0.02f, 0.02f), 0f);

            TacticalVisuals.Box(transform, "Charge", new Vector3(0f, 0.09f, 0f), new Vector3(0.42f, 0.18f, 0.28f), body);
            TacticalVisuals.Box(transform, "StrapA", new Vector3(-0.1f, 0.095f, 0f), new Vector3(0.05f, 0.19f, 0.3f), strap);
            TacticalVisuals.Box(transform, "StrapB", new Vector3(0.1f, 0.095f, 0f), new Vector3(0.05f, 0.19f, 0.3f), strap);
            GameObject lampObject = TacticalVisuals.Box(transform, "Lamp", new Vector3(0f, 0.2f, 0f), new Vector3(0.1f, 0.05f, 0.1f), lampMaterial);
            lamp = lampObject.transform;
            lampRenderer = lampObject.GetComponent<Renderer>();

            var lightObject = new GameObject("LampLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            lampLight = lightObject.AddComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.color = new Color(1f, 0.15f, 0.1f);
            lampLight.range = 5f;
            lampLight.intensity = 0f;
            lampLight.shadows = LightShadows.None;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 4f;
            audioSource.maxDistance = 60f;
            audioSource.playOnAwake = false;
        }

        public void Plant(Vector3 position)
        {
            transform.position = position;
            gameObject.name = "TacticalBomb_Planted";
            planted = true;
            fuse01 = 0f;
            nextBeep = Time.time + 0.2f;
        }

        public void SetFuse01(float elapsed01)
        {
            fuse01 = Mathf.Clamp01(elapsed01);
        }

        public void Explode()
        {
            if (exploded)
                return;
            exploded = true;
            planted = false;
            explodeTime = Time.time;
            SetLamp(false);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Blast";
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = Vector3.up * 0.5f;
            sphere.GetComponent<Renderer>().sharedMaterial = TacticalVisuals.MakeMaterial(new Color(1f, 0.55f, 0.15f), 0f);
            blast = sphere.transform;

            blastLight = new GameObject("BlastLight").AddComponent<Light>();
            blastLight.transform.SetParent(transform, false);
            blastLight.transform.localPosition = Vector3.up * 2f;
            blastLight.type = LightType.Point;
            blastLight.color = new Color(1f, 0.6f, 0.25f);
            blastLight.range = 40f;
            blastLight.intensity = 8f;
            blastLight.shadows = LightShadows.None;

            audioSource.PlayOneShot(GetClip(70f, 0.9f, true), 1f);
        }

        public void Defuse()
        {
            planted = false;
            SetLamp(false);
            if (lampMaterial != null)
                lampMaterial.color = new Color(0.05f, 0.35f, 0.08f);
        }

        private void Update()
        {
            if (exploded)
            {
                float t = Time.time - explodeTime;
                if (blast != null)
                {
                    float size = Mathf.Lerp(0.5f, 22f, 1f - Mathf.Exp(-t * 4f));
                    blast.localScale = Vector3.one * size;
                    if (t > 0.6f)
                        Destroy(blast.gameObject);
                }

                if (blastLight != null)
                    blastLight.intensity = Mathf.Max(0f, 8f * (1f - t / 1.2f));
                return;
            }

            if (Time.time < blinkUntil && !planted)
                return;

            if (!planted)
            {
                SetLamp(false);
                return;
            }

            float interval = Mathf.Lerp(1f, 0.1f, fuse01 * fuse01);
            if (Time.time >= nextBeep)
            {
                nextBeep = Time.time + interval;
                blinkUntil = Time.time + 0.08f;
                SetLamp(true);
                audioSource.PlayOneShot(GetClip(880f, 0.07f, false), 0.8f);
            }
            else if (Time.time >= blinkUntil)
            {
                SetLamp(false);
            }
        }

        private void SetLamp(bool on)
        {
            if (lampLight != null)
                lampLight.intensity = on ? 2.5f : 0f;
            if (lampMaterial != null && planted)
                lampMaterial.color = on ? new Color(1f, 0.1f, 0.05f) : new Color(0.35f, 0.02f, 0.02f);
            if (lampRenderer != null)
                lampRenderer.enabled = true;
        }

        private static AudioClip GetClip(float frequency, float seconds, bool noise)
        {
            if (!noise && beepClip != null)
                return beepClip;

            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[samples];
            var rng = new System.Random(7);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = 1f - i / (float)samples;
                float wave = noise
                    ? ((float)rng.NextDouble() * 2f - 1f) * 0.6f + Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.4f
                    : Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.5f;
                data[i] = wave * envelope * envelope;
            }

            AudioClip clip = AudioClip.Create(noise ? "TacticalBlast" : "TacticalBeep", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            if (!noise)
                beepClip = clip;
            return clip;
        }

        private void OnDestroy()
        {
            if (lampMaterial != null)
                Destroy(lampMaterial);
        }
    }

    internal static class TacticalVisuals
    {
        public static Material MakeMaterial(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        public static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
