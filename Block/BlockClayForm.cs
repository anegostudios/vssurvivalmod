using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockClayForm : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "clayformBlockInteractions", () =>
            {
                List<ItemStack> clayStackList = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is ItemClay)
                    {
                        clayStackList.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-clayform-addclay",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = clayStackList.ToArray(),
                        GetMatchingStacks = getMatchingStacks
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-clayform-removeclay",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = clayStackList.ToArray(),
                        GetMatchingStacks = getMatchingStacks
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-selecttoolmode",
                        HotKeyCode = "toolmodeselect",
                        MouseButton = EnumMouseButton.None,
                        Itemstacks = clayStackList.ToArray(),
                        GetMatchingStacks = getMatchingStacks
                    }
                };
            });
        }

        private ItemStack[] getMatchingStacks(WorldInteraction wi, BlockSelection bs, EntitySelection es)
        {
            BlockEntityClayForm bec = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityClayForm;
            List<ItemStack> stacks = new List<ItemStack>();

            foreach (var val in wi.Itemstacks)
            {
                if (bec?.BaseMaterial != null && bec.BaseMaterial.Collectible.LastCodePart() == val.Collectible.LastCodePart())
                {
                    stacks.Add(val);
                }
            }
            return stacks.ToArray();
        }

        Cuboidf box = new Cuboidf(0, 0, 0, 1, 1 / 16f, 1);

        public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
        {
            return box;
        }


        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityClayForm bea = blockAccessor.GetBlockEntity(pos) as BlockEntityClayForm;
            if (bea != null)
            {
                Cuboidf[] selectionBoxes = bea.GetSelectionBoxes(blockAccessor, pos);
                
                return selectionBoxes;
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[0];
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            return new ItemStack[0];
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!world.BlockAccessor.GetBlock(pos.DownCopy()).SideSolid[BlockFacing.UP.Index])
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }


}
