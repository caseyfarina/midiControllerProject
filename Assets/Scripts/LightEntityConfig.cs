using UnityEngine;

namespace LightEntityScene
{
    /// <summary>
    /// Tunables shared by every <see cref="LightEntityController"/>: control ranges,
    /// intensity/volume shaping, and per-parameter smoothing. One asset drives all six
    /// entities so they read as one instrument. Applied to the embedded
    /// <see cref="MidiResponse"/> instances at startup.
    /// </summary>
    [CreateAssetMenu(menuName = "LightEntityScene/Light Entity Config", fileName = "LightEntityConfig")]
    public class LightEntityConfig : ScriptableObject
    {
        [Header("Knob ranges (Linear)")]
        [Tooltip("Knob row 1 → spot local Y rotation, 0..this, degrees.")]
        public float yawMax = 360f;
        [Tooltip("Knob row 3 → spot Spot Angle, degrees. (Knob row 2 is unused — range is pinned to the camera frame.)")]
        public float spotAngleMin = 1f;
        public float spotAngleMax = 179f;

        [Header("Framing (range + pitch derived from the drone camera)")]
        [Tooltip("Metres added past the farthest visible floor point when pinning Range, so the cone reaches the frame edge.")]
        public float rangeMargin = 3f;
        [Tooltip("Where the beam aims along the max view distance: 1 = far corner (shallowest pitch, most floor spread), lower = nearer/steeper.")]
        [Range(0.1f, 1f)] public float pitchAimFraction = 1f;

        [Header("Intensity (Perceptual)")]
        [Tooltip("Fader → spot Intensity. URP spot lights use physical units, so useful values are large — the shipped test lights sat at 250–1100. Tune in-scene against the drone cam.")]
        public float maxIntensity = 1000f;
        public float intensityExponent = 1.67f;

        [Header("Drone volume (Decibel)")]
        public float floorDb = -60f;
        public float headroomDb = 0f;
        [Range(0f, 0.2f)] public float muteThreshold = 0.02f;

        [Header("Smoothing time constants (seconds)")]
        [Tooltip("Yaw / range / angle knobs.")]
        public float knobSmoothing = 0.02f;
        [Tooltip("Spot intensity.")]
        public float lightSmoothing = 0.05f;
        [Tooltip("Drone volume.")]
        public float audioSmoothing = 0.03f;
    }
}
