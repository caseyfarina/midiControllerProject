using NUnit.Framework;
using UnityEngine;

namespace LightEntityScene.Tests
{
    public class MidiResponseTests
    {
        static MidiResponse Linear(float tau)
            => new MidiResponse
            {
                Curve = ResponseCurve.Linear,
                OutputMin = 0f,
                OutputMax = 1f,
                Smoothing = true,
                SmoothingTimeConstant = tau,
            };

        // The one-pole k = 1 - e^(-dt/τ) makes the settle depend only on elapsed time,
        // not on how many steps that time is chopped into. Stepping 1 s at 30 fps and at
        // 144 fps must land on the same value — the property gallery hardware relies on.
        [Test]
        public void Smoothing_IsFramerateIndependent()
        {
            var slow = Linear(0.3f);
            var fast = Linear(0.3f);
            slow.SnapTo(0f);
            fast.SnapTo(0f);
            slow.SetTarget(1f);
            fast.SetTarget(1f);

            for (int i = 0; i < 30; i++) slow.Tick(1f / 30f);   // 1.0 s total
            for (int i = 0; i < 144; i++) fast.Tick(1f / 144f); // 1.0 s total

            Assert.AreEqual(slow.Current, fast.Current, 1e-4f,
                "One-pole smoothing must settle identically regardless of frame rate.");
            // And it must actually be mid-throw, not saturated — otherwise the test is trivial.
            Assert.That(slow.Current, Is.GreaterThan(0.9f).And.LessThan(0.99f));
        }

        [Test]
        public void Decibel_ReturnsZeroBelowThreshold_And_OutputMaxAtOne()
        {
            var r = new MidiResponse
            {
                Curve = ResponseCurve.Decibel,
                OutputMax = 5f,
                Smoothing = false, // Tick jumps straight to target so we read Shape() directly
                MuteThreshold = 0.02f,
                FloorDb = -60f,
                HeadroomDb = 0f,
            };

            r.SetTarget(0f);
            Assert.AreEqual(0f, r.Tick(1f), 1e-6f, "t = 0 must be exact silence.");

            r.SetTarget(0.01f); // below the 0.02 threshold
            Assert.AreEqual(0f, r.Tick(1f), 1e-6f, "Below the mute threshold must be exact 0.");

            r.SetTarget(1f);
            Assert.AreEqual(5f, r.Tick(1f), 1e-4f, "t = 1 must reach OutputMax.");
        }

        [Test]
        public void Perceptual_IsMonotonicOnUnitInterval()
        {
            var r = new MidiResponse
            {
                Curve = ResponseCurve.Perceptual,
                OutputMin = 0f,
                OutputMax = 10f,
                Smoothing = false,
                Exponent = 1.67f,
            };

            float prev = float.NegativeInfinity;
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                r.SetTarget(t);
                float v = r.Tick(1f);
                Assert.GreaterOrEqual(v, prev - 1e-5f, $"Perceptual must not decrease (t = {t}).");
                prev = v;
            }

            // Endpoints land where Linear would.
            r.SetTarget(0f); Assert.AreEqual(0f, r.Tick(1f), 1e-5f);
            r.SetTarget(1f); Assert.AreEqual(10f, r.Tick(1f), 1e-5f);
        }

        [Test]
        public void SnapTo_JumpsWithoutSmoothing()
        {
            var r = Linear(0.5f);
            r.SnapTo(1f);
            Assert.AreEqual(1f, r.Current, 1e-5f, "SnapTo must bypass smoothing.");
        }
    }
}
