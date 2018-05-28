using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CollectibleType
    {

        public AssetLocation Code;
        public CollectibleVariantGroup[] VariantGroups;
        public AssetLocation[] SkipVariants;

        public bool Enabled = true;

        public JObject jsonObject;

        [JsonProperty]
        public string Class;

        [JsonProperty]
        public float RenderAlphaTest = 0.01f;
        [JsonProperty]
        public int StorageFlags = 1;
        [JsonProperty]
        public int MaxStackSize = 1;
        [JsonProperty]
        public float AttackPower = 0.5f;
        [JsonProperty]
        public float AttackRange = GlobalConstants.DefaultAttackRange;
        [JsonProperty]
        public Dictionary<EnumBlockMaterial, float> MiningSpeed;
        [JsonProperty]
        public int MiningTier;

        // Determines on whether an object floats on liquids or not
        // Water has a density of 1000
        [JsonProperty]
        public int MaterialDensity = 9999;

        [JsonProperty, JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject Attributes;

        [JsonProperty]
        public CompositeShape Shape = null;

        [JsonProperty]
        public ModelTransform GuiTransform;
        [JsonProperty]
        public ModelTransform FpHandTransform;
        [JsonProperty]
        public ModelTransform TpHandTransform;
        [JsonProperty]
        public ModelTransform GroundTransform;

        [JsonProperty]
        public CompositeTexture Texture;
        [JsonProperty]
        public Dictionary<string, CompositeTexture> Textures = new Dictionary<string, CompositeTexture>();

        [JsonProperty]
        public CombustibleProperties CombustibleProps = null;
        [JsonProperty]
        public FoodNutritionProperties NutritionProps = null;
        [JsonProperty]
        public GrindingProperties GrindingProps = null;

        [JsonProperty]
        public bool LiquidSelectable = false;

        [JsonProperty]
        public Dictionary<string, string[]> CreativeInventory = new Dictionary<string, string[]>();

        [JsonProperty]
        public CreativeTabAndStackList[] CreativeInventoryStacks;

        [JsonProperty]
        public string HeldTpHitAnimation = "breakhand";

        [JsonProperty]
        public string HeldTpIdleAnimation;

        [JsonProperty]
        public string HeldTpUseAnimation = "placeblock";


        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            OnDeserialized();
        }

        virtual internal void OnDeserialized()
        {
            GuiTransform.EnsureDefaultValues();
            FpHandTransform.EnsureDefaultValues();
            TpHandTransform.EnsureDefaultValues();
            GroundTransform.EnsureDefaultValues();

            if (Texture != null)
            {
                Textures["all"] = Texture;
            }
        }
    }
}
