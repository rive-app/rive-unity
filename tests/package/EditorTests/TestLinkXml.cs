using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace Rive.Tests
{
    /// <summary>
    /// The linker only reads link.xml files under Assets, so the tests
    /// package hands it Shared/link.xml itself. It keeps Addressables, which
    /// the test player loads through, from losing types it makes by
    /// reflection. This assembly only exists when the tests do.
    /// </summary>
    internal sealed class TestLinkXml : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            return Path.GetFullPath("Packages/app.rive.rive-unity.tests/Shared/link.xml");
        }
    }
}
