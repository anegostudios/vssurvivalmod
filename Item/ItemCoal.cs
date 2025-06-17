using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemCoal : ItemPileable
    {
        protected override AssetLocation PileBlockCode => new AssetLocation("coalpile");
    }
}
