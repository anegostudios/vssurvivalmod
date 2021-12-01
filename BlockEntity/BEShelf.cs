using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityShelf : BlockEntityDisplay
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "shelf";

        Block block;
        Matrixf mat = new Matrixf();


        public BlockEntityShelf()
        {
            inv = new InventoryGeneric(8, "shelf-0", null, null);
            meshes = new MeshData[8];
        }

        public override void Initialize(ICoreAPI api)
        {
            block = api.World.BlockAccessor.GetBlock(Pos);
            mat.RotateYDeg(block.Shape.rotateY);

            base.Initialize(api);
        }

        protected override float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            if (transType == EnumTransitionType.Dry) return room?.ExitCount == 0 ? 2f : 0.5f;
            if (Api == null) return 0;

            if (transType == EnumTransitionType.Perish || transType == EnumTransitionType.Ripen)
            {
                float perishRate = GetPerishRate();
                if (transType == EnumTransitionType.Ripen)
                {
                    return GameMath.Clamp(((1 - perishRate) - 0.5f) * 3, 0, 1);
                }

                return baseMul * perishRate;
            }

            return 1;

        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (slot.Empty) {
                if (TryTake(byPlayer, blockSel))
                {
                    return true;
                }
                return false;
            } else
            {
                CollectibleObject colObj = slot.Itemstack.Collectible;
                if (colObj.Attributes != null && colObj.Attributes["shelvable"].AsBool(false) == true)
                {
                    AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
                    
                    if (TryPut(slot, blockSel))
                    {
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                        updateMeshes();
                        return true;
                    }

                    return false;
                }
            }


            return false;
        }



        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            bool up = blockSel.SelectionBoxIndex > 1;
            bool left = (blockSel.SelectionBoxIndex % 2) == 0;

            int start = (up ? 4 : 0) + (left ? 0 : 2);
            int end = start + 2;

            for (int i = start; i < end; i++)
            {
                if (inv[i].Empty)
                {
                    int moved = slot.TryPutInto(Api.World, inv[i]);
                    updateMeshes();
                    MarkDirty(true);
                    return moved > 0;
                }
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            bool up = blockSel.SelectionBoxIndex > 1;
            bool left = (blockSel.SelectionBoxIndex % 2) == 0;

            int start = (up ? 4 : 0) + (left ? 0 : 2);
            int end = start + 2;

            for (int i = end - 1; i >= start; i--)
            {
                if (!inv[i].Empty)
                {
                    ItemStack stack = inv[i].TakeOut(1);
                    if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                    {
                        AssetLocation sound = stack.Block?.Sounds?.Place;
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                    }

                    if (stack.StackSize > 0)
                    {
                        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                    MarkDirty(true);
                    updateMeshes();
                    return true;
                }
            }

            return false;
        }



        public override void TranslateMesh(MeshData mesh, int index)
        {
            float x = ((index % 4) >= 2) ? 12 / 16f : 4 / 16f;
            float y = index >= 4 ? 10 / 16f : 2 / 16f;
            float z = (index % 2 == 0) ? 4 / 16f : 10 / 16f;

            Vec4f offset = mat.TransformVector(new Vec4f(x - 0.5f, y, z - 0.5f, 0));
            mesh.Translate(offset.XYZ);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);


            float ripenRate = GameMath.Clamp(((1 - GetPerishRate()) - 0.5f) * 3, 0, 1);
            if (ripenRate > 0)
            {
                sb.Append(Lang.Get("Suitable spot for food ripening."));
            }


            sb.AppendLine();

            bool up = forPlayer.CurrentBlockSelection != null && forPlayer.CurrentBlockSelection.SelectionBoxIndex > 1;

            for (int j = 3; j >= 0; j--)
            {
                int i = j + (up ? 4 : 0);
                i ^= 2;   //Display shelf contents text for items from left-to-right, not right-to-left

                if (inv[i].Empty) continue;

                ItemStack stack = inv[i].Itemstack;

                if (stack.Collectible is BlockCrock)
                {
                    sb.Append(CrockInfoCompact(inv[i]));
                } else
                {
                    if (stack.Collectible.TransitionableProps != null && stack.Collectible.TransitionableProps.Length > 0)
                    {
                        sb.Append(PerishableInfoCompact(Api, inv[i], ripenRate));
                    }
                    else
                    {
                        sb.AppendLine(stack.GetName());
                    }
                }
            }
        }

        public static string PerishableInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
        {
            if (contentSlot.Empty) return "";

            StringBuilder dsc = new StringBuilder();

            if (withStackName)
            {
                dsc.Append(contentSlot.Itemstack.GetName());
            }

            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);

            bool nowSpoiling = false;

            if (transitionStates != null)
            {
                bool appendLine = false;
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                nowSpoiling = true;
                                dsc.Append(", " + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;

                        case EnumTransitionType.Ripen:
                            if (nowSpoiling) break;

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                dsc.Append(", " + Lang.Get("{1:0.#} days left to ripen ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / ripenRate));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                    }
                }


                if (appendLine) dsc.AppendLine();
            }

            return dsc.ToString();
        }

        public string CrockInfoCompact(ItemSlot inSlot)
        {
            BlockMeal mealblock = Api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;
            BlockCrock crock = inSlot.Itemstack.Collectible as BlockCrock;
            IWorldAccessor world = Api.World;

            CookingRecipe recipe = crock.GetCookingRecipe(world, inSlot.Itemstack);
            ItemStack[] stacks = crock.GetNonEmptyContents(world, inSlot.Itemstack);

            if (stacks == null || stacks.Length == 0)
            {
                return Lang.Get("Empty Crock") + "\n";
            }

            StringBuilder dsc = new StringBuilder();

            if (recipe != null)
            {
                double servings = inSlot.Itemstack.Attributes.GetDecimal("quantityServings");

                if (recipe != null)
                {
                    if (servings == 1)
                    {
                        dsc.Append(Lang.Get("{0}x {1}.", servings, recipe.GetOutputName(world, stacks)));
                    }
                    else
                    {
                        dsc.Append(Lang.Get("{0}x {1}.", servings, recipe.GetOutputName(world, stacks)));
                    }
                }
            }
            else
            {
                int i = 0;
                foreach (var stack in stacks)
                {
                    if (stack == null) continue;
                    if (i++ > 0) dsc.Append(", ");
                    dsc.Append(stack.StackSize + "x " + stack.GetName());
                }

                dsc.Append(".");
            }

            DummyInventory dummyInv = new DummyInventory(Api);

            ItemSlot contentSlot = BlockCrock.GetDummySlotForFirstPerishableStack(Api.World, stacks, null, dummyInv);
            dummyInv.OnAcquireTransitionSpeed = (transType, stack, mul) =>
            {
                return mul * crock.GetContainingTransitionModifierContained(world, inSlot, transType) * inv.GetTransitionSpeedMul(transType, stack);
            };

            
            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);
            bool addNewLine = true;
            if (transitionStates != null)
            {
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(world, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    addNewLine = false;
                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:
                            if (transitionLevel > 0)
                            {
                                dsc.AppendLine(" " + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.AppendLine(" " + Lang.Get("Fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                /*else if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerMonth)  - confusing. 12 days per months and stuff..
                                {
                                    dsc.AppendLine(Lang.Get("<font color=\"orange\">Perishable.</font> Fresh for {0} months", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerMonth, 1)));
                                }*/
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.AppendLine(" " + Lang.Get("Fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.AppendLine(" " + Lang.Get("Fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                    }
                }
            }
            
            
            if (addNewLine)
            {
                dsc.AppendLine("");
            }

            return dsc.ToString();
        }

    }
}
