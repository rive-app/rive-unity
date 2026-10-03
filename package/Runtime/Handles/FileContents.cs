using System;
using System.Collections.Generic;

namespace Rive
{
    /// <summary>
    /// Everything name shaped in a file, read once when it loads, so handle
    /// code can check names without asking Rive's thread.
    /// </summary>
    internal sealed class FileContents
    {
        internal sealed class ArtboardInfo
        {
            internal string Name;
            // -1 when the artboard has no default view model.
            internal int DefaultViewModelIndex;
            // -1 when it has no state machines.
            internal int DefaultStateMachineIndex;
            internal string[] StateMachineNames;
        }

        internal sealed class ViewModelInfo
        {
            internal string Name;
            internal string[] InstanceNames;
            internal ViewModelPropertyData[] Properties;
            // Per property: its enum's index in Enums, or -1.
            internal int[] PropertyEnums;
            // Per property: the view model a nested view model property holds, or -1.
            internal int[] PropertyViewModels;
        }

        internal ArtboardInfo[] Artboards = Array.Empty<ArtboardInfo>();
        internal ViewModelInfo[] ViewModels = Array.Empty<ViewModelInfo>();
        internal string[] GlobalViewModelNames = Array.Empty<string>();
        internal string[] ArtboardNames = Array.Empty<string>();
        internal string[] ViewModelNames = Array.Empty<string>();
        internal ViewModelEnumData[] Enums = Array.Empty<ViewModelEnumData>();

        internal struct AssetInfo
        {
            internal ushort Type;
            internal uint Id;
            internal string Name;
            internal uint EmbeddedBytes;
        }

        // Every asset the import saw, in file order.
        internal AssetInfo[] Assets = Array.Empty<AssetInfo>();

        /// Walks a property path through nested view models. False when a
        /// segment isn't a property, or goes through one that isn't a nested
        /// view model.
        internal bool TryResolvePath(int viewModelIndex, string path, out int ownerIndex, out int propertyIndex)
        {
            ownerIndex = viewModelIndex;
            propertyIndex = -1;
            if (viewModelIndex < 0 || viewModelIndex >= ViewModels.Length || string.IsNullOrEmpty(path))
            {
                return false;
            }
            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                ViewModelInfo owner = ViewModels[ownerIndex];
                propertyIndex = -1;
                for (int j = 0; j < owner.Properties.Length; j++)
                {
                    if (owner.Properties[j].Name == segments[i])
                    {
                        propertyIndex = j;
                        break;
                    }
                }
                if (propertyIndex < 0)
                {
                    return false;
                }
                if (i < segments.Length - 1)
                {
                    int next = owner.PropertyViewModels[propertyIndex];
                    if (next < 0)
                    {
                        return false;
                    }
                    ownerIndex = next;
                }
            }
            return true;
        }

        /// The values of the enum at a path, or null when that isn't known.
        internal IReadOnlyList<string> EnumValuesAt(int viewModelIndex, string path)
        {
            if (!TryResolvePath(viewModelIndex, path, out int owner, out int property))
            {
                return null;
            }
            int enumIndex = ViewModels[owner].PropertyEnums[property];
            return enumIndex >= 0 && enumIndex < Enums.Length ? Enums[enumIndex].Values : null;
        }

        /// The view model a nested view model property at a path holds, or -1.
        internal int ViewModelAt(int viewModelIndex, string path)
        {
            if (!TryResolvePath(viewModelIndex, path, out int owner, out int property))
            {
                return -1;
            }
            return ViewModels[owner].PropertyViewModels[property];
        }

        internal int ArtboardIndex(string name)
        {
            for (int i = 0; i < Artboards.Length; i++)
            {
                if (Artboards[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        internal static int IndexOf(string[] names, string name)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                {
                    return i;
                }
            }
            return -1;
        }

        internal int ViewModelIndex(string name)
        {
            for (int i = 0; i < ViewModels.Length; i++)
            {
                if (ViewModels[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        /// A u32 count, then that many strings, as getViewModelInstanceEnumValues writes them.
        internal static string[] ParseStrings(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return Array.Empty<string>();
            }
            var reader = new Reader(bytes, 0);
            var strings = new string[reader.U32()];
            for (int i = 0; i < strings.Length; i++)
            {
                strings[i] = reader.String();
            }
            return strings;
        }

        /// Reads the describe riveLoadFile's reply carries, from offset on.
        /// See file_routines.cpp for the layout.
        internal static FileContents Parse(byte[] bytes, int offset = 0)
        {
            var reader = new Reader(bytes, offset);
            var contents = new FileContents();

            contents.Artboards = new ArtboardInfo[reader.U32()];
            contents.ArtboardNames = new string[contents.Artboards.Length];
            for (int i = 0; i < contents.Artboards.Length; i++)
            {
                var artboard = new ArtboardInfo
                {
                    Name = reader.String(),
                    DefaultViewModelIndex = (int)reader.U32(),
                    DefaultStateMachineIndex = (int)reader.U32(),
                };
                artboard.StateMachineNames = new string[reader.U32()];
                for (int j = 0; j < artboard.StateMachineNames.Length; j++)
                {
                    artboard.StateMachineNames[j] = reader.String();
                }
                contents.Artboards[i] = artboard;
                contents.ArtboardNames[i] = artboard.Name;
            }

            contents.ViewModels = new ViewModelInfo[reader.U32()];
            contents.ViewModelNames = new string[contents.ViewModels.Length];
            for (int i = 0; i < contents.ViewModels.Length; i++)
            {
                var viewModel = new ViewModelInfo { Name = reader.String() };
                viewModel.InstanceNames = new string[reader.U32()];
                for (int j = 0; j < viewModel.InstanceNames.Length; j++)
                {
                    viewModel.InstanceNames[j] = reader.String();
                }
                int propertyCount = (int)reader.U32();
                viewModel.Properties = new ViewModelPropertyData[propertyCount];
                viewModel.PropertyEnums = new int[propertyCount];
                viewModel.PropertyViewModels = new int[propertyCount];
                for (int j = 0; j < propertyCount; j++)
                {
                    string name = reader.String();
                    var type = (ViewModelDataType)reader.U32();
                    viewModel.Properties[j] = new ViewModelPropertyData(name, type);
                    viewModel.PropertyEnums[j] = (int)reader.U32();
                    viewModel.PropertyViewModels[j] = (int)reader.U32();
                }
                contents.ViewModels[i] = viewModel;
                contents.ViewModelNames[i] = viewModel.Name;
            }

            contents.GlobalViewModelNames = new string[reader.U32()];
            for (int i = 0; i < contents.GlobalViewModelNames.Length; i++)
            {
                contents.GlobalViewModelNames[i] = reader.String();
            }

            contents.Enums = new ViewModelEnumData[reader.U32()];
            for (int i = 0; i < contents.Enums.Length; i++)
            {
                string name = reader.String();
                var values = new string[reader.U32()];
                for (int j = 0; j < values.Length; j++)
                {
                    values[j] = reader.String();
                }
                contents.Enums[i] = new ViewModelEnumData(name, values);
            }
            contents.Assets = ReadAssets(ref reader);
            return contents;
        }

        /// Reads just an assets section, as riveListFileAssets writes it.
        internal static AssetInfo[] ParseAssets(byte[] bytes, int offset)
        {
            var reader = new Reader(bytes, offset);
            return ReadAssets(ref reader);
        }

        private static AssetInfo[] ReadAssets(ref Reader reader)
        {
            var assets = new AssetInfo[reader.U32()];
            for (int i = 0; i < assets.Length; i++)
            {
                assets[i] = new AssetInfo
                {
                    Type = (ushort)reader.U32(),
                    Id = reader.U32(),
                    Name = reader.String(),
                    EmbeddedBytes = reader.U32(),
                };
            }
            return assets;
        }

        private struct Reader
        {
            private readonly byte[] m_bytes;
            private int m_offset;

            internal Reader(byte[] bytes, int offset = 0)
            {
                m_bytes = bytes;
                m_offset = offset;
            }

            internal uint U32()
            {
                uint value = (uint)(m_bytes[m_offset]
                                    | (m_bytes[m_offset + 1] << 8)
                                    | (m_bytes[m_offset + 2] << 16)
                                    | (m_bytes[m_offset + 3] << 24));
                m_offset += 4;
                return value;
            }

            internal string String()
            {
                int length = (int)U32();
                string value = System.Text.Encoding.UTF8.GetString(m_bytes, m_offset, length);
                m_offset += length;
                return value;
            }
        }
    }
}
