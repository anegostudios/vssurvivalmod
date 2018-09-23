using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemShears : Item
    {
        public virtual int MultiBreakQuantity { get { return 5; } }

        public virtual bool CanMultiBreak(Block block)
        {
            return block.BlockMaterial == EnumBlockMaterial.Leaves;
        }
        

        public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, IItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            float newResist = base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
            int leftDurability = itemslot.Itemstack.Attributes.GetInt("durability", Durability);
            DamageNearbyBlocks(player, blockSel, remainingResistance - newResist, leftDurability);

            return newResist;
        }


        private void DamageNearbyBlocks(IPlayer player, BlockSelection blockSel, float damage, int leftDurability)
        {
            Block block = player.Entity.World.BlockAccessor.GetBlock(blockSel.Position);

            if (!CanMultiBreak(block)) return;

            Vec3d hitPos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
            var orderedPositions = GetNearblyMultibreakables(player.Entity.World, blockSel.Position, hitPos).OrderBy(x => x.Value);

            int q = Math.Min(MultiBreakQuantity, leftDurability);
            foreach (var val in orderedPositions)
            {
                if (q == 0) break;
                BlockFacing facing = BlockFacing.FromVector(player.Entity.ServerPos.GetViewVector()).GetOpposite();

                if (!player.Entity.World.CanPlayerAccessBlock(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) continue;
                
                player.Entity.World.BlockAccessor.DamageBlock(val.Key, facing, damage);
                q--;
            }
        }

        



        public override bool OnBlockBrokenWith(IWorldAccessor world, IEntity byEntity, IItemSlot itemslot, BlockSelection blockSel)
        {
            Block block = world.BlockAccessor.GetBlock(blockSel.Position);

            base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel);            

            if (byEntity as IEntityPlayer == null || itemslot.Itemstack == null) return true;

            IPlayer plr = world.PlayerByUid((byEntity as IEntityPlayer).PlayerUID);

            if (!CanMultiBreak(block)) return true;

            Vec3d hitPos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
            var orderedPositions = GetNearblyMultibreakables(world, blockSel.Position, hitPos).OrderBy(x => x.Value);

            int leftDurability = itemslot.Itemstack.Attributes.GetInt("durability", Durability);
            int q = 0;

            
            foreach (var val in orderedPositions)
            {
                world.BlockAccessor.BreakBlock(val.Key, plr);
                world.BlockAccessor.MarkBlockDirty(val.Key);
                DamageItem(world, byEntity, itemslot);
                
                q++;
                

                if (q >= MultiBreakQuantity || itemslot.Itemstack == null) break;
            }
            
            return true;
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
