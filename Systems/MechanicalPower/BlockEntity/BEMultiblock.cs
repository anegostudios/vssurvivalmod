using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEMPMultiblock : BlockEntity, IRotatable
    {
        const string principalOffsetKey = "principalOffset";

        public BlockPos Principal { get; set; }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
        {
            base.FromTreeAttributes(tree, world);
            int cx = tree.GetInt("cx");
            int cy = tree.GetInt("cy");
            int cz = tree.GetInt("cz");
            // (-1, -1, -1) signifies a null center; this cannot happen spontaneously
            if (cx == -1 && cy == -1 && cz == -1)
            {
                Principal = null;
            }
            else
            {
                var principalOffset = tree.GetVec3i(principalOffsetKey);
                Principal = principalOffset != null
                    ? Pos.AddCopy(principalOffset)
                    : new(cx, cy, cz, Pos.dimension);
                if (world.BlockAccessor.GetBlockEntity(Principal) is IGearAcceptor gearAcceptor) gearAcceptor.RemoveGearAt(Pos);
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

        // The schematic can be pasted elsewhere, so transform the link as a relative offset.
        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            int cx = tree.GetInt("cx");
            int cy = tree.GetInt("cy");
            int cz = tree.GetInt("cz");
            if (cx == -1 && cy == -1 && cz == -1) return;

            Vec3i offset = tree.GetVec3i(principalOffsetKey) ?? new(
                cx - tree.GetInt("posx"),
                cy - tree.GetInt("posy") % BlockPos.DimensionBoundary,
                cz - tree.GetInt("posz")
            );

            if (flipAxis == EnumAxis.X) offset.X = -offset.X;
            if (flipAxis == EnumAxis.Y) offset.Y = -offset.Y;
            if (flipAxis == EnumAxis.Z) offset.Z = -offset.Z;

            (offset.X, offset.Z) = degreeRotation switch
            {
                90 => (-offset.Z, offset.X),
                180 => (-offset.X, -offset.Z),
                270 => (offset.Z, -offset.X),
                _ => (offset.X, offset.Z)
            };

            tree.SetVec3i(principalOffsetKey, offset);
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
