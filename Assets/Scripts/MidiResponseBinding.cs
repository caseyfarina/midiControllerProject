using MidiFighter64;
using UnityEngine;
using UnityEngine.Events;

namespace LightEntityScene
{
    /// <summary>
    /// Low-code surface for a single MIDI-Mix control → one <see cref="MidiResponse"/> →
    /// a <see cref="UnityEvent{T}"/>. Pick a source (knob / channel fader / master
    /// fader), a channel/row, wire the event in the Inspector. For one-off patches and
    /// students; the six <see cref="LightEntityController"/>s use <see cref="MidiResponse"/>
    /// directly instead.
    /// </summary>
    public class MidiResponseBinding : MonoBehaviour
    {
        public enum Source { Knob, ChannelFader, MasterFader }

        [System.Serializable] public class FloatEvent : UnityEvent<float> { }

        [SerializeField] Source _source = Source.ChannelFader;
        [Tooltip("MIDI Mix channel (1–8). Ignored for the master fader.")]
        [Range(1, 8)] [SerializeField] int _channel = 1;
        [Tooltip("Knob row (1–3). Only used when Source = Knob.")]
        [Range(1, 3)] [SerializeField] int _knobRow = 1;

        [SerializeField] MidiResponse _response = new MidiResponse();

        [Tooltip("Invoked every frame with the smoothed, shaped value.")]
        [SerializeField] FloatEvent _onValue;

        /// <summary>The shaped value from the last frame.</summary>
        public float Value => _response.Current;

        void Awake() => _response.SnapTo(0f);

        void OnEnable()
        {
            switch (_source)
            {
                case Source.Knob: MidiMixRouter.OnKnob += HandleKnob; break;
                case Source.ChannelFader: MidiMixRouter.OnChannelFader += HandleChannelFader; break;
                case Source.MasterFader: MidiMixRouter.OnMasterFader += HandleMasterFader; break;
            }
        }

        void OnDisable()
        {
            switch (_source)
            {
                case Source.Knob: MidiMixRouter.OnKnob -= HandleKnob; break;
                case Source.ChannelFader: MidiMixRouter.OnChannelFader -= HandleChannelFader; break;
                case Source.MasterFader: MidiMixRouter.OnMasterFader -= HandleMasterFader; break;
            }
        }

        void HandleKnob(int channel, int row, float value)
        {
            if (channel == _channel && row == _knobRow) _response.SetTarget(value);
        }

        void HandleChannelFader(int channel, float value)
        {
            if (channel == _channel) _response.SetTarget(value);
        }

        void HandleMasterFader(float value) => _response.SetTarget(value);

        void Update() => _onValue?.Invoke(_response.Tick(Time.deltaTime));
    }
}
