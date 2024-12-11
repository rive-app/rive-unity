using NUnit.Framework;
using Rive.Components;
using UnityEngine;

namespace Rive.Tests
{
    public class RenderTargetStrategyTests
    {
        [Test]
        public void ShouldSkipClipping_WidgetAndPanelFillTargets_ReturnsTrue()
        {
            Vector2 widgetSize = new Vector2(100, 100);
            Vector2 containerSize = new Vector2(100, 100);
            Vector2Int panelAllocation = new Vector2Int(100, 100);
            Vector2Int targetSize = new Vector2Int(100, 100);

            bool result = RenderTargetStrategy.ShouldSkipClipping(
                widgetSize,
                containerSize,
                panelAllocation,
                targetSize
            );

            Assert.IsTrue(result, "Should skip clipping when everything is exactly filled");
        }

        [Test]
        public void ShouldSkipClipping_WidgetLargerThanTargets_ReturnsTrue()
        {
            Vector2 widgetSize = new Vector2(150, 150);
            Vector2 containerSize = new Vector2(100, 100);
            Vector2Int panelAllocation = new Vector2Int(100, 100);
            Vector2Int targetSize = new Vector2Int(100, 100);

            bool result = RenderTargetStrategy.ShouldSkipClipping(
                widgetSize,
                containerSize,
                panelAllocation,
                targetSize
            );

            Assert.IsTrue(result, "Should skip clipping when everything is overfilled");
        }

        [Test]
        public void ShouldSkipClipping_WidgetSmallerThanPanel_ReturnsFalse()
        {
            Vector2 widgetSize = new Vector2(90, 90);
            Vector2 containerSize = new Vector2(100, 100);
            Vector2Int panelAllocation = new Vector2Int(100, 100);
            Vector2Int targetSize = new Vector2Int(100, 100);

            bool result = RenderTargetStrategy.ShouldSkipClipping(
                widgetSize,
                containerSize,
                panelAllocation,
                targetSize
            );

            Assert.IsFalse(result, "Should check clipping when widget is smaller than panel");
        }

        [Test]
        public void ShouldSkipClipping_PanelSmallerThanTarget_ReturnsFalse()
        {
            Vector2 widgetSize = new Vector2(100, 100);
            Vector2 containerSize = new Vector2(100, 100);
            Vector2Int panelAllocation = new Vector2Int(90, 90);
            Vector2Int targetSize = new Vector2Int(100, 100);

            bool result = RenderTargetStrategy.ShouldSkipClipping(
                widgetSize,
                containerSize,
                panelAllocation,
                targetSize
            );

            Assert.IsFalse(result, "Should check clipping when panel is smaller than target");
        }

        [Test]
        public void ShouldSkipClipping_PartiallyFilled_ReturnsFalse()
        {
            Vector2 widgetSize = new Vector2(110, 90);  // Only width exceeds
            Vector2 containerSize = new Vector2(100, 100);
            Vector2Int panelAllocation = new Vector2Int(100, 100);
            Vector2Int targetSize = new Vector2Int(100, 100);

            bool result = RenderTargetStrategy.ShouldSkipClipping(
                widgetSize,
                containerSize,
                panelAllocation,
                targetSize
            );

            Assert.IsFalse(result, "Should check clipping when only partially filled");
        }
    }
}