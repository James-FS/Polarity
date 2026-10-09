using UnityEngine;

namespace Polarity.Config
{
    /// <summary>Reusable tuning values. Worlds copy them; runtime state never changes this asset.</summary>
    [CreateAssetMenu(fileName = "GameplayConfig", menuName = "Polarity/Gameplay Config")]
    public sealed class GameplayConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float magneticStrength = 20f;
        [SerializeField, Min(0.01f)] private float magneticRadius = 7f;
        [SerializeField, Min(0.01f)] private float markingDistance = 7f;
        [SerializeField, Min(0.01f)] private float maxMagneticAcceleration = 30f;
        [Tooltip("Global safety ceiling, separate from future walking/chasing speed.")]
        [SerializeField, Min(0.01f)] private float maxBodySpeed = 12f;
        [Tooltip("Only solid walls block magnetism; never include the Crate layer.")]
        [SerializeField] private LayerMask magneticBlockingLayers = 1 << 12;

        public Settings CreateRuntimeSettings() => new Settings(
            SafeValue(magneticStrength, 20f, 0f),
            SafeValue(magneticRadius, 7f, 0.01f),
            SafeValue(markingDistance, 7f, 0.01f),
            SafeValue(maxMagneticAcceleration, 30f, 0.01f),
            SafeValue(maxBodySpeed, 12f, 0.01f), magneticBlockingLayers.value);

        private static float SafeValue(float value, float fallback, float minimum)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(minimum, value);

        private void OnValidate()
        {
            Settings settings = CreateRuntimeSettings();
            magneticStrength = settings.MagneticStrength;
            magneticRadius = settings.MagneticRadius;
            markingDistance = settings.MarkingDistance;
            maxMagneticAcceleration = settings.MaxMagneticAcceleration;
            maxBodySpeed = settings.MaxBodySpeed;
        }

        public readonly struct Settings
        {
            public readonly float MagneticStrength;
            public readonly float MagneticRadius;
            public readonly float MarkingDistance;
            public readonly float MaxMagneticAcceleration;
            public readonly float MaxBodySpeed;
            public readonly int MagneticBlockingLayers;

            internal Settings(float strength, float radius, float markingDistance,
                float maxAcceleration, float maxSpeed, int blockingLayers)
            {
                MagneticStrength = strength;
                MagneticRadius = radius;
                MarkingDistance = markingDistance;
                MaxMagneticAcceleration = maxAcceleration;
                MaxBodySpeed = maxSpeed;
                MagneticBlockingLayers = blockingLayers;
            }
        }
    }
}
