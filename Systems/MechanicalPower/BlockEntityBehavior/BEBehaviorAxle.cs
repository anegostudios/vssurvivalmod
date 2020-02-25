using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPAxle : BEBehaviorMPBase
    {
        BlockFacing[] orients = new BlockFacing[2];
        ICoreClientAPI capi;
        string orientations;

        public BEBehaviorMPAxle(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
            }

            orientations = Block.Variant["rotation"];
            switch (orientations)
            {
                case "ns":
                    AxisSign = new int[] { -1, -1, -1 };
                    orients[0] = BlockFacing.NORTH;
                    orients[1] = BlockFacing.SOUTH;
                    break;

                case "we":
                    AxisMapping = new int[] { 2, 1, 0 };
                    AxisSign = new int[] { -1, -1, -1 };

                    orients[0] = BlockFacing.WEST;
                    orients[1] = BlockFacing.EAST;
                    break;

                case "ud":
                    AxisMapping = new int[] { 1, 2, 0 };
                    AxisSign = new int[] { 1, 1, 1 };

                    orients[0] = BlockFacing.DOWN;
                    orients[1] = BlockFacing.UP;
                    break;
            }
        }

        public override TurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            return GetInTurnDirection();
        }

        public override float GetResistance()
        {
            return 0.0005f;
        }

        public override float GetTorque()
        {
            return 0;
        }

        protected override MechPowerPath[] GetMechPowerExits(TurnDirection fromExitTurnDir)
        {
            // Axles just forward mechanical power in the same direction with the same turn direction
            return new MechPowerPath[] { new MechPowerPath(fromExitTurnDir.Facing, fromExitTurnDir.Rot) };
        }

        protected virtual MeshData getStandMesh(string orient)
        {
            return ObjectCacheUtil.GetOrCreate(Api, "axle-" + orient + "-stand", () =>
            {
                Shape shape = capi.Assets.TryGet("shapes/block/wood/mechanics/axle-stand-" + orient + ".json").ToObject<Shape>();
                MeshData mesh;
                capi.Tesselator.TesselateShape(Block, shape, out mesh);
                return mesh;
            });
        }

        public bool IsAttachedToBlock()
        {
            if (orientations == "ns" || orientations == "we")
            {
                // Up or down
                if (
                    Api.World.BlockAccessor.GetBlock(Position.X, Position.Y - 1, Position.Z).SideSolid[BlockFacing.UP.Index] ||
                    Api.World.BlockAccessor.GetBlock(Position.X, Position.Y + 1, Position.Z).SideSolid[BlockFacing.DOWN.Index]
                ) return true;

                // Front or back
                BlockFacing frontFacing = Block.Variant["rotation"] == "ns" ? BlockFacing.WEST : BlockFacing.NORTH;
                return
                    Api.World.BlockAccessor.GetBlock(Position.AddCopy(frontFacing)).SideSolid[frontFacing.GetOpposite().Index] ||
                    Api.World.BlockAccessor.GetBlock(Position.AddCopy(frontFacing.GetOpposite())).SideSolid[frontFacing.Index]
                ;
            }



            if (orientations == "ud")
            {
                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[i];
                    Block block = Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y + face.Normali.Y, Position.Z + face.Normali.Z);
                    if (Block != block && block.SideSolid[face.GetOpposite().Index])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected virtual bool AddStands => true;


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (AddStands)
            {
                Block block = Api.World.BlockAccessor.GetBlock(Position.X + orients[0].Normali.X, Position.Y + orients[0].Normali.Y, Position.Z + orients[0].Normali.Z);
                if (block != Block)
                {
                    // Add west stand
                    MeshData mesh = getStandMesh("west");

                    mesh = rotStand(mesh);
                    mesher.AddMeshData(mesh);
                }

                block = Api.World.BlockAccessor.GetBlock(Position.X + orients[1].Normali.X, Position.Y + orients[1].Normali.Y, Position.Z + orients[1].Normali.Z);
                if (block != Block)
                {
                    // Add east stand
                    MeshData mesh = getStandMesh("east");
                    mesh = rotStand(mesh);
                    mesher.AddMeshData(mesh);
                }
            }


            return base.OnTesselation(mesher, tesselator);
        }

        private MeshData rotStand(MeshData mesh)
        {
            if (orientations == "ns" || orientations == "we")
            {
                mesh = mesh.Clone();

                if (orientations == "ns") mesh = mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, -GameMath.PIHALF, 0);

                if (!Api.World.BlockAccessor.GetBlock(Position.X, Position.Y - 1, Position.Z).SideSolid[BlockFacing.UP.Index])
                {
                    if (Api.World.BlockAccessor.GetBlock(Position.X, Position.Y + 1, Position.Z).SideSolid[BlockFacing.DOWN.Index])
                    {
                        mesh = mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), GameMath.PI, 0, 0);
                    } else
                    if (orientations == "ns")
                    {
                        BlockFacing face = BlockFacing.EAST;
                        if (Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y, Position.Z + face.Normali.Z).SideSolid[face.GetOpposite().Index])
                        {
                            mesh = mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 0, GameMath.PIHALF);
                        }
                        else
                        {
                            face = BlockFacing.WEST;
                            if (Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y, Position.Z + face.Normali.Z).SideSolid[face.GetOpposite().Index])
                            {
                                mesh = mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 0, -GameMath.PIHALF);
                            }
                        }
                    } else
                    {
                        BlockFacing face = BlockFacing.NORTH;
                        if (Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y, Position.Z + face.Normali.Z).SideSolid[face.GetOpposite().Index])
                        {
                            mesh = mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), GameMath.PIHALF, 0, 0);
                        }
                        else
                        {
                            face = BlockFacing.SOUTH;
                            if (Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y, Position.Z + face.Normali.Z).SideSolid[face.GetOpposite().Index])
                            {
                                mesh = mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), -GameMath.PIHALF, 0, 0);
                            }
                        }
                    }
                }
            }

            if (orientations == "ud")
            {
                BlockFacing attachFace = null;
                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[i];
                    if (Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y + face.Normali.Y, Position.Z + face.Normali.Z).SideSolid[face.GetOpposite().Index])
                    {
                        attachFace = face;
                        break;
                    }
                }

                if (attachFace != null)
                {
                    mesh = mesh.Clone()
                        .Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 0, GameMath.PIHALF)
                        .Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, attachFace.HorizontalAngleIndex * 90 * GameMath.DEG2RAD, 0)
                    ;
                    return mesh;
                }

                return null;

            }

            return mesh;
        }
    }
}
