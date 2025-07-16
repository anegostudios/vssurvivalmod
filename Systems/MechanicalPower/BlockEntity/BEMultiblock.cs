using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEMPMultiblock: BlockEntity
    {
        public BlockPos Principal { get; set; }

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
            // (-1, -1, -1) signifies a null center; this cannot happen spontaneously
            if (cy == -1 && cx == -1 && cz == -1)
            {
                Principal = null;
            }
            else
            {
                Principal = new BlockPos(cx, cy, cz);
                if (world.BlockAccessor.GetBlockEntity(Principal) is IGearAcceptor beg) beg.RemoveGearAt(this.Pos);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            // (-1, -1, -1) signifies a null center; this cannot happen spontaneously
            tree.SetInt("cx", Principal == null ? -1 : Principal.X);
            tree.SetInt("cy", Principal == null ? -1 : Principal.Y);
            tree.SetInt("cz", Principal == null ? -1 : Principal.Z);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (Api.World.EntityDebugMode)
            {
                if (Principal == null)
                {
                    sb.AppendLine("null center");
                    return;
                }
                sb.AppendLine("center at " + Principal);
            }

            if (Principal == null) return;

            BlockEntity be = this.Api.World?.BlockAccessor.GetBlockEntity(Principal);
            if (be == null) sb.AppendLine("null be");
            be?.GetBlockInfo(forPlayer, sb);
        }


    }
}
