using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPTransmission : BEBehaviorMPBase
    {
        public bool engaged;
        protected float[] rotPrev = new float[2];

        BlockFacing[] orients = new BlockFacing[2];
        string orientations;

        public override CompositeShape Shape
        {
            get
            {
                string side = Block.Variant["orientation"];

                CompositeShape shape = new CompositeShape() { 
                    Base = new AssetLocation("shapes/block/wood/mechanics/transmission-leftgear.json"), Overlays = new CompositeShape[] { 
                           new CompositeShape() { Base = new AssetLocation("shapes/block/wood/mechanics/transmission-rightgear.json") } } 
                };

                if (side == "ns")
                {
                    shape.rotateY = 90;
                    shape.Overlays[0].rotateY = 90;
                }

                return shape;
            }
            set
            {

            }
        }

        public BEBehaviorMPTransmission(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            orientations = Block.Variant["orientation"];
            switch (orientations)
            {
                case "ns":
                    AxisSign = new int[] { 0, 0, -1 };
                    orients[0] = BlockFacing.NORTH;
                    orients[1] = BlockFacing.SOUTH;
                    break;

                case "we":
                    AxisSign = new int[] { 1, 0, 0 };
                    orients[0] = BlockFacing.EAST;
                    orients[1] = BlockFacing.WEST;
                    break;
            }

            //CheckEngaged(api.World.BlockAccessor, false);
            if (this.engaged) ChangeState(true);
        }

        public void CheckEngaged(IBlockAccessor access, bool updateNetwork)
        {
            BlockFacing side = orients[0] == BlockFacing.NORTH ? BlockFacing.EAST : BlockFacing.NORTH;
            bool clutchEngaged = false;
            BEClutch bec = access.GetBlockEntity(Position.AddCopy(side)) as BEClutch;
            if (bec?.Facing == side.Opposite) clutchEngaged = bec.Engaged;
            if (!clutchEngaged)
            {
                bec = access.GetBlockEntity(Position.AddCopy(side.Opposite)) as BEClutch;
                if (bec?.Facing == side) clutchEngaged = bec.Engaged;
            }
            if (clutchEngaged != this.engaged)
            {
                this.engaged = clutchEngaged;
                if (updateNetwork) this.ChangeState(clutchEngaged);
            }
        }

        protected override MechPowerPath[] GetMechPowerExits(MechPowerPath fromExitTurnDir)
        {
            if (!engaged) return new MechPowerPath[0];

            return base.GetMechPowerExits(fromExitTurnDir);
        }

        public override float GetResistance()
        {
            return 0.0005f;
        }

        private void ChangeState(bool newEngaged)
        {
            if (newEngaged)
            {
                CreateJoinAndDiscoverNetwork(orients[0]);
                CreateJoinAndDiscoverNetwork(orients[1]);
                this.tryConnect(orients[0]);

                ////Test for connection on opposite side as well
                //pos = Position.AddCopy(orients[1]);
                //block = Api.World.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                //if (block != null && this.network != block.GetNetwork(Api.World, pos))
                //{
                //    block.DidConnectAt(Api.World, pos, orients[0]);
                //    this.WasPlaced(orients[1]);
                //}
                Blockentity.MarkDirty(true);
            }
            else
            {
                if (network != null)
                {
                    manager.OnNodeRemoved(this);
                }
            }
        }

        internal float RotationNeighbour(int side, bool allowIndirect)
        {
            BlockPos pos = Position.AddCopy(orients[side]);
            IMechanicalPowerBlock block = Api.World.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
            if (block?.HasMechPowerConnectorAt(Api.World, pos, orients[side].Opposite) != true)
            {
                block = null;
            }
            BlockEntity be = block == null ? null : Api.World.BlockAccessor.GetBlockEntity(pos);
            IMechanicalPowerDevice node = be?.GetBehavior<BEBehaviorMPBase>() as IMechanicalPowerDevice;
            if (node is BEBehaviorMPTransmission trans && !trans.engaged) node = null;
            float rot;
            if (node == null || node.Network == null)
            {
                if (this.engaged && allowIndirect)
                {
                    rot = this.RotationNeighbour(1 - side, false);
                    rotPrev[side] = rot;
                }
                else
                {
                    rot = rotPrev[side];
                }
            }
            else
            {
                rot = node.Network.AngleRad * node.GearedRatio;
                bool invert = node.GetPropagationDirection() != orients[side];
                if (side == 1) invert = !invert;
                if (invert) rot = GameMath.TWOPI - rot;
                rotPrev[side] = rot;
            }
            return rot;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            this.engaged = tree.GetBool("engaged");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("engaged", this.engaged);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            base.OnTesselation(mesher, tesselator);
            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            if (Api.World.EntityDebugMode)
            {
                sb.AppendLine(string.Format(Lang.Get(engaged ? "Engaged" : "Disengaged")));
            }
        }
    }
}
