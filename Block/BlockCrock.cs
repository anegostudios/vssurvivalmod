using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockCrock : BlockCookedContainerBase, IBlockMealContainer, IContainedMeshSource
    {
        public override float GetContainingTransitionModifierContained(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
        {
            float mul = 1;

            if (transType == EnumTransitionType.Perish)
            {
                if (inSlot.Itemstack.Attributes.GetBool("sealed"))
                {
                    if (inSlot.Itemstack.Attributes.GetString("recipeCode") != null)
                    {
                        mul *= 0.1f;
                    }
                    else
                    {
                        mul *= 0.25f;
                    }
                }
                else
                {
                    mul *= 0.85f;
                }
            }

            return mul;
        }

        public override float GetContainingTransitionModifierPlaced(IWorldAccessor world, BlockPos pos, EnumTransitionType transType)
        {
            float mul = 1;

            BlockEntityCrock becrock = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrock;
            if (becrock == null) return mul;

            if (transType == EnumTransitionType.Perish)
            {
                if (becrock.Sealed)
                {
                    if (becrock.RecipeCode != null)
                    {
                        mul *= 0.1f;
                    }
                    else
                    {
                        mul *= 0.25f;
                    }
                }
                else
                {
                    mul *= 0.85f;
                }
            }

            return mul;
        }


        public AssetLocation LabelForContents(string recipeCode, ItemStack[] contents)
        {
            if (recipeCode != null && recipeCode.Length > 0)
            {
                return AssetLocation.Create("shapes/block/clay/crock/label-meal.json", Code.Domain);
            }
            if (contents == null || contents.Length == 0 || contents[0] == null)
            {
                return AssetLocation.Create("shapes/block/clay/crock/label-empty.json", Code.Domain);
            }

            string contentCode = contents[0].Collectible.Code.Path;
            string type = "empty";

            if (contentCode.Contains("carrot"))
            {
                type = "carrot";
            }
            else if (contentCode.Contains("cabbage"))
            {
                type = "cabbage";
            }
            else if (contentCode.Contains("onion"))
            {
                type = "onion";
            }
            else if (contentCode.Contains("parsnip"))
            {
                type = "parsnip";
            }
            else if (contentCode.Contains("turnip"))
            {
                type = "turnip";
            }
            else if (contentCode.Contains("pumpkin"))
            {
                type = "pumpkin";
            }
            else if (contentCode.Contains("soybean"))
            {
                type = "soybean";
            }

            return AssetLocation.Create("shapes/block/clay/crock/label-" + type + ".json", Code.Domain);
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = new ItemStack(world.GetBlock(CodeWithVariant("side", "east")));

            BlockEntityCrock becrock = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrock;
            if (becrock != null)
            {
                ItemStack[] contents = becrock.GetContentStacks();
                for (int i = 0; i < contents.Length; i++)
                {
                    // if any of the crock's contents still has a stack, return a crock with all the contents
                    if (contents[i] != null)
                    {
                        SetContents(stack, contents);

                        if (becrock.RecipeCode != null)
                        {
                            stack.Attributes.SetString("recipeCode", becrock.RecipeCode);
                            stack.Attributes.SetFloat("quantityServings", becrock.QuantityServings);
                            stack.Attributes.SetBool("sealed", becrock.Sealed);
                        }
                    }
                }
            }

            return stack;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            ItemStack[] contents = GetNonEmptyContents(capi.World, itemstack);
            string recipeCode = itemstack.Attributes.GetString("recipeCode");
            AssetLocation loc = LabelForContents(recipeCode, contents);
            if (loc == null) return;

            Dictionary<string, MeshRef> meshrefs = ObjectCacheUtil.GetOrCreate(capi, "blockcrockGuiMeshRefs", () =>
            {
                return new Dictionary<string, MeshRef>();
            });

            string key = Code.ToShortString() + loc.ToShortString();
            MeshRef meshref;
            if (!meshrefs.TryGetValue(key, out meshref))
            {
                MeshData mesh = GenMesh(capi, loc, new Vec3f(0, 270, 0));
                meshrefs[key] = meshref = capi.Render.UploadMesh(mesh);
            }

            renderinfo.ModelRef = meshref;
        }

        public virtual string GetMeshCacheKey(ItemStack itemstack)
        {
            ItemStack[] contents = GetNonEmptyContents(api.World, itemstack);
            string recipeCode = itemstack.Attributes.GetString("recipeCode");
            AssetLocation loc = LabelForContents(recipeCode, contents);

            return Code.ToShortString() + loc.ToShortString();
        }


        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            ItemStack[] contents = GetNonEmptyContents(api.World, itemstack);
            string recipeCode = itemstack.Attributes.GetString("recipeCode");
            
            return GenMesh(api as ICoreClientAPI, LabelForContents(recipeCode, contents));
        }

        public MeshData GenMesh(ICoreClientAPI capi, AssetLocation labelLoc, Vec3f rot = null)
        {
            var tesselator = capi.Tesselator;
            Shape baseshape = capi.Assets.TryGet(AssetLocation.Create("shapes/block/clay/crock/base.json", Code.Domain)).ToObject<Shape>();
            Shape labelshape = capi.Assets.TryGet(labelLoc).ToObject<Shape>();

            MeshData mesh, labelmesh;
            tesselator.TesselateShape(this, baseshape, out mesh, rot);
            tesselator.TesselateShape(this, labelshape, out labelmesh, rot);

            mesh.AddMeshData(labelmesh);

            return mesh;
        }


        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            for (int i = 0; i < allInputslots.Length; i++)
            {
                ItemSlot slot = allInputslots[i];
                if (slot.Itemstack?.Collectible is BlockCrock)
                {
                    outputSlot.Itemstack.Attributes = slot.Itemstack.Attributes.Clone();
                    outputSlot.Itemstack.Attributes.SetBool("sealed", true);
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel?.Position == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            Block block = api.World.BlockAccessor.GetBlock(blockSel.Position);
            float quantityServings = (float)slot.Itemstack.Attributes.GetDecimal("quantityServings");

            if (block?.Attributes?.IsTrue("mealContainer") == true)
            {
                if (!byEntity.Controls.Sneak) return;
                if (quantityServings > 0)
                {
                    ServeIntoBowl(block, blockSel.Position, slot, byEntity.World);
                }
                handHandling = EnumHandHandling.PreventDefault;
                return;
            }


            if (block is BlockGroundStorage)
            {
                if (!byEntity.Controls.Sneak) return;
                var begs = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
                ItemSlot gsslot = begs.GetSlotAt(blockSel);
                if (gsslot == null || gsslot.Empty) return;

                if (gsslot.Itemstack.ItemAttributes?.IsTrue("mealContainer") == true)
                {
                    if (quantityServings > 0)
                    {
                        ServeIntoStack(gsslot, slot, byEntity.World);
                        gsslot.MarkDirty();
                        begs.updateMeshes();
                        begs.MarkDirty(true);
                    }

                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            if (block is BlockCookingContainer && slot.Itemstack.Attributes.HasAttribute("recipeCode"))
            {
                handHandling = EnumHandHandling.PreventDefault;
                ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                string recipeCode = slot.Itemstack.Attributes.GetString("recipeCode");
                

                float movedServings = (block as BlockCookingContainer).PutMeal(blockSel.Position, GetNonEmptyContents(api.World, slot.Itemstack), recipeCode, quantityServings);

                quantityServings -= movedServings;

                if (quantityServings > 0) {
                    slot.Itemstack.Attributes.SetFloat("quantityServings", quantityServings);
                } else {

                    slot.Itemstack.Attributes.RemoveAttribute("recipeCode");
                    slot.Itemstack.Attributes.RemoveAttribute("quantityServings");
                    slot.Itemstack.Attributes.RemoveAttribute("contents");
                }

                return;
            }

            if (block is BlockBarrel)
            {
                BlockEntityBarrel bebarrel = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBarrel;
                if (bebarrel != null)
                {
                    ItemStack stack = bebarrel.Inventory[0].Itemstack;

                    ItemStack[] ownContentStacks = GetNonEmptyContents(api.World, slot.Itemstack);
                    if (ownContentStacks == null || ownContentStacks.Length == 0)
                    {
                        if (stack != null && stack.Collectible.Attributes?.IsTrue("crockable") == true)
                        {
                            float servingCapacity = slot.Itemstack.Block.Attributes["servingCapacity"].AsFloat(1);

                            var foodstack = bebarrel.Inventory[0].TakeOut((int)servingCapacity * 4);
                            float servingSize = foodstack.StackSize / 4f; 
                            
                            foodstack.StackSize = Math.Max(0, foodstack.StackSize / 4);

                            SetContents(null, slot.Itemstack, new ItemStack[] { foodstack }, servingSize);
                            bebarrel.MarkDirty(true);
                            slot.MarkDirty();
                        }
                    }

                    // non-meal items (eg. pickled vegetables) can be placed from a crock INTO a barrel - this is useful eg. for cheese-making
                    else if (ownContentStacks.Length == 1 && slot.Itemstack.Attributes.GetString("recipeCode") == null)
                    {
                        var foodstack = ownContentStacks[0].Clone();
                        foodstack.StackSize = (int)(foodstack.StackSize * quantityServings);
                        new DummySlot(foodstack).TryPutInto(api.World, bebarrel.Inventory[0], foodstack.StackSize);
                        foodstack.StackSize = (int)(foodstack.StackSize / quantityServings);

                        if (foodstack.StackSize <= 0)
                        {
                            SetContents(slot.Itemstack, new ItemStack[0]);
                        } else
                        {
                            SetContents(slot.Itemstack, new ItemStack[] { foodstack });
                        }

                        bebarrel.MarkDirty(true);
                        slot.MarkDirty();
                    }
                }

                handHandling = EnumHandHandling.PreventDefault;
                ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                return;
            }


            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }


        

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("mealContainer") == true && (!(hotbarSlot.Itemstack.Collectible is BlockCrock) || hotbarSlot.StackSize == 1))
            {
                BlockEntityCrock bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCrock;
                if (bec == null) return false;

                if (hotbarSlot.Itemstack.Attributes.GetDecimal("quantityServings", 0) == 0)
                {
                    bec.ServeInto(byPlayer, hotbarSlot);
                    
                    return true;
                }

                if (bec.QuantityServings == 0)
                {
                    ServeIntoBowl(this, blockSel.Position, hotbarSlot, world);
                    
                    bec.Sealed = false;
                    bec.MarkDirty(true);
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }



        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            BlockMeal mealblock = world.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;

            CookingRecipe recipe = GetCookingRecipe(world, inSlot.Itemstack);
            ItemStack[] stacks = GetNonEmptyContents(world, inSlot.Itemstack);

            if (stacks == null || stacks.Length == 0)
            {
                dsc.AppendLine(Lang.Get("Empty"));

                if (inSlot.Itemstack.Attributes.GetBool("sealed"))
                {
                    dsc.AppendLine("<font color=\"lightgreen\">" + Lang.Get("Sealed.") + "</font>");
                }

                return;
            }

            DummyInventory dummyInv = new DummyInventory(api);

            ItemSlot slot = GetDummySlotForFirstPerishableStack(api.World, stacks, null, dummyInv);
            dummyInv.OnAcquireTransitionSpeed = (transType, stack, mul) =>
            {
                float val = mul * GetContainingTransitionModifierContained(world, inSlot, transType);

                val *= inSlot.Inventory.GetTransitionSpeedMul(transType, inSlot.Itemstack);

                return val;
            };
            

            if (recipe != null)
            {
                double servings = inSlot.Itemstack.Attributes.GetDecimal("quantityServings");

                if (recipe != null)
                {
                    if (servings == 1)
                    {
                        dsc.AppendLine(Lang.Get("{0} serving of {1}", servings, recipe.GetOutputName(world, stacks)));
                    }
                    else
                    {
                        dsc.AppendLine(Lang.Get("{0} servings of {1}", servings, recipe.GetOutputName(world, stacks)));
                    }
                }

                string facts = mealblock.GetContentNutritionFacts(world, inSlot, null);
                if (facts != null)
                {
                    dsc.Append(facts);
                }



            } else
            {
                if (inSlot.Itemstack.Attributes.HasAttribute("quantityServings"))
                {
                    double servings = inSlot.Itemstack.Attributes.GetDecimal("quantityServings");
                    dsc.AppendLine(Lang.Get("{0} servings left", Math.Round(servings, 1)));
                }
                else
                {
                    dsc.AppendLine("Contents:");
                    foreach (var stack in stacks)
                    {
                        if (stack == null) continue;

                        dsc.AppendLine(stack.StackSize + "x  " + stack.GetName());
                    }
                }
            }


            slot.Itemstack?.Collectible.AppendPerishableInfoText(slot, dsc, world);

            if (inSlot.Itemstack.Attributes.GetBool("sealed"))
            {
                dsc.AppendLine("<font color=\"lightgreen\">" + Lang.Get("Sealed.") + "</font>");
            }            
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockEntityCrock becrock = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrock;
            if (becrock == null) return "";

            BlockMeal mealblock = world.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;

            CookingRecipe recipe = world.CookingRecipes.FirstOrDefault((rec) => becrock.RecipeCode == rec.Code);
            ItemStack[] stacks = becrock.inventory.Where(slot => !slot.Empty).Select(slot => slot.Itemstack).ToArray();

            if (stacks == null || stacks.Length == 0)
            {
                return Lang.Get("Empty");
            }

            StringBuilder dsc = new StringBuilder();

            if (recipe != null)
            {
                ItemSlot slot = GetDummySlotForFirstPerishableStack(api.World, stacks, forPlayer.Entity, becrock.inventory);

                if (recipe != null)
                {
                    if (becrock.QuantityServings == 1)
                    {
                        dsc.AppendLine(Lang.Get("{0} serving of {1}", Math.Round(becrock.QuantityServings, 1), recipe.GetOutputName(world, stacks)));
                    }
                    else
                    {
                        dsc.AppendLine(Lang.Get("{0} servings of {1}", Math.Round(becrock.QuantityServings, 1), recipe.GetOutputName(world, stacks)));
                    }
                }

                string facts = mealblock.GetContentNutritionFacts(world, new DummySlot(OnPickBlock(world, pos)), null);

                if (facts != null)
                {
                    dsc.Append(facts);
                }

                slot.Itemstack?.Collectible.AppendPerishableInfoText(slot, dsc, world);
            }
            else
            {
                dsc.AppendLine("Contents:");
                foreach (var stack in stacks)
                {
                    if (stack == null) continue;

                    dsc.AppendLine(stack.StackSize + "x  " + stack.GetName());
                }

                becrock.inventory[0].Itemstack?.Collectible.AppendPerishableInfoText(becrock.inventory[0], dsc, api.World);
            }

            if (becrock.Sealed)
            {
                dsc.AppendLine("<font color=\"lightgreen\">" + Lang.Get("Sealed.") + "</font>");
            }
            

            return dsc.ToString();
        }


        public static ItemSlot GetDummySlotForFirstPerishableStack(IWorldAccessor world, ItemStack[] stacks, Entity forEntity, InventoryBase slotInventory)
        {
            ItemStack stack = null;
            if (stacks != null)
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    if (stacks[i] != null)
                    {
                        TransitionableProperties[] props = stacks[i].Collectible.GetTransitionableProperties(world, stacks[i], forEntity);
                        if (props != null && props.Length > 0)
                        {
                            stack = stacks[i];
                            break;
                        }
                    }
                }
            }

            DummySlot slot = new DummySlot(stack, slotInventory);
            slot.MarkedDirty += () => true;

            return slot;
        }




        public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            TransitionState[] states = base.UpdateAndGetTransitionStates(world, inslot);

            ItemStack[] stacks = GetNonEmptyContents(world, inslot.Itemstack);
            if (MealMeshCache.ContentsRotten(stacks))
            {
                inslot.Itemstack.Attributes.RemoveAttribute("recipeCode");
                inslot.Itemstack.Attributes?.RemoveAttribute("quantityServings");
            }
            if (stacks == null || stacks.Length == 0)
            {
                inslot.Itemstack.Attributes.RemoveAttribute("recipeCode");
                inslot.Itemstack.Attributes?.RemoveAttribute("quantityServings");
            }

            return states;
        }



        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            IWorldAccessor world = entityItem.World;
            if (world.Side != EnumAppSide.Server) return;

            if (entityItem.Swimming && world.Rand.NextDouble() < 0.01)
            {
                ItemStack[] stacks = GetContents(world, entityItem.Itemstack);
                if (MealMeshCache.ContentsRotten(stacks))
                {
                    for (int i = 0; i < stacks.Length; i++)
                    {
                        if (stacks[i] != null && stacks[i].StackSize > 0 && stacks[i].Collectible.Code.Path == "rot")
                        {
                            world.SpawnItemEntity(stacks[i], entityItem.ServerPos.XYZ);
                        }
                    }

                    entityItem.Itemstack.Attributes.RemoveAttribute("sealed");
                    entityItem.Itemstack.Attributes.RemoveAttribute("recipeCode");
                    entityItem.Itemstack.Attributes.RemoveAttribute("quantityServings");
                    entityItem.Itemstack.Attributes.RemoveAttribute("contents");
                }
            }
        }



        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("blockcrockGuiMeshRefs", out obj))
            {
                Dictionary<string, MeshRef> meshrefs = obj as Dictionary<string, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("blockcrockGuiMeshRefs");
            }
        }


    }
}
