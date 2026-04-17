using OpenTK.Windowing.GraphicsLibraryFramework;
using Vintagestory.API;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent;

/// <summary>
/// Defines a smithing recipe, to be created on an anvil. Uses a total of 6 layers, and gets its properties from <see cref="LayeredVoxelRecipe{T}"/>.
/// </summary>
/// <example><code language="json">
///{
///	"ingredient": {
///		"type": "item",
///		"code": "ingot-*",
///		"name": "metal",
///		"allowedVariants": [ "tinbronze", "bismuthbronze", "blackbronze", "silver", "gold", "iron", "meteoriciron", "steel" ]
///	},
///	"name": "boss",
///	"pattern": [
///		[
///			"____",
///			"_##_",
///			"_##_",
///			"____"
///		],
///		[
///			"####",
///			"#__#",
///			"#__#",
///			"####"
///		]
///	],
///	"output": {
///		"type": "item",
///		"code": "boss-{metal}"
///	}
///}
/// </code></example>
[DocumentAsJson]
public class SmithingRecipe : LayeredVoxelRecipe, IConcreteCloneable<SmithingRecipe>
{
    public override int QuantityLayers => 6;
    public override string RecipeCategoryCode => "smithing";
    protected override bool RotateRecipe => true;

    /// <summary>
    /// A unique identifier for this recipe, must remain unique and unchanging across game udpates.
    /// Not used in 1.22 but will be used in 1.23
    /// </summary>
    public AssetLocation? Code;

    protected override void FillPlaceHolder(string key, string value)
    {
        if (Code != null)
        {
            Code = Code.CopyWithPath(Code.Path.Replace("{" + key + "}", value));
        }
        base.FillPlaceHolder(key, value);
    }

    public override SmithingRecipe Clone()
    {
        SmithingRecipe recipe = new();

        CloneTo(recipe);
        recipe.Code = Code?.Clone();

        return recipe;
    }
}
