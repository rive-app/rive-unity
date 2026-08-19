using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Rive.Components;

namespace Rive.Tests.EditorTests
{
    // Native C++ bool is 1 byte. Unannotated C# bool is a 4-byte Win32 BOOL.
    // On Linux IL2CPP the native return only sets the low byte, so the 4-byte
    // read sees leftover pointer bits and HasChanged (and other bools) read as
    // true even after we clear them — so triggers fire every frame. This test
    // just walks our DllImports and fails if anyone adds a bool without MarshalAs(U1).
    public class NativeBoolPInvokeMarshalTests
    {
        [Test]
        public void NativeBoolPInvokes_AreMarshaledAsOneByte()
        {
            var assemblies = new[]
            {
                typeof(Rive.File).Assembly,                 // Rive.Runtime
                typeof(RivePanel).Assembly,                 // Rive.Runtime.Components
            };

            var failures = new List<string>();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (method.GetCustomAttribute<DllImportAttribute>() == null)
                        {
                            continue;
                        }

                        if (method.ReturnType == typeof(bool) && !IsOneByteBool(method.ReturnParameter))
                        {
                            failures.Add($"{type.FullName ?? type.Name}.{method.Name} return");
                        }

                        foreach (var param in method.GetParameters())
                        {
                            if (param.ParameterType == typeof(bool) && !IsOneByteBool(param))
                            {
                                failures.Add($"{type.FullName ?? type.Name}.{method.Name}({param.Name})");
                            }
                        }
                    }
                }
            }

            Assert.IsEmpty(failures,
                "C++ bool is 1 byte. Unannotated C# bool is 4-byte BOOL and misreads native false as true on IL2CPP x86-64:\n"
                + string.Join("\n", failures));
        }

        static bool IsOneByteBool(ParameterInfo p)
        {
            var marshal = p.GetCustomAttribute<MarshalAsAttribute>();
            return marshal != null && marshal.Value == UnmanagedType.U1;
        }
    }
}
