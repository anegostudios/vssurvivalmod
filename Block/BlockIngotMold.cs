
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockIngotMold : Block
    {
        WorldInteraction[] interactionsLeft;
        WorldInteraction[] interactionsRight;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            if (LastCodePart() == "raw") return;

            interactionsLeft = ObjectCacheUtil.GetOrCreate(api, "ingotmoldBlockInteractionsLeft", () =>
            {
                List<ItemStack> smeltedContainerStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is BlockSmeltedContainer)
                    {
                        smeltedContainerStacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-pour",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = smeltedContainerStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return (betm != null && !betm.IsFullLeft) ? wi.Itemstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-takeingot",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return betm != null && betm.IsFullLeft && betm.IsHardenedLeft;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-pickup",
                        HotKeyCode = null,
                        RequireFreeHand = true,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return betm != null && betm.contentsRight == null && betm.contentsLeft == null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-placemold",
                        HotKeyCode = "sneak",
                        Itemstacks = new ItemStack[] { new ItemStack(this) },
                        MouseButton = EnumMouseButton.Right,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return (betm != null && betm.quantityMolds < 2) ? wi.Itemstacks : null;
                        }
                    }
                };
            });



            interactionsRight = ObjectCacheUtil.GetOrCreate(api, "ingotmoldBlockInteractionsRight", () =>
            {
                List<ItemStack> smeltedContainerStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is BlockSmeltedContainer)
                    {
                        smeltedContainerStacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-pour",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = smeltedContainerStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return (betm != null && betm.quantityMolds > 1 && !betm.IsFullRight) ? wi.Itemstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-takeingot",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return betm != null && betm.quantityMolds > 1 && betm.IsFullRight && betm.IsHardenedRight;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-ingotmold-pickup",
                        HotKeyCode = null,
                        RequireFreeHand = true,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityIngotMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityIngotMold;
                            return betm != null && betm.quantityMolds > 1 && betm.contentsRight == null && betm.contentsLeft == null;
                        }
                    }
                };
            });


        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        Cuboidf[] oneMoldBoxes = new Cuboidf[] { new Cuboidf(0, 0, 0, 1, 0.1875f, 1)  };
        Cuboidf[] twoMoldBoxes = new Cuboidf[] { new Cuboidf(0, 0, 0, 0.5f, 0.1875f, 1), new Cuboidf(0.5f, 0, 0, 1, 0.1875f, 1) };

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            BlockEntityIngotMold betm = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityIngotMold;

            if (betm == null || betm.quantityMolds == 1)
            {
                return oneMoldBoxes;
            }

            return twoMoldBoxes;
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face.GetOpposite()));

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            if (byPlayer != null && be is BlockEntityIngotMold)
            {
                BlockEntityIngotMold beim = (BlockEntityIngotMold)be;
                if (beim.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition))
                {
                    handling = EnumHandHandling.PreventDefault;
                }
                
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return false;

            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);

            if (be is BlockEntityIngotMold)
            {
                BlockEntityIngotMold beim = (BlockEntityIngotMold)be;
                return beim.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition);
            }

            return false;
        }

        
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                failureCode = "claimed";
                return false;
            }

            if (!byPlayer.Entity.Controls.Sneak)
            {
                failureCode = "onlywhensneaking";
                return false;
            }

            if (IsSuitablePosition(world, blockSel.Position, ref failureCode) && world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()).SideSolid[BlockFacing.UP.Index])
            {
                DoPlaceBlock(world, blockSel.Position, blockSel.Face, itemstack);
                return true;
            }

            return false;
        }

        public override BlockDropItemStack[] GetDropsForHandbook(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
        {
            return Drops;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            List<ItemStack> stacks = new List<ItemStack>();

            BlockEntityIngotMold bei = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityIngotMold;
            if (bei != null)
            {
                stacks.Add(new ItemStack(this, bei.quantityMolds));

                ItemStack stackl = bei.GetLeftContents();
                if (stackl != null)
                {
                    stacks.Add(stackl);
                }
                ItemStack stackr = bei.GetRightContents();
                if (stackr != null)
                {
                    stacks.Add(stackr);
                }
            } else
            {
                stacks.Add(new ItemStack(this, 1));
            }

            return stacks.ToArray();
        }
        

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return (selection.SelectionBoxIndex == 0 ? interactionsLeft : interactionsRight).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

    }
}
