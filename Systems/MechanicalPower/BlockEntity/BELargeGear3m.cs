using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BELargeGear3m : BlockEntity, IGearAcceptor
    {
        public BlockPos[] gear = new BlockPos[4];

        bool IGearAcceptor.CanAcceptGear(BlockPos pos)
        {
            if (pos.Y != Pos.Y) return false;
            int dx = Pos.X - pos.X;
            int dz = Pos.Z - pos.Z;
            if (dx != 0 && dz != 0) return false;  //one of dx and dz must be 0
            if (HasGearAt(pos)) return false;
            return dx + dz == 1 || dx + dz == -1;  //this should always be true if replacing a multiblock fake block with this gear as centre, but check just in case
        }

        private bool HasGearAt(BlockPos pos)
        {
            return pos.Equals(gear[0]) || pos.Equals(gear[1]) || pos.Equals(gear[2]) || pos.Equals(gear[3]);
        }

        public void AddGear(BlockPos pos)
        {
            for (int i = 0; i < 4; i++)
            {
                if (gear[i] == null)
                {
                    gear[i] = pos;
                    return;
                }
            }
        }

        public void RemoveGearAt(BlockPos pos)
        {
            for (int i = 0; i < 4; i++)
            {
                if (pos.Equals(gear[i]))
                {
                    gear[i] = null;
                    return;
                }
            }
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);
            int gx = tree.GetInt("gx0");
            int gy = tree.GetInt("gy0");
            int gz = tree.GetInt("gz0");
            gear[0] = gx == 0 && gy == 0 && gz == 0 ? null : new BlockPos(gx, gy, gz);
            gx = tree.GetInt("gx1");
            gy = tree.GetInt("gy1");
            gz = tree.GetInt("gz1");
            gear[1] = gx == 0 && gy == 0 && gz == 0 ? null : new BlockPos(gx, gy, gz);
            gx = tree.GetInt("gx2");
            gy = tree.GetInt("gy2");
            gz = tree.GetInt("gz2");
            gear[2] = gx == 0 && gy == 0 && gz == 0 ? null : new BlockPos(gx, gy, gz);
            gx = tree.GetInt("gx3");
            gy = tree.GetInt("gy3");
            gz = tree.GetInt("gz3");
            gear[3] = gx == 0 && gy == 0 && gz == 0 ? null : new BlockPos(gx, gy, gz);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("gx0", gear[0] == null ? 0 : gear[0].X);
            tree.SetInt("gy0", gear[0] == null ? 0 : gear[0].Y);
            tree.SetInt("gz0", gear[0] == null ? 0 : gear[0].Z);
            tree.SetInt("gx1", gear[1] == null ? 0 : gear[1].X);
            tree.SetInt("gy1", gear[1] == null ? 0 : gear[1].Y);
            tree.SetInt("gz1", gear[1] == null ? 0 : gear[1].Z);
            tree.SetInt("gx2", gear[2] == null ? 0 : gear[2].X);
            tree.SetInt("gy2", gear[2] == null ? 0 : gear[2].Y);
            tree.SetInt("gz2", gear[2] == null ? 0 : gear[2].Z);
            tree.SetInt("gx3", gear[3] == null ? 0 : gear[3].X);
            tree.SetInt("gy3", gear[3] == null ? 0 : gear[3].Y);
            tree.SetInt("gz3", gear[3] == null ? 0 : gear[3].Z);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
        }
    }
}
