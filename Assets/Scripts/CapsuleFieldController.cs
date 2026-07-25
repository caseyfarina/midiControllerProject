using System.Collections.Generic;
using MidiFighter64;
using UnityEngine;

namespace LightEntityScene
{
    /// <summary>
    /// A stable constellation of 64 capsules the performer reveals and hides with the
    /// MF64 pads. Placement (position + uniform scale) is decided <b>once, at Start</b>,
    /// per pad by <see cref="GridButton.linearIndex"/> (0–63) — a pad only toggles the
    /// visibility of *its* capsule, which always appears in the same spot.
    ///
    /// Pads are momentary Buttons: press reveals, release hides, no re-randomisation.
    /// LED feedback is left to <see cref="MidiFighterButtonRouter"/> (its
    /// <c>driveButtonLEDs</c> already mirrors Button-mode presses to the hardware), so
    /// this controller adds no redundant <see cref="MidiFighterOutput"/> calls.
    /// </summary>
    [DisallowMultipleComponent]
    public class CapsuleFieldController : MonoBehaviour
    {
        const int PadCount = 64;

        [Header("Scene references")]
        [Tooltip("The drone camera, used to sample floor positions inside its frame. Defaults to Camera.main.")]
        [SerializeField] Camera _camera;
        [Tooltip("Optional capsule prefab. If empty, a primitive Capsule is created per pad.")]
        [SerializeField] GameObject _capsulePrefab;
        [SerializeField] CapsuleFieldConfig _config;

        [Header("Placement (used when no config is assigned)")]
        [Range(0f, 0.4f)] [SerializeField] float _viewportMargin = 0.08f;
        [SerializeField] float _maxRayDistance = 1000f;
        [SerializeField] float _minScale = 0.25f;
        [SerializeField] float _maxScale = 2.0f;
        [SerializeField] bool _pivotAtFloor = true;
        [SerializeField] bool _enforceSpacing = false;
        [SerializeField] float _minSpacing = 1.5f;
        [SerializeField] int _maxPlacementAttempts = 30;

        readonly GameObject[] _capsules = new GameObject[PadCount];

        void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            ApplyConfig();
        }

        void Start() => BuildField();

        void OnEnable()
        {
            MidiFighterButtonRouter.OnButtonPress += HandlePress;
            MidiFighterButtonRouter.OnButtonRelease += HandleRelease;
        }

        void OnDisable()
        {
            MidiFighterButtonRouter.OnButtonPress -= HandlePress;
            MidiFighterButtonRouter.OnButtonRelease -= HandleRelease;
        }

        void ApplyConfig()
        {
            if (_config == null) return;
            _viewportMargin = _config.viewportMargin;
            _maxRayDistance = _config.maxRayDistance;
            _minScale = _config.minScale;
            _maxScale = _config.maxScale;
            _pivotAtFloor = _config.pivotAtFloor;
            _enforceSpacing = _config.enforceSpacing;
            _minSpacing = _config.minSpacing;
            _maxPlacementAttempts = _config.maxPlacementAttempts;
        }

        void BuildField()
        {
            if (_camera == null)
            {
                Debug.LogWarning("[CapsuleFieldController] No camera — cannot place capsules.");
                return;
            }

            var ground = new Plane(Vector3.up, Vector3.zero);
            var placed = new List<Vector3>(PadCount);

            for (int i = 0; i < PadCount; i++)
            {
                float scale = Random.Range(_minScale, _maxScale);
                Vector3 pos = SamplePosition(ground, placed);
                placed.Add(pos);

                // A Unity capsule is 2 units tall at scale 1 (half-height 1), so a
                // uniform scale s rests on the floor at y = s.
                if (_pivotAtFloor) pos.y = scale;

                GameObject cap = _capsulePrefab != null
                    ? Instantiate(_capsulePrefab, transform)
                    : GameObject.CreatePrimitive(PrimitiveType.Capsule);

                cap.transform.SetParent(transform, worldPositionStays: true);
                cap.transform.position = pos;
                cap.transform.localScale = Vector3.one * scale;
                cap.name = $"Capsule_{i:D2}";
                cap.SetActive(false);
                _capsules[i] = cap;
            }
        }

        Vector3 SamplePosition(Plane ground, List<Vector3> placed)
        {
            int attempts = _enforceSpacing ? Mathf.Max(1, _maxPlacementAttempts) : 1;
            Vector3 pos = Vector3.zero;

            for (int a = 0; a < attempts; a++)
            {
                var vp = new Vector3(
                    Random.Range(_viewportMargin, 1f - _viewportMargin),
                    Random.Range(_viewportMargin, 1f - _viewportMargin),
                    0f);
                Ray ray = _camera.ViewportPointToRay(vp);
                if (!ground.Raycast(ray, out float d) || d > _maxRayDistance) continue;

                pos = ray.GetPoint(d);
                if (!_enforceSpacing) break;

                bool clear = true;
                float sqrMin = _minSpacing * _minSpacing;
                foreach (var p in placed)
                {
                    if ((p - pos).sqrMagnitude < sqrMin) { clear = false; break; }
                }
                if (clear) break; // otherwise retry; last sample is accepted on timeout
            }
            return pos;
        }

        void HandlePress(GridButton btn, float velocity) => SetCapsule(btn.linearIndex, true);
        void HandleRelease(GridButton btn) => SetCapsule(btn.linearIndex, false);

        void SetCapsule(int index, bool visible)
        {
            if (index < 0 || index >= PadCount) return;
            var cap = _capsules[index];
            if (cap != null) cap.SetActive(visible);
        }
    }
}
