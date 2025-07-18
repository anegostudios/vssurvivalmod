﻿using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityCookedContainer : BlockEntityContainer, IBlockEntityMealContainer
    {
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "cookedcontainer";


        internal InventoryGeneric inventory;
        public float QuantityServings { get; set; }
        public string? RecipeCode { get; set; }

        internal BlockCookedContainer? ownBlock;

        MeshData? currentMesh;

        bool wasRotten;
        int tickCnt = 0;


        InventoryBase IBlockEntityMealContainer.inventory => inventory;



        public bool Rotten
        {
            get
            {
                bool rotten = false;
                for (int i = 0; i < inventory.Count; i++)
                {
                    rotten |= inventory[i].Itemstack?.Collectible.Code.Path == "rot";
                }

                return rotten;
            }
        }

        public CookingRecipe? FromRecipe
        {
            get { return Api.GetCookingRecipe(RecipeCode); }
        }



        public BlockEntityCookedContainer()
        {
            inventory = new InventoryGeneric(4, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = Block as BlockCookedContainer;

            if (Api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(Every100ms, 200);
            }
        }

        private void Every100ms(float dt)
        {
            float temp = GetTemperature();
            if (Api.World.Rand.NextDouble() < (temp - 50) / 160)
            {
                BlockCookedContainer.smokeHeld.MinPos = Pos.ToVec3d().AddCopy(0.5-0.05, 0.3125, 0.5-0.05);
                Api.World.SpawnParticles(BlockCookedContainer.smokeHeld);
            }

            if (tickCnt++ % 20 == 0)
            {
                if (!wasRotten && Rotten)
                {
                    currentMesh = GenMesh();
                    MarkDirty(true);
                    wasRotten = true;
                }
            }
        }


        private int GetTemperature()
        {
            ItemStack[] stacks = GetNonEmptyContentStacks(false);
            if (stacks.Length == 0 || stacks[0] == null) return 0;

            return (int)stacks[0].Collectible.GetTemperature(Api.World, stacks[0]);
        }

        public override void OnBlockPlaced(ItemStack? byItemStack = null)
        {
            BlockCookedContainer? blockpot = byItemStack?.Block as BlockCookedContainer;
            if (blockpot != null)
            {
                TreeAttribute? tempTree = byItemStack?.Attributes?["temperature"] as TreeAttribute;

                if (blockpot.GetNonEmptyContents(Api.World, byItemStack) is ItemStack[] stacks)
                {
                    for (int i = 0; i < stacks.Length; i++)
                    {
                        ItemStack stack = stacks[i].Clone();
                        Inventory[i].Itemstack = stack;

                        // Clone temp attribute
                        if (tempTree != null) stack.Attributes["temperature"] = tempTree.Clone();
                    }
                }

                RecipeCode = blockpot.GetRecipeCode(Api.World, byItemStack);
                QuantityServings = blockpot.GetServings(Api.World, byItemStack);
            }

            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }


        public override void OnBlockBroken(IPlayer? byPlayer = null)
        {
            // Don't drop contents
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            QuantityServings = (float)tree.GetDecimal("quantityServings", 1);
            RecipeCode = tree.GetString("recipeCode");

            if (Api?.Side == EnumAppSide.Client && currentMesh == null)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat("quantityServings", QuantityServings);
            tree.SetString("recipeCode", RecipeCode == null ? "" : RecipeCode);
        }

        public bool ServeInto(IPlayer player, ItemSlot slot)
        {
            if (slot.Itemstack == null) return false;

            int capacity = slot.Itemstack.Collectible.Attributes["servingCapacity"].AsInt();
            float servings = Math.Min(QuantityServings, capacity);

            ItemStack mealStack;
            IBlockMealContainer? ibm = slot.Itemstack.Collectible as IBlockMealContainer;

            if (ibm != null && ibm.GetQuantityServings(Api.World, slot.Itemstack) > 0)
            {
                float existingServings = ibm.GetQuantityServings(Api.World, slot.Itemstack);
                //string recipeCode = ibm.GetRecipeCode(Api.World, slot.Itemstack);
                ItemStack[] existingContent = ibm.GetNonEmptyContents(Api.World, slot.Itemstack);

                servings = Math.Min(servings, capacity - existingServings);
                ItemStack[] potStacks = GetNonEmptyContentStacks();

                if (servings == 0) return false;
                if (existingContent.Length != potStacks.Length) return false;
                for (int i = 0; i < existingContent.Length; i++)
                {
                    if (!existingContent[i].Equals(Api.World, potStacks[i], GlobalConstants.IgnoredStackAttributes))
                    {
                        return false;
                    }
                }

                if (slot.StackSize == 1)
                {
                    mealStack = slot.Itemstack;
                    ibm.SetContents(RecipeCode, slot.Itemstack, GetNonEmptyContentStacks(), existingServings + servings);
                }
                else
                {
                    mealStack = slot.Itemstack.Clone();
                    ibm.SetContents(RecipeCode, mealStack, GetNonEmptyContentStacks(), existingServings + servings);
                }
            }
            else
            {
                mealStack = new ItemStack(Api.World.GetBlock(AssetLocation.Create(slot.Itemstack.Collectible.Attributes["mealBlockCode"].AsString(), slot.Itemstack.Collectible.Code.Domain)));
                mealStack.StackSize = 1;
                (mealStack.Collectible as IBlockMealContainer)?.SetContents(RecipeCode, mealStack, GetNonEmptyContentStacks(), servings);
            }


            if (slot.StackSize == 1)
            {
                slot.Itemstack = mealStack;
                slot.MarkDirty();
            }
            else
            {
                slot.TakeOut(1);
                if (!player.InventoryManager.TryGiveItemstack(mealStack, true))
                {
                    Api.World.SpawnItemEntity(mealStack, Pos);
                }
                Api.World.Logger.Audit("{0} Took 1x{1} Meal from {2} at {3}.",
                    player.PlayerName,
                    mealStack.Collectible.Code,
                    Block.Code,
                    Pos
                );
                slot.MarkDirty();
            }

            QuantityServings -= servings;

            if (QuantityServings <= 0)
            {
                AssetLocation loc = AssetLocation.CreateOrNull(Block.Attributes?["emptiedBlockCode"]?.AsString()) ?? Block.CodeWithVariant("type", "fired");
                Block? block = Api.World.GetBlock(loc);
                Api.World.BlockAccessor.SetBlock(block?.BlockId ?? 0, Pos);
                return true;
            }

            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
            }

            MarkDirty(true);

            return true;
        }

        public MeshData? GenMesh()
        {
            if (ownBlock == null) return null;
            ItemStack[] stacks = GetNonEmptyContentStacks();
            if (stacks == null || stacks.Length == 0) return null;

            ICoreClientAPI? capi = Api as ICoreClientAPI;
            return capi?.ModLoader.GetModSystem<MealMeshCache>().GenMealInContainerMesh(ownBlock, FromRecipe, stacks, new Vec3f(0, 2.5f/16f, 0));
        }



        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (currentMesh == null)
            {
                currentMesh = GenMesh();
            }

            if (currentMesh != null)
            {
                mesher.AddMeshData(currentMesh);
                return true;
            }
            return false;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (Api.GetCookingRecipe(RecipeCode) is not CookingRecipe recipe) return;
            ItemStack[] contentStacks = GetNonEmptyContentStacks();

            float servings = QuantityServings;
            int temp = GetTemperature();
            string temppretty = Lang.Get("{0}°C", temp);
            if (temp < 20) temppretty = Lang.Get("Cold");

            string? nutriFacts = BlockMeal.AllMealBowls?[0]?.GetContentNutritionFacts(Api.World, inventory[0], contentStacks, forPlayer.Entity);


            if (servings == 1)
            {
                dsc.Append(Lang.Get("cookedcontainer-servingstemp-singular", Math.Round(servings, 1), recipe.GetOutputName(forPlayer.Entity.World, contentStacks), temppretty, nutriFacts != null ? "\n" : "", nutriFacts));
            }
            else
            {
                dsc.Append(Lang.Get("cookedcontainer-servingstemp-plural", Math.Round(servings, 1), recipe.GetOutputName(forPlayer.Entity.World, contentStacks), temppretty, nutriFacts != null ? "\n" : "", nutriFacts));
            }


            foreach (var slot in inventory)
            {
                if (slot.Empty) continue;

                TransitionableProperties[]? propsm = slot.Itemstack.Collectible.GetTransitionableProperties(Api.World, slot.Itemstack, null);
                if (propsm != null && propsm.Length > 0)
                {
                    slot.Itemstack.Collectible.AppendPerishableInfoText(slot, dsc, Api.World);
                    break;
                }
            }
        }

    }
}
