using UnityEngine;

namespace Polarity.Config
{
    /// <summary>Reusable tuning values. Worlds copy them; runtime state never changes this asset.</summary>
    [CreateAssetMenu(fileName = "GameplayConfig", menuName = "Polarity/Gameplay Config")]
    public sealed class GameplayConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float magneticStrength = 20f;
        [SerializeField, Min(0.01f)] private float magneticRadius = 7f;
        [SerializeField, Min(0.01f)] private float maxMagneticAcceleration = 30f;
        [Tooltip("Global safety ceiling, separate from future walking/chasing speed.")]
        [SerializeField, Min(0.01f)] private float maxBodySpeed = 12f;
        [Tooltip("Only solid walls block magnetism; never include the Crate layer.")]
        [SerializeField] private LayerMask magneticBlockingLayers = 1 << 12;

        [SerializeField, Min(0.01f)] private float impactDamageThreshold = 4.5f;
        [SerializeField, Min(0f)] private float enemyDamageInterval = 0.35f;
        [SerializeField, Min(0f)] private float playerDamageInterval = 0.8f;
        [SerializeField, Min(1)] private int damagePerHit = 1;
        [SerializeField, Range(0.01f, 1f)] private float slowMotionScale = 0.3f;

        public Settings CreateRuntimeSettings() => new Settings(
            SafeValue(magneticStrength, 20f, 0f),
            SafeValue(magneticRadius, 7f, 0.01f),
            SafeValue(maxMagneticAcceleration, 30f, 0.01f),
            SafeValue(maxBodySpeed, 12f, 0.01f), magneticBlockingLayers.value,
            SafeValue(impactDamageThreshold,4.5f,0.01f),SafeValue(enemyDamageInterval,0.35f,0f),
            SafeValue(playerDamageInterval,0.8f,0f),Mathf.Max(1,damagePerHit), Mathf.Clamp(SafeValue(slowMotionScale,0.3f,0.01f),0.01f,1f));

        private static float SafeValue(float value, float fallback, float minimum)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(minimum, value);

        private void OnValidate()
        {
            Settings settings = CreateRuntimeSettings();
            magneticStrength = settings.MagneticStrength;
            magneticRadius = settings.MagneticRadius;
            maxMagneticAcceleration = settings.MaxMagneticAcceleration;
            maxBodySpeed = settings.MaxBodySpeed;
            impactDamageThreshold=settings.ImpactDamageThreshold; enemyDamageInterval=settings.EnemyDamageInterval;
            playerDamageInterval=settings.PlayerDamageInterval; damagePerHit=settings.DamagePerHit;
            slowMotionScale=settings.SlowMotionScale;
        }

        public readonly struct Settings
        {
            public readonly float MagneticStrength;
            public readonly float MagneticRadius;
            public readonly float MaxMagneticAcceleration;
            public readonly float MaxBodySpeed;
            public readonly int MagneticBlockingLayers;
            public readonly float ImpactDamageThreshold,EnemyDamageInterval,PlayerDamageInterval;
            public readonly int DamagePerHit;
            public readonly float SlowMotionScale;

            internal Settings(float strength, float radius,
                float maxAcceleration, float maxSpeed, int blockingLayers,float threshold,float enemyInterval,float playerInterval,int damage,float slowScale)
            {
                MagneticStrength = strength;
                MagneticRadius = radius;
                MaxMagneticAcceleration = maxAcceleration;
                MaxBodySpeed = maxSpeed;
                MagneticBlockingLayers = blockingLayers;
                ImpactDamageThreshold=threshold;EnemyDamageInterval=enemyInterval;PlayerDamageInterval=playerInterval;DamagePerHit=damage;SlowMotionScale=slowScale;
            }
        }
    }
}
