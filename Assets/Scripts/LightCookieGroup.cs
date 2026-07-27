using System.Collections.Generic;
using UnityEngine;

namespace LightEntityScene
{
    /// <summary>
    /// Gives each of the six <see cref="LightEntityController"/> spots its own animated
    /// cookie so the projected noise differs per light. A single shared cookie asset would
    /// look identical on every spot (the texture is rendered once, not per light), so this
    /// instead builds, per entity, a private seeded copy of the cookie material wrapped in a
    /// runtime <see cref="CustomRenderTexture"/>.
    ///
    /// Each material instance gets a unique <c>_Seed</c> (from the entity's index) which the
    /// proceduralCookie graph turns into a different 2-D region of its noise field, plus a
    /// shared <c>_Speed</c>. The CRT uses the Init-from-Material + Realtime-update combo that
    /// the graph needs to render (see the harness CLAUDE.md cookie note).
    ///
    /// Runtime only: it creates materials and textures, so it does nothing in edit mode.
    /// </summary>
    [DisallowMultipleComponent]
    public class LightCookieGroup : MonoBehaviour
    {
        [Tooltip("The six entities, in seed order. Leave empty to use the children in hierarchy order.")]
        [SerializeField] LightEntityController[] _entities;

        [Header("Cookie source")]
        [Tooltip("The proceduralCookie material (Shader Graph). Copied per light — the asset itself is never mutated.")]
        [SerializeField] Material _cookieMaterial;
        [Tooltip("Resolution of each per-light cookie render texture.")]
        [SerializeField] int _size = 256;

        [Header("Per-light parameters")]
        [Tooltip("Scroll speed written to every cookie's _Speed. Set < 0 to leave the material's own default.")]
        [SerializeField] float _speed = 0.1f;
        [Tooltip("Seed step between lights. Light i gets _Seed = i * this. Keep small so the noise field's ~289 repeat isn't an issue.")]
        [SerializeField] float _seedSpacing = 1f;

        // Shader Graph property reference names. References are CASE-SENSITIVE and Shader
        // Graph derives the default from the display name, so "_seed" (lowercase) and "_Seed"
        // can both occur depending on how the property was authored — try both, warn if
        // neither exists so a rename can never silently no-op the seeding again.
        static readonly string[] SeedNames  = { "_seed", "_Seed" };
        static readonly string[] SpeedNames = { "_speed", "_Speed" };

        // Everything we allocate, tracked so we can tear it down cleanly.
        readonly List<Material> _instances = new List<Material>();
        readonly List<CustomRenderTexture> _textures = new List<CustomRenderTexture>();
        readonly List<Light> _cookedLights = new List<Light>();

        void OnEnable()
        {
            if (!Application.isPlaying) return; // runtime resources only
            Build();
        }

        void OnDisable()
        {
            Teardown();
        }

        void Build()
        {
            if (_cookieMaterial == null)
            {
                Debug.LogWarning($"{nameof(LightCookieGroup)}: no cookie material assigned.", this);
                return;
            }

            var entities = ResolveEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                Light spot = entities[i] != null ? entities[i].Spot : null;
                if (spot == null) continue;

                // Private material copy — the shared asset is never touched, so each light
                // can carry its own seed.
                var mat = new Material(_cookieMaterial) { name = $"{_cookieMaterial.name} (seed {i})" };
                SetFloatByAnyName(mat, SeedNames, i * _seedSpacing, "seed");
                if (_speed >= 0f) SetFloatByAnyName(mat, SpeedNames, _speed, "speed");

                var crt = new CustomRenderTexture(_size, _size, RenderTextureFormat.ARGB32)
                {
                    name = $"Cookie CRT (seed {i})",
                    material = mat,
                    updateMode = CustomRenderTextureUpdateMode.Realtime,
                    initializationMode = CustomRenderTextureUpdateMode.Realtime,
                    initializationSource = CustomRenderTextureInitializationSource.Material,
                    initializationMaterial = mat,
                    wrapMode = TextureWrapMode.Clamp, // outside the disc stays black = no light leak
                    filterMode = FilterMode.Bilinear,
                };
                crt.Initialize();

                spot.cookie = crt;

                _instances.Add(mat);
                _textures.Add(crt);
                _cookedLights.Add(spot);
            }
        }

        void Teardown()
        {
            foreach (Light l in _cookedLights)
                if (l != null) l.cookie = null;
            _cookedLights.Clear();

            foreach (CustomRenderTexture t in _textures)
                if (t != null) { t.Release(); Destroy(t); }
            _textures.Clear();

            foreach (Material m in _instances)
                if (m != null) Destroy(m);
            _instances.Clear();
        }

        // Sets a float on the first reference name the material actually has. A wrong/renamed
        // reference is the classic "all cookies look identical" cause (every seed stays 0), so
        // fail loud rather than silent.
        void SetFloatByAnyName(Material mat, string[] names, float value, string label)
        {
            foreach (string n in names)
                if (mat.HasProperty(n)) { mat.SetFloat(n, value); return; }

            Debug.LogWarning(
                $"{nameof(LightCookieGroup)}: none of [{string.Join(", ", names)}] exist on " +
                $"'{mat.name}' — {label} not applied. Check the Shader Graph property's Reference name.",
                this);
        }

        LightEntityController[] ResolveEntities()
        {
            if (_entities != null && _entities.Length > 0) return _entities;
            return GetComponentsInChildren<LightEntityController>(includeInactive: true);
        }
    }
}
