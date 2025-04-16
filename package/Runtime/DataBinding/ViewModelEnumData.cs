using System.Collections.Generic;

namespace Rive
{
    /// <summary>
    /// Represents an enum type defined in a Rive file.
    /// </summary>
    public sealed class ViewModelEnumData
    {

        private string[] m_values;
        /// <summary>
        /// The name of the enum defined in the Rive file.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The values of the enum defined in the Rive file.
        /// </summary>
        public IReadOnlyList<string> Values { get { return m_values; } }

        internal string[] ValuesArray => m_values;

        /// <summary>
        /// Creates a new instance of the <see cref="ViewModelEnumData"/> class.
        /// </summary>
        /// <param name="name"> The name of the enum.</param>
        /// <param name="values"> The values of the enum.</param>
        internal ViewModelEnumData(string name, string[] values)
        {
            Name = name;
            m_values = values;
        }
    }
}
