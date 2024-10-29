using System.Collections.Generic;
using Rive.Tests.Utils;

namespace Rive.Tests.PlayMode
{
    /// <summary>
    /// Tests for the EmbeddedAssetLoader class.
    /// </summary>
    public class EmbeddedAssetDataLoaderTests : BaseEmbeddedAssetLoaderTests
    {
        /// <summary>
        /// Test cases for Rive files with embedded assets. Add new test cases here as needed.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<EmbeddedAssetTestOption> GetTestCases()
        {
            yield return new EmbeddedAssetTestOption
            {
                AssetPath = TestAssetReferences.riv_sophiaHud,
                EmbeddedDataList = new List<EmbeddedAssetTestDataItem>
                {
                    new EmbeddedAssetTestDataItem
                    {
                        ExpectedName = "balls_texture_01.jpg",
                        ExpectedType = EmbeddedAssetType.Image,
                        ExpectedId = 679343,
                        ExpectedBytes = 753474
                    }
                }
            };

            // This Rive file has no embedded assets
            yield return new EmbeddedAssetTestOption
            {
                AssetPath = TestAssetReferences.riv_roboDude,
                EmbeddedDataList = new List<EmbeddedAssetTestDataItem>(),
            };
            // Keep the expected order of the embedded assets in the Rive file
            yield return new EmbeddedAssetTestOption
            {
                AssetPath = TestAssetReferences.riv_gameHudScope,
                EmbeddedDataList = new List<EmbeddedAssetTestDataItem>
                {
                    new EmbeddedAssetTestDataItem
                    {
                        ExpectedName = "Tomorrow",
                        ExpectedType = EmbeddedAssetType.Font,
                        ExpectedId = 895439,
                        ExpectedBytes = 59020
                    },
                     new EmbeddedAssetTestDataItem
                    {
                        ExpectedName = "Tomorrow",
                        ExpectedType = EmbeddedAssetType.Font,
                        ExpectedId = 895432,
                        ExpectedBytes = 59520
                    },
                     new EmbeddedAssetTestDataItem
                    {
                        ExpectedName = "Tomorrow",
                        ExpectedType = EmbeddedAssetType.Font,
                        ExpectedId = 895442,
                        ExpectedBytes = 59772
                    }
                }
            };
        }


    }
}