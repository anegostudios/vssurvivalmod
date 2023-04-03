using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class ItemCoal : ItemPileable
    {
        protected override AssetLocation PileBlockCode => new AssetLocation("coalpile");
    }
}
