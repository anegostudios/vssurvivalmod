using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{

    public class BEBehaviorMPAxle : BEBehaviorMPBase
    {
        private Vec3f center = new Vec3f(0.5f, 0.5f, 0.5f);
        BlockFacing[] orients = new BlockFacing[2];
        ICoreClientAPI capi;
        string orientations;

        AssetLocation axleStandLocWest, axleStandLocEast;
        CompositeShape axleshape = null;

        public BEBehaviorMPAxle(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            axleshape = properties["shape"].AsObject<CompositeShape>();

            base.Initialize(api, properties);

            if (properties["withStands"].AsBool(true))
            {
                axleStandLocWest = AssetLocation.Create("block/wood/mechanics/axle-stand-west", Block.Code?.Domain);
                axleStandLocEast = AssetLocation.Create("block/wood/mechanics/axle-stand-east", Block.Code?.Domain);

                if (Block.Attributes?["axleStandLocWest"].Exists == true)
                {
                    axleStandLocWest = Block.Attributes["axleStandLocWest"].AsObject<AssetLocation>();
                }
                if (Block.Attributes?["axleStandLocEast"].Exists == true)
                {
                    axleStandLocEast = Block.Attributes["axleStandLocEast"].AsObject<AssetLocation>();
                }
                axleStandLocWest.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
                axleStandLocEast.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            }


            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
            }

            orientations = Block.Variant["rotation"];
            switch (orientations)
            {
                case "ns":
                    AxisSign = new int[] { 0, 0, -1 };
                    orients[0] = BlockFacing.NORTH;
                    orients[1] = BlockFacing.SOUTH;
                    break;

                case "we":
                    AxisSign = new int[] { -1, 0, 0 };
                    orients[0] = BlockFacing.WEST;
                    orients[1] = BlockFacing.EAST;
                    break;

                case "ud":
                    AxisSign = new int[] { 0, 1, 0 };
                    orients[0] = BlockFacing.DOWN;
                    orients[1] = BlockFacing.UP;
                    break;
            }
        }

        public override float GetResistance()
        {
            return 0.0005f;
        }

        protected override CompositeShape GetShape()
        {
            return axleshape ?? base.GetShape();
        }


        protected virtual MeshData getStandMesh(string orient)
        {
            if (axleStandLocWest == null) return null;

            return ObjectCacheUtil.GetOrCreate(Api, Block.Code + "-" + orient + "-stand", () =>
            {
                Shape shape = API.Common.Shape.TryGet(capi, orient == "west" ? axleStandLocWest : axleStandLocEast);
                capi.Tesselator.TesselateShape(Block, shape, out MeshData mesh);
                return mesh;
            });
        }

        public static bool IsAttachedToBlock(IBlockAccessor blockaccessor, Block block, BlockPos Position)
        {
            if (block.FirstCodePart() == "woodenaxlewall") return true;

            string orientations = block.Variant["rotation"];
            if (orientations == "ns" || orientations == "we")
            {
                // Up or down
                if (
                    blockaccessor.GetBlockBelow(Position, 1, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index] ||
                    blockaccessor.GetBlockAbove(Position, 1, BlockLayersAccess.Solid).SideSolid[BlockFacing.DOWN.Index]
                ) return true;

                // Front or back
                BlockFacing frontFacing = orientations == "ns" ? BlockFacing.WEST : BlockFacing.NORTH;
                return
                    blockaccessor.GetBlockOnSide(Position, frontFacing, BlockLayersAccess.Solid).SideSolid[frontFacing.Opposite.Index] ||
                    blockaccessor.GetBlockOnSide(Position, frontFacing.Opposite, BlockLayersAccess.Solid).SideSolid[frontFacing.Index]
                ;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[i];
                    Block blockNeib = blockaccessor.GetBlockOnSide(Position, face, BlockLayersAccess.Solid);
                    if (blockNeib.SideSolid[face.Opposite.Index])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected virtual bool AddStands => axleStandLocWest != null;


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (AddStands)
            {
                if (RequiresStand(Api.World, Position, orients[0].Normali))
                {
                    // Add west stand
                    MeshData mesh = getStandMesh("west");

                    mesh = rotStand(mesh);
                    if (mesh != null) mesher.AddMeshData(mesh);
                }

                if (RequiresStand(Api.World, Position, orients[1].Normali))
                {
                    // Add east stand
                    MeshData mesh = getStandMesh("east");
                    mesh = rotStand(mesh);
                    if (mesh != null) mesher.AddMeshData(mesh);
                }
            }

            base.OnTesselation(mesher, tesselator);
            return true;
        }

        private bool RequiresStand(IWorldAccessor world, BlockPos pos, Vec3i vector)
        {
            try
            {
                BlockMPBase block = world.BlockAccessor.GetBlockRaw(pos.X + vector.X, pos.InternalY + vector.Y, pos.Z + vector.Z, BlockLayersAccess.Solid) as BlockMPBase;
                if (block == null) return true;

                BlockPos sidePos = new BlockPos(pos.X + vector.X, pos.Y + vector.Y, pos.Z + vector.Z, pos.dimension);
                BEBehaviorMPBase bemp = world.BlockAccessor.GetBlockEntity(sidePos)?.GetBehavior<BEBehaviorMPBase>();
                if (bemp == null) return true;

                BEBehaviorMPAxle bempaxle = bemp as BEBehaviorMPAxle;
                if (bempaxle == null)
                {
                    if (bemp is BEBehaviorMPBrake || bemp is BEBehaviorMPCreativeRotor)
                    {
                        BlockFacing side = BlockFacing.FromNormal(vector);
                        if (side != null && block.HasMechPowerConnectorAt(world, sidePos, side.Opposite, Block as BlockMPBase)) return false;
                    }
                    return true;
                }

                if (bempaxle.orientations == orientations && IsAttachedToBlock(world.BlockAccessor, block, sidePos)) return false;
                return bempaxle.RequiresStand(world, sidePos, vector);
            }
#if DEBUG
            catch (Exception)
            {
                throw;
            }
#else
            catch (Exception e)
            {
                world.Logger.Error("Exception thrown in RequiresStand, will log exception but silently ignore it: at " + pos);
                world.Logger.Error(e);
                return false;
            }
#endif
        }

        private MeshData rotStand(MeshData mesh)
        {
            if (orientations == "ns" || orientations == "we")
            {
                mesh = mesh.Clone();

                if (orientations == "ns") mesh = mesh.Rotate(center, 0, -GameMath.PIHALF, 0);

                // No stand rotation if standing on a solid block below
                if (!Api.World.BlockAccessor.GetBlockBelow(Position, 1, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index])
                {
                    if (Api.World.BlockAccessor.GetBlockAbove(Position, 1, BlockLayersAccess.Solid).SideSolid[BlockFacing.DOWN.Index])
                    {
                        mesh = mesh.Rotate(center, GameMath.PI, 0, 0);
                        if (orientations == "ns") mesh = mesh.Rotate(center, 0, GameMath.PI, 0);
                    }
                    else if (orientations == "ns")
                    {
                        BlockFacing face = BlockFacing.EAST;
                        if (Api.World.BlockAccessor.GetBlockOnSide(Position, face, BlockLayersAccess.Solid).SideSolid[face.Opposite.Index])
                        {
                            mesh = mesh.Rotate(center, 0, 0, GameMath.PIHALF);
                        }
                        else
                        {
                            face = BlockFacing.WEST;
                            if (Api.World.BlockAccessor.GetBlockOnSide(Position, face, BlockLayersAccess.Solid).SideSolid[face.Opposite.Index])
                            {
                                mesh = mesh.Rotate(center, 0, 0, -GameMath.PIHALF);
                            }
                            else return null;
                        }
                    } else
                    {
                        BlockFacing face = BlockFacing.NORTH;
                        if (Api.World.BlockAccessor.GetBlockOnSide(Position, face, BlockLayersAccess.Solid).SideSolid[face.Opposite.Index])
                        {
                            mesh = mesh.Rotate(center, GameMath.PIHALF, 0, 0);
                        }
                        else
                        {
                            face = BlockFacing.SOUTH;
                            if (Api.World.BlockAccessor.GetBlockOnSide(Position, face, BlockLayersAccess.Solid).SideSolid[face.Opposite.Index])
                            {
                                mesh = mesh.Rotate(center, -GameMath.PIHALF, 0, 0);
                            }
                            else return null;
                        }
                    }
                }
            }
            else
            {
                BlockFacing attachFace = null;
                for (int i = 0; i < 4; i++)
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[i];
                    if (Api.World.BlockAccessor.GetBlockOnSide(Position, face, BlockLayersAccess.Solid).SideSolid[face.Opposite.Index])
                    {
                        attachFace = face;
                        break;
                    }
                }

                if (attachFace != null)
                {
                    mesh = mesh.Clone()
                        .Rotate(center, 0, 0, GameMath.PIHALF)
                        .Rotate(center, 0, attachFace.HorizontalAngleIndex * 90 * GameMath.DEG2RAD, 0)
                    ;
                    return mesh;
                }

                return null;
            }

            return mesh;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            if (Api.World.EntityDebugMode)
            {
                string orientations = Block.Variant["orientation"];
                sb.AppendLine(string.Format(Lang.Get("Orientation: {0}", orientations)));
            }
        }
    }
}
