namespace Rive
{
    /// <summary>
    /// A view model value as a read or capture replies with it, so it gets from
    /// the drain to a callback or Future without boxing. Mirrors appendValue in
    /// viewmodel_routines.cpp.
    /// </summary>
    internal struct PropertyValue
    {
        /// Number.
        internal float Number;

        /// Boolean (not 0), color (ARGB), enum index and list size.
        internal uint Bits;

        /// String and enum value.
        internal string Text;
    }

    /// <summary>
    /// Something that turns a <see cref="PropertyValue"/> into its own type.
    /// </summary>
    internal interface IPropertyValueOf<T>
    {
        T FromValue(in PropertyValue value);
    }
}
