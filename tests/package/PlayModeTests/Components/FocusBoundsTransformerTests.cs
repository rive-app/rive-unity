using NUnit.Framework;
using Rive.Components;
using UnityEngine;

namespace Rive.Tests
{
    public class FocusBoundsTransformerTests
    {
        private GameObject m_root;
        private RectTransform m_panel;
        private RectTransform m_widget;

        [SetUp]
        public void Setup()
        {
            m_root = new GameObject("FocusBoundsPanel", typeof(RectTransform));
            m_panel = (RectTransform)m_root.transform;
            m_panel.sizeDelta = new Vector2(400f, 200f);
            var widgetObj = new GameObject("FocusBoundsWidget", typeof(RectTransform));
            m_widget = (RectTransform)widgetObj.transform;
            m_widget.SetParent(m_panel, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_root);
        }

        [Test]
        public void LayoutFit_UsesTheFrameDrawingUses()
        {
            // A 200 x 100 artboard at scale 2 fills the 400 x 200 widget, as drawing places it.
            m_widget.sizeDelta = new Vector2(400f, 200f);
            var bounds = new AABB(0f, 25f, 50f, 75f);

            Assert.IsTrue(FocusBoundsTransformer.TryTransformToPanel(
                bounds, new Vector2(200f, 100f), Fit.Layout, Alignment.Center, 2f, m_widget, m_panel, out Rect rect));

            // Vertically centered bounds, so the backend's Y flip doesn't change the answer.
            AssertRect(new Rect(-200f, -50f, 100f, 100f), rect);
        }

        [Test]
        public void RotatedWidget_BoundsEveryCorner()
        {
            m_widget.sizeDelta = new Vector2(100f, 100f);
            m_widget.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Assert.IsTrue(FocusBoundsTransformer.TryTransformToPanel(
                new AABB(0f, 0f, 100f, 100f), new Vector2(100f, 100f), Fit.Fill, Alignment.Center, 1f, m_widget, m_panel, out Rect rect));

            float half = 100f * Mathf.Sqrt(2f) / 2f;
            AssertRect(new Rect(-half, -half, 2f * half, 2f * half), rect);
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.xMin, actual.xMin, 0.01f, $"xMin of {actual}");
            Assert.AreEqual(expected.yMin, actual.yMin, 0.01f, $"yMin of {actual}");
            Assert.AreEqual(expected.width, actual.width, 0.01f, $"width of {actual}");
            Assert.AreEqual(expected.height, actual.height, 0.01f, $"height of {actual}");
        }
    }
}
