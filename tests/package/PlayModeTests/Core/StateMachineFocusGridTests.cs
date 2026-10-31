using System.Collections;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class StateMachineFocusGridTests
    {
        private const string TopLeft = "topLeft";
        private const string TopRight = "topRight";
        private const string BottomLeft = "bottomLeft";
        private const string BottomRight = "bottomRight";

        private static readonly string[] AllButtons = { TopLeft, TopRight, BottomLeft, BottomRight };

        private TestAssetLoadingManager m_testAssetLoadingManager;
        private MockLogger m_mockLogger;
        private File m_loadedFile;
        private Artboard m_artboard;
        private StateMachine m_stateMachine;
        private ViewModelInstance m_viewModel;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            m_testAssetLoadingManager = new TestAssetLoadingManager();
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            Asset riveAsset = null;
            string path = TestAssetReferences.riv_focus_grid_test;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                path,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {path}"));

            m_loadedFile = File.Load(riveAsset);
            m_artboard = m_loadedFile.Artboard("FocusGrid");
            Assert.IsNotNull(m_artboard, "FocusGrid artboard should exist");
            Assert.AreEqual(500f, m_artboard.Width, 0.1f);
            Assert.AreEqual(400f, m_artboard.Height, 0.1f);

            m_stateMachine = m_artboard.StateMachine();
            Assert.IsNotNull(m_stateMachine);

            ViewModel viewModel = m_artboard.DefaultViewModel;
            Assert.IsNotNull(viewModel, "FocusGrid should have a default view model");
            m_viewModel = viewModel.CreateDefaultInstance();
            m_stateMachine.BindViewModelInstance(m_viewModel);
            m_stateMachine.Advance(0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_loadedFile != null)
            {
                m_loadedFile.Dispose();
            }

            m_testAssetLoadingManager?.UnloadAllAssets();
        }

        [Test]
        public void ReportsFocusableTreeAndEmptyInitialFocus()
        {
            Assert.IsTrue(m_stateMachine.HasFocusNodes);
            Assert.IsFalse(m_stateMachine.HasFocus);
            Assert.IsFalse(m_stateMachine.FocusState.ExpectsKeyboardInput);
            Assert.IsFalse(m_stateMachine.TryGetPrimaryFocusBounds(out _));
            AssertNoButtonFocused();
        }

        [Test]
        public void FocusNext_WalksTreeOrderWithoutWrapping()
        {
            Assert.IsTrue(m_stateMachine.FocusNext());
            AdvanceAndAssertFocused(TopLeft);

            Assert.IsTrue(m_stateMachine.FocusNext());
            AdvanceAndAssertFocused(TopRight);

            Assert.IsTrue(m_stateMachine.FocusNext());
            AdvanceAndAssertFocused(BottomLeft);

            Assert.IsTrue(m_stateMachine.FocusNext());
            AdvanceAndAssertFocused(BottomRight);

            // This file uses the `Parent Scope` focus behavior. At the last item, Next does not wrap
            // or stay put, it clears focus so Unity can take over.
            Assert.IsFalse(m_stateMachine.FocusNext());
            m_stateMachine.Advance(0f);
            Assert.IsFalse(m_stateMachine.HasFocus);
            AssertNoButtonFocused();
        }

        [Test]
        public void FocusPrevious_WalksReverseTreeOrderWithoutWrapping()
        {
            Assert.IsTrue(m_stateMachine.FocusPrevious());
            AdvanceAndAssertFocused(BottomRight);

            Assert.IsTrue(m_stateMachine.FocusPrevious());
            AdvanceAndAssertFocused(BottomLeft);

            Assert.IsTrue(m_stateMachine.FocusPrevious());
            AdvanceAndAssertFocused(TopRight);

            Assert.IsTrue(m_stateMachine.FocusPrevious());
            AdvanceAndAssertFocused(TopLeft);

            // This file uses the `Parent Scope` focus behavior. At the first item, Previous clears focus instead of
            // wrapping or staying put.
            Assert.IsFalse(m_stateMachine.FocusPrevious());
            m_stateMachine.Advance(0f);
            Assert.IsFalse(m_stateMachine.HasFocus);
            AssertNoButtonFocused();
        }

        [Test]
        public void DirectionalFocus_FollowsGridLayout()
        {
            Assert.IsTrue(m_stateMachine.FocusNext());
            m_stateMachine.Advance(0f);

            Assert.IsFalse(m_stateMachine.FocusLeft());
            Assert.IsFalse(m_stateMachine.FocusUp());
            AdvanceAndAssertFocused(TopLeft);

            Assert.IsTrue(m_stateMachine.FocusRight());
            AdvanceAndAssertFocused(TopRight);

            Assert.IsTrue(m_stateMachine.FocusDown());
            AdvanceAndAssertFocused(BottomRight);

            Assert.IsTrue(m_stateMachine.FocusLeft());
            AdvanceAndAssertFocused(BottomLeft);

            Assert.IsTrue(m_stateMachine.FocusUp());
            AdvanceAndAssertFocused(TopLeft);
        }

        [Test]
        public void DirectionalFocus_DoesNothingWhenUnfocused()
        {
            Assert.IsFalse(m_stateMachine.FocusLeft());
            Assert.IsFalse(m_stateMachine.FocusRight());
            Assert.IsFalse(m_stateMachine.FocusUp());
            Assert.IsFalse(m_stateMachine.FocusDown());
            Assert.IsFalse(m_stateMachine.HasFocus);
            AssertNoButtonFocused();
        }

        [Test]
        public void ClearFocus_ClearsNativeAndViewModelState()
        {
            Assert.IsTrue(m_stateMachine.FocusNext());
            m_stateMachine.Advance(0f);
            Assert.IsTrue(m_stateMachine.HasFocus);

            m_stateMachine.ClearFocus();
            m_stateMachine.Advance(0f);

            Assert.IsFalse(m_stateMachine.HasFocus);
            Assert.IsFalse(m_stateMachine.TryGetPrimaryFocusBounds(out _));
            AssertNoButtonFocused();
        }

        [Test]
        public void PrimaryFocusBounds_MatchFocusedButton()
        {
            Assert.IsTrue(m_stateMachine.FocusNext());
            Assert.IsTrue(m_stateMachine.TryGetPrimaryFocusBounds(out AABB topLeftBounds));
            AssertBoundsNear(topLeftBounds, expectedMinX: 20f, expectedMinY: 20f, expectedWidth: 180f, expectedHeight: 140f);

            Assert.IsTrue(m_stateMachine.FocusRight());
            Assert.IsTrue(m_stateMachine.TryGetPrimaryFocusBounds(out AABB topRightBounds));
            AssertBoundsNear(topRightBounds, expectedMinX: 300f, expectedMinY: 20f, expectedWidth: 180f, expectedHeight: 140f);
        }

        [Test]
        public void Click_FocusesHitButton_AndIgnoresDecoy()
        {
            Click(110f, 90f);
            AdvanceAndAssertFocused(TopLeft);

            m_stateMachine.ClearFocus();
            m_stateMachine.Advance(0f);

            Click(250f, 200f);
            m_stateMachine.Advance(0f);
            Assert.IsFalse(m_stateMachine.HasFocus);
            AssertNoButtonFocused();

            Click(390f, 90f);
            AdvanceAndAssertFocused(TopRight);
        }

        [Test]
        public void DisablingCanFocus_RemovesButtonFromTraversal()
        {
            SetCanFocus(TopRight, false);
            m_stateMachine.Advance(0f);

            Assert.IsTrue(m_stateMachine.HasFocusNodes);
            Assert.IsTrue(m_stateMachine.FocusNext());
            AdvanceAndAssertFocused(TopLeft);

            Assert.IsTrue(m_stateMachine.FocusNext());
            AdvanceAndAssertFocused(BottomLeft);

            m_stateMachine.ClearFocus();
            m_stateMachine.Advance(0f);
            Assert.IsTrue(m_stateMachine.FocusNext());
            m_stateMachine.Advance(0f);
            AdvanceAndAssertFocused(TopLeft);

            // Sequential skip already covered above. Spatial right still has
            // BottomRight (it sits to the right of TopLeft), so this move
            // succeeds and just does not land on the disabled button.
            Assert.IsTrue(m_stateMachine.FocusRight());
            Assert.IsFalse(IsFocused(TopRight));
            AdvanceAndAssertFocused(BottomRight);
        }

        private void Click(float x, float y)
        {
            Vector2 point = new Vector2(x, y);
            m_stateMachine.PointerDown(point);
            m_stateMachine.PointerUp(point);
        }

        private void AdvanceAndAssertFocused(string expectedButton)
        {
            m_stateMachine.Advance(0f);
            Assert.IsTrue(m_stateMachine.HasFocus);
            Assert.IsTrue(IsFocused(expectedButton), $"{expectedButton}/isFocused should be true");
            foreach (string button in AllButtons)
            {
                if (button != expectedButton)
                {
                    Assert.IsFalse(IsFocused(button), $"{button}/isFocused should be false");
                }
            }
        }

        private void AssertNoButtonFocused()
        {
            foreach (string button in AllButtons)
            {
                Assert.IsFalse(IsFocused(button), $"{button}/isFocused should be false");
            }
        }

        private bool IsFocused(string button)
        {
            ViewModelInstanceBooleanProperty property =
                m_viewModel.GetProperty<ViewModelInstanceBooleanProperty>($"{button}/isFocused");
            Assert.IsNotNull(property, $"{button}/isFocused should exist");
            return property.Value;
        }

        private void SetCanFocus(string button, bool canFocus)
        {
            ViewModelInstanceBooleanProperty property =
                m_viewModel.GetProperty<ViewModelInstanceBooleanProperty>($"{button}/canFocus");
            Assert.IsNotNull(property, $"{button}/canFocus should exist");
            property.Value = canFocus;
        }

        private static void AssertBoundsNear(
            AABB bounds,
            float expectedMinX,
            float expectedMinY,
            float expectedWidth,
            float expectedHeight)
        {
            const float tolerance = 8f;
            Assert.AreEqual(expectedMinX, bounds.minX, tolerance, "minX");
            Assert.AreEqual(expectedMinY, bounds.minY, tolerance, "minY");
            Assert.AreEqual(expectedWidth, bounds.maxX - bounds.minX, tolerance, "width");
            Assert.AreEqual(expectedHeight, bounds.maxY - bounds.minY, tolerance, "height");
        }
    }
}
