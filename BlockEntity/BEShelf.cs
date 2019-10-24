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

namespace Vintagestory.GameContent
{
    public class BlockEntityShelf : BlockEntityContainer, IBlockShapeSupplier
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "shelf";

        Block block;

        public BlockEntityShelf()
        {
            inv = new InventoryGeneric(8, "shelf-0", null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            block = Api.World.BlockAccessor.GetBlock(Pos);
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (slot.Empty) {
                if (TryTake(byPlayer, blockSel))
                {
                    AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
                    if (sound != null) Api.World.PlaySoundAt(sound, byPlayer.Entity, byPlayer, true, 16);
                    byPlayer.InventoryManager.BroadcastHotbarSlot();
                    return true;
                }
                return false;
            } else
            {
                if (slot.Itemstack.Collectible is BlockCrock)
                {
                    AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
                    
                    if (TryPut(slot, blockSel))
                    {
                        if (sound != null) Api.World.PlaySoundAt(sound, byPlayer.Entity, byPlayer, true, 16);
                        byPlayer.InventoryManager.BroadcastHotbarSlot();
                        return true;
                    }

                    return false;
                }
            }


            return false;
        }



        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            bool up = blockSel.SelectionBoxIndex > 0;

            for (int i = up ? 4 : 0; i < (up ? 8 : 4); i++)
            {
                if (inv[i].Empty)
                {
                    slot.TryPutInto(Api.World, inv[i]);
                    MarkDirty(true);
                    return true;
                }
            }

            return true;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            bool up = blockSel.SelectionBoxIndex > 0;

            for (int i = up ? 7 : 3; i >= (up ? 4 : 0); i--)
            {
                if (!inv[i].Empty)
                {
                    inv[i].TryPutInto(Api.World, byPlayer.InventoryManager.ActiveHotbarSlot);
                    MarkDirty(true);
                    return true;
                }
            }

            return false;
        }



        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;

            Matrixf mat = new Matrixf();
            mat.RotateYDeg(block.Shape.rotateY);

            for (int i = 0; i < 8; i++)
            {
                if (inv[i].Empty) continue;

                ItemStack stack = inv[i].Itemstack;
                BlockCrock crockblock = stack.Collectible as BlockCrock;
                Vec3f rot = new Vec3f(0, block.Shape.rotateY, 0);

                MeshData mesh = BlockEntityCrock.GetMesh(tessThreadTesselator, Api, crockblock, crockblock.GetContents(Api.World, stack), crockblock.GetRecipeCode(Api.World, stack), rot).Clone();

                float y = i >= 4 ? 10 / 16f : 2 / 16f;
                float x = (i % 2 == 0) ? 4 / 16f : 12 / 16f;
                float z = ((i % 4) >= 2) ? 10 / 16f : 4 / 16f;

                Vec4f offset = mat.TransformVector(new Vec4f(x - 0.5f, y, z - 0.5f, 0));
                mesh.Translate(offset.XYZ);
                mesher.AddMeshData(mesh);
            }

            return false;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);

            sb.AppendLine();

            foreach (var slot in inv)
            {
                if (slot.Empty) continue;

                ItemStack stack = slot.Itemstack;

                sb.Append("- ");
                sb.Append(CrockInfoCompact(slot));
            }
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
                        dsc.Append(Lang.Get("{0} serving of {1}.", servings, recipe.GetOutputName(world, stacks)));
                    }
                    else
                    {
                        dsc.Append(Lang.Get("{0} servings of {1}.", servings, recipe.GetOutputName(world, stacks)));
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
            }

            DummyInventory dummyInv = new DummyInventory(Api);

            ItemSlot contentSlot = BlockCrock.GetDummySlotForFirstPerishableStack(Api.World, stacks, null, dummyInv);
            dummyInv.OnAcquireTransitionSpeed = (transType, stack, mul) =>
            {
                return mul * crock.GetContainingTransitionModifierContained(world, inSlot, transType) * inv.GetTransitionSpeedMul(transType, stack);
            };

            float spoilState = 0;
            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);
            if (transitionStates != null)
            {
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(world, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:
                            spoilState = transitionLevel;

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
            } else
            {
                dsc.AppendLine("");
            }

            return dsc.ToString();
        }

    }
}
