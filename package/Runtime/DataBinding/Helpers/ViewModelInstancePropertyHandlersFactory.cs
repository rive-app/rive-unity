using System;
using System.Collections.Generic;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Makes the property objects for a ViewModelInstance. Each instance keeps its own by name,
    /// so asking twice gives the same object.
    /// </summary>
    internal static class ViewModelInstancePropertyHandlersFactory
    {
        private static readonly Dictionary<Type, (ViewModelDataType type, Func<ViewModelInstance, string, int, ViewModelInstancePrimitiveProperty> create)>
            s_handlers = new Dictionary<Type, (ViewModelDataType, Func<ViewModelInstance, string, int, ViewModelInstancePrimitiveProperty>)>
            {
                { typeof(ViewModelInstanceEnumProperty), (ViewModelDataType.Enum, CreateEnumProperty) },
                { typeof(ViewModelInstanceTriggerProperty), (ViewModelDataType.Trigger, (i, n, slot) => new ViewModelInstanceTriggerProperty(i, n, slot)) },
                { typeof(ViewModelInstanceBooleanProperty), (ViewModelDataType.Boolean, (i, n, slot) => new ViewModelInstanceBooleanProperty(i, n, slot)) },
                { typeof(ViewModelInstanceNumberProperty), (ViewModelDataType.Number, (i, n, slot) => new ViewModelInstanceNumberProperty(i, n, slot)) },
                { typeof(ViewModelInstanceStringProperty), (ViewModelDataType.String, (i, n, slot) => new ViewModelInstanceStringProperty(i, n, slot)) },
                { typeof(ViewModelInstanceColorProperty), (ViewModelDataType.Color, (i, n, slot) => new ViewModelInstanceColorProperty(i, n, slot)) },
                { typeof(ViewModelInstanceImageProperty), (ViewModelDataType.AssetImage, (i, n, slot) => new ViewModelInstanceImageProperty(i, n, slot)) },
                { typeof(ViewModelInstanceFontProperty), (ViewModelDataType.AssetFont, (i, n, slot) => new ViewModelInstanceFontProperty(i, n, slot)) },
                { typeof(ViewModelInstanceListProperty), (ViewModelDataType.List, (i, n, slot) => new ViewModelInstanceListProperty(i, n, slot)) },
                { typeof(ViewModelInstanceArtboardProperty), (ViewModelDataType.Artboard, (i, n, slot) => new ViewModelInstanceArtboardProperty(i, n, slot)) },
            };

        /// <summary>
        /// Gets a property of the specified type from a view model instance.
        /// </summary>
        /// <param name="instance">The instance that directly holds the property.</param>
        /// <param name="name">The property's name on that instance. Not a path.</param>
        /// <returns>The property, or null if it doesn't exist or is another type.</returns>
        public static T GetPrimitiveProperty<T>(ViewModelInstance instance, string name) where T : ViewModelInstanceProperty
        {
            if (!s_handlers.TryGetValue(typeof(T), out var handler))
            {
                DebugLogger.Instance.LogError("Property type not supported: " + typeof(T).Name);
                return null;
            }

            if (instance.TryGetCachedProperty(name, out ViewModelInstancePrimitiveProperty cached))
            {
                if (cached is T typed)
                {
                    return typed;
                }
                DebugLogger.Instance.LogError("Failed to get property: " + name + ". Expected type: " + typeof(T).Name);
                return null;
            }

            if (!ViewModelNative.HasProperty(instance.NativeHandle, name, handler.type))
            {
                DebugLogger.Instance.LogError("Property not found: " + name);
                return null;
            }

            ViewModelInstancePrimitiveProperty created = handler.create(instance, name, 0);
            created.PropertyType = handler.type;
            // Another thread may have cached one for this name meanwhile, and its object is the one to use.
            var cachedProperty = instance.CacheProperty(name, created);
            if (cachedProperty is T typedProperty)
            {
                return typedProperty;
            }
            DebugLogger.Instance.LogError("Failed to get property: " + name + ". Expected type: " + typeof(T).Name);
            return null;
        }

        private static ViewModelInstancePrimitiveProperty CreateEnumProperty(ViewModelInstance instance, string name, int slot)
        {
            // Shared through the file's enum table when it's there, read off the property when not.
            ViewModelEnumData enumData = GetEnumData(instance, name, out string[] values);
            return new ViewModelInstanceEnumProperty(instance, name, slot, enumData?.ValuesArray ?? values);
        }

        private static ViewModelEnumData GetEnumData(ViewModelInstance instance, string name, out string[] values)
        {
            File file = instance.RiveFile;
            int index = ViewModelNative.EnumType(instance.NativeHandle, file != null ? file.NativeFile : default, name, out values);
            if (file == null)
            {
                return null;
            }
            IReadOnlyList<ViewModelEnumData> enums = file.ViewModelEnums;
            if (index < 0 || enums == null || index >= enums.Count)
            {
                return null;
            }
            return enums[index];
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only utility to get the enum data for a property on an instance.
        /// </summary>
        internal static ViewModelEnumData GetEnumForPropertyAtPath(ViewModelInstance vmInstance, string path)
        {
            if (vmInstance == null || vmInstance.RiveFile == null)
            {
                return null;
            }
            return GetEnumData(vmInstance, path, out _);
        }
#endif
    }
}
