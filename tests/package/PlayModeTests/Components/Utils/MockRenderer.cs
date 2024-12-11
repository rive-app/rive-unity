using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rive.Tests.Utils
{
    public class MockRenderer : IRenderer
    {
        public event Action<CommandBuffer, bool> OnAddToCommandBuffer;
        public event Action OnClear;
        public event Action OnSubmit;
        public event Action OnSubmitAndRelease;
        public event Action<Path> OnClip;
        public event Action<Artboard> OnDraw;
        public event Action<Path, Paint> OnDrawPath;
        public event Action OnSave;
        public event Action OnRestore;
        public event Action<System.Numerics.Matrix3x2> OnTransform;
        public event Action<float, float> OnTranslate;
        public event Action<Fit, Alignment, Artboard, AABB, float> OnAlign;

        public void AddToCommandBuffer(CommandBuffer commandBuffer, bool release = false)
        {
            OnAddToCommandBuffer?.Invoke(commandBuffer, release);
        }

        public void Clear() => OnClear?.Invoke();
        public void Submit() => OnSubmit?.Invoke();
        public void SubmitAndRelease() => OnSubmitAndRelease?.Invoke();
        public void Clip(Path path) => OnClip?.Invoke(path);
        public void Draw(Artboard artboard) => OnDraw?.Invoke(artboard);
        public void Draw(Path path, Paint paint) => OnDrawPath?.Invoke(path, paint);
        public void Save() => OnSave?.Invoke();
        public void Restore() => OnRestore?.Invoke();
        public void Transform(System.Numerics.Matrix3x2 matrix) => OnTransform?.Invoke(matrix);
        public void Translate(System.Numerics.Vector2 translation) =>
            OnTranslate?.Invoke(translation.X, translation.Y);
        public void Translate(float x, float y) => OnTranslate?.Invoke(x, y);

        public void Align(Fit fit, Alignment alignment, Artboard artboard, float scaleFactor = 1) =>
            OnAlign?.Invoke(fit, alignment, artboard, default, scaleFactor);

        public void Align(Fit fit, Alignment alignment, Artboard artboard, AABB frame, float scaleFactor = 1) =>
            OnAlign?.Invoke(fit, alignment, artboard, frame, scaleFactor);
    }
}