using UnityEngine;

namespace FpsMaps.Dome
{
    /// <summary>Gentle bob and spin for floating holo displays. Visual only; never adds colliders.</summary>
    [DisallowMultipleComponent]
    public sealed class DomeHoloFloat : MonoBehaviour
    {
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float bobSpeed = 0.8f;
        [SerializeField] private float phase;
        [SerializeField] private Vector3 spinDegreesPerSecond = Vector3.zero;

        private Vector3 baseLocalPosition;

        public float BobAmplitude => bobAmplitude;

        public void Configure(float amplitude, float speed, float phaseOffset, Vector3 spin)
        {
            bobAmplitude = amplitude;
            bobSpeed = speed;
            phase = phaseOffset;
            spinDegreesPerSecond = spin;
        }

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.time * bobSpeed + phase) * bobAmplitude;
            transform.localPosition = baseLocalPosition + new Vector3(0f, bob, 0f);

            if (spinDegreesPerSecond != Vector3.zero)
                transform.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
