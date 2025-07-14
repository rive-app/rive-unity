using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Factory class responsible for creating property handlers for the ViewModelInstance class.
    ///  This centralizes all property-related functionality outside of ViewModelInstance.
    /// </summary>
    internal static class ViewModelInstancePropertyHandlersFactory
    {
        #region Property Result Types

        /// <summary>
        /// Represents the result of a property getter operation.
        /// </summary>
        private readonly struct PropertyGetterResult
        {
            public enum EnumTypeOption
            {
                None = 0,
                CustomEnum = 1,
                SystemEnum = 2
            }

            public readonly IntPtr PropertyPtr { get; }
            public readonly EnumTypeOption EnumType { get; }
            public readonly nuint? EnumIndex { get; }
            public ViewModelInstance RootViewModelInstance { get; }

            public PropertyGetterResult(IntPtr property, ViewModelInstance rootVmInstance, EnumTypeOption enumType = EnumTypeOption.None, nuint? enumIndex = null)
            {
                PropertyPtr = property;
                EnumType = enumType;
                EnumIndex = enumIndex;
                RootViewModelInstance = rootVmInstance;
            }
        }

        /// <summary>
        /// Delegate for property getter functions
        /// </summary>
        private delegate PropertyGetterResult PropertyGetter(ViewModelInstance instanceToGetPropertyFrom, string path, ViewModelInstance rootInstance);

        #endregion

        #region Property Cache



        #endregion

        #region Property Handlers

        private static readonly Dictionary<Type, (PropertyGetter getter, Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty> creator)>
            PropertyHandlers = InitializePropertyHandlers();

        private static Dictionary<Type, (PropertyGetter getter, Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty> creator)> InitializePropertyHandlers()
        {
            return new Dictionary<Type, (PropertyGetter, Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty>)>
            {
                { typeof(ViewModelInstanceEnumProperty), CreateEnumPropertyHandler() },
                { typeof(ViewModelInstanceTriggerProperty), CreateTriggerPropertyHandler() },
                { typeof(ViewModelInstanceBooleanProperty), CreateBooleanPropertyHandler() },
                { typeof(ViewModelInstanceNumberProperty), CreateNumberPropertyHandler() },
                { typeof(ViewModelInstanceStringProperty), CreateStringPropertyHandler() },
                { typeof(ViewModelInstanceColorProperty), CreateColorPropertyHandler() },
                { typeof(ViewModelInstanceImageProperty), CreateImagePropertyHandler() },
                { typeof(ViewModelInstanceListProperty), CreateListPropertyHandler() }
            };
        }

        /// <summary>
        /// Creates a handler for enum properties
        /// </summary>
        private static (PropertyGetter getter,
            Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty> creator) CreateEnumPropertyHandler()
        {
            return (
                (instance, path, rootInstance) =>
                {
                    // If the file that the instance was loaded from is no longer available, this will be null
                    IntPtr nativeFilePtr = instance.RiveFile != null ? instance.RiveFile.NativeFile : IntPtr.Zero;
                    var info = getEnumPropertyInfoFromViewModelInstance(
                        instance.NativeSafeHandle,
                        nativeFilePtr,
                        path);



                    return new PropertyGetterResult(
                        info.propertyPtr,
                        rootInstance,
                        PropertyGetterResult.EnumTypeOption.CustomEnum,
                        info.enumIndex);
                },
                (result) =>
                {
                    // If the property was not found, or if the property is not an enum, this will be true
                    if (result.PropertyPtr == IntPtr.Zero)
                    {
                        return null;
                    }

                    IReadOnlyList<ViewModelEnumData> enumsForFile = result.RootViewModelInstance.RiveFile != null ? result.RootViewModelInstance.RiveFile.ViewModelEnums : null;


                    bool isValidIndex = result.EnumIndex.HasValue && result.EnumIndex.Value >= 0 && (int)result.EnumIndex.Value < enumsForFile.Count;

                    // If the enums are included in the file, we can reuse them across multiple instances
                    // Otherwise, we'll have to fetch the enum values from the instance. This happens in the ViewModelInstanceEnumProperty constructor if we don't have the enum values already.
                    if (isValidIndex && enumsForFile != null)
                    {
                        string[] enumValues = enumsForFile[(int)result.EnumIndex.Value].ValuesArray;
                        return new ViewModelInstanceEnumProperty(
                            result.PropertyPtr,
                            result.RootViewModelInstance,
                            enumValues);
                    }
                    else
                    {
                        return new ViewModelInstanceEnumProperty(
                            result.PropertyPtr,
                            result.RootViewModelInstance);
                    }
                }
            );
        }

        /// <summary>
        /// Creates a handler for trigger properties
        /// </summary>
        private static (PropertyGetter getter,
            Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty> creator) CreateTriggerPropertyHandler()
        {
            return (
                (instance, path, rootInstance) => new PropertyGetterResult(
                    getViewModelInstanceTriggerProperty(instance.NativeSafeHandle, path),
                    rootInstance,
                    PropertyGetterResult.EnumTypeOption.None),
                (result) => new ViewModelInstanceTriggerProperty(
                    result.PropertyPtr,
                    result.RootViewModelInstance)
            );
        }

        /// <summary>
        /// Creates a handler for boolean properties
        /// </summary>
        private static (PropertyGetter getter,
            Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty> creator) CreateBooleanPropertyHandler()
        {
            return (
                (instance, path, rootInstance) => new PropertyGetterResult(
                    getViewModelInstanceBooleanProperty(instance.NativeSafeHandle, path),
                    rootInstance,
                    PropertyGetterResult.EnumTypeOption.None),
                (result) => new ViewModelInstanceBooleanProperty(
                    result.PropertyPtr,
                    result.RootViewModelInstance)
            );
        }

        /// <summary>
        /// Creates a handler for number properties
        /// </summary>
        private static (PropertyGetter getter,
            Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty> creator) CreateNumberPropertyHandler()
        {
            return (
                (instance, path, rootInstance) => new PropertyGetterResult(
                    getViewModelInstanceNumberProperty(instance.NativeSafeHandle, path),
                    rootInstance,
                    PropertyGetterResult.EnumTypeOption.None),
                (result) => new ViewModelInstanceNumberProperty(
                    result.PropertyPtr,
                    result.RootViewModelInstance)
            );
        }

        /// <summary>
        /// Creates a handler for string properties
        /// </summary>
        private static (PropertyGetter getter,
            Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty> creator) CreateStringPropertyHandler()
        {
            return (
                (instance, path, rootInstance) => new PropertyGetterResult(
                    getViewModelInstanceStringProperty(instance.NativeSafeHandle, path),
                    rootInstance,
                    PropertyGetterResult.EnumTypeOption.None),
                (result) => new ViewModelInstanceStringProperty(
                    result.PropertyPtr,
                    result.RootViewModelInstance)
            );
        }

        /// <summary>
        /// Creates a handler for color properties
        /// </summary>
        private static (PropertyGetter getter,
            Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty> creator) CreateColorPropertyHandler()
        {
            return (
                (instance, path, rootInstance) => new PropertyGetterResult(
                    getViewModelInstanceColorProperty(instance.NativeSafeHandle, path),
                    rootInstance,
                    PropertyGetterResult.EnumTypeOption.None),
                (result) => new ViewModelInstanceColorProperty(
                    result.PropertyPtr,
                    result.RootViewModelInstance)
            );
        }

        /// <summary>
        /// Creates a handler for image properties
        /// </summary>
        private static (PropertyGetter getter,
            Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty> creator) CreateImagePropertyHandler()
        {
            return (
                (instance, path, rootInstance) => new PropertyGetterResult(
                    getViewModelInstanceImageProperty(instance.NativeSafeHandle, path),
                    rootInstance),
                (result) => new ViewModelInstanceImageProperty(
                    result.PropertyPtr,
                    result.RootViewModelInstance)
            );
        }

        private static (PropertyGetter getter,
    Func<PropertyGetterResult, ViewModelInstancePrimitiveProperty> creator) CreateListPropertyHandler()
        {
            return (
                (instance, path, rootInstance) => new PropertyGetterResult(
                    getViewModelInstanceListProperty(instance.NativeSafeHandle, path),
                    rootInstance,
                    PropertyGetterResult.EnumTypeOption.None),
                (result) => new ViewModelInstanceListProperty(
                    result.PropertyPtr,
                    result.RootViewModelInstance)
            );
        }

        #endregion

        #region Public Property Fetching API

        /// <summary>
        /// Gets a property of the specified type from a view model instance.
        /// </summary>
        /// <typeparam name="T">The type of property to get</typeparam>
        /// <param name="instance">The view model instance to get the property from</param>
        /// <param name="path">The path to the property</param>
        /// <returns>The property instance or null if not found</returns>
        public static T GetPrimitiveProperty<T>(ViewModelInstance instance, string path) where T : ViewModelInstanceProperty
        {
            if (!PropertyHandlers.TryGetValue(typeof(T), out var handler))
            {
                DebugLogger.Instance.LogError("Property type not supported: " + typeof(T).Name);
                return null;
            }

            var result = GetPropertyPointer(handler.getter, instance, path, instance);

            if (result.PropertyPtr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogError("Property not found: " + path);
                return null;
            }

            T instanceAsExpectedType = null;

            // Check if we have already created an instance of this property
            if (ViewModelInstanceProperty.TryGetGloballyCachedVMPropertyForPointer(result.PropertyPtr, out var cachedProperty))
            {
                instanceAsExpectedType = cachedProperty as T;

                // We want to catch cases where the property was created as a different type than expected
                if (instanceAsExpectedType == null)
                {
                    DebugLogger.Instance.LogError("Failed to get property: " + path + ". Expected type: " + typeof(T).Name);
                    return null;
                }
                return instanceAsExpectedType;
            }

            // Create a new property instance
            var propInstance = handler.creator(result);
            ViewModelInstanceProperty.AddGloballyCachedVMPropertyForPointer(result.PropertyPtr, propInstance);

            instanceAsExpectedType = propInstance as T;
            return instanceAsExpectedType;
        }

        private static PropertyGetterResult GetPropertyPointer(
            PropertyGetter getter,
            ViewModelInstance instance,
            string path,
            ViewModelInstance rootInstance)
        {
            return getter(instance, path, rootInstance);
        }
#if UNITY_EDITOR
        /// <summary>
        /// Editor-only utility to get the enum data for a property at a given path.
        /// </summary>
        /// <param name="vmInstance"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static ViewModelEnumData GetEnumForPropertyAtPath(ViewModelInstance vmInstance, string path)
        {
            if (vmInstance == null || vmInstance.RiveFile == null)
            {
                return null;
            }

            IntPtr nativeFilePtr = vmInstance.RiveFile != null ? vmInstance.RiveFile.NativeFile : IntPtr.Zero;
            var info = getEnumPropertyInfoFromViewModelInstance(
                vmInstance.NativeSafeHandle,
                nativeFilePtr,
                path);

            if (info.propertyPtr == IntPtr.Zero)
            {
                return null;
            }
            if (info.enumIndex < 0)
            {
                return null;
            }

            IReadOnlyList<ViewModelEnumData> enumsForFile = vmInstance.RiveFile.ViewModelEnums;

            if (enumsForFile == null || (int)info.enumIndex >= enumsForFile.Count)
            {
                return null;
            }

            return enumsForFile[(int)info.enumIndex];


        }
#endif
        #endregion
        #region Native Calls
        [StructLayout(LayoutKind.Sequential)]
        private struct ViewModelInstanceEnumPropertyInfo
        {
            /// <summary>
            /// The pointer to the instance property.
            /// </summary>
            public IntPtr propertyPtr;

            /// <summary>
            /// The index of the enum value in the Rive file. Allows us to share the same enum value across multiple instances.
            /// </summary>
            public nuint enumIndex;
        }

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceNumberProperty(ViewModelInstanceSafeHandle instanceValue, string path);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceBooleanProperty(ViewModelInstanceSafeHandle instanceValue, string path);


        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceTriggerProperty(ViewModelInstanceSafeHandle instanceValue, string path);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceStringProperty(ViewModelInstanceSafeHandle instanceValue, string path);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceColorProperty(ViewModelInstanceSafeHandle instanceValue, string path);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceImageProperty(ViewModelInstanceSafeHandle instanceValue, string path);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceListProperty(ViewModelInstanceSafeHandle instanceValue, string path);

        [DllImport(NativeLibrary.name)]
        private static extern ViewModelInstanceEnumPropertyInfo getEnumPropertyInfoFromViewModelInstance(
            ViewModelInstanceSafeHandle instanceValue,
            IntPtr fileWrapper,
            string path);

        #endregion
    }
}
