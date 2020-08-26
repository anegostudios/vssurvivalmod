using System;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BELargeGear3m : BlockEntity, IGearAcceptor
    {
        public BlockPos[] gear = null;  //if this is null it signifies this is not yet initialised - if this leads to a crash, we need to add code to check this is initialised (see the HasGearAt(api, pos) overload for an example)

        public override void Initialize(ICoreAPI api)
        {
            this.gear = new BlockPos[4];
            IBlockAccessor accessor = api.World.BlockAccessor;
            TestGear(accessor, this.Pos.NorthCopy());
            TestGear(accessor, this.Pos.SouthCopy());
            TestGear(accessor, this.Pos.WestCopy());
            TestGear(accessor, this.Pos.EastCopy());
            base.Initialize(api);
        }

        private void TestGear(IBlockAccessor accessor, BlockPos pos)
        {
            if (accessor.GetBlock(pos) is BlockAngledGears) AddGear(pos);
        }

        bool IGearAcceptor.CanAcceptGear(BlockPos pos)
        {
            if (pos.Y != Pos.Y) return false;
            int dx = Pos.X - pos.X;
            int dz = Pos.Z - pos.Z;
            if (dx != 0 && dz != 0) return false;  //one of dx and dz must be 0
            if (HasGearAt(pos)) return false;
            return dx + dz == 1 || dx + dz == -1;  //this should always be true if replacing a multiblock fake block with this gear as centre, but check just in case
        }

        public bool HasGears()
        {
            for (int i = 0; i < 4; i++)
            {
                if (gear[i] != null) return true;
            }
            return false;
        }

        public int CountGears()
        {
            int result = 0;
            for (int i = 0; i < 4; i++)
            {
                if (gear[i] != null) result++;
            }
            return result;
        }

        /// <summary>
        /// This overload adds an api parameter - it is called from BlockLargeGear3m.HasMechPowerConnectorAt() which may be called before this BlockEntity has been initialised
        /// </summary>
        /// <param name="api"></param>
        /// <returns></returns>
        public int CountGears(ICoreAPI api)
        {
            if (gear == null) this.Initialize(api);
            return CountGears();
        }

        public bool HasGearAt(BlockPos pos)
        {
            return pos.Equals(gear[0]) || pos.Equals(gear[1]) || pos.Equals(gear[2]) || pos.Equals(gear[3]);
        }

        /// <summary>
        /// This overload adds an api parameter - it is called from BlockLargeGear3m.HasMechPowerConnectorAt() which may be called before this BlockEntity has been initialised
        /// </summary>
        /// <param name="api"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool HasGearAt(ICoreAPI api, BlockPos pos)
        {
            if (gear == null) this.Initialize(api);
            return HasGearAt(pos);
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
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
        }
    }
}
