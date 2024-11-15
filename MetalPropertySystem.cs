using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Vintagestory.GameContent;

public class MetalPropertySystem : ModSystem
{
    public override void AssetsLoaded(ICoreAPI api)
    {
        var scSystem = api.ModLoader.GetModSystem<SurvivalCoreSystem>();
        var propertyAssets = api.Assets.GetMany("worldproperties/block/metal.json");
        var metalProperties = propertyAssets.Select(asset => asset.ToObject<MetalProperty>()).ToList();
        foreach (var metalProperty in metalProperties)
        {
            for (int index = 0; index < metalProperty.Variants.Length; ++index)
            {
                MetalPropertyVariant variant = metalProperty.Variants[index];
                scSystem.metalsByCode[variant.Code.Path] = variant;
            }
        }
    }
}
