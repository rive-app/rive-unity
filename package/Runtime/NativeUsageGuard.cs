using System;
using Rive.Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rive
{
    internal enum NativeLoadFailureReason
    {
        None = 0,
        LibraryNotFound,
        EntryPointMissing,
        RestartRequired,
    }

    /// <summary>
    /// Guard to prevent usage of native Rive code if the native library is not available.
    /// e.g. if the user has not restarted the Unity Editor after updating the Rive package.
    /// </summary>
    internal static class NativeUsageGuard
    {
        private const string RESTART_REQUIRED_KEY = "Rive.NativeRestartRequired";
        private static bool? s_availabilityOverride;
        private static NativeLoadFailureReason s_nativeLoadFailureReason;
        private static string s_failureMessage;
        private static bool s_loggedNativeLoadFailure;

        /// <summary>
        /// Whether the native plugin is available for use. Cheap check suitable for hot paths like editor repainting.
        /// </summary>
        internal static bool IsNativeAvailable
        {
            get
            {
                if (s_availabilityOverride.HasValue)
                {
                    return s_availabilityOverride.Value;
                }

                if (s_nativeLoadFailureReason != NativeLoadFailureReason.None)
                {
                    return false;
                }

#if UNITY_EDITOR
                return !SessionState.GetBool(RESTART_REQUIRED_KEY, false);
#else
                return true;
#endif
            }
        }

        /// <summary>
        /// Marks the native runtime as permanently unavailable until the next domain reload.
        /// Called when the native library fails to load or is missing expected entry points.
        /// The corresponding message is logged once as an error.
        /// </summary>
        internal static void MarkNativeLoadFailed(NativeLoadFailureReason reason, string messageOverride = null)
        {
            if (s_nativeLoadFailureReason != NativeLoadFailureReason.None)
            {
                return;
            }

            s_nativeLoadFailureReason = reason;
            s_failureMessage = messageOverride ?? GetFailureMessage(reason);

            if (reason == NativeLoadFailureReason.RestartRequired)
            {
                MarkRestartRequired();
            }

            if (!s_loggedNativeLoadFailure)
            {
                s_loggedNativeLoadFailure = true;
                DebugLogger.Instance.LogError(s_failureMessage);
            }
        }

        private static string GetFailureMessage(NativeLoadFailureReason reason)
        {
            switch (reason)
            {
                case NativeLoadFailureReason.LibraryNotFound:
                    return "Native plugin could not be loaded in this environment. Please visit the documentation for more information about supported platforms.";
                case NativeLoadFailureReason.EntryPointMissing:
                    return "Native plugin is present but does not expose the expected entry points.";
                case NativeLoadFailureReason.RestartRequired:
                    return "Rive native plugin is unavailable because the package was updated. Please restart the Unity Editor.";
                default:
                    return "Native plugin is unavailable.";
            }
        }

        /// <summary>
        /// Temporarily overrides native availability for the lifetime of the returned scope.
        /// Intended for tests that need to simulate an unavailable native plugin without mutating editor session state.
        /// </summary>
        internal static IDisposable SetAvailabilityOverrideForScope(bool isAvailable)
        {
            return new AvailabilityOverrideScope(isAvailable);
        }

        private static void MarkRestartRequired()
        {
#if UNITY_EDITOR
            SessionState.SetBool(RESTART_REQUIRED_KEY, true);
#endif
        }

        /// <summary>
        /// Throws an InvalidOperationException if the native plugin is unavailable (e.g. after a package upgrade without an editor restart).
        /// Use this for user-facing API entry points where a clear exception is preferred over silent failure.
        /// </summary>
        internal static void ThrowIfNativeUnavailable()
        {
            if (s_nativeLoadFailureReason != NativeLoadFailureReason.None)
            {
                throw new InvalidOperationException(s_failureMessage);
            }

#if UNITY_EDITOR
            // s_failureMessage is set when MarkNativeLoadFailed is called within the current domain.
            // After a domain reload it resets to null, but the SessionState restart flag survives,
            // so we fall back to the restart message.
            if (!IsNativeAvailable)
            {
                throw new InvalidOperationException(
                    s_failureMessage ?? "Native plugin is unavailable because the package was updated. Please restart the Unity Editor."
                );
            }
#endif
        }

        private sealed class AvailabilityOverrideScope : IDisposable
        {
            private readonly bool? m_previousOverride;
            private bool m_disposed;

            internal AvailabilityOverrideScope(bool isAvailable)
            {
                m_previousOverride = s_availabilityOverride;
                s_availabilityOverride = isAvailable;
            }

            public void Dispose()
            {
                if (m_disposed)
                {
                    return;
                }

                s_availabilityOverride = m_previousOverride;
                m_disposed = true;
            }
        }
    }
}
