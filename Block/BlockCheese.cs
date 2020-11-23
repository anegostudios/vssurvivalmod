using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockCheese : Block
    {

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }

        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos)
        {
            base.OnDecalTesselation(world, decalMesh, pos);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BECheese bec = world.BlockAccessor.GetBlockEntity(pos) as BECheese;
            if (bec != null) return bec.Inventory[0].Itemstack;

            return base.OnPickBlock(world, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.Tool == EnumTool.Knife)
            {
                BECheese bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BECheese;

                if (bec.Inventory[0].Itemstack?.Collectible.Variant["type"] == "waxedcheddar")
                {
                    bec.Inventory[0].Itemstack = new ItemStack(api.World.GetItem(bec.Inventory[0].Itemstack?.Collectible.CodeWithVariant("type", "cheddar")));
                    bec.Inventory[0].MarkDirty();
                    bec.MarkDirty(true);
                    return true;
                }

                ItemStack stack = bec?.TakeSlice();
                if (stack != null)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        world.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }

                return true;
            } else
            {
                BECheese bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BECheese;
                var stack = bec.Inventory[0].Itemstack;
                if (stack != null)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        world.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }

                world.BlockAccessor.SetBlock(0, blockSel.Position);
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
