
using Rive;
using Rive.Tests;
using Rive.Tests.Utils;

public class RiveFileTests : BaseRiveFileTests
{
    protected override TestAssetData[] GetTestRiveAssetInfo()
    {
        return new TestAssetData[]
        {

            new TestAssetData
            {
                addressableAssetPath = TestAssetReferences.riv_sophiaHud,
                expectedArtboardCount = 4,
                expectedReferencedAssetData = new EmbeddedAssetTestDataItem[]
                {
                    new EmbeddedAssetTestDataItem
                    {
                        ExpectedName = "balls_texture_01.jpg",
                        ExpectedType = EmbeddedAssetType.Image,
                        ExpectedId = 679343,
                        ExpectedBytesSize = 753474
                    }
                }
            },
            new TestAssetData
            {
                addressableAssetPath = TestAssetReferences.riv_roboDude,
                expectedArtboardCount = 2,
                expectedReferencedAssetData = new EmbeddedAssetTestDataItem[] { }
            },
            new TestAssetData{
                 addressableAssetPath = TestAssetReferences.riv_gameHudScope,
                expectedArtboardCount = 2,
                expectedReferencedAssetData = new EmbeddedAssetTestDataItem[]{
                    new EmbeddedAssetTestDataItem
                    {
                        ExpectedName = "Tomorrow",
                        ExpectedType = EmbeddedAssetType.Font,
                        ExpectedId = 895439,
                        ExpectedBytesSize = 59020
                    },
                     new EmbeddedAssetTestDataItem
                    {
                        ExpectedName = "Tomorrow",
                        ExpectedType = EmbeddedAssetType.Font,
                        ExpectedId = 895432,
                        ExpectedBytesSize = 59520
                    },
                     new EmbeddedAssetTestDataItem
                    {
                        ExpectedName = "Tomorrow",
                        ExpectedType = EmbeddedAssetType.Font,
                        ExpectedId = 895442,
                        ExpectedBytesSize = 59772
                    }
                }
            },
            new TestAssetData
            {
                addressableAssetPath = TestAssetReferences.riv_stormtrooper_bird,
                expectedArtboardCount = 1,
                expectedReferencedAssetData = new EmbeddedAssetTestDataItem[]
                {
                    new EmbeddedAssetTestDataItem
                    {
                        ExpectedName = "michael-myers-trooperbird.jpg",
                        ExpectedType = EmbeddedAssetType.Image,
                        ExpectedId = 266093,
                        ExpectedBytesSize = 0
                    }
                }
            }

        };
    }

    protected override TestAssetData[] GetTestTextAssetInfo()
    {
        return new TestAssetData[]
        {
            new TestAssetData
            {
                addressableAssetPath = TestAssetReferences.textasset_roboDude,
                expectedArtboardCount = 2,
                expectedReferencedAssetData = new EmbeddedAssetTestDataItem[] {
            }
        }
        };
    }
}
