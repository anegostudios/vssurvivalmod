using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public class LiquidTopOpenContainerProps
    {
        public float CapacityLitres = 10f;
        public float DrinkPortionSize = 1f;
        public float TransferSizeLitres = 0.01f;
        public AssetLocation EmptyShapeLoc;
        public AssetLocation OpaqueContentShapeLoc;
        public AssetLocation LiquidContentShapeLoc;
        public float LiquidMaxYTranslate;
    }

    public class WaterTightContainableProps
    {
        public bool Containable;
        public float ItemsPerLitre = 1f;  //prevent possible divide by zero if people are not careful to include this in assets...
        public AssetLocation FillSpillSound = new AssetLocation("sounds/block/water");

        public AssetLocation PourSound = new AssetLocation("sounds/effect/water-pour.ogg");
        public AssetLocation FillSound = new AssetLocation("sounds/effect/water-fill.ogg");

        public CompositeTexture Texture;
        public string ClimateColorMap = null;
        public bool AllowSpill = true;
        public bool IsOpaque = false;
        public WhenSpilledProps WhenSpilled;
        public WhenFilledProps WhenFilled;
        public int MaxStackSize;
        public int GlowLevel;
        public FoodNutritionProperties NutritionPropsPerLitre;
        public FoodNutritionProperties NutritionPropsPerLitreWhenInMeal = null;

        public enum EnumSpilledAction { PlaceBlock, DropContents };

        public class WhenFilledProps
        {
            public JsonItemStack Stack;
        }

        public class WhenSpilledProps
        {
            public Dictionary<int, JsonItemStack> StackByFillLevel;
            public EnumSpilledAction Action;
            public JsonItemStack Stack;
        }
    }

}
