using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Components;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rive.Tests
{
    public class FrameLoopTests
    {
        private sealed class TickInUpdate : MonoBehaviour
        {
            internal RivePanel Panel;

            private void Update()
            {
                Panel.Tick(0.016f);
            }
        }

        private readonly List<GameObject> m_objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            DebugLogger.Instance = new MockLogger();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in m_objects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            m_objects.Clear();
        }

        [UnityTest]
        public IEnumerator TickFromUpdate_GoesOutTheSameFrame()
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel("FrameLoopPanel");
            m_objects.Add(panel.gameObject);
            panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;
            yield return null;

            var ticker = new GameObject("Ticker").AddComponent<TickInUpdate>();
            m_objects.Add(ticker.gameObject);
            ticker.Panel = panel;

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
                Assert.IsFalse(panel.HasTimeWaiting, $"Frame {frame}: the Tick from Update should have gone out.");
            }
        }

        [UnityTest]
        public IEnumerator EachStep_IsInstalledOnce_AroundScriptUpdate()
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel("FrameLoopPanel");
            m_objects.Add(panel.gameObject);
            yield return null;
            RiveFrameLoop.Ensure();

            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            List<Type> update = SystemsIn(loop, typeof(UnityEngine.PlayerLoop.Update));
            List<Type> lateUpdate = SystemsIn(loop, typeof(UnityEngine.PlayerLoop.PreLateUpdate));

            int scripts = update.IndexOf(typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate));
            Assert.AreEqual(1, update.FindAll(t => t == typeof(RiveFrameLoop.RiveEarlyUpdate)).Count);
            Assert.AreEqual(1, update.FindAll(t => t == typeof(RiveFrameLoop.RiveUpdate)).Count);
            Assert.AreEqual(scripts - 1, update.IndexOf(typeof(RiveFrameLoop.RiveEarlyUpdate)));
            Assert.AreEqual(scripts + 1, update.IndexOf(typeof(RiveFrameLoop.RiveUpdate)));

            int lateScripts = lateUpdate.IndexOf(typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate));
            Assert.AreEqual(1, lateUpdate.FindAll(t => t == typeof(RiveFrameLoop.RiveLateUpdate)).Count);
            Assert.AreEqual(lateScripts + 1, lateUpdate.IndexOf(typeof(RiveFrameLoop.RiveLateUpdate)));
        }

        [UnityTest]
        public IEnumerator LoopResetUnderUs_IsReinstalledOnVerify()
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel("FrameLoopPanel");
            m_objects.Add(panel.gameObject);
            yield return null;
            PlayerLoopSystem original = PlayerLoop.GetCurrentPlayerLoop();
            try
            {
                PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());

                RiveFrameLoop.Ensure(verify: true);

                List<Type> update = SystemsIn(PlayerLoop.GetCurrentPlayerLoop(), typeof(UnityEngine.PlayerLoop.Update));
                Assert.AreEqual(1, update.FindAll(t => t == typeof(RiveFrameLoop.RiveUpdate)).Count);
            }
            finally
            {
                PlayerLoop.SetPlayerLoop(original);
            }
        }

        private static List<Type> SystemsIn(PlayerLoopSystem loop, Type phase)
        {
            foreach (PlayerLoopSystem system in loop.subSystemList)
            {
                if (system.type == phase)
                {
                    return new List<PlayerLoopSystem>(system.subSystemList).ConvertAll(s => s.type);
                }
            }
            Assert.Fail($"No {phase.Name} phase.");
            return null;
        }
    }
}
