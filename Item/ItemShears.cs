﻿using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemShears : Item
    {
        public virtual int MultiBreakQuantity { get { return 5; } }

        public virtual bool CanMultiBreak(Block block)
        {
            return block.BlockMaterial == EnumBlockMaterial.Leaves;
        }
        

        public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            float newResist = base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
            int leftDurability = itemslot.Itemstack.Collectible.GetRemainingDurability(itemslot.Itemstack);
            DamageNearbyBlocks(player, blockSel, remainingResistance - newResist, leftDurability);

            return newResist;
        }


        private void DamageNearbyBlocks(IPlayer player, BlockSelection blockSel, float damage, int leftDurability)
        {
            Block block = player.Entity.World.BlockAccessor.GetBlock(blockSel.Position);

            if (!CanMultiBreak(block)) return;

            Vec3d hitPos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
            OrderedDictionary<BlockPos, float> dict = GetNearblyMultibreakables(player.Entity.World, blockSel.Position, hitPos);
            var orderedPositions = dict.OrderBy(x => x.Value).Select(x => x.Key);

            int q = Math.Min(MultiBreakQuantity, leftDurability);
            foreach (var pos in orderedPositions)
            {
                if (q == 0) break;
                BlockFacing facing = BlockFacing.FromNormal(player.Entity.ServerPos.GetViewVector()).Opposite;

                if (!player.Entity.World.Claims.TryAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak)) continue;
                
                player.Entity.World.BlockAccessor.DamageBlock(pos, facing, damage);
                q--;
            }
        }

        



        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            Block block = world.BlockAccessor.GetBlock(blockSel.Position);

            if (byEntity as EntityPlayer == null || itemslot.Itemstack == null) return true;

            IPlayer plr = world.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

            //base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
            breakMultiBlock(blockSel.Position, plr);

            if (!CanMultiBreak(block)) return true;

            Vec3d hitPos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
            var orderedPositions = GetNearblyMultibreakables(world, blockSel.Position, hitPos).OrderBy(x => x.Value);

            int leftDurability = itemslot.Itemstack.Collectible.GetRemainingDurability(itemslot.Itemstack);
            int q = 0;

            
            foreach (var val in orderedPositions)
            {
                if (!plr.Entity.World.Claims.TryAccess(plr, val.Key, EnumBlockAccessFlags.BuildOrBreak)) continue;

                breakMultiBlock(val.Key, plr);

                DamageItem(world, byEntity, itemslot);
                
                q++;
                if (q >= MultiBreakQuantity || itemslot.Itemstack == null) break;
            }

            return true;
        }

        protected virtual void breakMultiBlock(BlockPos pos, IPlayer plr)
        {
            api.World.BlockAccessor.BreakBlock(pos, plr);
            api.World.BlockAccessor.MarkBlockDirty(pos);
        }


        OrderedDictionary<BlockPos, float> GetNearblyMultibreakables(IWorldAccessor world, BlockPos pos, Vec3d hitPos)
        {
            OrderedDictionary<BlockPos, float> positions = new OrderedDictionary<BlockPos, float>();
            
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;

                        BlockPos dpos = pos.AddCopy(dx, dy, dz);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos, hitPos.SquareDistanceTo(dpos.X + 0.5, dpos.Y + 0.5, dpos.Z + 0.5));
                        }
                    }
                }
            }

            return positions;
        }



    }
}
