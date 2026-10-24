using NUnit.Framework;

namespace Rive.Tests
{
    /// <summary>
    /// What handle warnings say about their caller with Preferences > Rive >
    /// Caller In Warnings on and off.
    /// </summary>
    public class HandleCallSiteTests
    {
        private bool m_capture;

        [SetUp]
        public void SetUp()
        {
            m_capture = HandleErrors.CaptureCallSites;
        }

        [TearDown]
        public void TearDown()
        {
            HandleErrors.CaptureCallSites = m_capture;
        }

        [Test]
        public void On_NamesTheCallerFileAndLine()
        {
            HandleErrors.CaptureCallSites = true;
            string callSite = HandleErrors.CaptureCallSite();
            StringAssert.Contains($"{nameof(HandleCallSiteTests)}.{nameof(On_NamesTheCallerFileAndLine)}", callSite);
            StringAssert.Contains($"{nameof(HandleCallSiteTests)}.cs:", callSite);
        }

        [Test]
        public void On_SkipsRiveFrames_ToTheCaller()
        {
            HandleErrors.CaptureCallSites = true;
            // The handle captures in its constructor, several Rive frames
            // below this method.
            var instance = new ViewModelInstanceHandle(
                new NativeSlot<NativeViewModelInstanceHandle>(), HandleResolution.Pending(), null, -1, owned: false);
            NumberPropertyHandle property = instance.GetNumberProperty("Value");
            StringAssert.Contains($"{nameof(HandleCallSiteTests)}.{nameof(On_SkipsRiveFrames_ToTheCaller)}", property.CallSiteForTests);
            StringAssert.Contains($"{nameof(HandleCallSiteTests)}.cs:", property.CallSiteForTests);
        }

        [Test]
        public void On_NamesTheMethodALambdaCameFrom()
        {
            HandleErrors.CaptureCallSites = true;
            System.Func<string> capture = () => HandleErrors.CaptureCallSite();
            StringAssert.Contains($"(from {nameof(HandleCallSiteTests)}.{nameof(On_NamesTheMethodALambdaCameFrom)}, at ", capture());
        }

        [Test]
        public void Off_NotesNothing()
        {
            HandleErrors.CaptureCallSites = false;
            Assert.IsNull(HandleErrors.CaptureCallSite());
        }
    }
}
