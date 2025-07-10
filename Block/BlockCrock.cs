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
                if (inSlot.Itemstack?.Attributes?.GetBool("sealed") == true)
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

            if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityCrock becrock) return mul;

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

        string[] vegetableLabels = ["carrot", "cabbage", "onion", "parsnip", "turnip", "pumpkin", "soybean", "bellpepper", "cassava", "mushroom", "redmeat", "poultry", "porridge"];

        public AssetLocation LabelForContents(string? recipeCode, ItemStack[]? contents)
        {
            if (contents == null || contents.Length == 0 || contents[0] == null)
            {
                return AssetLocation.Create("shapes/block/clay/crock/label-empty.json", Code.Domain);
            }

            if (MealMeshCache.ContentsRotten(contents))
            {
                return AssetLocation.Create("shapes/block/clay/crock/label-rot.json", Code.Domain);
            }

            if (recipeCode != null && recipeCode.Length > 0)
            {
                return AssetLocation.Create("shapes/block/clay/crock/label-" + (CodeToLabel(getMostCommonMealIngredient(contents)) ?? "meal") + ".json", Code.Domain);
            }

            return AssetLocation.Create("shapes/block/clay/crock/label-" + (CodeToLabel(contents[0].Collectible.Code) ?? "empty") + ".json", Code.Domain);
        }

        public string? CodeToLabel(AssetLocation? loc)
        {
            if (loc == null) return null;
            string? type = null;

            foreach (var label in vegetableLabels)
            {
                if (loc.Path.Contains(label))
                {
                    type = label;
                    break;
                }
            }

            return type;
        }

        private AssetLocation? getMostCommonMealIngredient(ItemStack[] contents)
        {
            Dictionary<AssetLocation, int> sdf = new Dictionary<AssetLocation, int>();

            foreach (var stack in contents)
            {
                sdf.TryGetValue(stack.Collectible.Code, out int cnt);
                sdf[stack.Collectible.Code] = 1 + cnt;
            }

            var key = sdf.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;

            return sdf[key] >= 3 ? key : null;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = new ItemStack(world.GetBlock(CodeWithVariant("side", "east")));

            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityCrock becrock)
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

            Dictionary<string, MultiTextureMeshRef> meshrefs = ObjectCacheUtil.GetOrCreate(capi, "blockcrockGuiMeshRefs", () =>
            {
                return new Dictionary<string, MultiTextureMeshRef>();
            });

            string key = Code.ToShortString() + loc.ToShortString();
            if (!meshrefs.TryGetValue(key, out MultiTextureMeshRef? meshref))
            {
                MeshData mesh = GenMesh(capi, loc, new Vec3f(0, 270, 0));
                meshrefs[key] = meshref = capi.Render.UploadMultiTextureMesh(mesh);
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


        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos? forBlockPos = null)
        {
            ItemStack[] contents = GetNonEmptyContents(api.World, itemstack);
            string recipeCode = itemstack.Attributes.GetString("recipeCode");
            
            return GenMesh((ICoreClientAPI)api, LabelForContents(recipeCode, contents));
        }

        public MeshData GenMesh(ICoreClientAPI capi, AssetLocation labelLoc, Vec3f? rot = null)
        {
            var tesselator = capi.Tesselator;

            Shape baseshape = API.Common.Shape.TryGet(capi, AssetLocation.Create("shapes/block/clay/crock/base.json", Code.Domain));
            Shape labelshape = API.Common.Shape.TryGet(capi, labelLoc);

            tesselator.TesselateShape(this, baseshape, out MeshData mesh, rot);
            tesselator.TesselateShape(this, labelshape, out MeshData labelmesh, rot);

            mesh.AddMeshData(labelmesh);

            return mesh;
        }


        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            if (outputSlot.Itemstack == null) return;

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

            if (slot.Itemstack is not ItemStack crockStack) return;

            Block block = api.World.BlockAccessor.GetBlock(blockSel.Position);
            float quantityServings = (float)crockStack.Attributes.GetDecimal("quantityServings");

            if (block?.Attributes?.IsTrue("mealContainer") == true)
            {
                if (!byEntity.Controls.ShiftKey) return;
                if (quantityServings > 0)
                {
                    ServeIntoBowl(block, blockSel.Position, slot, byEntity.World);
                }
                handHandling = EnumHandHandling.PreventDefault;
                return;
            }


            if (block is BlockGroundStorage)
            {
                if (!byEntity.Controls.ShiftKey) return;
                if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityGroundStorage begs || begs?.GetSlotAt(blockSel) is not ItemSlot gsslot || gsslot.Empty) return;

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

            if (block is BlockCookingContainer bcc && crockStack.Attributes.HasAttribute("recipeCode"))
            {
                handHandling = EnumHandHandling.PreventDefault;
                ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                string recipeCode = crockStack.Attributes.GetString("recipeCode");
                

                float movedServings = bcc.PutMeal(blockSel.Position, GetNonEmptyContents(api.World, crockStack), recipeCode, quantityServings);

                quantityServings -= movedServings;

                if (quantityServings > 0) {
                    crockStack.Attributes.SetFloat("quantityServings", quantityServings);
                } else {

                    crockStack.Attributes.RemoveAttribute("recipeCode");
                    crockStack.Attributes.RemoveAttribute("quantityServings");
                    crockStack.Attributes.RemoveAttribute("contents");
                }

                return;
            }

            if (block is BlockBarrel)
            {
                if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBarrel bebarrel)
                {
                    ItemStack[] ownContentStacks = GetNonEmptyContents(api.World, crockStack);
                    if (ownContentStacks == null || ownContentStacks.Length == 0)
                    {
                        if (bebarrel.Inventory[0].Itemstack?.Collectible.Attributes?.IsTrue("crockable") == true)
                        {
                            float servingCapacity = crockStack.Block.Attributes["servingCapacity"].AsFloat(1);

                            if (bebarrel.Inventory[0].TakeOut((int)servingCapacity * 4) is ItemStack foodstack)
                            {
                                float servingSize = foodstack.StackSize / 4f;

                                foodstack.StackSize = Math.Max(0, foodstack.StackSize / 4);

                                SetContents(null, crockStack, [foodstack], servingSize);
                                bebarrel.MarkDirty(true);
                                slot.MarkDirty();
                            }
                        }
                    }

                    // non-meal items (eg. pickled vegetables) can be placed from a crock INTO a barrel - this is useful eg. for cheese-making
                    else if (ownContentStacks.Length == 1 && crockStack.Attributes.GetString("recipeCode") == null)
                    {
                        var foodstack = ownContentStacks[0].Clone();
                        foodstack.StackSize = (int)(foodstack.StackSize * quantityServings);
                        new DummySlot(foodstack).TryPutInto(api.World, bebarrel.Inventory[0], foodstack.StackSize);
                        foodstack.StackSize = (int)(foodstack.StackSize / quantityServings);

                        if (foodstack.StackSize <= 0)
                        {
                            SetContents(crockStack, Array.Empty<ItemStack>());
                        } else
                        {
                            SetContents(crockStack, [foodstack]);
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
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityCrock bec) return false;

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

            if (inSlot.Itemstack is not ItemStack crockStack) return;

            CookingRecipe? recipe = GetCookingRecipe(world, crockStack);
            ItemStack[]? stacks = GetNonEmptyContents(world, crockStack);

            if (stacks == null || stacks.Length == 0)
            {
                dsc.AppendLine(Lang.Get("Empty"));

                if (crockStack.Attributes.GetBool("sealed") == true)
                {
                    dsc.AppendLine("<font color=\"lightgreen\">" + Lang.Get("Sealed.") + "</font>");
                }

                return;
            }

            DummyInventory dummyInv = new DummyInventory(api);

            ItemSlot mealSlot = GetDummySlotForFirstPerishableStack(api.World, stacks, null, dummyInv);
            dummyInv.OnAcquireTransitionSpeed += (transType, stack, mul) =>
            {
                float val = mul * GetContainingTransitionModifierContained(world, inSlot, transType);

                if (inSlot.Inventory != null) val *= inSlot.Inventory.GetTransitionSpeedMul(transType, crockStack);

                return val;
            };
            

            if (recipe != null)
            {
                double servings = crockStack.Attributes.GetDecimal("quantityServings");

                if (recipe != null)
                {
                    if (servings == 1)
                    {
                        dsc.AppendLine(Lang.Get("{0} serving of {1}", Math.Round(servings, 1), recipe.GetOutputName(world, stacks)));
                    }
                    else
                    {
                        dsc.AppendLine(Lang.Get("{0} servings of {1}", Math.Round(servings, 1), recipe.GetOutputName(world, stacks)));
                    }
                }

                string? facts = BlockMeal.AllMealBowls?[0]?.GetContentNutritionFacts(world, inSlot, null);
                if (facts != null)
                {
                    dsc.Append(facts);
                }



            } else
            {
                if (crockStack.Attributes.HasAttribute("quantityServings"))
                {
                    double servings = crockStack.Attributes.GetDecimal("quantityServings");
                    dsc.AppendLine(Lang.Get("{0} servings left", Math.Round(servings, 1)));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("Contents:"));
                    foreach (var stack in stacks)
                    {
                        if (stack == null) continue;

                        dsc.AppendLine(stack.StackSize + "x  " + stack.GetName());
                    }
                }
            }


            mealSlot.Itemstack?.Collectible.AppendPerishableInfoText(mealSlot, dsc, world);

            if (crockStack.Attributes.GetBool("sealed"))
            {
                dsc.AppendLine("<font color=\"lightgreen\">" + Lang.Get("Sealed.") + "</font>");
            }
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityCrock becrock) return "";

            CookingRecipe? recipe = api.GetCookingRecipe(becrock.RecipeCode);
            ItemStack[] stacks = becrock.inventory.Where(slot => !slot.Empty).Select(slot => slot.Itemstack!).ToArray();

            if (stacks.Length == 0)
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

                string? facts = BlockMeal.AllMealBowls?[0]?.GetContentNutritionFacts(world, new DummySlot(OnPickBlock(world, pos)), null);

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


        public static ItemSlot GetDummySlotForFirstPerishableStack(IWorldAccessor world, ItemStack[]? stacks, Entity? forEntity, InventoryBase slotInventory)
        {
            ItemStack? stack = null;
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




        public override TransitionState[]? UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            TransitionState[]? states = base.UpdateAndGetTransitionStates(world, inslot);

            if (inslot.Itemstack is ItemStack crockStack && (GetNonEmptyContents(world, crockStack) is not ItemStack[] stacks || stacks.Length == 0 || MealMeshCache.ContentsRotten(stacks)))
            {
                crockStack.Attributes?.RemoveAttribute("recipeCode");
                crockStack.Attributes?.RemoveAttribute("quantityServings");
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
            if (api is not ICoreClientAPI capi) return;

            if (capi.ObjectCache.TryGetValue("blockcrockGuiMeshRefs", out object? obj))
            {
                if (obj is Dictionary<string, MultiTextureMeshRef> meshrefs)
                {
                    foreach (var val in meshrefs)
                    {
                        val.Value.Dispose();
                    }
                }

                capi.ObjectCache.Remove("blockcrockGuiMeshRefs");
            }
        }

        public bool IsFullAndUnsealed(ItemStack stack)
        {
            return !IsEmpty(stack) && !stack.Attributes.GetBool("sealed");
        }

        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (priority == EnumMergePriority.DirectMerge && sourceStack.ItemAttributes?["canSealCrock"]?.AsBool() == true && IsFullAndUnsealed(sinkStack))
            {
                return 1;
            }

            return base.GetMergableQuantity(sinkStack, sourceStack, priority);
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            ItemSlot sourceSlot = op.SourceSlot;

            if (op.CurrentPriority == EnumMergePriority.DirectMerge && sourceSlot.Itemstack.ItemAttributes?["canSealCrock"]?.AsBool() == true)
            {
                ItemSlot sinkSlot = op.SinkSlot;

                if (IsFullAndUnsealed(sinkSlot.Itemstack))
                {
                    sinkSlot.Itemstack.Attributes.SetBool("sealed", true);
                    op.MovedQuantity = 1;
                    sourceSlot.TakeOut(1);
                    sinkSlot.MarkDirty();
                }

                return;
            }

            base.TryMergeStacks(op);
        }

        public override bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            var handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Itemstack?.Collectible.Attributes?["canSealCrock"]?.AsBool() == true)
            {
                if (IsFullAndUnsealed(slot.Itemstack))
                {
                    slot.Itemstack.Attributes.SetBool("sealed", true);
                    handSlot.TakeOut(1);
                    handSlot.MarkDirty();
                }
                else (api as ICoreClientAPI)?.TriggerIngameError(this, "crockemptyorsealed", Lang.Get("ingameerror-crock-empty-or-sealed"));

                return true;
            }

            return base.OnContainedInteractStart(be, slot, byPlayer, blockSel);
        }
    }
}
