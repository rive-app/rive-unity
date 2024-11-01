using Rive.Tests;
using Rive.Tests.Utils;

public class ArtboardTests : BaseArtboardTests
{
    protected override TestArtboardAssetData[] GetTestRiveAssetData()
    {
        return new TestArtboardAssetData[]
        {
            new TestArtboardAssetData(TestAssetReferences.riv_sophiaHud, 0),
            new TestArtboardAssetData(TestAssetReferences.riv_stormtrooper_bird, 0),
        };
    }


}
