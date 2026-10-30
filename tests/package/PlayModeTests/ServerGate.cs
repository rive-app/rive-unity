using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using Rive.Host;

namespace Rive.Tests
{
    /// <summary>
    /// Holds the command server so a test can queue work behind it. The
    /// server parks before Hold returns, so started is already set. Setting
    /// gate lets it go at the next drain, and so does five seconds passing.
    /// </summary>
    internal static class ServerGate
    {
        private const int TimeoutMs = 5000;

        private sealed class Held
        {
            internal ManualResetEventSlim Gate;
            internal Stopwatch Since;
        }

        private static readonly List<Held> s_held = new List<Held>();

        /// True while a hold hasn't been let go.
        internal static bool StillHeld => s_held.Count > 0;

        internal static void Hold(ManualResetEventSlim started, ManualResetEventSlim gate)
        {
            bool held = false;
            RequestTicket ticket = CommandTransport.Send(
                id => held = HostNative.riveHostHold(id),
                (batch, message) => started.Set());
            Assert.IsTrue(held, "Holding the server needs a threaded host.");

            s_held.Add(new Held { Gate = gate, Since = Stopwatch.StartNew() });
            CommandTransport.BeforeDrainForTests = ReleaseOpenGates;

            var timer = Stopwatch.StartNew();
            while (!started.IsSet && timer.ElapsedMilliseconds < 2000)
            {
                CommandTransport.Drain();
                Thread.Yield();
            }
            CommandTransport.IsDone(ref ticket);
        }

        /// Parks the server behind everything sent so far and lets it go from
        /// another thread after milliseconds, without the main thread waiting.
        /// Join the thread before the test ends.
        internal static Thread HoldFor(int milliseconds)
        {
            CommandTransport.Send(id => HostNative.riveHostHold(id), (batch, message) => { }, keep: false);
            var release = new Thread(() =>
            {
                Thread.Sleep(milliseconds);
                HostNative.riveHostRelease();
            })
            { IsBackground = true };
            release.Start();
            return release;
        }

        // One release per hold, oldest first, the order the server parks in.
        private static void ReleaseOpenGates()
        {
            while (s_held.Count > 0)
            {
                Held oldest = s_held[0];
                if (!oldest.Gate.IsSet && oldest.Since.ElapsedMilliseconds < TimeoutMs)
                {
                    return;
                }
                s_held.RemoveAt(0);
                HostNative.riveHostRelease();
            }
            CommandTransport.BeforeDrainForTests = null;
        }
    }

    internal static class TestServer
    {
        /// A Future that finishes with value once the server has run
        /// everything sent before it.
        internal static Future<T> EchoAsync<T>(T value)
        {
            return EchoAsync(() => value);
        }

        /// The same, with the result made when the reply lands. A throw fails it.
        internal static Future<T> EchoAsync<T>(System.Func<T> result)
        {
            return CommandTransport.SendFuture(
                id => HostNative.riveHostEcho(id, System.Array.Empty<byte>(), 0),
                (batch, message) => result());
        }
    }
}
