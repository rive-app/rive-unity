using System.Collections.Generic;
using UnityEngine;

namespace Rive.Utils
{
    [System.Serializable]
    internal class SerializedDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> m_keys = new List<TKey>();

        [SerializeField]
        private List<TValue> m_values = new List<TValue>();


        public void OnBeforeSerialize()
        {
            m_keys.Clear();
            m_values.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                m_keys.Add(pair.Key);
                m_values.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            this.Clear();

            if (m_keys.Count != m_values.Count)
                throw new System.Exception($"Key count ({m_keys.Count}) does not match value count ({m_values.Count}). Verify that both key and value types can be serialized.");

            for (int i = 0; i < m_keys.Count; i++)
                this.Add(m_keys[i], m_values[i]);
        }

#if UNITY_EDITOR
        internal static string BindingPath_Keys => nameof(m_keys);
        internal static string BindingPath_Values => nameof(m_values);
#endif
    }
}