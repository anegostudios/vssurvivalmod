using Vintagestory.API;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent;

/// <summary>
/// Creates a recipe that can be created using clay. This class takes all of its properties from the <see cref="LayeredVoxelRecipe{T}"/> class.
/// </summary>
/// <example>
/// <code language="json">
///{
///  "ingredient": {
///    "type": "item",
///    "code": "clay-*",
///    "name": "type",
///    "allowedVariants": [ "blue", "fire", "red" ]
///  },
///  "pattern": [
///    [
///      "#####",
///      "#####",
///      "#####",
///      "#####",
///      "#####"
///    ],
///    [
///      "#####",
///      "#___#",
///      "#___#",
///      "#___#",
///      "#####"
///    ]
///  ],
///  "name": "Bowl",
///  "output": {
///    "type": "block",
///    "code": "bowl-raw"
///  }
///}</code></example>
[DocumentAsJson]
public class ClayFormingRecipe : LayeredVoxelRecipe, IConcreteCloneable<ClayFormingRecipe>
{
    public override int QuantityLayers => 16;
    public override string RecipeCategoryCode => "clay forming";



    public override ClayFormingRecipe Clone()
    {
        ClayFormingRecipe recipe = new();

        CloneTo(recipe);

        return recipe;
    }
}
