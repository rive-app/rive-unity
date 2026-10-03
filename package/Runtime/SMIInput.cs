using System;

namespace Rive
{
    internal static class ObsoleteMessages
    {
        internal const string Inputs =
            "State machine inputs will be removed in a future version. Please use data binding instead.";

        internal const string HandleCallbacks =
            "HandleCallbacks will be removed in a future version. Widgets deliver OnValueChanged after each advance on their own.";
    }

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
    [Obsolete(ObsoleteMessages.Inputs)]
    public class SMIInput
    {
        private readonly NativeSMIInputHandle m_nativeSMI;

        // This is a reference to the StateMachine that owns this SMIInput.
        // It is used to keep the StateMachine alive while the SMIInput is alive.
        private StateMachine m_stateMachineReference;

        internal NativeSMIInputHandle NativeSMI => m_nativeSMI;

        internal SMIInput(NativeSMIInputHandle smi, StateMachine stateMachineReference)
        {
            m_nativeSMI = smi;
            m_stateMachineReference = stateMachineReference;
        }

        // Null once the state machine is disposed.
        private StateMachine Owner =>
            m_stateMachineReference != null && !m_stateMachineReference.IsDisposed ? m_stateMachineReference : null;

        private ArtboardNative.InputKind Kind =>
            Owner != null ? Owner.InputAt(m_nativeSMI).Kind : ArtboardNative.InputKind.None;

        /// <summary>
        /// The name of the State Machine Input.
        /// </summary>
        public string Name => Owner?.InputAt(m_nativeSMI).Name;

        /// Returns true if the SMIInput is a Boolean (SMIBool).
        public bool IsBoolean => Kind == ArtboardNative.InputKind.Boolean;

        /// Returns true if the SMIInput is a Trigger (SMITrigger).
        public bool IsTrigger => Kind == ArtboardNative.InputKind.Trigger;

        /// Returns true if the SMIInput is a Number (SMINumber).
        public bool IsNumber => Kind == ArtboardNative.InputKind.Number;

        /// 0 once the state machine is disposed.
        internal float GetValue()
        {
            StateMachine owner = Owner;
            return owner != null ? StateMachineNative.GetInput(owner.NativeStateMachine, m_nativeSMI) : 0f;
        }

        /// Does nothing once the state machine is disposed.
        internal void SetValue(float value)
        {
            StateMachine owner = Owner;
            if (owner != null)
            {
                StateMachineNative.SetInput(owner.NativeStateMachine, m_nativeSMI, value);
            }
        }
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
    [Obsolete(ObsoleteMessages.Inputs)]
    public sealed class SMITrigger : SMIInput
    {
        internal SMITrigger(NativeSMIInputHandle smi, StateMachine stateMachineReference)
            : base(smi, stateMachineReference) { }

        ///  Fire the State Machine Trigger.
        public void Fire()
        {
            SetValue(1f);
        }
    }

    /// <summary>
    /// Represents a State Machine Boolean.
    /// </summary>
    /// <remarks>
    /// A SMIBool contains a value of type boolean that can be get/set.
    /// The SMIBool keeps the StateMachine alive by maintaining a reference to it.
    /// </remarks>
    [Obsolete(ObsoleteMessages.Inputs)]
    public sealed class SMIBool : SMIInput
    {
        internal SMIBool(NativeSMIInputHandle smi, StateMachine stateMachineReference)
            : base(smi, stateMachineReference) { }

        ///  The value of the State Machine Boolean.
        public bool Value
        {
            get => GetValue() != 0f;
            set => SetValue(value ? 1f : 0f);
        }
    }

    /// <summary>
    /// Represents a State Machine Number.
    /// </summary>
    /// <remarks>
    /// A SMINumber contain a value of type float that can be get/set.
    /// The SMINumber keeps the StateMachine alive by maintaining a reference to it.
    /// </remarks>
    [Obsolete(ObsoleteMessages.Inputs)]
    public sealed class SMINumber : SMIInput
    {
        internal SMINumber(NativeSMIInputHandle smi, StateMachine stateMachineReference)
            : base(smi, stateMachineReference) { }

        ///  The value of the State Machine Number.
        public float Value
        {
            get => GetValue();
            set => SetValue(value);
        }
    }
}
