using UnityEngine;

namespace LightEntityScene
{
    /// <summary>Tunables for the 64-capsule field. See <see cref="CapsuleFieldController"/>.</summary>
    [CreateAssetMenu(menuName = "LightEntityScene/Capsule Field Config", fileName = "CapsuleFieldConfig")]
    public class CapsuleFieldConfig : ScriptableObject
    {
        [Header("Placement (decided once, at Start)")]
        [Tooltip("Inset from the viewport edges when sampling floor positions, 0–0.4.")]
        [Range(0f, 0.4f)] public float viewportMargin = 0.08f;
        [Tooltip("Guard against a near-parallel ray missing the floor. Keep even with a straight-down cam in case it is re-aimed.")]
        public float maxRayDistance = 1000f;

        [Header("Scale (uniform, once)")]
        public float minScale = 0.25f;
        public float maxScale = 2.0f;
        [Tooltip("Rest capsules on the floor (y = scale) instead of sinking half into it.")]
        public bool pivotAtFloor = true;

        [Header("Even spacing (optional Poisson-style rejection)")]
        [Tooltip("Because positions never change, startup overlap is permanent. On: reject a sample within minSpacing of an already-placed capsule.")]
        public bool enforceSpacing = false;
        public float minSpacing = 1.5f;
        [Tooltip("Retries before accepting a crowded sample.")]
        public int maxPlacementAttempts = 30;
    }
}
