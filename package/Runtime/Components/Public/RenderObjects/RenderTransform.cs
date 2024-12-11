using Rive.Utils;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Holds the data for a render object's transform in Rive coordinates. Rive uses a cartesian coordinate system where the positive x-axis extends towards the right, and the positive y-axis extends towards the bottom of the screen. The origin (0,0) is located at the top left corner of the screen.
    /// </summary>
    public struct RenderTransform
    {
        private Vector2 m_position;
        private Vector2 m_size;
        private float m_rotation;
        private Vector2 m_scale;
        private Vector2 m_pivot;

        private static readonly Vector3[] s_widgetCorners = new Vector3[4];
        private static readonly Vector3[] s_panelCorners = new Vector3[4];


        // Top-left corner in Rive coordinates

        /// <summary>
        /// The position of the Transform in Rive coordinates within it's panel
        /// </summary>
        public Vector2 Position => m_position;

        /// <summary>
        /// The dimensions of the Transform;
        /// </summary>
        public Vector2 Size => m_size;

        /// <summary>
        /// The rotation of the Transform in degrees
        /// </summary>
        public float Rotation => m_rotation;

        /// <summary>
        /// The scale of the Transform within it's parent panel
        /// </summary>
        public Vector2 Scale => m_scale;

        /// <summary>
        /// The pivot point of the Transform
        /// </summary>
        public Vector2 Pivot => m_pivot;

        /// <summary>
        /// Creates a new RenderTransform with the given position, size, rotation, scale, and pivot.
        /// </summary>
        /// <param name="position"> The position of the Transform in Rive coordinates within it's panel. </param>
        /// <param name="size"> The dimensions of the Transform. </param>
        /// <param name="rotation"> The rotation of the Transform in degrees. </param>
        /// <param name="scale"> The scale of the Transform within it's parent panel. </param>
        /// <param name="pivot"> The pivot point of the Transform. </param>
        public RenderTransform(Vector2 position, Vector2 size, float rotation, Vector2 scale, Vector2 pivot)
        {
            m_position = position;
            m_size = size;
            m_rotation = rotation;
            m_scale = scale;
            m_pivot = pivot;
        }



        /// <summary>
        /// Creates a new RenderTransform from the given RectTransform. This method will convert the RectTransform's position, size, rotation, scale, and pivot to Rive coordinates.
        /// </summary>
        /// <param name="rectTransform"> The RectTransform to create the RenderTransform from. </param>
        /// <returns> A new RenderTransform with the given RectTransform's data.</returns>
        public static RenderTransform FromRectTransform(RectTransform rectTransform, RectTransform panelRectTransform)
        {
            if (rectTransform == null || panelRectTransform == null)
            {
                DebugLogger.Instance.LogError("FromRectTransform called with null rectTransform or panelRectTransform");
                return default;
            }

            // Calculate adjusted data if pivot is not (0,0)
            RectTransformPivotUtility.RectTransformData adjustedData = rectTransform.pivot != Vector2.zero
                ? RectTransformPivotUtility.CalculatePivotChange(rectTransform, Vector2.zero)
                : new RectTransformPivotUtility.RectTransformData(rectTransform.pivot, rectTransform.anchoredPosition, rectTransform.localPosition);

            Vector2 size = rectTransform.rect.size;
            bool shouldFlip = TextureHelper.ShouldFlipTexture();

            Matrix4x4 parentMatrix = panelRectTransform.localToWorldMatrix;
            Matrix4x4 inverseParentMatrix = parentMatrix.inverse;
            Matrix4x4 childMatrix = rectTransform.localToWorldMatrix;

            Matrix4x4 relativeMatrix = inverseParentMatrix * childMatrix;

            // Get the world position of the widget and panel corners
            // We use world positions for our calculations to account for the rect transform being in a complicated heirarchy
            // Before we pass the transform info to the Rive renderer, we essentially flatten the hierarchy and render the widgets in absolute space to simplify the matrix calculations.

            rectTransform.GetWorldCorners(s_widgetCorners);
            panelRectTransform.GetWorldCorners(s_panelCorners);

            Vector3 widgetWorldPosTL = s_widgetCorners[1];
            Vector3 panelWorldPosTL = s_panelCorners[1];

            // Calculate relative position in world space
            Vector3 relativeWorldPos = widgetWorldPosTL - panelWorldPosTL;

            // Convert world space difference to panel's local space
            Vector3 relativeLocalPos = inverseParentMatrix.MultiplyVector(relativeWorldPos);

            Vector2 position = new Vector2(relativeLocalPos.x, relativeLocalPos.y);

            // Adjust Y position if flipping is required based on the texture coordinate system
            if (shouldFlip)
            {
                position.y = panelRectTransform.rect.size.y + position.y + rectTransform.rect.height;
            }
            else
            {
                position.y = panelRectTransform.rect.size.y - position.y - panelRectTransform.rect.height;
            }

            // Extract rotation (in radians) from the relative matrix
            float rotation = Mathf.Atan2(relativeMatrix.m01, relativeMatrix.m00);

            // Extract scale from the relative matrix
            Vector2 scale = new Vector2(
                new Vector2(relativeMatrix.m00, relativeMatrix.m01).magnitude,
                new Vector2(relativeMatrix.m10, relativeMatrix.m11).magnitude
            );

            Vector2 pivot = adjustedData.Pivot;

            return new RenderTransform(position, size, rotation, scale, pivot);
        }

    }


    /// <summary>
    /// We use this to calculate pivot changes for RectTransforms while maintaining the world position.
    /// </summary>
    internal class RectTransformPivotUtility
    {
        public struct RectTransformData
        {
            public Vector2 Pivot;
            public Vector2 AnchoredPosition;
            public Vector3 LocalPosition;

            public RectTransformData(Vector2 pivot, Vector2 anchoredPosition, Vector3 localPosition)
            {
                Pivot = pivot;
                AnchoredPosition = anchoredPosition;
                LocalPosition = localPosition;
            }
        }

        public static RectTransformData CalculatePivotChange(RectTransform rectTransform, Vector2 newPivot)
        {
            Vector2 size = rectTransform.rect.size;

            // Get the offset in anchored position
            Vector2 pivotDelta = newPivot - rectTransform.pivot;
            Vector2 positionDelta = new Vector2(
                pivotDelta.x * size.x,
                pivotDelta.y * size.y
            );

            Vector2 newAnchoredPosition = rectTransform.anchoredPosition - positionDelta;

            // Calculate new local position (to maintain world position)
            Vector3 localPositionDelta = rectTransform.localRotation * new Vector3(positionDelta.x, positionDelta.y, 0);
            Vector3 newLocalPosition = rectTransform.localPosition + localPositionDelta;

            return new RectTransformData(newPivot, newAnchoredPosition, newLocalPosition);
        }

        public static void ApplyPivotChange(RectTransform rectTransform, RectTransformData data)
        {
            rectTransform.pivot = data.Pivot;
            rectTransform.anchoredPosition = data.AnchoredPosition;
            rectTransform.localPosition = data.LocalPosition;
        }

        public static void SetPivot(RectTransform rectTransform, Vector2 newPivot)
        {
            RectTransformData newData = CalculatePivotChange(rectTransform, newPivot);
            ApplyPivotChange(rectTransform, newData);
        }
    }




}
