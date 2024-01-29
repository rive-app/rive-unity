using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// Represents a State Machine Input.
    ///
    /// An Input can be a Trigger, a Boolean, or a Number.
    /// Use the derived classes SMITrigger, SMIBool and SMINumber.
    /// </summary>
    /// <remarks>
    /// An SMIInput is owned by a StateMachine.
    /// The SMIInput keeps the StateMachine alive by maintaining a reference to it.
    /// </remarks>
    public class SMIInput
    {
        private readonly IntPtr m_nativeSMI;

        // This is a reference to the StateMachine that owns this SMIInput.
        // It is used to keep the StateMachine alive while the SMIInput is alive.
        private StateMachine m_stateMachineReference;

        internal IntPtr NativeSMI => m_nativeSMI;

        internal SMIInput(IntPtr smi, StateMachine stateMachineReference)
        {
            m_nativeSMI = smi;
            m_stateMachineReference = stateMachineReference;
        }

        /// <summary>
        /// The name of the State Machine Input.
        /// </summary>
        public string Name
        {
            get
            {
                IntPtr ptr = getSMIInputName(m_nativeSMI);
                return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
            }
        }

        /// Returns true if the SMIInput is a Boolean (SMIBool).
        public bool IsBoolean => isSMIBoolean(m_nativeSMI);

        /// Returns true if the SMIInput is a Trigger (SMITrigger).
        public bool IsTrigger => isSMITrigger(m_nativeSMI);

        /// Returns true if the SMIInput is a Number (SMINumber).
        public bool IsNumber => isSMINumber(m_nativeSMI);

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getSMIInputName(IntPtr nativeSMI);

        [DllImport(NativeLibrary.name)]
        internal static extern bool isSMIBoolean(IntPtr nativeSMI);

        [DllImport(NativeLibrary.name)]
        internal static extern bool isSMITrigger(IntPtr nativeSMI);

        [DllImport(NativeLibrary.name)]
        internal static extern bool isSMINumber(IntPtr nativeSMI);
        #endregion
    }

    /// <summary>
    /// Represents a State Machine Trigger.
    /// </summary>
    /// <remarks>
    /// A SMITrigger is a boolean that is set to true for one frame.
    ///
    /// A SMITrigger is owned by a StateMachine.
    /// The SMITrigger keeps the StateMachine alive by maintaining a reference to it.
    /// </remarks>
    public sealed class SMITrigger : SMIInput
    {
        internal SMITrigger(IntPtr smi, StateMachine stateMachineReference)
            : base(smi, stateMachineReference) { }

        ///  Fire the State Machine Trigger.
        public void Fire()
        {
            fireSMITriggerStateMachine(NativeSMI);
        }

        #region Native Methods

        [DllImport(NativeLibrary.name)]
        internal static extern void fireSMITriggerStateMachine(IntPtr nativeSMI);

        #endregion
    }

    /// <summary>
    /// Represents a State Machine Boolean.
    /// </summary>
    /// <remarks>
    /// A SMIBool contains a value of type boolean that can be get/set.
    /// The SMIBool keeps the StateMachine alive by maintaining a reference to it.
    /// </remarks>
    public sealed class SMIBool : SMIInput
    {
        internal SMIBool(IntPtr smi, StateMachine stateMachineReference)
            : base(smi, stateMachineReference) { }

        ///  The value of the State Machine Boolean.
        public bool Value
        {
            get => getSMIBoolValueStateMachine(NativeSMI);
            set => setSMIBoolValueStateMachine(NativeSMI, value);
        }

        #region Native Methods

        [DllImport(NativeLibrary.name)]
        internal static extern bool getSMIBoolValueStateMachine(IntPtr nativeSMI);

        [DllImport(NativeLibrary.name)]
        internal static extern void setSMIBoolValueStateMachine(IntPtr nativeSMI, bool newValue);

        #endregion
    }

    /// <summary>
    /// Represents a State Machine Number.
    /// </summary>
    /// <remarks>
    /// A SMINumber contain a value of type float that can be get/set.
    /// The SMINumber keeps the StateMachine alive by maintaining a reference to it.
    /// </remarks>
    public sealed class SMINumber : SMIInput
    {
        internal SMINumber(IntPtr smi, StateMachine stateMachineReference)
            : base(smi, stateMachineReference) { }

        ///  The value of the State Machine Number.
        public float Value
        {
            get => getSMINumberValueStateMachine(NativeSMI);
            set => setSMINumberValueStateMachine(NativeSMI, value);
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern float getSMINumberValueStateMachine(IntPtr nativeSMI);

        [DllImport(NativeLibrary.name)]
        internal static extern void setSMINumberValueStateMachine(IntPtr nativeSMI, float newValue);
        #endregion
    }
}
