namespace Rive
{
    /// <summary>
    /// Holds data about a view model property.
    /// </summary>
    public readonly struct ViewModelPropertyData
    {

        private readonly string m_name;


        private readonly ViewModelDataType m_type;


        /// <summary>
        /// The name of the property.
        /// </summary>
        public string Name => m_name;


        /// <summary>
        /// The type of the property.
        /// </summary>
        public ViewModelDataType Type => m_type;

        internal ViewModelPropertyData(string name, ViewModelDataType type)
        {
            m_name = name;
            m_type = type;
        }
    }
}
