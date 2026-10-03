using System;
using Rive.Producer;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// Sends the lookup behind a handle that's returned straight away. Its
    /// reply records the result before replies to anything sent after it,
    /// and reports a failure once. A handle built on one that failed takes
    /// its error without reporting it again.
    /// </summary>
    internal static class HandleLookup
    {
        /// send queues the native work and a check that replies with a u32
        /// ok. notFound says why when it's 0.
        internal static void Later(
            HandleResolution resolution,
            HandleResolution parent,
            Action<ulong> send,
            Func<RiveException> notFound,
            Action<RiveException> errorSink,
            string callSite)
        {
            CommandTransport.Send(
                send,
                (batch, message) =>
                {
                    bool found = new PayloadReader(batch, message).Ok();
                    Run(resolution, parent, () => found ? null : notFound(), errorSink, callSite);
                },
                keep: false);
        }

        /// For a reply that isn't a plain ok. check reads it and says why
        /// nothing was found, or null.
        internal static void Later(
            HandleResolution resolution,
            HandleResolution parent,
            Action<ulong> send,
            Func<HostMessageBatch, HostMessage, RiveException> check,
            Action<RiveException> errorSink,
            string callSite)
        {
            CommandTransport.Send(
                send,
                (batch, message) =>
                {
                    RiveException problem = check(batch, message);
                    Run(resolution, parent, () => problem, errorSink, callSite);
                },
                keep: false);
        }

        /// In the drain that delivers the reply.
        internal static void Run(
            HandleResolution resolution,
            HandleResolution parent,
            Func<RiveException> lookup,
            Action<RiveException> errorSink,
            string callSite)
        {
            if (parent != null && parent.FailedOnRive)
            {
                resolution.FailOnRive(parent.RiveError);
                return;
            }
            RiveException error;
            try
            {
                error = lookup();
            }
            catch (RiveException e)
            {
                error = e;
            }
            if (error == null)
            {
                resolution.SucceedOnRive();
                return;
            }
            HandleErrors.ReportLater(error, errorSink, callSite);
            resolution.FailOnRive(error);
        }
    }
}
