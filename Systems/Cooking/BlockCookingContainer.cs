using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCookingContainer : Block, IInFirepitRendererSupplier
    {
        public int MaxServingSize = 6;
        Cuboidi attachmentArea;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            attachmentArea = Attributes?["attachmentArea"].AsObject<Cuboidi>(null);

            MaxServingSize = Attributes?["maxServingSize"].AsInt(6) ?? 6;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("handleCookingContainerInteract") == true)
            {
                EnumHandHandling handling = EnumHandHandling.NotHandled;
                hotbarSlot.Itemstack.Collectible.OnHeldInteractStart(hotbarSlot, byPlayer.Entity, blockSel, null, true, ref handling);
                if (handling == EnumHandHandling.PreventDefault || handling == EnumHandHandling.PreventDefaultAction) return true;
            }

            ItemStack stack = OnPickBlock(world, blockSel.Position);

            if (byPlayer.InventoryManager.TryGiveItemstack(stack, true))
            {
                world.BlockAccessor.SetBlock(0, blockSel.Position);
                world.PlaySoundAt(this.Sounds.Place, byPlayer, byPlayer);
                return true;
            }
            return false;
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                failureCode = "onlywhensneaking";
                return false;
            }

            if (CanPlaceBlock(world, byPlayer, blockSel, ref failureCode) && world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()).CanAttachBlockAt(world.BlockAccessor, this, blockSel.Position.DownCopy(), BlockFacing.UP, attachmentArea))
            {
                DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }

            return false;
        }
        


        public override float GetMeltingDuration(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float duration = 0;

            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider, false);
            for (int i = 0; i < stacks.Length; i++)
            {
                var stack = stacks[i];
                int portionSize = stack.StackSize;

                if (stack.Collectible?.CombustibleProps == null)
                {
                    if (stack.Collectible.Attributes?["waterTightContainerProps"].Exists == true)
                    {
                        var props = BlockLiquidContainerBase.GetContainableProps(stack);
                        portionSize = (int)(stack.StackSize / props.ItemsPerLitre);
                    }

                    duration += 20 * portionSize;
                    continue;
                }

                float singleDuration = stack.Collectible.GetMeltingDuration(world, cookingSlotsProvider, inputSlot);
                duration += singleDuration * portionSize / stack.Collectible.CombustibleProps.SmeltedRatio;
            }

            duration = Math.Max(40, duration / 3);

            return duration;
        }


        public override float GetMeltingPoint(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float meltpoint = 0;

            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider, false);
            for (int i = 0; i < stacks.Length; i++)
            {
                meltpoint = Math.Max(meltpoint, stacks[i].Collectible.GetMeltingPoint(world, cookingSlotsProvider, inputSlot));
            }

            return Math.Max(100, meltpoint);
        }


        public override bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
        {
            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider, false);

            // Got recipe?
            if (GetMatchingCookingRecipe(world, stacks) != null)
            {
                return true;
            }

            return false;
        }


        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider);
            CookingRecipe recipe = GetMatchingCookingRecipe(world, stacks);

            Block block = world.GetBlock(CodeWithVariant("type", "cooked"));

            if (recipe != null)
            {
                int quantityServings = recipe.GetQuantityServings(stacks);

                if (recipe.CooksInto != null)
                {
                    var outstack = recipe.CooksInto.ResolvedItemstack?.Clone();
                    if (outstack != null)
                    {
                        outstack.StackSize *= quantityServings;
                        stacks = new ItemStack[] { outstack };
                        block = world.GetBlock(new AssetLocation(Attributes["dirtiedBlockCode"].AsString()));
                    }
                }
                else
                {
                    for (int i = 0; i < stacks.Length; i++)
                    {
                        CookingRecipeIngredient ingred = recipe.GetIngrendientFor(stacks[i]);
                        ItemStack cookedStack = ingred.GetMatchingStack(stacks[i])?.CookedStack?.ResolvedItemstack.Clone();
                        if (cookedStack != null)
                        {
                            stacks[i] = cookedStack;
                        }
                    }
                }

                ItemStack outputStack = new ItemStack(block);

                // Carry over and set perishable properties
                TransitionableProperties cookedPerishProps = recipe.PerishableProps.Clone();
                cookedPerishProps.TransitionedStack.Resolve(world, "cooking container perished stack");

                CarryOverFreshness(api, cookingSlotsProvider.Slots, stacks, cookedPerishProps);

                for (int i = 0; i < stacks.Length; i++)
                {
                    stacks[i].StackSize /= quantityServings; // whats this good for? Probably doesn't do anything meaningful
                }


                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                {
                    cookingSlotsProvider.Slots[i].Itemstack = null;
                }

                ((BlockCookedContainer)block).SetContents(recipe.Code, quantityServings, outputStack, stacks);
                
                outputStack.Collectible.SetTemperature(world, outputStack, GetIngredientsTemperature(world, stacks));
                if (recipe.CooksInto != null)
                {
                    inputSlot.Itemstack = outputStack;
                } else
                {
                    outputSlot.Itemstack = outputStack;
                    inputSlot.Itemstack = null;
                }

                return;
            }
        }

        internal float PutMeal(BlockPos pos, ItemStack[] itemStack, string recipeCode, float quantityServings)
        {
            Block block = api.World.GetBlock(CodeWithVariant("type", "cooked"));
            api.World.BlockAccessor.SetBlock(block.Id, pos);

            float servingsToTransfer = Math.Min(quantityServings, this.Attributes["servingCapacity"].AsInt(1));

            BlockEntityCookedContainer be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCookedContainer;
            be.RecipeCode = recipeCode;
            be.QuantityServings = quantityServings;
            for (int i = 0; i < itemStack.Length; i++)
            {
                be.inventory[i].Itemstack = itemStack[i];
            }

            be.MarkDirty(true);

            return servingsToTransfer;
        }


        public string GetOutputText(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            if (inputSlot.Itemstack == null) return null;
            if (!(inputSlot.Itemstack.Collectible is BlockCookingContainer)) return null;            

            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider);
            
            CookingRecipe recipe = GetMatchingCookingRecipe(world, stacks);

            if (recipe != null)
            {
                double quantity = recipe.GetQuantityServings(stacks);
                string message;
                string outputName = recipe.GetOutputName(world, stacks);

                if (recipe.CooksInto != null)
                {
                    message = "mealcreation-nonfood";
                    outputName = recipe.CooksInto.ResolvedItemstack?.GetName();
                }
                else
                {
                    message = quantity == 1 ? "mealcreation-makesingular" : "mealcreation-makeplural";
                    // We need to use language plural format instead, here and all similar code!
                }
                return Lang.Get(message, (int)quantity, outputName.ToLower());
            }

            return null;
        
        }

        



        public CookingRecipe GetMatchingCookingRecipe(IWorldAccessor world, ItemStack[] stacks)
        {
            List<CookingRecipe> recipes = world.Api.GetCookingRecipes();
            if (recipes == null) return null;

            bool isDirtyPot = Attributes["isDirtyPot"].AsBool(false);
            foreach (var recipe in recipes)
            {
                if (isDirtyPot && recipe.CooksInto == null) continue;   // Prevent normal food from being cooked in a dirty pot
                if (recipe.Matches(stacks))
                {
                    if (recipe.GetQuantityServings(stacks) > MaxServingSize) continue;

                    return recipe;
                }
            }

            return null;
        }


        public static float GetIngredientsTemperature(IWorldAccessor world, ItemStack[] ingredients)
        {
            bool haveStack = false;
            float lowestTemp = 0;
            for (int i = 0; i < ingredients.Length; i++)
            {
                if (ingredients[i] != null)
                {
                    float stackTemp = ingredients[i].Collectible.GetTemperature(world, ingredients[i]);
                    lowestTemp = haveStack ? Math.Min(lowestTemp, stackTemp) : stackTemp;
                    haveStack = true;
                }

            }

            return lowestTemp;
        }


        


        public ItemStack[] GetCookingStacks(ISlotProvider cookingSlotsProvider, bool clone = true)
        {
            List<ItemStack> stacks = new List<ItemStack>(4);

            for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
            {
                ItemStack stack = cookingSlotsProvider.Slots[i].Itemstack;
                if (stack == null) continue;
                stacks.Add(clone ? stack.Clone() : stack);
            }

            return stacks.ToArray();
        }

        public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return new PotInFirepitRenderer(api as ICoreClientAPI, stack, firepit.Pos, forOutputSlot);
        }

        public EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return EnumFirepitModel.Wide;
        }



        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            return capi.BlockTextureAtlas.GetRandomColor(Textures["ceramic"].Baked.TextureSubId, rndIndex);
        }

        public override int GetRandomColor(ICoreClientAPI capi, ItemStack stack)
        {
            return capi.BlockTextureAtlas.GetRandomColor(Textures["ceramic"].Baked.TextureSubId);
        }
    }
}
