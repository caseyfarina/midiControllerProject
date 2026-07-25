using UnityEngine;

namespace LightEntityScene
{
    /// <summary>Curve shaping applied to a smoothed 0–1 MIDI value.</summary>
    public enum ResponseCurve
    {
        /// <summary>Straight <c>Lerp(outputMin, outputMax, t)</c>. Rotation, range, angle.</summary>
        Linear,
        /// <summary><c>Pow(t, exponent)</c> then lerp. Stevens' power law — the right shape
        /// for perceived light intensity and loudness (default exponent 1.67).</summary>
        Perceptual,
        /// <summary>Console fader taper: true silence below <see cref="MuteThreshold"/>,
        /// then a dB-linear ramp <see cref="FloorDb"/>→<see cref="HeadroomDb"/>. Drone volume.</summary>
        Decibel,
        /// <summary>An arbitrary <see cref="AnimationCurve"/> tuned by ear, then lerp.</summary>
        Custom,
    }

    /// <summary>
    /// Turns a raw 0–1 MIDI value into a real, smoothed, curve-shaped parameter value.
    /// A plain <c>[Serializable]</c> class embedded as a field on whatever owns the
    /// parameter — intensity, spot angle, drone volume, scale bounds. Everything that
    /// maps a fader/knob to a value goes through one of these.
    ///
    /// Smoothing is a framerate-independent one-pole (analog slew-limiter feel): the
    /// *normalized input* is smoothed, then the curve is applied, so the time constant
    /// stays meaningful in fader-throw terms.
    /// </summary>
    [System.Serializable]
    public class MidiResponse
    {
        [SerializeField] ResponseCurve _curve = ResponseCurve.Linear;
        [SerializeField] float _outputMin = 0f;
        [SerializeField] float _outputMax = 1f;

        [Header("Smoothing (one-pole, framerate-independent)")]
        [SerializeField] bool _smoothing = true;
        [Tooltip("Seconds to settle ~63% of the way to a new value. Smaller = snappier.")]
        [Min(0.0001f)]
        [SerializeField] float _smoothingTimeConstant = 0.05f;

        [Header("Perceptual")]
        [Tooltip("Stevens' power-law exponent. 1.67 ≈ perceptually-linear loudness/brightness; 2.0 is a common practical alternative.")]
        [SerializeField] float _exponent = 1.67f;

        [Header("Decibel")]
        [SerializeField] float _floorDb = -60f;
        [SerializeField] float _headroomDb = 0f;
        [Range(0f, 0.2f)]
        [SerializeField] float _muteThreshold = 0.02f;

        [Header("Custom")]
        [SerializeField] AnimationCurve _customCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // Runtime state — normalized 0–1 except _current, which is the shaped output.
        float _target;
        float _smoothed;
        float _current;

        // ---- Configuration accessors (for ScriptableObject config and tests) ----
        public ResponseCurve Curve { get => _curve; set => _curve = value; }
        public float OutputMin { get => _outputMin; set => _outputMin = value; }
        public float OutputMax { get => _outputMax; set => _outputMax = value; }
        public bool Smoothing { get => _smoothing; set => _smoothing = value; }
        public float SmoothingTimeConstant { get => _smoothingTimeConstant; set => _smoothingTimeConstant = value; }
        public float Exponent { get => _exponent; set => _exponent = value; }
        public float FloorDb { get => _floorDb; set => _floorDb = value; }
        public float HeadroomDb { get => _headroomDb; set => _headroomDb = value; }
        public float MuteThreshold { get => _muteThreshold; set => _muteThreshold = value; }
        public AnimationCurve CustomCurve { get => _customCurve; set => _customCurve = value; }

        /// <summary>Last shaped value, without advancing the smoother.</summary>
        public float Current => _current;

        /// <summary>Set the desired value from a MIDI handler (0–1). Smoothing carries the rest.</summary>
        public void SetTarget(float normalized01) => _target = Mathf.Clamp01(normalized01);

        /// <summary>Jump straight to a value with no smoothing — init or preset recall.</summary>
        public void SnapTo(float normalized01)
        {
            _target = _smoothed = Mathf.Clamp01(normalized01);
            _current = Shape(_smoothed);
        }

        /// <summary>Advance the smoother by <paramref name="deltaTime"/> and return the shaped value.</summary>
        public float Tick(float deltaTime)
        {
            // One-pole: k = 1 - e^(-dt/τ). Exact at any dt, so the time constant does
            // not shift with frame rate. (The usual Lerp(_,_,speed*dt) idiom is not.)
            float k = _smoothing
                ? 1f - Mathf.Exp(-deltaTime / Mathf.Max(_smoothingTimeConstant, 1e-5f))
                : 1f;
            _smoothed = Mathf.Lerp(_smoothed, _target, k);
            _current = Shape(_smoothed);
            return _current;
        }

        float Shape(float t)
        {
            t = Mathf.Clamp01(t);
            switch (_curve)
            {
                case ResponseCurve.Linear:
                    return Mathf.Lerp(_outputMin, _outputMax, t);
                case ResponseCurve.Perceptual:
                    return Mathf.Lerp(_outputMin, _outputMax, Mathf.Pow(t, _exponent));
                case ResponseCurve.Decibel:
                    return Decibel(t);
                case ResponseCurve.Custom:
                    return Mathf.Lerp(_outputMin, _outputMax, _customCurve.Evaluate(t));
                default:
                    return Mathf.Lerp(_outputMin, _outputMax, t);
            }
        }

        // dB taper. Below the mute threshold it returns a true 0 (−60 dB is still
        // audible in a gallery; the threshold is what buys actual silence). Above it,
        // amplitude ramps dB-linearly and scales to OutputMax (OutputMin is not used —
        // the floor of a dB taper is silence, not a nonzero minimum).
        float Decibel(float t)
        {
            if (t <= _muteThreshold) return 0f;
            float db = Mathf.Lerp(_floorDb, _headroomDb, Mathf.InverseLerp(_muteThreshold, 1f, t));
            return _outputMax * Mathf.Pow(10f, db / 20f);
        }
    }
}
