using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityMeal : BlockEntityContainer, IBlockShapeSupplier
    {
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "meal";


        internal InventoryGeneric inventory;
        internal BlockMeal ownBlock;
        MeshData currentMesh;

        public string RecipeCode;

        public CookingRecipe FromRecipe
        {
            get { return api.World.CookingRecipes.FirstOrDefault(rec => rec.Code == RecipeCode); }
        }

        public BlockEntityMeal()
        {
            inventory = new InventoryGeneric(4, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = api.World.BlockAccessor.GetBlock(pos) as BlockMeal;

            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(Every100ms, 200);
            }

            if (api.Side == EnumAppSide.Client && currentMesh == null)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void OnBlockBroken()
        {
            // Don't drop inventory contents
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            BlockMeal blockmeal = byItemStack?.Block as BlockMeal;
            if (blockmeal != null)
            {
                ItemStack[] stacks = blockmeal.GetContents(api.World, byItemStack);
                for (int i = 0; i < stacks.Length; i++)
                {
                    Inventory.GetSlot(i).Itemstack = stacks[i];
                }

                RecipeCode = blockmeal.GetRecipeCode(api.World, byItemStack);
            }
            
            if (api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        private void Every100ms(float dt)
        {
            float temp = GetTemperature();
            if (api.World.Rand.NextDouble() < (temp - 50) / 320)
            {
                BlockCookedContainer.smokeHeld.minPos = pos.ToVec3d().AddCopy(0.5 - 0.05, 0.125, 0.5 - 0.05);
                api.World.SpawnParticles(BlockCookedContainer.smokeHeld);
            }
        }


        private int GetTemperature()
        {
            ItemStack[] stacks = GetContentStacks(false);
            if (stacks.Length == 0 || stacks[0] == null) return 0;

            return (int)stacks[0].Collectible.GetTemperature(api.World, stacks[0]);
        }


        internal MeshData GenMesh()
        {
            if (ownBlock == null) return null;
            ItemStack[] stacks = GetContentStacks();
            if (stacks == null || stacks.Length == 0) return null;

            ICoreClientAPI capi = api as ICoreClientAPI;
            return capi.ModLoader.GetModSystem<MealMeshCache>().CreateMealMesh(ownBlock.Shape, FromRecipe, stacks);
        }

        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            return true;
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            RecipeCode = tree.GetString("recipeCode");

            if (api?.Side == EnumAppSide.Client && currentMesh == null)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("recipeCode", RecipeCode == null ? "" : RecipeCode);
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            CookingRecipe recipe = FromRecipe;
            if (recipe == null) return "Unknown recipe :O";

            StringBuilder dsc = new StringBuilder();

            dsc.AppendLine(recipe.GetOutputName(forPlayer.Entity.World, GetContentStacks()).UcFirst());

            if (ownBlock == null) return dsc.ToString();


            int temp = GetTemperature();
            string temppretty = Lang.Get("{0}°C", temp);
            if (temp < 20) temppretty = "Cold";

            dsc.AppendLine(Lang.Get("Temperature: {0}", temppretty));

            string nutriFacts = ownBlock.GetContentNutritionFacts(api.World, ownBlock.OnPickBlock(api.World, pos), forPlayer.Entity);
            if (nutriFacts != null)
            {
                dsc.Append(nutriFacts);
            }


            return dsc.ToString();
        }
    }
}
