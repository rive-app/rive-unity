namespace Rive
{
    /// <summary>
    /// The type of data that a view model property can hold.
    /// </summary>
    public enum ViewModelDataType : uint
    {
        /// <summary>None.</summary>
        None = 0,

        /// <summary>String.</summary>
        String = 1,

        /// <summary>Number.</summary>
        Number = 2,

        /// <summary>Bool.</summary>
        Boolean = 3,

        /// <summary>Color.</summary>
        Color = 4,

        /// <summary>List.</summary>
        List = 5,

        /// <summary>Enum.</summary>
        Enum = 6,

        /// <summary>Trigger.</summary>
        Trigger = 7,

        /// <summary>View Model.</summary>
        ViewModel = 8
    }
}
