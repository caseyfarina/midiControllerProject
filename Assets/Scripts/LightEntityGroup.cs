using MidiFighter64;
using UnityEngine;

namespace LightEntityScene
{
    /// <summary>
    /// Drives the six entities' spot colours from one MIDI-Mix channel (8 by default).
    /// All hue work happens on a 1-D axis <c>a ∈ [0,1]</c> that runs from orange (a = 0)
    /// to light blue (a = 1) the <b>long way</b> — through red / magenta / violet, never
    /// green — matching the original ramp's look. Interpolating this axis (not raw RGB)
    /// keeps the midtones vivid.
    ///
    /// Channel-8 knobs (router rows are 1-based):
    ///   • Row 1 — hue centre. Min = orange, max = light blue; shared by all six.
    ///   • Row 2 — hue spread. The six fan out symmetrically around the centre, each
    ///     position clamped to [orange, blue]. Off-centre + wide spread bunches entities
    ///     against an endpoint — accepted, per design.
    ///   • Row 3 — saturation, shared by all six.
    ///
    /// Router events are static, so this hears every channel and early-outs unless the
    /// incoming channel matches <see cref="_midiChannel"/>. Before channel 8 is touched
    /// (and in edit mode) the serialized defaults below drive the look.
    /// </summary>
    [ExecuteAlways]
    public class LightEntityGroup : MonoBehaviour
    {
        [Tooltip("The six entities, in ramp order. Leave empty to use the children in hierarchy order.")]
        [SerializeField] LightEntityController[] _entities;

        [Header("MIDI")]
        [Tooltip("Which MIDI Mix channel's knobs drive hue centre / spread / saturation.")]
        [Range(1, 8)] [SerializeField] int _midiChannel = 8;

        [Header("Hue axis endpoints (HSV hue, 0–1)")]
        [Tooltip("a = 0 end. Orange.")]
        [Range(0f, 1f)] [SerializeField] float _hueOrange = 0.08f;
        [Tooltip("Signed hue travel from orange to the blue end. -0.53 goes the long way " +
                 "(down through red/magenta/violet, wrapping past 0) to land on light blue (~0.55).")]
        [SerializeField] float _hueSpanOrangeToBlue = -0.53f;
        [Tooltip("Value (HSV brightness) of the colours. Saturation is knob-driven; V is fixed.")]
        [Range(0f, 1f)] [SerializeField] float _value = 1.0f;
        [Tooltip("Top of the saturation knob's range. Knob 3's full throw spans 0 → this, so the whole travel is usable.")]
        [Range(0f, 1f)] [SerializeField] float _maxSaturation = 0.7f;

        [Header("Defaults (used in edit mode and before channel 8 is touched)")]
        [Tooltip("Hue centre on the orange(0)→blue(1) axis.")]
        [Range(0f, 1f)] [SerializeField] float _defaultCenter = 0.5f;
        [Tooltip("Full-width spread across the axis. 1 = the whole orange↔blue range when centred.")]
        [Range(0f, 1f)] [SerializeField] float _defaultSpread = 1.0f;
        [Tooltip("Actual saturation before knob 3 is touched. Capped at Max Saturation.")]
        [Range(0f, 1f)] [SerializeField] float _defaultSaturation = 0.7f;

        [Header("Response (mapping + smoothing per knob)")]
        [SerializeField] MidiResponse _centerResponse = new MidiResponse();
        [SerializeField] MidiResponse _spreadResponse = new MidiResponse();
        [SerializeField] MidiResponse _saturationResponse = new MidiResponse();

        void Awake()
        {
            ConfigureResponses();
            // Start at the serialized defaults so the rig looks right before any knob move.
            _centerResponse.SnapTo(_defaultCenter);
            _spreadResponse.SnapTo(_defaultSpread);
            // SnapTo takes a 0–1 knob position; the saturation knob maps 0→_maxSaturation,
            // so divide the desired saturation back out to find its knob position.
            _saturationResponse.SnapTo(_defaultSaturation / Mathf.Max(_maxSaturation, 1e-5f));
        }

        void Start() => Apply();

        void OnEnable()
        {
            if (!Application.isPlaying) return; // router only fires at runtime; ExecuteAlways calls this in edit mode too
            MidiMixRouter.OnKnob += HandleKnob;
        }

        void OnDisable()
        {
            if (!Application.isPlaying) return;
            MidiMixRouter.OnKnob -= HandleKnob;
        }

        void OnValidate()
        {
            // Live preview while authoring (ExecuteAlways runs this in edit mode).
            if (!Application.isPlaying) Apply();
        }

        void HandleKnob(int channel, int row, float value)
        {
            if (channel != _midiChannel) return;
            switch (row)
            {
                case 1: _centerResponse.SetTarget(value); break;
                case 2: _spreadResponse.SetTarget(value); break;
                case 3: _saturationResponse.SetTarget(value); break;
            }
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            float dt = Time.deltaTime;
            _centerResponse.Tick(dt);
            _spreadResponse.Tick(dt);
            _saturationResponse.Tick(dt);
            Apply();
        }

        /// <summary>Recompute and assign every entity's spot colour from the current knob state.</summary>
        public void Apply()
        {
            float center, spread, sat;
            if (Application.isPlaying)
            {
                center = _centerResponse.Current;
                spread = _spreadResponse.Current;
                sat    = _saturationResponse.Current;
            }
            else
            {
                center = _defaultCenter;
                spread = _defaultSpread;
                sat    = _defaultSaturation;
            }

            var entities = ResolveEntities();
            int count = entities.Length;
            for (int i = 0; i < count; i++)
            {
                // Fan the entities out symmetrically around the centre: u ∈ [-0.5, +0.5].
                float u = (count > 1) ? (float)i / (count - 1) - 0.5f : 0f;
                float a = Mathf.Clamp01(center + u * spread);
                Color c = Color.HSVToRGB(HueAt(a), sat, _value);

                Light spot = entities[i] != null ? entities[i].Spot : null;
                if (spot != null) spot.color = c;
            }
        }

        // Map the orange(0)→blue(1) axis onto an HSV hue, wrapping so the travel stays the
        // long way round the colour wheel.
        float HueAt(float a) => Mathf.Repeat(_hueOrange + Mathf.Clamp01(a) * _hueSpanOrangeToBlue, 1f);

        void ConfigureResponses()
        {
            Configure(_centerResponse, 1f);
            Configure(_spreadResponse, 1f);
            // Full knob throw spans 0 → _maxSaturation so the whole range is usable.
            Configure(_saturationResponse, _maxSaturation);
        }

        static void Configure(MidiResponse r, float outputMax)
        {
            r.Curve = ResponseCurve.Linear;
            r.OutputMin = 0f;
            r.OutputMax = outputMax;
            r.Smoothing = true;
            r.SmoothingTimeConstant = 0.05f;
        }

        LightEntityController[] ResolveEntities()
        {
            if (_entities != null && _entities.Length > 0) return _entities;
            return GetComponentsInChildren<LightEntityController>(includeInactive: true);
        }
    }
}
