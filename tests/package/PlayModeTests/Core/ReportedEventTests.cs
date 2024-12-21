using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Tests.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public static class EventTestUtils
    {
        /// <summary>
        /// Waits for an event to be triggered within a specified timeout period
        /// </summary>
        /// <param name="stateMachine">The state machine to check for events</param>
        /// <param name="onEvent">Action to handle any matching events</param>
        /// <param name="shouldContinue">Function that returns whether to continue waiting for more events (defaults to stopping after events have been received in a frame)</param>
        /// <param name="timeoutSeconds">Maximum time to wait for the event (defaults to 0.3s)</param>
        /// <param name="requireAdvance0">Whether to call Advance(0) before checking for events (defaults to true)</param>
        /// <param name="useEnumerator">Whether to use the enumerator version of the coroutine (defaults to false). This is useful when you want to process events as they come in, rather than waiting for all events to be processed</param>
        /// <returns>IEnumerator for Unity coroutine</returns>
        public static IEnumerator WaitForEvent(
            StateMachine stateMachine,
            Action<ReportedEvent> onEvent,
            Func<bool> shouldContinue = null,
            float timeoutSeconds = 0.3f,
            bool requireAdvance0 = true,
            bool useEnumerator = false
            )
        {
            if (requireAdvance0)
            {
                stateMachine.Advance(0f);
            }

            // If no continue condition provided, stop after processing any events
            if (shouldContinue == null)
            {
                shouldContinue = () => false;
            }
            bool foundEventThisFrame = false;

            float elapsedTime = 0f;
            while (elapsedTime < timeoutSeconds)
            {
                foundEventThisFrame = false;

                if (useEnumerator)
                {
                    foreach (var reportedEvent in stateMachine.EnumerateReportedEvents())
                    {
                        foundEventThisFrame = true;
                        onEvent(reportedEvent);
                    }
                }
                else
                {


                    foreach (var reportedEvent in stateMachine.ReportedEvents())
                    {
                        foundEventThisFrame = true;
                        onEvent(reportedEvent);
                    }
                }
                // If we found events this frame and shouldn't continue, stop
                if (foundEventThisFrame && !shouldContinue())
                {
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                stateMachine.Advance(Time.deltaTime);
                yield return null;
            }
        }
    }
    public class ReportedEventTests
    {
        TestAssetLoadingManager testAssetLoadingManager;
        private Artboard m_loadedArtboard;
        private File m_loadedFile;
        private StateMachine m_stateMachine;

        /// <summary>
        /// The name of the trigger that fires a simple event
        /// </summary>
        public const string TRIGGER_SIMPLE = "simple_trigger";

        /// <summary>
        /// The name of the trigger that fires an event with properties
        /// </summary>
        public const string TRIGGER_WITH_PROPS = "props_trigger";

        /// <summary>
        /// The name of the trigger that fires multiple events in the same frame
        /// </summary>
        public const string TRIGGER_MULTI_EVENT = "multi_event_trigger";

        /// <summary>
        /// The name of the simple event
        /// </summary>
        public const string EVENT_SIMPLE = "SIMPLE_EVENT";

        /// <summary>
        /// The name of the event with properties
        /// </summary>
        public const string EVENT_WITH_PROPS = "PROPS_EVENT";


        /// <summary>
        /// The name of the property field in the event with properties
        /// </summary>
        public const string PROP_EVENT_NUMBER_FIELD = "numberProp";

        /// <summary>
        /// The name of the property field in the event with properties
        /// </summary>
        public const string PROP_EVENT_BOOL_FIELD = "boolProp";

        /// <summary>
        /// The name of the property field in the event with properties
        /// </summary>
        public const string PROP_EVENT_STRING_FIELD = "stringProp";


        [UnitySetUp]
        public IEnumerator Setup()
        {
            testAssetLoadingManager = new TestAssetLoadingManager();
            Asset riveAsset = null;

            string riveAssetPath = TestAssetReferences.riv_events_test;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(riveAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {riveAssetPath}"));

            m_loadedFile = File.Load(riveAsset);
            m_loadedArtboard = m_loadedFile.Artboard(0);
            m_stateMachine = m_loadedArtboard.StateMachine();
        }

        [TearDown]
        public void TearDown()
        {
            m_loadedFile?.Dispose();
            testAssetLoadingManager.UnloadAllAssets();
        }

        [UnityTest]
        public IEnumerator EventPooling_ReturnsNewInstance_WhenNotDisposed()
        {
            var trigger = m_stateMachine.GetTrigger(TRIGGER_SIMPLE);
            ReportedEvent firstEvent = null;
            ReportedEvent secondEvent = null;

            m_stateMachine.Advance(0);
            yield return null;

            trigger.Fire();

            // Get the first event
            yield return EventTestUtils.WaitForEvent(
                m_stateMachine,
                evt =>
                {
                    if (evt.Name == EVENT_SIMPLE)
                    {
                        firstEvent = evt;
                    }
                }
            );

            // Fire again to get second event
            trigger.Fire();

            yield return EventTestUtils.WaitForEvent(
                m_stateMachine,
                evt =>
                {
                    if (evt.Name == EVENT_SIMPLE)
                    {
                        secondEvent = evt;
                    }
                }
            );


            Assert.That(firstEvent, Is.Not.Null);
            Assert.That(secondEvent, Is.Not.Null);
            Assert.That(firstEvent, Is.Not.SameAs(secondEvent), "Events should be different instances when not disposed");

            yield return null;
        }

        [UnityTest]
        public IEnumerator EventPooling_ReuseInstance_WhenDisposed()
        {
            var triggerSimple = m_stateMachine.GetTrigger(TRIGGER_SIMPLE);
            var triggerWithProps = m_stateMachine.GetTrigger(TRIGGER_WITH_PROPS);
            ReportedEvent firstEvent = null;
            ReportedEvent secondEvent = null;

            m_stateMachine.Advance(0);
            yield return null;
            triggerSimple.Fire();

            // Capture and dispose first event
            yield return EventTestUtils.WaitForEvent(
               m_stateMachine,
               evt =>
               {
                   if (evt.Name == EVENT_SIMPLE)
                   {
                       firstEvent = evt;
                       firstEvent.Dispose();
                   }
               }
           );

            // Assert the values are expected
            Assert.That(firstEvent, Is.Not.Null);
            Assert.That(firstEvent.Name, Is.EqualTo(EVENT_SIMPLE));
            Assert.That(firstEvent.Properties, Is.Empty);
            Assert.That(firstEvent.PropertyCount, Is.Zero);

            yield return null;

            // Fire again to get second event
            triggerWithProps.Fire();

            yield return EventTestUtils.WaitForEvent(
                m_stateMachine,
                evt =>
                {
                    if (evt.Name == EVENT_WITH_PROPS)
                    {
                        secondEvent = evt;
                        secondEvent.Dispose();
                    }
                }
            );

            Assert.That(secondEvent, Is.Not.Null);
            Assert.That(firstEvent, Is.SameAs(secondEvent), "Event instance should be reused after disposal");
            Assert.That(secondEvent.Name, Is.EqualTo(EVENT_WITH_PROPS));
            Assert.That(secondEvent.Properties, Is.Not.Null);
            Assert.That(secondEvent.PropertyCount, Is.EqualTo(3));

            yield return null;

            // Try the simple event again to ensure it's still working

            triggerSimple.Fire();

            yield return EventTestUtils.WaitForEvent(
                m_stateMachine,
                evt =>
                {
                    if (evt.Name == EVENT_SIMPLE)
                    {
                        firstEvent = evt;
                    }
                }
            );

            Assert.That(firstEvent, Is.Not.Null);
            Assert.That(firstEvent.Name, Is.EqualTo(EVENT_SIMPLE));
            Assert.That(firstEvent.PropertyCount, Is.Zero);
            Assert.That(firstEvent, Is.SameAs(secondEvent), "Event instance should be reused after disposal");



            yield return null;
        }

        [UnityTest]
        public IEnumerator EventProperties_CanBeAccessed_BothWays()
        {
            var trigger = m_stateMachine.GetTrigger(TRIGGER_WITH_PROPS);
            ReportedEvent eventWithProps = null;

            m_stateMachine.Advance(0);
            trigger.Fire();

            yield return EventTestUtils.WaitForEvent(
                m_stateMachine,
                evt =>
                {
                    if (evt.Name == EVENT_WITH_PROPS)
                    {
                        eventWithProps = evt;
                    }
                }
            );

            Assert.That(eventWithProps, Is.Not.Null);

            // Test both access methods
            Assert.That(eventWithProps[PROP_EVENT_NUMBER_FIELD], Is.EqualTo(eventWithProps.Properties[PROP_EVENT_NUMBER_FIELD]));
            Assert.That(eventWithProps[PROP_EVENT_BOOL_FIELD], Is.EqualTo(eventWithProps.Properties[PROP_EVENT_BOOL_FIELD]));
            Assert.That(eventWithProps[PROP_EVENT_STRING_FIELD], Is.EqualTo(eventWithProps.Properties[PROP_EVENT_STRING_FIELD]));

            yield return null;
        }

        [UnityTest]
        public IEnumerator EventProperties_CanBeIterated_AndTyped()
        {
            var trigger = m_stateMachine.GetTrigger(TRIGGER_WITH_PROPS);
            ReportedEvent eventWithProps = null;

            m_stateMachine.Advance(0);
            trigger.Fire();


            yield return EventTestUtils.WaitForEvent(
                m_stateMachine,
                evt =>
                {
                    if (evt.Name == EVENT_WITH_PROPS)
                    {
                        eventWithProps = evt;
                    }
                }
            );

            Assert.That(eventWithProps, Is.Not.Null);
            Assert.That(eventWithProps.PropertyCount, Is.EqualTo(3), "Should have 3 properties");

            // We want to test that we can loop over the properties and use type-safe getters as an alternative to the dictionary approach since that requires casting
            var foundProps = new Dictionary<string, object>();

            for (uint i = 0; i < eventWithProps.PropertyCount; i++)
            {
                var prop = eventWithProps.GetProperty(i);

                switch (prop.Type)
                {
                    case ReportedEvent.PropertyType.Number:
                        float numberValue;
                        Assert.That(prop.TryGetNumber(out numberValue), Is.True);
                        foundProps[prop.Name] = numberValue;
                        break;

                    case ReportedEvent.PropertyType.Bool:
                        bool boolValue;
                        Assert.That(prop.TryGetBool(out boolValue), Is.True);
                        foundProps[prop.Name] = boolValue;
                        break;

                    case ReportedEvent.PropertyType.String:
                        string stringValue;
                        Assert.That(prop.TryGetString(out stringValue), Is.True);
                        foundProps[prop.Name] = stringValue;
                        break;
                }

                // Test that wrong type getters return false
                if (prop.Type != ReportedEvent.PropertyType.Number)
                {
                    float dummy;
                    Assert.That(prop.TryGetNumber(out dummy), Is.False);
                }
                if (prop.Type != ReportedEvent.PropertyType.Bool)
                {
                    bool dummy;
                    Assert.That(prop.TryGetBool(out dummy), Is.False);
                }
                if (prop.Type != ReportedEvent.PropertyType.String)
                {
                    string dummy;
                    Assert.That(prop.TryGetString(out dummy), Is.False);
                }
            }

            // Verify we found all properties with correct types
            Assert.That(foundProps.Count, Is.EqualTo(3));
            Assert.That(foundProps[PROP_EVENT_NUMBER_FIELD], Is.TypeOf<float>());
            Assert.That(foundProps[PROP_EVENT_BOOL_FIELD], Is.TypeOf<bool>());
            Assert.That(foundProps[PROP_EVENT_STRING_FIELD], Is.TypeOf<string>());

            yield return null;
        }

        [UnityTest]
        public IEnumerator MultipleEvents_InSameFrame_AreHandled()
        {
            var multiTrigger = m_stateMachine.GetTrigger(TRIGGER_MULTI_EVENT);
            var receivedEvents = new List<string>();

            m_stateMachine.Advance(0);

            multiTrigger.Fire();

            yield return EventTestUtils.WaitForEvent(
                m_stateMachine,
                evt => receivedEvents.Add(evt.Name)
            );

            Assert.That(receivedEvents, Has.Count.EqualTo(2), "Should receive both events");
            Assert.That(receivedEvents, Contains.Item(EVENT_SIMPLE), "Should receive simple event");
            Assert.That(receivedEvents, Contains.Item(EVENT_WITH_PROPS), "Should receive props event");
            Assert.That(receivedEvents[0], Is.Not.SameAs(receivedEvents[1]), "Events should be different instances");
        }

        [UnityTest]
        public IEnumerator MultipleEvents_InSameFrame_ReuseInstance_WhenDisposed()
        {
            var multiTrigger = m_stateMachine.GetTrigger(TRIGGER_MULTI_EVENT);
            var firstFrameEvents = new List<ReportedEvent>();
            var eventNames = new List<string>();

            m_stateMachine.Advance(0);
            multiTrigger.Fire();

            yield return EventTestUtils.WaitForEvent(
                m_stateMachine,
                evt =>
                {
                    firstFrameEvents.Add(evt);
                    eventNames.Add(evt.Name);
                    evt.Dispose(); // Dispose each event to force it to use the same instance for both events
                },
                useEnumerator: true // So that the callback is called for each event, instead of waiting for all events to be processed
            );

            Assert.That(firstFrameEvents, Has.Count.EqualTo(2), "Should receive both events");

            Assert.That(firstFrameEvents[0], Is.SameAs(firstFrameEvents[1]), "Second event should be reused from pool");

            // We want to test that we still have access to the unique values in the callback
            Assert.That(eventNames, Contains.Item(EVENT_SIMPLE), "Should receive simple event");
            Assert.That(eventNames, Contains.Item(EVENT_WITH_PROPS), "Should receive props event");

        }


    }
}