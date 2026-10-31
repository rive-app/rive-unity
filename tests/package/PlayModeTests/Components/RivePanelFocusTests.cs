using System.Collections.Generic;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;

namespace Rive.Tests
{
    public class RivePanelFocusTests
    {
        private RivePanel m_panel;
        private FakeFocusProvider m_provider;
        private MockLogger m_mockLogger;
        private PanelFocusController FocusController => m_panel.FocusController;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            m_panel = RivePanelTestUtils.CreatePanel("FocusPanel");
            m_panel.SetDimensions(new Vector2(400, 200));
            var canvasObj = new GameObject("FocusTestCanvas");
            canvasObj.AddComponent<Canvas>();
            m_panel.transform.SetParent(canvasObj.transform, false);
            m_provider = new FakeFocusProvider();
            FocusController.FocusProvider = m_provider;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_panel != null)
            {
                Canvas canvas = m_panel.GetComponentInParent<Canvas>();
                Object.Destroy(m_panel.gameObject);
                if (canvas != null)
                {
                    Object.Destroy(canvas.gameObject);
                }
            }
        }

        [Test]
        public void PanelWithNoEligibleWidgets_IsNotFocusable()
        {
            Assert.IsFalse(FocusController.HasFocusableContent);
            Assert.IsFalse(m_provider.IsFocusable);
            Assert.IsFalse(FocusController.TryMoveFocus(FocusNavigationDirection.Next));
        }

        [Test]
        public void AddedEligibleWidget_UpdatesFocusability()
        {
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();

            Assert.IsTrue(FocusController.HasFocusableContent);
            Assert.IsTrue(m_provider.IsFocusable);
        }

        [Test]
        public void DisabledRemovedOrIneligibleWidget_IsSkipped()
        {
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            Assert.IsTrue(FocusController.HasFocusableContent);

            widget.MockEligible = false;
            TickPanel();
            Assert.IsFalse(FocusController.HasFocusableContent);

            widget.MockEligible = true;
            widget.gameObject.SetActive(false);
            TickPanel();
            Assert.IsFalse(FocusController.HasFocusableContent);
        }

        [Test]
        public void SequentialTraversal_CrossesWidgetsWithoutWrapping()
        {
            var first = CreateFocusWidget("First");
            var second = CreateFocusWidget("Second");
            first.SeedSucceeds = true;
            second.SeedSucceeds = true;
            first.MoveResults[FocusNavigationDirection.Next] = false;
            second.MoveResults[FocusNavigationDirection.Next] = false;
            first.MoveResults[FocusNavigationDirection.Previous] = false;
            second.MoveResults[FocusNavigationDirection.Previous] = false;

            m_panel.AddToHierarchy(first);
            m_panel.AddToHierarchy(second);
            RivePanelTestUtils.PositionWidget(first, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(80, 80), new Vector2(50, 100));
            RivePanelTestUtils.PositionWidget(second, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(80, 80), new Vector2(250, 100));
            TickPanel();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            Assert.AreSame(first, FocusController.ActiveWidget);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Next));
            Assert.AreSame(second, FocusController.ActiveWidget);
            Assert.AreEqual(1, first.ClearFocusCount);

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Previous));
            Assert.AreSame(first, FocusController.ActiveWidget);

            // Off the start: Rive lets go and Unity moves on.
            Assert.IsFalse(FocusController.TryMoveFocus(FocusNavigationDirection.Previous));
            Assert.AreEqual(PanelFocusSessionState.Released, FocusController.SessionState);
            Assert.IsNull(FocusController.ActiveWidget);
        }

        [Test]
        public void SequentialTraversal_AtAnEdgeThatStops_KeepsFocusInTheWidget()
        {
            var first = CreateFocusWidget("First");
            var second = CreateFocusWidget("Second");
            first.MoveResults[FocusNavigationDirection.Next] = false;
            first.StopsAtEdge = true;
            m_panel.AddToHierarchy(first);
            m_panel.AddToHierarchy(second);
            TickPanel();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            Assert.AreSame(first, FocusController.ActiveWidget);

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Next));
            Assert.AreSame(first, FocusController.ActiveWidget);
            Assert.AreEqual(0, second.SeedCount);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
        }

        [Test]
        public void DirectionalTraversal_CallsExactNativeDirection()
        {
            var widget = CreateFocusWidget("Only");
            widget.MoveResults[FocusNavigationDirection.Left] = true;
            widget.MoveResults[FocusNavigationDirection.Right] = true;
            widget.MoveResults[FocusNavigationDirection.Up] = true;
            widget.MoveResults[FocusNavigationDirection.Down] = true;
            m_panel.AddToHierarchy(widget);
            TickPanel();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Left));
            Assert.AreEqual(FocusNavigationDirection.Left, widget.LastMove);
            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Right));
            Assert.AreEqual(FocusNavigationDirection.Right, widget.LastMove);
            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Up));
            Assert.AreEqual(FocusNavigationDirection.Up, widget.LastMove);
            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Down));
            Assert.AreEqual(FocusNavigationDirection.Down, widget.LastMove);
        }

        [Test]
        public void DirectionalTraversal_ChoosesSpatialSiblingNotHierarchyNext()
        {
            var left = CreateFocusWidget("Left");
            var right = CreateFocusWidget("Right");
            var below = CreateFocusWidget("Below");
            left.MoveResults[FocusNavigationDirection.Right] = false;
            right.SeedSucceeds = true;
            below.SeedSucceeds = true;

            m_panel.AddToHierarchy(left);
            m_panel.AddToHierarchy(below);
            m_panel.AddToHierarchy(right);

            RivePanelTestUtils.PositionWidget(left, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(60, 60), new Vector2(40, 150));
            RivePanelTestUtils.PositionWidget(below, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(60, 60), new Vector2(40, 40));
            RivePanelTestUtils.PositionWidget(right, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(60, 60), new Vector2(300, 150));

            TickPanel();

            Canvas.ForceUpdateCanvases();
            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            Assert.AreSame(left, FocusController.ActiveWidget);

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Right));
            Assert.AreSame(right, FocusController.ActiveWidget, "Right should pick the spatially right widget, not the next hierarchy sibling.");
        }

        [Test]
        public void DirectionalTraversal_Down_PicksStackedSiblingWhenRectsOverlap()
        {
            var top = CreateFocusWidget("Top");
            var bottom = CreateFocusWidget("Bottom");
            top.MoveResults[FocusNavigationDirection.Down] = false;
            bottom.SeedSucceeds = true;

            m_panel.AddToHierarchy(top);
            m_panel.AddToHierarchy(bottom);

            // These two widgets look visually stacked (one above the other), and their centers are clearly separated vertically.
            // However, their RectTransforms overlap by 20 pixels. Pressing Down should still move focus to the widget below.

            RivePanelTestUtils.PositionWidget(top, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(80, 80), new Vector2(0, 50));
            RivePanelTestUtils.PositionWidget(bottom, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(80, 80), new Vector2(0, -10));

            TickPanel();
            Canvas.ForceUpdateCanvases();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            Assert.AreSame(top, FocusController.ActiveWidget);

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Down));
            Assert.AreSame(bottom, FocusController.ActiveWidget, "Down should pick the widget below even when its rect overlaps the focused widget.");
            Assert.AreEqual(FocusNavigationDirection.Down, bottom.LastSeed);
        }

        [Test]
        public void SpatialNavigator_LeftEntry_PrefersWidgetFlushWithPanelRightEdge()
        {
            var panelRect = new Rect(-200f, -100f, 400f, 200f);
            Rect origin = FocusSpatialNavigator.CreateEntryOrigin(panelRect, FocusNavigationDirection.Left);
            var candidates = new List<Rect>
            {
                new Rect(-200f, -80f, 180f, 160f),
                new Rect(20f, -80f, 181f, 160f)
            };

            Assert.AreEqual(
                1,
                FocusSpatialNavigator.FindBestIndex(origin, candidates, FocusNavigationDirection.Left),
                "Entering from the right should pick the widget on the right edge, even if that widget is flush with or slightly past the panel.");
        }

        [Test]
        public void DirectionalEntry_FromRight_SelectsVisuallyNearestWidget()
        {
            var left = CreateFocusWidget("Left");
            var right = CreateFocusWidget("Right");
            left.SeedSucceeds = true;
            right.SeedSucceeds = true;

            m_panel.AddToHierarchy(left);
            m_panel.AddToHierarchy(right);
            RivePanelTestUtils.PositionWidget(left, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(200, 180), new Vector2(-100, 0));
            RivePanelTestUtils.PositionWidget(right, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(204, 180), new Vector2(102, 0));
            TickPanel();
            Canvas.ForceUpdateCanvases();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Left);

            Assert.AreSame(
                right,
                FocusController.ActiveWidget,
                "Left from a control on the right of the panel should enter the right-hand widget, not the first widget in hierarchy.");
            Assert.AreEqual(FocusNavigationDirection.Left, right.LastSeed);
            Assert.AreEqual(0, left.SeedCount);
        }

        [Test]
        public void DirectionalEntry_FromLeft_SelectsVisuallyNearestWidget()
        {
            var left = CreateFocusWidget("Left");
            var right = CreateFocusWidget("Right");
            left.SeedSucceeds = true;
            right.SeedSucceeds = true;

            m_panel.AddToHierarchy(left);
            m_panel.AddToHierarchy(right);
            RivePanelTestUtils.PositionWidget(left, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(204, 180), new Vector2(-102, 0));
            RivePanelTestUtils.PositionWidget(right, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(200, 180), new Vector2(100, 0));
            TickPanel();
            Canvas.ForceUpdateCanvases();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Right);

            Assert.AreSame(
                left,
                FocusController.ActiveWidget,
                "Right from a control on the left of the panel should enter the left-hand widget.");
            Assert.AreEqual(FocusNavigationDirection.Right, left.LastSeed);
            Assert.AreEqual(0, right.SeedCount);
        }

        [Test]
        public void FailedMovement_DoesNotClearLastFocusedNode()
        {
            var widget = CreateFocusWidget("Only");
            widget.MoveResults[FocusNavigationDirection.Right] = false;
            m_panel.AddToHierarchy(widget);
            TickPanel();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            widget.ClearFocusCount = 0;

            Assert.IsFalse(FocusController.TryMoveFocus(FocusNavigationDirection.Right));
            Assert.AreSame(widget, FocusController.ActiveWidget);
            Assert.AreEqual(0, widget.ClearFocusCount);
            Assert.IsTrue(widget.HasFocus);
        }

        [Test]
        public void HostFocusLost_ClearsRiveFocusOnce()
        {
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            Assert.IsTrue(widget.HasFocus);

            m_provider.HasHostFocus = false;
            FocusController.NotifyHostFocusLost();
            Assert.AreEqual(1, widget.ClearFocusCount);
            Assert.IsFalse(widget.HasFocus);
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);

            FocusController.NotifyHostFocusLost();
            Assert.AreEqual(1, widget.ClearFocusCount);
        }

        [Test]
        public void PointerEntry_DoesNotSeedFirstNode()
        {
            var widget = CreateFocusWidget("A");
            widget.SeedSucceeds = true;
            m_panel.AddToHierarchy(widget);
            TickPanel();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Pointer);
            Assert.AreEqual(PanelFocusSessionState.EntryPending, FocusController.SessionState);
            Assert.AreEqual(0, widget.SeedCount);
            Assert.IsNull(FocusController.ActiveWidget);
        }

        [Test]
        public void AuthoredFocusAfterPointer_IsPreservedOnPoll()
        {
            var widget = CreateFocusWidget("A");
            widget.SeedSucceeds = true;
            m_panel.AddToHierarchy(widget);
            TickPanel();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Pointer);
            widget.HasFocus = true;
            TickPanel();

            Assert.AreSame(widget, FocusController.ActiveWidget);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
            Assert.AreEqual(0, widget.SeedCount);
        }

        [Test]
        public void InternalFalseToTrueEdge_UpdatesOwner()
        {
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();

            widget.HasFocus = true;
            TickPanel();

            Assert.AreSame(widget, FocusController.ActiveWidget);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
        }

        [Test]
        public void FocusInterrupt_IsDisabledByDefault_AndRequestsOnceWhenEnabled()
        {
            Assert.IsFalse(m_panel.AllowFocusInterrupt);

            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();

            widget.HasFocus = true;
            TickPanel();
            Assert.AreEqual(0, m_provider.RequestNativeFocusCount);

            m_panel.AllowFocusInterrupt = true;
            widget.HasFocus = false;
            TickPanel();
            widget.HasFocus = true;
            TickPanel();
            Assert.AreEqual(1, m_provider.RequestNativeFocusCount);

            TickPanel();
            Assert.AreEqual(1, m_provider.RequestNativeFocusCount);
        }

        [Test]
        public void ManualTick_PollsFocus()
        {
            m_panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            widget.HasFocus = true;

            TickPanel();
            Assert.AreSame(widget, FocusController.ActiveWidget);
        }

        [Test]
        public void OverlappingFocus_KeepsOnlyTopmostWidget()
        {
            var back = CreateFocusWidget("Back");
            var front = CreateFocusWidget("Front");
            m_panel.AddToHierarchy(back);
            m_panel.AddToHierarchy(front);
            TickPanel();

            back.HasFocus = true;
            front.HasFocus = true;
            TickPanel();

            Assert.AreSame(front, FocusController.ActiveWidget);
            Assert.IsFalse(back.HasFocus);
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("reported focus"));
        }

        [Test]
        public void PointerTransfer_FromFocusedWidgetToAnother_DoesNotWarn()
        {
            var first = CreateFocusWidget("First");
            var second = CreateFocusWidget("Second");
            m_panel.AddToHierarchy(first);
            m_panel.AddToHierarchy(second);
            TickPanel();

            first.HasFocus = true;
            TickPanel();
            Assert.AreSame(first, FocusController.ActiveWidget);

            m_mockLogger.Clear();
            second.HasFocus = true;
            TickPanel();

            Assert.AreSame(second, FocusController.ActiveWidget);
            Assert.IsFalse(first.HasFocus);
            Assert.IsFalse(m_mockLogger.LoggedWarningsContains("reported focus"));
        }

        [Test]
        public void OverlappingFocus_WithoutProvider_ReconcilesWithoutWarning()
        {
            FocusController.FocusProvider = null;

            var back = CreateFocusWidget("Back");
            var front = CreateFocusWidget("Front");
            m_panel.AddToHierarchy(back);
            m_panel.AddToHierarchy(front);
            TickPanel();

            back.HasFocus = true;
            front.HasFocus = true;
            TickPanel();

            Assert.AreSame(front, FocusController.ActiveWidget);
            Assert.IsFalse(back.HasFocus);
            Assert.IsFalse(m_mockLogger.LoggedWarningsContains("reported focus"));
        }

        [Test]
        public void SecondFocusProvider_ReplacesFirstWithoutWarning()
        {
            var second = new FakeFocusProvider();
            FocusController.FocusProvider = second;

            Assert.AreSame(second, FocusController.FocusProvider);
            Assert.IsTrue(m_provider.WasDetached);
            Assert.IsFalse(m_provider.IsFocusable);
            Assert.IsFalse(m_mockLogger.LoggedWarningsContains("focus provider"));
        }

        [Test]
        public void ReplacingProvider_WithHostFocus_ClearsRiveFocus()
        {
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            Assert.IsTrue(widget.HasFocus);

            var second = new FakeFocusProvider();
            FocusController.FocusProvider = second;

            Assert.AreSame(second, FocusController.FocusProvider);
            Assert.IsFalse(widget.HasFocus);
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
        }

        [Test]
        public void UnregisteringActiveProvider_ClearsOwnershipAndAllowsReplacement()
        {
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            Assert.IsTrue(widget.HasFocus);

            FocusController.FocusProvider = null;
            Assert.IsFalse(widget.HasFocus);
            Assert.IsNull(FocusController.FocusProvider);

            var replacement = new FakeFocusProvider();
            FocusController.FocusProvider = replacement;
            Assert.AreSame(replacement, FocusController.FocusProvider);
        }

        [Test]
        public void UserClearsActiveWidgetFocus_ReleasesSessionImmediately()
        {
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);

            widget.SimulateUserClearFocus();

            Assert.IsFalse(widget.HasFocus);
            Assert.IsNull(FocusController.ActiveWidget);
            Assert.AreEqual(PanelFocusSessionState.Released, FocusController.SessionState);
            widget.SeedCount = 0;
            Assert.IsFalse(FocusController.TryMoveFocus(FocusNavigationDirection.Next));
            Assert.AreEqual(0, widget.SeedCount);
        }

        [Test]
        public void ReleasedSession_DoesNotReenterOnNextNavigation()
        {
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);

            widget.HasFocus = false;
            TickPanel();
            Assert.AreEqual(PanelFocusSessionState.Released, FocusController.SessionState);

            widget.SeedCount = 0;
            Assert.IsFalse(FocusController.TryMoveFocus(FocusNavigationDirection.Next));
            Assert.AreEqual(0, widget.SeedCount);
        }

        [Test]
        public void ReleasedSession_ProgrammaticFocus_ReentersImmediately()
        {
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            widget.SimulateUserClearFocus();
            Assert.AreEqual(PanelFocusSessionState.Released, FocusController.SessionState);

            // The native focus might come back before the next Tick happens.
            // Until the widget actually says it has focus again, the Released state will still allow the next navigation event.

            widget.HasFocus = true;
            widget.SeedCount = 0;
            Assert.IsFalse(FocusController.TryMoveFocus(FocusNavigationDirection.Next));
            Assert.AreEqual(PanelFocusSessionState.Released, FocusController.SessionState);
            Assert.IsNull(FocusController.ActiveWidget);
            Assert.AreEqual(0, widget.SeedCount);

            widget.SimulateUserMoveFocus();

            Assert.AreSame(widget, FocusController.ActiveWidget);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Next));
            Assert.AreSame(widget, FocusController.ActiveWidget);
            Assert.AreEqual(0, widget.SeedCount);
        }

        [Test]
        public void SpatialNavigator_ScoresPrimaryAxisCandidates()
        {
            Rect origin = new Rect(0, 0, 10, 10);
            var candidates = new List<Rect>
            {
                new Rect(-40, 0, 10, 10),
                new Rect(40, 0, 10, 10),
                new Rect(0, 40, 10, 10)
            };

            Assert.AreEqual(1, FocusSpatialNavigator.FindBestIndex(origin, candidates, FocusNavigationDirection.Right));
            Assert.AreEqual(0, FocusSpatialNavigator.FindBestIndex(origin, candidates, FocusNavigationDirection.Left));
            Assert.AreEqual(2, FocusSpatialNavigator.FindBestIndex(origin, candidates, FocusNavigationDirection.Up));
        }

        [Test]
        public void SpatialNavigator_Down_PicksVerticallyOverlappingWidgetThatIsLower()
        {
            // This test checks that when two widgets overlap vertically,
            // but the "candidate" widget's center is still below the focused ("origin") widget,
            // pressing Down moves focus to the widget positioned lower on the Y axis.
            //
            //   Y axis ↓
            //
            //      +-----------------------+
            //      |      origin           |
            //      |   (focused widget)    |
            //      +-----------------------+
            //             overlap
            //           +-----------------------+
            //           |  candidate below      |
            //           | (center is below origin)
            //           +-----------------------+
            //
            // Even though their rectangles overlap by 20px,
            // Down should still pick the widget whose center is lower.

            Rect origin = new Rect(0f, 40f, 100f, 80f);   // Focused widget, higher up
            var candidates = new List<Rect>
            {
                new Rect(0f, -20f, 100f, 80f)             // Candidate widget overlaps but is mostly below
            };

            Assert.AreEqual(
                0,
                FocusSpatialNavigator.FindBestIndex(origin, candidates, FocusNavigationDirection.Down),
                "Moving Down should focus the widget whose center is below, even with vertical overlap.");
        }

        [Test]
        public void SpatialNavigator_Up_PicksVerticallyOverlappingWidgetThatIsHigher()
        {
            Rect origin = new Rect(0f, -20f, 100f, 80f);
            var candidates = new List<Rect>
            {
                new Rect(0f, 40f, 100f, 80f)
            };

            Assert.AreEqual(
                0,
                FocusSpatialNavigator.FindBestIndex(origin, candidates, FocusNavigationDirection.Up),
                "A widget whose center is above should be reachable with Up, even if rectangles overlap vertically.");
        }

        [Test]
        public void SpatialNavigator_Down_IgnoresFullyOverlappingSameRect()
        {
            Rect origin = new Rect(0f, 0f, 80f, 80f);
            var candidates = new List<Rect>
            {
                new Rect(0f, 0f, 80f, 80f)
            };

            Assert.AreEqual(
                -1,
                FocusSpatialNavigator.FindBestIndex(origin, candidates, FocusNavigationDirection.Down),
                "A widget covering the same rect is not below the origin.");
        }

        // A panel that doesn't wait for Rive, with answers held until AnswerPending.
        private (MockFocusWidget left, MockFocusWidget right) CreateAsyncPair()
        {
            m_panel.ThreadingMode = ThreadingMode.BackgroundThread;
            var left = CreateFocusWidget("Left");
            var right = CreateFocusWidget("Right");
            m_panel.AddToHierarchy(left);
            m_panel.AddToHierarchy(right);
            RivePanelTestUtils.PositionWidget(left, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(80, 80), new Vector2(50, 100));
            RivePanelTestUtils.PositionWidget(right, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(80, 80), new Vector2(250, 100));
            TickPanel();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Right);
            Assert.AreSame(left, FocusController.ActiveWidget);
            left.DeferAnswers = true;
            right.DeferAnswers = true;
            return (left, right);
        }

        [Test]
        public void AsyncPanel_TakesTheMoveUpFront_AndFinishesWhenRiveAnswers()
        {
            var (left, right) = CreateAsyncPair();
            left.MoveResults[FocusNavigationDirection.Right] = false;

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Right));
            Assert.IsTrue(FocusController.MoveInFlight);
            Assert.AreSame(left, FocusController.ActiveWidget);

            left.AnswerPending();
            right.AnswerPending();

            Assert.IsFalse(FocusController.MoveInFlight);
            Assert.AreSame(right, FocusController.ActiveWidget);
            Assert.AreEqual(1, left.ClearFocusCount);
            Assert.IsEmpty(m_provider.ContinuedNavigation);
        }

        [Test]
        public void AsyncPanel_MoveThatLeavesRive_HandsNavigationBackToTheProvider()
        {
            var (left, right) = CreateAsyncPair();
            left.MoveResults[FocusNavigationDirection.Left] = false;

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Left));
            left.AnswerPending();

            CollectionAssert.AreEqual(new[] { FocusNavigationDirection.Left }, m_provider.ContinuedNavigation);
            Assert.AreEqual(0, right.SeedCount);
        }

        [Test]
        public void AsyncPanel_MovesWhileOneIsOut_RunInOrderAfterIt()
        {
            var (left, _) = CreateAsyncPair();

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Down));
            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Up));
            Assert.AreEqual(1, left.PendingAnswers);

            left.AnswerPending();
            Assert.AreEqual(FocusNavigationDirection.Up, left.LastMove);
            Assert.AreEqual(1, left.PendingAnswers);

            left.AnswerPending();
            Assert.IsFalse(FocusController.MoveInFlight);
        }

        [Test]
        public void AsyncPanel_SessionEndingWhileAMoveIsOut_DropsIt()
        {
            var (left, right) = CreateAsyncPair();
            left.MoveResults[FocusNavigationDirection.Left] = false;

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Left));
            m_provider.HasHostFocus = false;
            FocusController.NotifyHostFocusLost();
            left.AnswerPending();

            Assert.IsEmpty(m_provider.ContinuedNavigation);
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
            Assert.IsFalse(FocusController.MoveInFlight);
        }

        [Test]
        public void AsyncPanel_SessionEndingBeforeAFailedMove_DoesNotSeedTheNeighbor()
        {
            var (left, right) = CreateAsyncPair();
            left.MoveResults[FocusNavigationDirection.Right] = false;

            Assert.IsTrue(FocusController.TryMoveFocus(FocusNavigationDirection.Right));
            m_provider.HasHostFocus = false;
            FocusController.NotifyHostFocusLost();
            left.AnswerPending();
            right.AnswerPending();

            Assert.AreEqual(0, right.SeedCount, "a move from an ended session shouldn't reach the neighbor");
            Assert.IsFalse(right.HasFocus);
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
        }

        [Test]
        public void AsyncPanel_SessionEndingDuringEntry_UndoesTheFocusItTook()
        {
            m_panel.ThreadingMode = ThreadingMode.BackgroundThread;
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            widget.DeferAnswers = true;

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            Assert.AreEqual(1, widget.PendingAnswers);

            m_provider.HasHostFocus = false;
            FocusController.NotifyHostFocusLost();
            widget.AnswerPending();
            TickPanel();

            Assert.IsFalse(widget.HasFocus, "the entry landed after the panel lost selection, so its focus is dropped");
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
            Assert.IsNull(FocusController.ActiveWidget);
        }

        [Test]
        public void AsyncPanel_ReselectingBeforeAnOldEntryLands_KeepsTheNewEntrysFocus()
        {
            m_panel.ThreadingMode = ThreadingMode.BackgroundThread;
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            widget.DeferAnswers = true;

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            m_provider.HasHostFocus = false;
            FocusController.NotifyHostFocusLost();
            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            // The old entry, the deselect's clear, then the new entry, in the order Rive runs them.
            Assert.AreEqual(3, widget.PendingAnswers);

            // The deselect's clear runs between the two entries, so the new one's focus stands.
            widget.AnswerPending();
            TickPanel();

            Assert.AreEqual(1, widget.ClearFocusCount, "only the deselect clears");
            Assert.IsTrue(widget.HasFocus);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
            Assert.AreSame(widget, FocusController.ActiveWidget);
        }

        [Test]
        public void AsyncPanel_PointerReselectBeforeAnOldEntryLands_KeepsTheClickedFocus()
        {
            m_panel.ThreadingMode = ThreadingMode.BackgroundThread;
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            widget.DeferAnswers = true;

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            m_provider.HasHostFocus = false;
            FocusController.NotifyHostFocusLost();

            // A click selects the panel again and focuses a node, after the old entry ran.
            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Pointer);
            widget.SimulatePointerFocus();

            widget.AnswerPending();
            TickPanel();

            Assert.AreEqual(1, widget.ClearFocusCount, "only the deselect clears");
            Assert.IsTrue(widget.HasFocus);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
        }

        [Test]
        public void AsyncPanel_PointerMoveAfterDeselect_StillDropsTheOldEntrysFocus()
        {
            m_panel.ThreadingMode = ThreadingMode.BackgroundThread;
            var widget = CreateFocusWidget("A");
            m_panel.AddToHierarchy(widget);
            TickPanel();
            widget.DeferAnswers = true;

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            m_provider.HasHostFocus = false;
            FocusController.NotifyHostFocusLost();

            // A hover that changes nothing, with the panel still deselected.
            widget.SimulatePointerMove();

            widget.AnswerPending();
            TickPanel();

            Assert.IsFalse(widget.HasFocus, "a pointer move doesn't mean focus changed, so the old entry's focus goes");
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
            Assert.AreEqual(0, m_provider.RequestNativeFocusCount);
        }

        // Tick only queues time, so run the frame's update pass too.
        private void TickPanel()
        {
            m_panel.Tick(0f);
            Orchestrator.Instance.RunUpdatePass();
        }

        private MockFocusWidget CreateFocusWidget(string name)
        {
            var widget = RivePanelTestUtils.CreateWidget<MockFocusWidget>(name);
            widget.MockEligible = true;
            widget.SeedSucceeds = true;
            return widget;
        }
    }

    internal class FakeFocusProvider : IPanelFocusProvider
    {
        public bool HasHostFocus { get; set; }
        public bool IsFocusable { get; private set; }
        public bool WasDetached { get; private set; }
        public int RequestNativeFocusCount { get; private set; }
        public List<FocusNavigationDirection> ContinuedNavigation { get; } = new List<FocusNavigationDirection>();

        public void SetFocusable(bool isFocusable)
        {
            IsFocusable = isFocusable;
        }

        public void RequestNativeFocus()
        {
            RequestNativeFocusCount++;
            HasHostFocus = true;
        }

        public void ContinueNavigation(FocusNavigationDirection direction)
        {
            ContinuedNavigation.Add(direction);
        }

        public void NotifyDetached()
        {
            WasDetached = true;
            HasHostFocus = false;
            IsFocusable = false;
        }
    }

    internal class MockFocusWidget : WidgetBehaviour, IRiveFocusWidget
    {
        private IRenderObject m_renderObject;

        public bool MockEligible { get; set; } = true;
        public bool HasFocus { get; set; }
        public bool SeedSucceeds { get; set; } = true;
        public int SeedCount { get; set; }
        public int ClearFocusCount { get; set; }
        public FocusNavigationDirection? LastMove { get; private set; }
        public FocusNavigationDirection? LastSeed { get; private set; }
        public Dictionary<FocusNavigationDirection, bool> MoveResults { get; } =
            new Dictionary<FocusNavigationDirection, bool>();
        public bool WasClearedOnLastFailedMove => false;

        /// <summary>
        /// Holds answers until AnswerPending, like a panel that doesn't wait for Rive.
        /// </summary>
        public bool DeferAnswers { get; set; }

        /// <summary>
        /// A failed Next or Previous keeps focus, like an edge authored to stop.
        /// </summary>
        public bool StopsAtEdge { get; set; }
        public int PendingAnswers => m_pending.Count;
        private readonly List<System.Action> m_pending = new List<System.Action>();

        public override IRenderObject RenderObject => m_renderObject;
        public override HitTestBehavior HitTestBehavior { get; set; } = HitTestBehavior.Opaque;

        public bool IsEligibleForFocus => MockEligible && Enabled;

        public FocusState FocusState => new FocusState(HasFocus, HasFocus && ExpectsText, MockEligible);

        /// <summary>
        /// The focused node takes typed text, like a TextInput.
        /// </summary>
        public bool ExpectsText { get; set; }

        /// <summary>
        /// Keys the focused node claims, like an authored key listener.
        /// </summary>
        public HashSet<RiveKey> ClaimedKeys { get; } = new HashSet<RiveKey>();
        public List<(RiveKey key, RiveKeyModifiers modifiers, bool pressed, bool repeat)> Keys { get; } =
            new List<(RiveKey, RiveKeyModifiers, bool, bool)>();
        public List<string> Texts { get; } = new List<string>();

        /// <summary>
        /// What's selected in the focused node, for copy and cut.
        /// </summary>
        public string Selection { get; set; } = "";
        public List<KeySelection> KeySelections { get; } = new List<KeySelection>();

        protected override void OnEnable()
        {
            m_renderObject = new MockRenderObject();
            Status = WidgetStatus.Loaded;
            base.OnEnable();
        }

        public void MoveFocus(FocusNavigationDirection direction, System.Action<StateMachineNative.FocusResult> done)
        {
            LastMove = direction;
            bool moved = !MoveResults.TryGetValue(direction, out bool result) || result;
            // Like core: walking off the end drops focus, unless the edge stops.
            bool walksOff = !moved && FocusSpatialNavigator.IsSequential(direction) && !StopsAtEdge;
            Answer(moved, done, walksOff);
        }

        public void SeedFocus(FocusNavigationDirection direction, System.Action<StateMachineNative.FocusResult> done)
        {
            LastSeed = direction;
            SeedCount++;
            Answer(SeedSucceeds, done);
        }

        public void KeyInput(
            RiveKey key, RiveKeyModifiers modifiers, bool pressed, bool repeat, KeySelection selection, System.Action<StateMachineNative.FocusResult> done)
        {
            Keys.Add((key, modifiers, pressed, repeat));
            KeySelections.Add(selection);
            bool claimed = HasFocus && ClaimedKeys.Contains(key);
            Queue(() =>
            {
                string selected = selection != KeySelection.None ? Selection : null;
                if (selection == KeySelection.Cut && ExpectsText)
                {
                    Selection = "";
                }
                done?.Invoke(new StateMachineNative.FocusResult { Found = true, Moved = claimed, State = FocusState, SelectedText = selected });
            });
        }

        public void TextInput(string text, System.Action<StateMachineNative.FocusResult> done)
        {
            Texts.Add(text);
            Queue(() => done?.Invoke(new StateMachineNative.FocusResult { Found = true, Moved = ExpectsText, State = FocusState }));
        }

        public void AnswerPending()
        {
            var pending = new List<System.Action>(m_pending);
            m_pending.Clear();
            foreach (System.Action answer in pending)
            {
                answer();
            }
        }

        private void Answer(bool moved, System.Action<StateMachineNative.FocusResult> done, bool dropsFocus = false)
        {
            System.Action answer = () =>
            {
                if (moved)
                {
                    HasFocus = true;
                }
                else if (dropsFocus)
                {
                    HasFocus = false;
                }
                done(new StateMachineNative.FocusResult { Found = true, Moved = moved, State = FocusState });
            };
            if (DeferAnswers)
            {
                m_pending.Add(answer);
            }
            else
            {
                answer();
            }
        }

        // Like the real widget: shows straight away, and Rive runs it after what's already queued.
        public void ClearFocus()
        {
            ClearFocusCount++;
            HasFocus = false;
            Queue(() => HasFocus = false);
        }

        /// <summary>
        /// A pointer event that focuses a node, run in order with the held answers.
        /// </summary>
        public void SimulatePointerFocus()
        {
            Queue(() => HasFocus = true);
        }

        /// <summary>
        /// A pointer event that changes nothing, run in order with the held answers.
        /// </summary>
        public void SimulatePointerMove()
        {
            Queue(() => { });
        }

        private void Queue(System.Action command)
        {
            if (DeferAnswers)
            {
                m_pending.Add(command);
            }
            else
            {
                command();
            }
        }

        public void SimulateUserClearFocus()
        {
            HasFocus = false;
            if (RivePanel != null)
            {
                RivePanel.FocusController.PollFocus();
            }
        }

        public void SimulateUserMoveFocus()
        {
            HasFocus = true;
            if (RivePanel != null)
            {
                RivePanel.FocusController.PollFocus();
            }
        }

        public Rect GetPanelSpaceRect(RectTransform panelContainer)
        {
            return FocusSpatialNavigator.GetRectInPanelSpace(RectTransform, panelContainer);
        }

        public Rect GetOriginRect(RectTransform panelContainer)
        {
            return GetPanelSpaceRect(panelContainer);
        }
    }
}
