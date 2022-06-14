using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPToggle : BEBehaviorMPBase
    {
        protected readonly BlockFacing[] orients = new BlockFacing[2];

        protected readonly BlockPos[] sides = new BlockPos[2];

        ICoreClientAPI capi;
        string orientations;

        public BEBehaviorMPToggle(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
            }

            orientations = Block.Variant["orientation"];
            switch (orientations)
            {
                case "ns":
                    AxisSign = new int[] { 0, 0, -1 };
                    orients[0] = BlockFacing.NORTH;
                    orients[1] = BlockFacing.SOUTH;

                    sides[0] = Position.AddCopy(BlockFacing.WEST);
                    sides[1] = Position.AddCopy(BlockFacing.EAST);
                    break;

                case "we":
                    AxisSign = new int[] { -1, 0, 0 };
                    orients[0] = BlockFacing.WEST;
                    orients[1] = BlockFacing.EAST;

                    sides[0] = Position.AddCopy(BlockFacing.NORTH);
                    sides[1] = Position.AddCopy(BlockFacing.SOUTH);
                    break;
            }
        }

        public bool ValidHammerBase(BlockPos pos)
        {
            return sides[0] == pos || sides[1] == pos;
        }

        public override float GetResistance()
        {
            bool hasHammer = false;
            BEHelveHammer behh = Api.World.BlockAccessor.GetBlockEntity(sides[0]) as BEHelveHammer;
            if (behh != null && behh.HammerStack != null)
            {
                hasHammer = true;
            }
            else
            {
                behh = Api.World.BlockAccessor.GetBlockEntity(sides[1]) as BEHelveHammer;
                if (behh != null && behh.HammerStack != null)
                {
                    hasHammer = true;
                }
            }

            //Exponentially increase hammer resistance if the network is turning faster - should almost always prevent helvehammering at crazy speeds;
            float speed = this.network == null ? 0f : Math.Abs(this.network.Speed * this.GearedRatio);
            float speedLimiter = 5f * (float) Math.Exp(speed * 2.8 - 5.0);
            return hasHammer ? 0.125f + speedLimiter : 0.0005f;
        }

        public override void JoinNetwork(MechanicalNetwork network)
        {
            base.JoinNetwork(network);

            //Speed limit when joining a toggle to an existing network: this is to prevent crazy bursts of Helvehammer speed on first connection if the network was spinning fast (with low resistances)
            // (if the network has enough torque to drive faster than this - which is going to be uncommon - then the network speed can increase after the toggle is joined to the network)
            float speed = network == null ? 0f : Math.Abs(network.Speed * this.GearedRatio) * 1.6f;
            if (speed > 1f)
            {
                network.Speed /= speed;
                network.clientSpeed /= speed;
            }
        }

        public bool IsAttachedToBlock()
        {
            if (orientations == "ns" || orientations == "we")
            {
                return 
                    Api.World.BlockAccessor.IsSideSolid(Position.X, Position.Y - 1, Position.Z, BlockFacing.UP) ||
                    Api.World.BlockAccessor.IsSideSolid(Position.X, Position.Y + 1, Position.Z, BlockFacing.DOWN)
                ;
            }

            return false;
        }


        MeshData getStandMesh(string orient)
        {
            return ObjectCacheUtil.GetOrCreate(Api, "toggle-" + orient + "-stand", () =>
            {
                Shape shape = API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/toggle-stand.json");
                MeshData mesh;
                capi.Tesselator.TesselateShape(Block, shape, out mesh);

                if (orient == "ns")
                {
                    mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, GameMath.PIHALF, 0);
                }

                return mesh;
            });
            
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            MeshData mesh = getStandMesh(Block.Variant["orientation"]);
            mesher.AddMeshData(mesh);

            return base.OnTesselation(mesher, tesselator);
        }


    }
}
