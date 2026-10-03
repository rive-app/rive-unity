namespace Rive.Components
{
    /// <summary>
    /// Where a panel's Rive work happens, and so which API its widgets give you.
    /// </summary>
    public enum ThreadingMode
    {
        /// <summary>
        /// The frame waits for Rive's work, and widgets give you <see cref="File"/>, <see cref="Artboard"/> and <see cref="StateMachine"/>. This is how Rive has always behaved.
        /// </summary>
        MainThread = 0,

        /// <summary>
        /// Rive's work runs on its own thread and the frame never waits for it. Widgets give you <see cref="FileHandle"/>, <see cref="ArtboardHandle"/> and <see cref="StateMachineHandle"/>, on which nothing waits.
        /// </summary>
        /// <remarks>
        /// In edit mode and on WebGL there's no thread for Rive, so its work runs inline on the main thread. The API is the same.
        /// </remarks>
        BackgroundThread = 1
    }
}
