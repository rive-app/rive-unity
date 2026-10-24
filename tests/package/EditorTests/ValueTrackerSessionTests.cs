using NUnit.Framework;
using Rive.EditorTools;
using UnityEditor;

namespace Rive.Tests
{
    /// <summary>
    /// The value tracker window's rules: kept in SessionState and applied to the runtime.
    /// </summary>
    public class ValueTrackerSessionTests
    {
        private const string RulesKey = "Rive.ValueTracker.Rules";
        private string m_savedRules;

        [SetUp]
        public void SetUp()
        {
            m_savedRules = SessionState.GetString(RulesKey, string.Empty);
            ValueTrackerSession.ClearRules();
        }

        [TearDown]
        public void TearDown()
        {
            SessionState.SetString(RulesKey, m_savedRules);
            ValueTrackerSession.Reload();
        }

        [Test]
        public void TracingAProperty_SavesItAndAppliesIt()
        {
            ValueTrackerSession.SetTracked("Hex", "VideoZoomedInY", true);

            Assert.IsTrue(ValueTrackerSession.IsTracked("Hex", "VideoZoomedInY"));
            StringAssert.Contains("Hex\tVideoZoomedInY", SessionState.GetString(RulesKey, string.Empty));
            Assert.IsTrue(ValueTracker.Active);
            Assert.IsTrue(ValueTracker.Matches("Hex", "VideoZoomedInY"));
            Assert.IsFalse(ValueTracker.Matches("Hex", "VideoZoomedInX"));
        }

        [Test]
        public void Untracing_RemovesOnlyThatProperty()
        {
            ValueTrackerSession.SetTracked("Hex", "VideoZoomedInY", true);
            ValueTrackerSession.SetTracked("Hex", "VideoZoomedOutY", true);
            ValueTrackerSession.SetTracked("Hex", "VideoZoomedInY", false);

            Assert.IsFalse(ValueTracker.Matches("Hex", "VideoZoomedInY"));
            Assert.IsTrue(ValueTracker.Matches("Hex", "VideoZoomedOutY"));
            ValueTrackerSession.ClearRules();
            Assert.IsFalse(ValueTracker.Active);
        }

        [Test]
        public void PlaygroundCards_OfferTracking_OnlyForTypesTheTrackerFollows()
        {
            Assert.IsTrue(DataBindingPlaygroundWindow.CanTrack("Hex", "Number"));
            Assert.IsTrue(DataBindingPlaygroundWindow.CanTrack("Person", "Enum (Country)"));
            Assert.IsTrue(DataBindingPlaygroundWindow.CanTrack("Person", "Trigger"));
            Assert.IsFalse(DataBindingPlaygroundWindow.CanTrack("Person", "Image"), "Images aren't tracked.");
            Assert.IsFalse(DataBindingPlaygroundWindow.CanTrack("Person", "List"), "Lists aren't tracked.");
            Assert.IsFalse(DataBindingPlaygroundWindow.CanTrack(null, "Number"), "Without its view model a card can't be tracked.");
        }

        [Test]
        public void Window_OpensAndCloses()
        {
            var window = EditorWindow.GetWindow<RiveValueTrackerWindow>();
            Assert.IsNotNull(window);
            window.Close();
        }
    }
}
