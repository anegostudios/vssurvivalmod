using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEMPMultiblock: BlockEntity
    {
        public BlockPos Centre { get; set; }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
        {
            base.FromTreeAttributes(tree, world);
            int cx = tree.GetInt("cx");
            int cy = tree.GetInt("cy");
            int cz = tree.GetInt("cz");
            // (-1, -1, -1) signifies a null centre; this cannot happen spontaneously
            if (cy == -1 && cx == -1 && cz == -1)
            {
                Centre = null;
            }
            else
            {
                Centre = new BlockPos(cx, cy, cz);
                if (world.BlockAccessor.GetBlockEntity(Centre) is IGearAcceptor beg) beg.RemoveGearAt(this.Pos);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            // (-1, -1, -1) signifies a null centre; this cannot happen spontaneously
            tree.SetInt("cx", Centre == null ? -1 : Centre.X);
            tree.SetInt("cy", Centre == null ? -1 : Centre.Y);
            tree.SetInt("cz", Centre == null ? -1 : Centre.Z);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (Api.World.EntityDebugMode)
            {
                if (Centre == null)
                {
                    sb.AppendLine("null centre");
                    return;
                }
                sb.AppendLine("centre at " + Centre);
            }

            if (Centre == null) return;

            BlockEntity be = this.Api.World?.BlockAccessor.GetBlockEntity(Centre);
            if (be == null) sb.AppendLine("null be");
            be?.GetBlockInfo(forPlayer, sb);
        }


    }
}
