using UnityEngine;

namespace LightEntityScene
{
    /// <summary>
    /// Assigns each child <see cref="LightEntityController"/>'s spot colour along a hue
    /// ramp from light blue to orange, taken the <b>ascending / long way</b>
    /// (blue → violet → magenta → red → orange, no green). Interpolates in HSV — a
    /// straight RGB lerp between these endpoints passes through desaturated grey.
    /// </summary>
    [ExecuteAlways]
    public class LightEntityGroup : MonoBehaviour
    {
        [Tooltip("The six entities, in ramp order. Leave empty to use the children in hierarchy order.")]
        [SerializeField] LightEntityController[] _entities;

        [Header("Hue ramp (HSV)")]
        [Tooltip("Start hue — light blue.")]
        [Range(0f, 1f)] [SerializeField] float _startH = 0.55f;
        [Tooltip("Signed hue span. +0.53 wraps through 1.0 to land on orange (~0.08) the long way. Negative gives the rainbow path (a different piece).")]
        [SerializeField] float _hueSpan = 0.53f;

        [Tooltip("Saturation / value at the blue end.")]
        [Range(0f, 1f)] [SerializeField] float _startS = 0.45f;
        [Range(0f, 1f)] [SerializeField] float _startV = 1.0f;
        [Tooltip("Saturation / value at the orange end. S ramps up so the violet/magenta middle stays vivid.")]
        [Range(0f, 1f)] [SerializeField] float _endS = 0.90f;
        [Range(0f, 1f)] [SerializeField] float _endV = 1.0f;

        void Start() => Apply();

        void OnValidate()
        {
            // Live preview while authoring (ExecuteAlways runs this in edit mode).
            if (!Application.isPlaying) Apply();
        }

        /// <summary>Recompute and assign every entity's spot colour from the ramp.</summary>
        public void Apply()
        {
            var entities = ResolveEntities();
            int count = entities.Length;
            for (int i = 0; i < count; i++)
            {
                float t = (count > 1) ? (float)i / (count - 1) : 0f;
                float h = Mathf.Repeat(Mathf.Lerp(_startH, _startH + _hueSpan, t), 1f);
                float s = Mathf.Lerp(_startS, _endS, t);
                float v = Mathf.Lerp(_startV, _endV, t);
                Color c = Color.HSVToRGB(h, s, v);

                Light spot = entities[i] != null ? entities[i].Spot : null;
                if (spot != null) spot.color = c;
            }
        }

        LightEntityController[] ResolveEntities()
        {
            if (_entities != null && _entities.Length > 0) return _entities;
            return GetComponentsInChildren<LightEntityController>(includeInactive: true);
        }
    }
}
