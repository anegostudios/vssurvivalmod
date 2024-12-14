﻿using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPAxle : BEBehaviorMPBase
    {
        private Vec3f center = new Vec3f(0.5f, 0.5f, 0.5f);
        BlockFacing[] orients = new BlockFacing[2];
        ICoreClientAPI capi;
        string orientations;

        AssetLocation axleStandLocWest, axleStandLocEast;

        public BEBehaviorMPAxle(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

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

        protected virtual MeshData getStandMesh(string orient)
        {
            return ObjectCacheUtil.GetOrCreate(Api, Block.Code + "-" + orient + "-stand", () =>
            {
                Shape shape = API.Common.Shape.TryGet(capi, orient == "west" ? axleStandLocWest : axleStandLocEast);
                MeshData mesh;
                capi.Tesselator.TesselateShape(Block, shape, out mesh);
                return mesh;
            });
        }

        public static bool IsAttachedToBlock(IBlockAccessor blockaccessor, Block block, BlockPos Position)
        {
            string orientations = block.Variant["rotation"];
            if (orientations == "ns" || orientations == "we")
            {
                // Up or down
                if (
                    blockaccessor.GetBlock(Position.X, Position.Y - 1, Position.Z).SideSolid[BlockFacing.UP.Index] ||
                    blockaccessor.GetBlock(Position.X, Position.Y + 1, Position.Z).SideSolid[BlockFacing.DOWN.Index]
                ) return true;

                // Front or back
                BlockFacing frontFacing = orientations == "ns" ? BlockFacing.WEST : BlockFacing.NORTH;
                return
                    blockaccessor.GetBlock(Position.AddCopy(frontFacing)).SideSolid[frontFacing.Opposite.Index] ||
                    blockaccessor.GetBlock(Position.AddCopy(frontFacing.Opposite)).SideSolid[frontFacing.Index]
                ;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[i];
                    Block blockNeib = blockaccessor.GetBlock(Position.X + face.Normali.X, Position.Y, Position.Z + face.Normali.Z);
                    if (blockNeib.SideSolid[face.Opposite.Index])
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

            return base.OnTesselation(mesher, tesselator);
        }

        private bool RequiresStand(IWorldAccessor world, BlockPos pos, Vec3i vector)
        {
            try
            {
                BlockMPBase block = world.BlockAccessor.GetBlock(pos.X + vector.X, pos.Y + vector.Y, pos.Z + vector.Z) as BlockMPBase;
                if (block == null) return true;

                BlockPos sidePos = new BlockPos(pos.X + vector.X, pos.Y + vector.Y, pos.Z + vector.Z);
                BEBehaviorMPBase bemp = world.BlockAccessor.GetBlockEntity(sidePos)?.GetBehavior<BEBehaviorMPBase>();
                if (bemp == null) return true;

                BEBehaviorMPAxle bempaxle = bemp as BEBehaviorMPAxle;
                if (bempaxle == null)
                {
                    if (bemp is BEBehaviorMPBrake || bemp is BEBehaviorMPCreativeRotor)
                    {
                        BlockFacing side = BlockFacing.FromNormal(vector);
                        if (side != null && block.HasMechPowerConnectorAt(world, sidePos, side.Opposite)) return false;
                    }
                    return true;
                }

                if (bempaxle.orientations == orientations && IsAttachedToBlock(world.BlockAccessor, block, sidePos)) return false;
                return bempaxle.RequiresStand(world, sidePos, vector);
            }
            catch (Exception e)
            {
#if DEBUG
                throw;
#else
                world.Logger.Error("Exception thrown in RequiresStand, will log exception but silently ignore it: at " + pos);
                world.Logger.Error(e);
                return false;
#endif
            }
        }

        private MeshData rotStand(MeshData mesh)
        {
            if (orientations == "ns" || orientations == "we")
            {
                mesh = mesh.Clone();

                if (orientations == "ns") mesh = mesh.Rotate(center, 0, -GameMath.PIHALF, 0);

                // No stand rotation if standing on a solid block below
                if (!Api.World.BlockAccessor.GetBlock(Position.X, Position.Y - 1, Position.Z).SideSolid[BlockFacing.UP.Index])
                {
                    if (Api.World.BlockAccessor.GetBlock(Position.X, Position.Y + 1, Position.Z).SideSolid[BlockFacing.DOWN.Index])
                    {
                        mesh = mesh.Rotate(center, GameMath.PI, 0, 0);

                    }
                    else if (orientations == "ns")
                    {
                        BlockFacing face = BlockFacing.EAST;
                        if (Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y, Position.Z + face.Normali.Z).SideSolid[face.Opposite.Index])
                        {
                            mesh = mesh.Rotate(center, 0, 0, GameMath.PIHALF);
                        }
                        else
                        {
                            face = BlockFacing.WEST;
                            if (Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y, Position.Z + face.Normali.Z).SideSolid[face.Opposite.Index])
                            {
                                mesh = mesh.Rotate(center, 0, 0, -GameMath.PIHALF);
                            }
                            else return null;
                        }
                    } else
                    {
                        BlockFacing face = BlockFacing.NORTH;
                        if (Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y, Position.Z + face.Normali.Z).SideSolid[face.Opposite.Index])
                        {
                            mesh = mesh.Rotate(center, GameMath.PIHALF, 0, 0);
                        }
                        else
                        {
                            face = BlockFacing.SOUTH;
                            if (Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y, Position.Z + face.Normali.Z).SideSolid[face.Opposite.Index])
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
                    if (Api.World.BlockAccessor.GetBlock(Position.X + face.Normali.X, Position.Y, Position.Z + face.Normali.Z).SideSolid[face.Opposite.Index])
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
