using MidiFighter64;
using UnityEngine;

namespace LightEntityScene
{
    /// <summary>
    /// One MIDI-Mix channel drives one spot light + drone. Knob rows 1–3 steer the
    /// spot's yaw / range / spot-angle; the channel fader raises both intensity and
    /// drone volume. The entity sits at the origin (0, 0.5, 0) with the six siblings;
    /// they separate only by their individual yaw.
    ///
    /// All router events are static, so every instance hears every channel — each
    /// handler early-outs unless the incoming channel matches <see cref="midiChannel"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class LightEntityController : MonoBehaviour
    {
        [Tooltip("Which MIDI Mix channel (1–8) drives this entity. Instances 1–6 → channels 1–6.")]
        [Range(1, 8)]
        public int midiChannel = 1;

        [Header("Scene references")]
        [Tooltip("The child Spot Light. Its local X (pitch) is fixed; local Y (yaw) is knob-driven.")]
        [SerializeField] Light _spot;
        [Tooltip("Transform whose local rotation carries pitch+yaw. Defaults to the spot's own transform.")]
        [SerializeField] Transform _spotPivot;
        [SerializeField] AudioSource _drone;
        [Tooltip("The drone camera. Range and pitch are derived from its floor footprint. Defaults to Camera.main.")]
        [SerializeField] Camera _camera;

        [Header("Framing (derived from the camera at startup, not performed)")]
        [Tooltip("Computed downward pitch (spot local X) that aims the beam at the far edge of the visible floor so the cone rakes along it. Yaw is applied on top. Overwritten at Awake — the serialized value is only a fallback with no camera.")]
        [SerializeField] float _defaultPitch = 2f;

        [Header("Responses (mapping + smoothing per parameter)")]
        [SerializeField] MidiResponse _yawResponse = new MidiResponse();
        [SerializeField] MidiResponse _angleResponse = new MidiResponse();
        [SerializeField] MidiResponse _intensityResponse = new MidiResponse();
        [SerializeField] MidiResponse _volumeResponse = new MidiResponse();

        [Header("Config")]
        [Tooltip("Optional. When assigned, its ranges/curves/smoothing overwrite the responses above at startup so all six entities stay identical.")]
        [SerializeField] LightEntityConfig _config;

        /// <summary>The spot light this entity drives (read by <see cref="LightEntityGroup"/> for hue).</summary>
        public Light Spot => _spot;

        void Awake()
        {
            if (_spotPivot == null && _spot != null) _spotPivot = _spot.transform;
            ConfigureResponses();
            ConfigureFraming(); // pins range + computes pitch from the camera

            // Start dark and silent — nothing blasts on load before the first fader move.
            _yawResponse.SnapTo(0f);
            _angleResponse.SnapTo(0f);
            _intensityResponse.SnapTo(0f);
            _volumeResponse.SnapTo(0f);
            Apply();
        }

        void OnEnable()
        {
            MidiMixRouter.OnKnob += HandleKnob;
            MidiMixRouter.OnChannelFader += HandleFader;
        }

        void OnDisable()
        {
            MidiMixRouter.OnKnob -= HandleKnob;
            MidiMixRouter.OnChannelFader -= HandleFader;
        }

        void HandleKnob(int channel, int row, float value)
        {
            if (channel != midiChannel) return;
            switch (row)
            {
                case 1: _yawResponse.SetTarget(value); break;
                // row 2 (was Range) is now free — range is pinned to the camera frame.
                case 3: _angleResponse.SetTarget(value); break;
            }
        }

        void HandleFader(int channel, float value)
        {
            if (channel != midiChannel) return;
            // One fader drives two parameters, each with its own curve/smoothing.
            _intensityResponse.SetTarget(value);
            _volumeResponse.SetTarget(value);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _yawResponse.Tick(dt);
            _angleResponse.Tick(dt);
            _intensityResponse.Tick(dt);
            _volumeResponse.Tick(dt);
            Apply();
        }

        void Apply()
        {
            // Pitch and yaw go into one quaternion so they can't fight.
            if (_spotPivot != null)
                _spotPivot.localRotation = Quaternion.Euler(_defaultPitch, _yawResponse.Current, 0f);

            if (_spot != null)
            {
                // range is pinned in ConfigureFraming; not driven per-frame.
                _spot.spotAngle = _angleResponse.Current;
                _spot.intensity = _intensityResponse.Current;
            }

            if (_drone != null)
                _drone.volume = _volumeResponse.Current;
        }

        // Applies config (or sane hardcoded defaults) to the five responses. Runs at
        // Awake so an entity behaves correctly even before an asset is authored.
        void ConfigureResponses()
        {
            LightEntityConfig c = _config;

            Configure(_yawResponse, ResponseCurve.Linear, 0f,
                      c ? c.yawMax : 360f, c ? c.knobSmoothing : 0.02f);
            Configure(_angleResponse, ResponseCurve.Linear,
                      c ? c.spotAngleMin : 1f, c ? c.spotAngleMax : 179f, c ? c.knobSmoothing : 0.02f);

            _intensityResponse.Curve = ResponseCurve.Perceptual;
            _intensityResponse.OutputMin = 0f;
            _intensityResponse.OutputMax = c ? c.maxIntensity : 1000f;
            _intensityResponse.Exponent = c ? c.intensityExponent : 1.67f;
            _intensityResponse.Smoothing = true;
            _intensityResponse.SmoothingTimeConstant = c ? c.lightSmoothing : 0.05f;

            _volumeResponse.Curve = ResponseCurve.Decibel;
            _volumeResponse.OutputMin = 0f;
            _volumeResponse.OutputMax = 1f;
            _volumeResponse.FloorDb = c ? c.floorDb : -60f;
            _volumeResponse.HeadroomDb = c ? c.headroomDb : 0f;
            _volumeResponse.MuteThreshold = c ? c.muteThreshold : 0.02f;
            _volumeResponse.Smoothing = true;
            _volumeResponse.SmoothingTimeConstant = c ? c.audioSmoothing : 0.03f;
        }

        // Range and pitch are derived from the drone camera, not performed. The camera
        // looks straight down, so its floor footprint is centred on the coincident
        // emitters: range is pinned just past the farthest visible floor point, and pitch
        // aims the beam at that far edge so the cone rakes along the floor (grazing light
        // reads far better from directly above). Recomputed once at Awake — the camera is
        // fixed, so there is nothing to track per-frame.
        void ConfigureFraming()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || _spot == null) return;

            LightEntityConfig c = _config;
            float margin  = c ? c.rangeMargin : 3f;
            float aimFrac = c ? c.pitchAimFraction : 1f;

            float camHeight = _camera.transform.position.y; // floor at y = 0
            float halfV = camHeight * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfH = halfV * _camera.aspect;
            float maxViewDist = Mathf.Sqrt(halfV * halfV + halfH * halfH); // centre → far corner

            float emitterHeight = Mathf.Max(0.01f, transform.position.y);  // ~0.5 m above floor
            float aim = Mathf.Max(0.01f, maxViewDist * Mathf.Clamp01(aimFrac));

            _spot.range = maxViewDist + margin;
            _defaultPitch = Mathf.Atan2(emitterHeight, aim) * Mathf.Rad2Deg;
        }

        static void Configure(MidiResponse r, ResponseCurve curve, float min, float max, float tau)
        {
            r.Curve = curve;
            r.OutputMin = min;
            r.OutputMax = max;
            r.Smoothing = true;
            r.SmoothingTimeConstant = tau;
        }
    }
}
