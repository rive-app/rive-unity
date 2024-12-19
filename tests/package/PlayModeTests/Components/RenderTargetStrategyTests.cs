using System.Collections;
using NUnit.Framework;
using Rive.Components;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class RenderTargetStrategyTests
    {

        private GameObject containerGO;
        private RectTransform containerRectTransform;
        private GameObject widgetGO;
        private RectTransform widgetRectTransform;

        /// <summary>
        /// Set up a container RectTransform and a child widget RectTransform before each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            containerGO = new GameObject("Container");
            containerRectTransform = containerGO.AddComponent<RectTransform>();

            // Anchor at top-left, pivot at (0,0) just for consistency;
            containerRectTransform.anchorMin = Vector2.zero;
            containerRectTransform.anchorMax = Vector2.zero;
            containerRectTransform.pivot = Vector2.zero;

            widgetGO = new GameObject("Widget");
            widgetRectTransform = widgetGO.AddComponent<RectTransform>();

            // Make the widget a child of the container to match widget/panel hierarchy
            widgetRectTransform.SetParent(containerRectTransform, false);
            widgetRectTransform.anchorMin = Vector2.zero;
            widgetRectTransform.anchorMax = Vector2.zero;
            widgetRectTransform.pivot = Vector2.zero;
        }


        [TearDown]
        public void TearDown()
        {
            Object.Destroy(widgetGO);
            Object.Destroy(containerGO);
        }

        [Test]
        public void ShouldSkipClipping_WidgetFillsTargetExactly_ReturnsTrue()
        {
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);



            bool result = RenderTargetStrategy.ShouldSkipClippingForWidget(
                widgetRectTransform,
                containerRectTransform
            );



            Assert.IsTrue(result, "Should skip clipping when the widget exactly fills the container");
        }

        [Test]
        public void ShouldSkipClipping_WidgetLargerThanTargets_ReturnsTrue()
        {
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 150);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 150);


            bool result = RenderTargetStrategy.ShouldSkipClippingForWidget(
                widgetRectTransform,
                containerRectTransform
            );

            Assert.IsTrue(result, "Should skip clipping when widget completely overflows target");
        }

        [Test]
        public void ShouldSkipClipping_WidgetSmallerThanPanel_ReturnsFalse()
        {
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 90);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 90);


            bool result = RenderTargetStrategy.ShouldSkipClippingForWidget(
                widgetRectTransform,
                containerRectTransform
            );

            Assert.IsFalse(result, "Should not skip clipping when widget doesn't fully cover container");
        }



        [Test]
        public void ShouldSkipClipping_PartiallyFilled_ReturnsFalse()
        {
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
            // Only width is bigger
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 110);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 90);

            bool result = RenderTargetStrategy.ShouldSkipClippingForWidget(
                widgetRectTransform,
                containerRectTransform
            );

            Assert.IsFalse(result, "Should check clipping when only part of the container is covered");
        }

        [Test]
        public void ShouldSkipClipping_WithRotation_ReturnsFalseWhenNotFullyCovered()
        {
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);

            widgetRectTransform.pivot = new Vector2(0.5f, 0.5f);
            widgetRectTransform.anchoredPosition = new Vector2(50f, 50f);

            // Set to 100x100 and rotate 45 degrees - this will NOT fully cover
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
            widgetRectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);


            bool result = RenderTargetStrategy.ShouldSkipClippingForWidget(
                widgetRectTransform,
                containerRectTransform
            );


            Assert.IsFalse(result, "A 100x100 widget rotated 45° should not be considered as covering the container due to gaps");
        }

        [Test]
        public void ShouldSkipClipping_WithRotationAndScale_ReturnsTrueWhenFullyCovered()
        {
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);

            widgetRectTransform.pivot = new Vector2(0.5f, 0.5f);
            widgetRectTransform.anchoredPosition = new Vector2(50f, 50f);

            // Set to 100x100, scale by 1.5, and rotate 45 degrees - this WILL fully cover
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
            widgetRectTransform.localScale = new Vector3(1.5f, 1.5f, 1f);
            widgetRectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);


            bool result = RenderTargetStrategy.ShouldSkipClippingForWidget(
                widgetRectTransform,
                containerRectTransform
            );

            Assert.IsTrue(result, "A 100x100 widget scaled 1.5x and rotated 45° should fully cover the container");
        }

        [Test]
        public void DoesWidgetHavePointOutsidePanel_FullyInside_ReturnsFalse()
        {
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);

            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            widgetRectTransform.anchoredPosition = new Vector2(25, 25);

            bool result = RenderTargetStrategy.DoesWidgetHavePointOutsidePanel(
                containerRectTransform,
                widgetRectTransform
            );

            Assert.IsFalse(result, "Widget fully inside container should return false");
        }

        [Test]
        public void DoesWidgetHavePointOutsidePanel_PartiallyOutside_ReturnsTrue()
        {
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);

            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            widgetRectTransform.anchoredPosition = new Vector2(80, 80); // Partially outside

            bool result = RenderTargetStrategy.DoesWidgetHavePointOutsidePanel(
                containerRectTransform,
                widgetRectTransform
            );

            Assert.IsTrue(result, "Widget partially outside container should return true");
        }

        [Test]
        public void DoesWidgetHavePointOutsidePanel_RotatedPartiallyOutside_ReturnsTrue()
        {
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);

            widgetRectTransform.pivot = new Vector2(0.5f, 0.5f);
            widgetRectTransform.anchoredPosition = new Vector2(50, 50);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
            widgetRectTransform.localRotation = Quaternion.Euler(0, 0, 45); // 45-degree rotation

            bool result = RenderTargetStrategy.DoesWidgetHavePointOutsidePanel(
                containerRectTransform,
                widgetRectTransform
            );

            Assert.IsTrue(result, "Rotated widget extending outside container should return true");
        }

        [Test]
        public void DoesWidgetHavePointOutsidePanel_ScaledAndRotated_FullyInside_ReturnsFalse()
        {
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            containerRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);

            widgetRectTransform.pivot = new Vector2(0.5f, 0.5f);
            widgetRectTransform.anchoredPosition = new Vector2(50, 50);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
            widgetRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            widgetRectTransform.localScale = new Vector3(0.5f, 0.5f, 1);
            widgetRectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            bool result = RenderTargetStrategy.DoesWidgetHavePointOutsidePanel(
                containerRectTransform,
                widgetRectTransform
            );

            Assert.IsFalse(result, "Scaled and rotated widget fully inside container should return false");
        }


    }
}