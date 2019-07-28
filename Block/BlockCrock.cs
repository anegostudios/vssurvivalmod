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
    public class BlockCrock : BlockContainer, IBlockMealContainer
    {
        public AssetLocation LabelForContents(string recipeCode, ItemStack[] contents)
        {
            if (recipeCode != null && recipeCode.Length > 0)
            {
                return AssetLocation.Create("shapes/block/clay/crock/label-meal.json", Code.Domain);
            }
            if (contents == null || contents.Length == 0)
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
            ItemStack stack = base.OnPickBlock(world, pos);

            BlockEntityCrock becrock = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrock;
            if (becrock == null) return stack;

            ItemStack[] stacks = becrock.inventory.Where(slot => !slot.Empty).Select(slot => slot.Itemstack).ToArray();
            if (becrock.RecipeCode != null)
            {
                stack.Attributes.SetString("recipeCode", becrock.RecipeCode);
                stack.Attributes.SetFloat("quantityServings", becrock.QuantityServings);
                stack.Attributes.SetBool("sealed", becrock.Sealed);
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

            string key = loc.ToShortString();
            MeshRef meshref;
            if (!meshrefs.TryGetValue(key, out meshref))
            {
                MeshData mesh = GenMesh(capi, loc, new Vec3f(0, 270, 0));
                mesh.Rgba2 = null;
                meshrefs[key] = meshref = capi.Render.UploadMesh(mesh);
            }

            renderinfo.ModelRef = meshref;
        }


        public override float GetTransitionRateMul(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
        {
            float mul = base.GetTransitionRateMul(world, inSlot, transType);

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

            return mul;
        }


        public MeshData GenMesh(ICoreClientAPI capi, AssetLocation labelLoc, Vec3f rot = null, ITesselatorAPI tesselator = null)
        {
            if (tesselator == null) tesselator = capi.Tesselator;

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
            if (blockSel.Position == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            Block block = api.World.BlockAccessor.GetBlock(blockSel.Position);

            if (block?.Attributes?["mealcontainer"]?.AsBool() == true)
            {
                ServeIntoBowl(block, blockSel.Position, slot, byEntity.World);
                handHandling = EnumHandHandling.PreventDefault;
                return;
            }

            if (block is BlockCookingContainer && slot.Itemstack.Attributes.HasAttribute("recipeCode"))
            {
                handHandling = EnumHandHandling.PreventDefault;
                ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                string recipeCode = slot.Itemstack.Attributes.GetString("recipeCode");
                int quantityServings = slot.Itemstack.Attributes.GetInt("quantityServings");

                int movedServings = (block as BlockCookingContainer).PutMeal(blockSel.Position, GetContents(api.World, slot.Itemstack), recipeCode, quantityServings);

                quantityServings -= movedServings;

                if (quantityServings > 0) {
                    slot.Itemstack.Attributes.SetInt("quantityServings", quantityServings);
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
                ItemStack stack = bebarrel.inventory[0].Itemstack;

                ItemStack[] ownContentStacks = GetNonEmptyContents(api.World, slot.Itemstack);
                if (ownContentStacks == null || ownContentStacks.Length == 0)
                {
                    if (stack != null && stack.Collectible.Attributes["crockable"].AsBool() == true)
                    {
                        SetContents(slot.Itemstack, new ItemStack[] { bebarrel.inventory[0].TakeOut(4) });
                        bebarrel.MarkDirty(true);
                    }
                } else
                {
                    if (stack != null)
                    {
                        new DummySlot(ownContentStacks[0]).TryPutInto(api.World, bebarrel.inventory[0], ownContentStacks[0].StackSize);
                        if (ownContentStacks[0].StackSize <= 0)
                        {
                            SetContents(slot.Itemstack, new ItemStack[0]);
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

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?["mealContainer"].AsBool() == true)
            {
                BlockEntityCrock bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCrock;
                if (bec == null) return false;

                if ((hotbarSlot.Itemstack.Collectible as IBlockMealContainer).GetQuantityServings(world, hotbarSlot.Itemstack) == 0)
                {
                    bec.ServeInto(byPlayer, hotbarSlot);
                    bec.Sealed = false;
                    return true;
                }

                if (bec.QuantityServings == 0)
                {
                    bec.OnBlockPlaced(hotbarSlot.Itemstack);
                    bec.Sealed = false;
                    bec.MarkDirty(true);
                    hotbarSlot.Itemstack.Attributes.RemoveAttribute("recipeCode");
                    hotbarSlot.Itemstack.Attributes.RemoveAttribute("quantityServings");
                    hotbarSlot.Itemstack.Attributes.RemoveAttribute("contents");
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
            ItemStack[] stacks = GetContents(world, inSlot.Itemstack);

            if (stacks == null || stacks.Length == 0)
            {
                dsc.AppendLine("Empty");
                return;
            }

            DummySlot firstContentItemSlot = new DummySlot(stacks != null && stacks.Length > 0 ? stacks[0] : null);

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
                dsc.AppendLine("Contents:");
                foreach (var stack in stacks)
                {
                    if (stack == null) continue;

                    dsc.AppendLine(stack.StackSize + "x  " + stack.GetName());
                }
            }


            firstContentItemSlot.Itemstack?.Collectible.AppendPerishableInfoText(firstContentItemSlot, dsc, world);

            if (inSlot.Itemstack.Attributes.GetBool("sealed"))
            {
                dsc.AppendLine(Lang.Get("Sealed."));
            }            
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockEntityCrock becrock = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrock;
            if (becrock == null) return null;

            BlockMeal mealblock = world.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;

            CookingRecipe recipe = world.CookingRecipes.FirstOrDefault((rec) => becrock.RecipeCode == rec.Code);
            ItemStack[] stacks = becrock.inventory.Where(slot => !slot.Empty).Select(slot => slot.Itemstack).ToArray();

            if (stacks == null || stacks.Length == 0)
            {
                return "Empty";
            }

            StringBuilder dsc = new StringBuilder();

            if (recipe != null)
            {
                DummySlot firstContentItemSlot = new DummySlot(stacks != null && stacks.Length > 0 ? stacks[0] : null, becrock.inventory);

                if (recipe != null)
                {
                    dsc.AppendLine(recipe.GetOutputName(world, stacks).UcFirst());
                }

                string facts = mealblock.GetContentNutritionFacts(world, new DummySlot(OnPickBlock(world, pos)), null);

                if (facts != null)
                {
                    dsc.Append(facts);
                }

                firstContentItemSlot.Itemstack?.Collectible.AppendPerishableInfoText(firstContentItemSlot, dsc, world);
            }
            else
            {
                dsc.AppendLine("Contents:");
                foreach (var stack in stacks)
                {
                    if (stack == null) continue;

                    dsc.AppendLine(stack.StackSize + "x  " + stack.GetName());
                }

                becrock.inventory[0].Itemstack.Collectible.AppendPerishableInfoText(becrock.inventory[0], dsc, api.World);
            }
            

            return dsc.ToString();
        }



        public void SetContents(string recipeCode, ItemStack containerStack, ItemStack[] stacks, float quantityServings = 1)
        {
            base.SetContents(containerStack, stacks);

            containerStack.Attributes.SetString("recipeCode", recipeCode);
            containerStack.Attributes.SetFloat("quantityServings", quantityServings);
        }




        public void ServeIntoBowl(Block selectedBlock, BlockPos pos, ItemSlot potslot, IWorldAccessor world)
        {
            if (world.Side == EnumAppSide.Client) return;

            string code = selectedBlock.Attributes["mealBlockCode"].AsString();
            Block mealblock = api.World.GetBlock(new AssetLocation(code));

            world.BlockAccessor.SetBlock(mealblock.BlockId, pos);

            IBlockEntityMealContainer bemeal = api.World.BlockAccessor.GetBlockEntity(pos) as IBlockEntityMealContainer;
            if (bemeal == null || bemeal.QuantityServings > 0) return;

            bemeal.RecipeCode = GetRecipeCode(world, potslot.Itemstack);

            ItemStack[] stacks = GetContents(api.World, potslot.Itemstack);
            for (int i = 0; i < stacks.Length; i++)
            {
                bemeal.inventory[i].Itemstack = stacks[i].Clone();
            }



            float quantityServings = GetQuantityServings(world, potslot.Itemstack);
            float servingsToTransfer = Math.Min(quantityServings, selectedBlock.Attributes["servingCapacity"].AsInt(1));

            bemeal.QuantityServings = servingsToTransfer;


            SetQuantityServings(world, potslot.Itemstack, quantityServings - servingsToTransfer);

            if (quantityServings <= 0)
            {
                Block emptyPotBlock = world.GetBlock(new AssetLocation(Attributes["emptiedBlockCode"].AsString()));
                potslot.Itemstack = new ItemStack(emptyPotBlock);
            }

            potslot.MarkDirty();
            bemeal.MarkDirty(true);
        }

        public float GetQuantityServings(IWorldAccessor world, ItemStack byItemStack)
        {
            return (float)byItemStack.Attributes.GetDecimal("quantityServings");
        }

        public void SetQuantityServings(IWorldAccessor world, ItemStack byItemStack, float value)
        {
            byItemStack.Attributes.SetFloat("quantityServings", value);
        }


        public string GetRecipeCode(IWorldAccessor world, ItemStack containerStack)
        {
            return containerStack.Attributes.GetString("recipeCode");
        }

        public CookingRecipe GetCookingRecipe(IWorldAccessor world, ItemStack containerStack)
        {
            string recipecode = GetRecipeCode(world, containerStack);
            return world.CookingRecipes.FirstOrDefault((rec) => recipecode == rec.Code);
        }

    }
}
