using System.Collections;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class RiveTextureRendererFocusTests
    {
        private RivePanel m_panel;
        private GameObject m_rendererObject;
        private RiveTextureRenderer m_renderer;
        private GameObject m_eventSystemObject;
        private MockLogger m_mockLogger;
        private PanelFocusController FocusController => m_panel.FocusController;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            m_eventSystemObject = new GameObject("EventSystem");
            m_eventSystemObject.AddComponent<EventSystem>();
            m_eventSystemObject.AddComponent<StandaloneInputModule>();

            m_panel = RivePanelTestUtils.CreatePanel("FocusTexturePanel");
            m_rendererObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            m_renderer = m_rendererObject.AddComponent<RiveTextureRenderer>();
            m_renderer.SetPanel(m_panel);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_eventSystemObject != null)
            {
                Object.Destroy(m_eventSystemObject);
            }

            if (m_panel != null)
            {
                Object.Destroy(m_panel.gameObject);
            }

            if (m_rendererObject != null)
            {
                Object.Destroy(m_rendererObject);
            }
        }

        [UnityTest]
        public IEnumerator FocusProvider_AttachesToRendererObject()
        {
            yield return null;
            var provider = m_rendererObject.GetComponent<TextureFocusInputProvider>();
            Assert.IsNotNull(provider);
            Assert.AreSame(FocusController.FocusProvider, provider);
        }

        [UnityTest]
        public IEnumerator PointerSelect_DoesNotSeedFocus()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockFocusWidget>();
            widget.MockEligible = true;
            widget.SeedSucceeds = true;
            m_panel.AddToHierarchy(widget);
            m_panel.Tick(0f);
            yield return null;

            var provider = m_rendererObject.GetComponent<TextureFocusInputProvider>();
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(provider.gameObject, pointer, ExecuteEvents.selectHandler);

            Assert.AreEqual(PanelFocusSessionState.EntryPending, FocusController.SessionState);
            Assert.AreEqual(0, widget.SeedCount);
        }

        [UnityTest]
        public IEnumerator MoveHandler_ConsumesOnlyWhenPanelReportsMovement()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockFocusWidget>();
            widget.MockEligible = true;
            widget.SeedSucceeds = true;
            widget.MoveResults[FocusNavigationDirection.Right] = true;
            m_panel.AddToHierarchy(widget);
            m_panel.Tick(0f);
            yield return null;

            var provider = m_rendererObject.GetComponent<TextureFocusInputProvider>();
            ExecuteEvents.Execute(provider.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);

            var consumed = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Right,
                moveVector = Vector2.right
            };
            ExecuteEvents.Execute(provider.gameObject, consumed, ExecuteEvents.moveHandler);
            Assert.IsTrue(consumed.used);
            Assert.AreEqual(FocusNavigationDirection.Right, widget.LastMove);

            widget.MoveResults[FocusNavigationDirection.Left] = false;
            var released = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Left,
                moveVector = Vector2.left
            };
            ExecuteEvents.Execute(provider.gameObject, released, ExecuteEvents.moveHandler);
            Assert.IsFalse(released.used);
        }

        [UnityTest]
        public IEnumerator WorldRotation_DoesNotInvertLogicalDirections()
        {
            m_rendererObject.transform.rotation = Quaternion.Euler(0f, 90f, 45f);
            var widget = RivePanelTestUtils.CreateWidget<MockFocusWidget>();
            widget.MockEligible = true;
            widget.SeedSucceeds = true;
            widget.MoveResults[FocusNavigationDirection.Left] = true;
            m_panel.AddToHierarchy(widget);
            m_panel.Tick(0f);
            yield return null;

            var provider = m_rendererObject.GetComponent<TextureFocusInputProvider>();
            ExecuteEvents.Execute(provider.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);

            var axis = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Left,
                moveVector = Vector2.left
            };
            ExecuteEvents.Execute(provider.gameObject, axis, ExecuteEvents.moveHandler);
            Assert.AreEqual(FocusNavigationDirection.Left, widget.LastMove);
        }

        [UnityTest]
        public IEnumerator SecondProvider_ForSamePanel_ReplacesFirst()
        {
            yield return null;
            var extra = new GameObject("SecondTexture");
            extra.AddComponent<MeshRenderer>();
            var secondRenderer = extra.AddComponent<RiveTextureRenderer>();
            secondRenderer.SetPanel(m_panel);
            yield return null;

            var first = m_rendererObject.GetComponent<TextureFocusInputProvider>();
            var second = extra.GetComponent<TextureFocusInputProvider>();
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreSame(second, FocusController.FocusProvider);
            Assert.IsFalse(m_mockLogger.LoggedWarningsContains("focus provider"));

            Object.Destroy(extra);
        }

        [UnityTest]
        public IEnumerator DisablingReplacedProvider_DoesNotClearReplacement()
        {
            yield return null;
            var extra = new GameObject("SecondTexture");
            extra.AddComponent<MeshRenderer>();
            var secondRenderer = extra.AddComponent<RiveTextureRenderer>();
            secondRenderer.SetPanel(m_panel);
            yield return null;

            var second = extra.GetComponent<TextureFocusInputProvider>();
            Assert.AreSame(second, FocusController.FocusProvider);

            m_rendererObject.SetActive(false);
            yield return null;

            Assert.AreSame(second, FocusController.FocusProvider);

            Object.Destroy(extra);
        }
    }
}
