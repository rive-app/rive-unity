using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rive.Tests.Utils
{
    /// <summary>
    /// Test input module for simulating pointer events
    /// </summary>
    public class TestInputModule : StandaloneInputModule
    {
        public void PointerDownAt(Vector2 position)
        {
            float x = position.x;
            float y = position.y;
            Input.simulateMouseWithTouches = true;
            var pointerData = GetTouchPointerEventData(new Touch()
            {
                position = new Vector2(x, y),
            }, out bool b, out bool bb);

            ProcessTouchPress(pointerData, true, false);
        }

        public void PointerMoveAt(Vector2 position)
        {
            float x = position.x;
            float y = position.y;
            var pointerData = GetTouchPointerEventData(new Touch()
            {
                position = new Vector2(x, y),
            }, out bool b, out bool bb);

            ProcessMove(pointerData);
        }

        public void PointerUpAt(Vector2 position)
        {
            float x = position.x;
            float y = position.y;
            var pointerData = GetTouchPointerEventData(new Touch()
            {
                position = position,
            }, out bool b, out bool bb);

            ProcessTouchPress(pointerData, false, true);
        }
    }
}