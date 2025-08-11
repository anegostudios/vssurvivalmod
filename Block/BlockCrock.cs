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
        string shapeLocation = "game:shapes/block/clay/crock/";
        string[] labelNames = ["carrot", "cabbage", "onion", "parsnip", "turnip", "pumpkin", "soybean", "bellpepper", "cassava", "mushroom", "redmeat", "poultry", "porridge"];

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes?["labelNames"].AsArray<string>(null) is string[] labelNames) this.labelNames = labelNames;

            if (API.Common.Shape.TryGet(api, Shape.Base?.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")) != null)
            {
                shapeLocation = Shape.Base!.Clone().WithFilename("").WithPathPrefixOnce("shapes/").ToString();
            }
            else if(API.Common.Shape.TryGet(api, AssetLocation.Create("shapes/block/clay/crock/base.json", Code.Domain)) != null)
            {
                shapeLocation = Code.Domain + ":shapes/block/clay/crock/";
            }
        }

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

        public AssetLocation LabelForContents(string? recipeCode, ItemStack[]? contents)
        {
            if (contents == null || contents.Length == 0 || contents[0] == null)
            {
                return AssetLocation.Create(shapeLocation + "label-empty.json");
            }

            if (MealMeshCache.ContentsRotten(contents))
            {
                return AssetLocation.Create(shapeLocation + "label-rot.json");
            }

            if (recipeCode != null && recipeCode.Length > 0)
            {
                return AssetLocation.Create(shapeLocation + "label-" + ((labelNames.Contains(recipeCode) ? recipeCode : null) ?? CodeToLabel(getMostCommonMealIngredient(contents)) ?? "meal") + ".json");
            }

            return AssetLocation.Create(shapeLocation + "label-" + (CodeToLabel(contents[0].Collectible.Code) ?? "empty") + ".json");
        }

        public string? CodeToLabel(AssetLocation? loc)
        {
            if (loc == null) return null;
            string? type = null;

            foreach (var name in labelNames)
            {
                if (loc.Path.Contains(name))
                {
                    type = name;
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
            bool isSealed = itemstack.Attributes.GetBool("sealed") == true;
            ItemStack[] contents = GetNonEmptyContents(capi.World, itemstack);
            string recipeCode = itemstack.Attributes.GetString("recipeCode");
            AssetLocation loc = LabelForContents(recipeCode, contents);
            if (loc == null) return;

            Dictionary<string, MultiTextureMeshRef> meshrefs = ObjectCacheUtil.GetOrCreate(capi, "blockcrockGuiMeshRefs", () =>
            {
                return new Dictionary<string, MultiTextureMeshRef>();
            });

            string key = Code.ToShortString() + loc.ToShortString() + (isSealed ? "sealed" : "");
            if (!meshrefs.TryGetValue(key, out MultiTextureMeshRef? meshref))
            {
                MeshData mesh = GenMesh(capi, loc, new Vec3f(0, 270, 0));
                if (isSealed && GenSealMesh(capi) is MeshData sealMesh) mesh.AddMeshData(sealMesh);
                meshrefs[key] = meshref = capi.Render.UploadMultiTextureMesh(mesh);
            }

            renderinfo.ModelRef = meshref;
        }

        public virtual string GetMeshCacheKey(ItemStack itemstack)
        {
            bool isSealed = itemstack.Attributes.GetBool("sealed") == true;
            ItemStack[] contents = GetNonEmptyContents(api.World, itemstack);
            string recipeCode = itemstack.Attributes.GetString("recipeCode");
            AssetLocation loc = LabelForContents(recipeCode, contents);

            return Code.ToShortString() + loc.ToShortString() + (isSealed ? "sealed" : "");
        }


        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos? forBlockPos = null)
        {
            var capi = (ICoreClientAPI)api;
            ItemStack[] contents = GetNonEmptyContents(api.World, itemstack);
            string recipeCode = itemstack.Attributes.GetString("recipeCode");
            var mesh = GenMesh(capi, LabelForContents(recipeCode, contents));
            if (itemstack.Attributes.GetBool("sealed") == true && GenSealMesh(capi) is MeshData sealMesh) mesh.AddMeshData(sealMesh);


            return mesh;
        }

        public MeshData GenMesh(ICoreClientAPI capi, AssetLocation labelLoc, Vec3f? rot = null)
        {
            var tesselator = capi.Tesselator;

            Shape? baseshape = API.Common.Shape.TryGet(capi, AssetLocation.Create(shapeLocation + "base.json"));
            Shape? labelshape = API.Common.Shape.TryGet(capi, labelLoc);

            tesselator.TesselateShape(this, baseshape, out MeshData mesh, rot);

            if (labelshape != null)
            {
                tesselator.TesselateShape(this, labelshape, out MeshData labelmesh, rot);
                mesh.AddMeshData(labelmesh);
            }

            return mesh;
        }

        public MeshData? GenSealMesh(ICoreClientAPI capi)
        {
            var tesselator = capi.Tesselator;

            Shape? shape = API.Common.Shape.TryGet(capi, AssetLocation.Create(shapeLocation + "seal.json"));
            if (shape == null) return null;

            tesselator.TesselateShape(this, shape, out MeshData mesh);

            return mesh;
        }

        public override bool MatchesForCrafting(ItemStack inputStack, GridRecipe gridRecipe, CraftingRecipeIngredient ingredient)
        {
            if (gridRecipe.Output.ResolvedItemstack?.Collectible is not BlockCrock) return base.MatchesForCrafting(inputStack, gridRecipe, ingredient);
            bool sealingRecipe = false;
            bool isSealed = false;

            for (int i = 0; i < gridRecipe.resolvedIngredients.Length; i++)
            {
                ItemStack stack = gridRecipe.resolvedIngredients[i].ResolvedItemstack;
                if (stack?.Collectible is BlockCrock) isSealed = stack.Attributes.GetBool("sealed");
                else if (stack?.ItemAttributes["canSealCrock"].AsBool(false) == true) sealingRecipe = true;
            }

            return sealingRecipe && !isSealed;
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
                        // Check both slots, but try the solid slot first
                        if (bebarrel.Inventory.FirstOrDefault(slot => slot.Itemstack?.Collectible.Attributes?.IsTrue("crockable") == true) is ItemSlot sourceSlot)
                        {
                            float servingCapacity = crockStack.Block.Attributes["servingCapacity"].AsFloat(1);
                            float itemsPerServing = (BlockLiquidContainerBase.GetContainableProps(sourceSlot.Itemstack)?.ItemsPerLitre ?? 1) * 4;

                            if (sourceSlot.TakeOut((int)(servingCapacity * itemsPerServing)) is ItemStack foodstack)
                            {
                                float servingSize = foodstack.StackSize / itemsPerServing;

                                foodstack.StackSize = (int)itemsPerServing;

                                SetContents(null, crockStack, [foodstack], servingSize);
                                bebarrel.MarkDirty(true);
                                slot.MarkDirty();
                            }
                        }
                    }

                    // non-meal items (eg. pickled vegetables) can be placed from a crock INTO a barrel - this is useful eg. for cheese-making
                    else if (ownContentStacks.Length == 1 && crockStack.Attributes.GetString("recipeCode") == null)
                    {
                        var liquidProps = BlockLiquidContainerBase.GetContainableProps(ownContentStacks[0]);
                        var sinkSlot = bebarrel.Inventory[liquidProps == null ? 0 : 1]; // Solid or Liquid slot
                        float itemsPerServing = (liquidProps?.ItemsPerLitre ?? 1) * 4;

                        var foodstack = ownContentStacks[0].Clone();
                        foodstack.StackSize = (int)(quantityServings * itemsPerServing);
                        int transfered = new DummySlot(foodstack).TryPutInto(api.World, sinkSlot, foodstack.StackSize);
                        quantityServings = ((quantityServings * itemsPerServing) - transfered) / itemsPerServing;

                        if (quantityServings <= 0)
                        {
                            crockStack.Attributes.RemoveAttribute("recipeCode");
                            crockStack.Attributes.RemoveAttribute("quantityServings");
                            crockStack.Attributes.RemoveAttribute("contents");
                        }
                        else crockStack.Attributes.SetFloat("quantityServings", quantityServings);
                        crockStack.Attributes.RemoveAttribute("sealed");

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



            }
            else if (crockStack.Attributes.HasAttribute("quantityServings"))
            {
                double servings = crockStack.Attributes.GetDecimal("quantityServings");

                if (Math.Round(servings, 1) < 0.05)
                {
                    dsc.AppendLine(Lang.Get("meal-servingsleft-percent", Math.Round(servings * 100, 0)));
                }
                else dsc.AppendLine(Lang.Get("{0} servings left", Math.Round(servings, 1)));
            }
            else if (!MealMeshCache.ContentsRotten(stacks))
            {
                dsc.AppendLine(Lang.Get("Contents: {0}", Lang.Get("meal-ingredientlist-" + stacks.Length, stacks.Select(stack => Lang.Get("{0}x {1}", stack.StackSize, stack.GetName())))));
            }


            mealSlot.Itemstack?.Collectible.AppendPerishableInfoText(mealSlot, dsc, world);

            if (crockStack.Attributes.GetBool("sealed"))
            {
                dsc.AppendLine("<font color=\"lightgreen\">" + Lang.Get("Sealed.") + "</font>");
            }
        }

        public override string GetHeldItemName(ItemStack? itemStack)
        {
            if (IsEmpty(itemStack)) return base.GetHeldItemName(itemStack);

            ItemStack[] contentStacks = GetContents(api.World, itemStack);
            string? recipeCode = itemStack?.Collectible.GetCollectibleInterface<IBlockMealContainer>()?.GetRecipeCode(api.World, itemStack);
            string code = Lang.Get("mealrecipe-name-" + recipeCode + "-in-container");

            if (recipeCode == null)
            {
                if (MealMeshCache.ContentsRotten(contentStacks))
                {
                    code = Lang.Get("Rotten Food");
                }
                else
                {
                    code = contentStacks[0].GetName();
                }
            }

            var loc = CodeWithVariant("type", "meal");
            return Lang.GetMatching(loc.Domain + AssetLocation.LocationSeparator + "block-" + loc.Path, code);
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
            if (inslot.Itemstack is not ItemStack crockStack) return null;

            ItemStack[] stacks = GetNonEmptyContents(world, crockStack);
            foreach (var stack in stacks) stack.StackSize *= (int)Math.Max(1, crockStack.Attributes.TryGetFloat("quantityServings") ?? 1);
            SetContents(crockStack, stacks);

            TransitionState[]? states = base.UpdateAndGetTransitionStates(world, inslot);

            stacks = GetNonEmptyContents(world, crockStack);
            if (stacks.Length == 0 || MealMeshCache.ContentsRotten(stacks))
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    var transProps = stacks[i].Collectible.GetTransitionableProperties(world, stacks[i], null);
                    var spoilProps = transProps?.FirstOrDefault(props => props.Type == EnumTransitionType.Perish);
                    if (spoilProps == null) continue;
                    stacks[i] = stacks[i].Collectible.OnTransitionNow(GetContentInDummySlot(inslot, stacks[i]), spoilProps);
                }
                SetContents(crockStack, stacks);

                crockStack.Attributes.RemoveAttribute("recipeCode");
                crockStack.Attributes.RemoveAttribute("quantityServings");
            }

            foreach (var stack in stacks) stack.StackSize /= (int)Math.Max(1, crockStack.Attributes.TryGetFloat("quantityServings") ?? 1);
            SetContents(crockStack, stacks);

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
                    entityItem.MarkShapeModified();
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
