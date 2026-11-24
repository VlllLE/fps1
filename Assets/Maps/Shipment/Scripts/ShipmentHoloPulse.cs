using UnityEngine;

namespace CodMovementShooter.Maps.Shipment
{
    /// <summary>Subtle emission pulse and occasional flicker for holo signs (per-renderer, no material instancing).</summary>
    [RequireComponent(typeof(Renderer))]
    public class ShipmentHoloPulse : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private float pulseSpeed = 1.4f;
        [SerializeField, Range(0f, 1f)] private float pulseDepth = 0.25f;
        [SerializeField, Range(0f, 1f)] private float flickerChance = 0.015f;

        private Renderer cachedRenderer;
        private MaterialPropertyBlock block;
        private Color baseEmission = Color.white;
        private float phase;
        private float flickerUntil;

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            phase = Random.value * Mathf.PI * 2f;

            var shared = cachedRenderer.sharedMaterial;
            if (shared != null && shared.HasProperty(EmissionColorId))
                baseEmission = shared.GetColor(EmissionColorId);
        }

        private void Update()
        {
            if (cachedRenderer == null)
                return;

            float level = 1f - pulseDepth * 0.5f + Mathf.Sin(Time.time * pulseSpeed + phase) * pulseDepth * 0.5f;

            if (Time.time < flickerUntil)
                level *= 0.35f;
            else if (Random.value < flickerChance * Time.deltaTime * 10f)
                flickerUntil = Time.time + Random.Range(0.04f, 0.12f);

            cachedRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, baseEmission * level);
            cachedRenderer.SetPropertyBlock(block);
        }
    }
}
