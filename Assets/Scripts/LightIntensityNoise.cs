using UnityEngine;

namespace LightEntityScene
{
    /// <summary>
    /// Standalone flicker/wobble for a <see cref="Light"/>'s intensity. Drop it on any
    /// light (or point it at one) and it modulates the intensity every frame with smooth
    /// Perlin noise. Nothing here is MIDI-aware — it just rides on top of whatever set the
    /// intensity this frame.
    ///
    /// It runs in <c>LateUpdate</c>, so when a driver such as <see cref="LightEntityController"/>
    /// writes the intensity in <c>Update</c>, that fresh value is treated as the base and the
    /// noise is layered over it. In <see cref="Mode.Multiply"/> (the default) the base is scaled,
    /// so a light driven to zero stays perfectly dark — the wobble multiplies down to nothing.
    ///
    /// All controls live here in the inspector; they are not mapped to the MIDI controllers.
    /// </summary>
    [DisallowMultipleComponent]
    public class LightIntensityNoise : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>Scale the base intensity by (1 ± amplitude). Zero base stays dark.</summary>
            Multiply,
            /// <summary>Add ±amplitude (in intensity units) to the base. Wobbles even when off.</summary>
            Add
        }

        [Tooltip("Light to modulate. Defaults to a Light on this GameObject.")]
        [SerializeField] Light _light;

        [Header("Noise")]
        [Tooltip("Multiply scales the base intensity (dark stays dark); Add offsets it in raw units.")]
        [SerializeField] Mode _mode = Mode.Multiply;

        [Tooltip("Wobble depth. In Multiply mode this is a fraction: 0.05 = ±5%. In Add mode it is in raw intensity units.")]
        [Min(0f)]
        [SerializeField] float _amplitude = 0.05f;

        [Tooltip("Wobble speed in roughly cycles per second.")]
        [Min(0f)]
        [SerializeField] float _frequency = 9f;

        // Per-instance offset into the noise field so lights on the same rig don't wobble in lockstep.
        float _seed;

        // The un-noised intensity for this frame, and the value we last wrote. Comparing the two
        // lets us tell an external change (a driver, or a hand edit) from our own output, so the
        // noise never compounds on itself frame to frame.
        float _baseIntensity;
        float _lastApplied;

        void Awake()
        {
            if (_light == null) _light = GetComponent<Light>();
            _seed = Random.value * 1000f;
        }

        void OnEnable()
        {
            // Adopt whatever the light currently shows as the base so the first frame is clean.
            if (_light != null)
            {
                _baseIntensity = _light.intensity;
                _lastApplied = _baseIntensity;
            }
        }

        void OnDisable()
        {
            // Leave the light at its clean base rather than a frozen noisy value.
            if (_light != null && Mathf.Approximately(_light.intensity, _lastApplied))
                _light.intensity = _baseIntensity;
        }

        void LateUpdate()
        {
            if (_light == null) return;

            // If the intensity differs from what we wrote, something upstream set it this frame:
            // that value is the new base. If it matches our last output, the base is unchanged
            // (e.g. a static light), so keep the stored base instead of re-noising our own noise.
            if (!Mathf.Approximately(_light.intensity, _lastApplied))
                _baseIntensity = _light.intensity;

            // Perlin noise is [0,1]; remap to a signed [-1,1] wobble.
            float signal = Mathf.PerlinNoise(_seed, Time.time * _frequency) * 2f - 1f;

            float applied = _mode == Mode.Multiply
                ? _baseIntensity * (1f + _amplitude * signal)
                : _baseIntensity + _amplitude * signal;

            if (applied < 0f) applied = 0f;

            _light.intensity = applied;
            _lastApplied = applied;
        }
    }
}
