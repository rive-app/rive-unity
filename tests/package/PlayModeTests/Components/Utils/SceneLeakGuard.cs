using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rive.Tests.Utils
{
    /// <summary>
    /// Destroys whatever a test added to the scene, so a test that fails before
    /// its own cleanup doesn't leave panels or cameras behind for the next one.
    /// Rive's own objects are DontDestroyOnLoad, so they're never touched.
    /// </summary>
    internal sealed class SceneLeakGuard
    {
        private readonly HashSet<GameObject> m_before = new HashSet<GameObject>();

        /// Call at the end of SetUp, after the fixture's own objects exist.
        public void Begin()
        {
            m_before.Clear();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                m_before.Add(root);
            }
        }

        /// Destroys what's new since Begin. Destroy lands at the end of the
        /// frame, so yield a frame before the next test looks for objects.
        public int End()
        {
            int destroyed = 0;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!m_before.Contains(root))
                {
                    Object.Destroy(root);
                    destroyed++;
                }
            }
            m_before.Clear();
            return destroyed;
        }
    }
}
