﻿using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    /// <summary>
    /// Spawns an EntityBlockFalling when the user places a block that has air underneath it or if a neighbor block is
    /// removed and causes air to be underneath it.
    /// </summary>
    public class BlockBehaviorUnstableFalling : BlockBehavior
    {
        bool ignorePlaceTest;
        AssetLocation[] exceptions;
        public bool fallSideways;
        float dustIntensity;
        float fallSidewaysChance = 0.3f;

        AssetLocation fallSound;
        float impactDamageMul;
        Cuboidi[] attachmentAreas;

        BlockFacing[] attachableFaces;

        public BlockBehaviorUnstableFalling(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            attachableFaces = null;

            if (properties["attachableFaces"].Exists)
            {
                string[] faces = properties["attachableFaces"].AsArray<string>();
                attachableFaces = new BlockFacing[faces.Length];

                for (int i = 0; i < faces.Length; i++)
                {
                    attachableFaces[i] = BlockFacing.FromCode(faces[i]);
                }
            }
            
            var areas = properties["attachmentAreas"].AsObject<Dictionary<string, RotatableCube>>(null);
            attachmentAreas = new Cuboidi[6];
            if (areas != null)
            {
                foreach (var val in areas)
                {
                    val.Value.Origin.Set(8, 8, 8);
                    BlockFacing face = BlockFacing.FromFirstLetter(val.Key[0]);
                    attachmentAreas[face.Index] = val.Value.RotatedCopy().ConvertToCuboidi();
                }
            } else
            {
                attachmentAreas[4] = properties["attachmentArea"].AsObject<Cuboidi>(null);
            }

            ignorePlaceTest = properties["ignorePlaceTest"].AsBool(false);
            exceptions = properties["exceptions"].AsObject(new AssetLocation[0], block.Code.Domain);
            fallSideways = properties["fallSideways"].AsBool(false);
            dustIntensity = properties["dustIntensity"].AsFloat(0);

            fallSidewaysChance = properties["fallSidewaysChance"].AsFloat(0.3f);
            string sound = properties["fallSound"].AsString(null);
            if (sound != null)
            {
                fallSound = AssetLocation.Create(sound, block.Code.Domain);
            }

            impactDamageMul = properties["impactDamageMul"].AsFloat(1f);
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PassThrough;
            if (ignorePlaceTest) return true;

            Cuboidi attachmentArea = attachmentAreas[4];

            BlockPos pos = blockSel.Position.DownCopy();
            Block onBlock = world.BlockAccessor.GetBlock(pos);
            if (blockSel != null && !IsAttached(world.BlockAccessor, blockSel.Position) && !onBlock.CanAttachBlockAt(world.BlockAccessor, block, pos, BlockFacing.UP, attachmentArea) && block.Attributes?["allowUnstablePlacement"].AsBool() != true && !exceptions.Contains(onBlock.Code))
            {
                handling = EnumHandling.PreventSubsequent;
                failureCode = "requiresolidground";
                return false;
            }

            return TryFalling(world, blockSel.Position, ref handling, ref failureCode);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handling)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos, ref handling);

            if (world.Side == EnumAppSide.Client) return;

            EnumHandling bla = EnumHandling.PassThrough;
            string bla2 = "";
            TryFalling(world, pos, ref bla, ref bla2);
        }

        private bool TryFalling(IWorldAccessor world, BlockPos pos, ref EnumHandling handling, ref string failureCode)
        {
            if (world.Side != EnumAppSide.Server) return false;
            if (!fallSideways && IsAttached(world.BlockAccessor, pos)) return false;

            ICoreServerAPI sapi = (world as IServerWorldAccessor).Api as ICoreServerAPI;
            if (!sapi.Server.Config.AllowFallingBlocks) return false;


            if (IsReplacableBeneath(world, pos) || (fallSideways && world.Rand.NextDouble() < fallSidewaysChance && IsReplacableBeneathAndSideways(world, pos)))
            {
                // Prevents duplication
                Entity entity = world.GetNearestEntity(pos.ToVec3d().Add(0.5, 0.5, 0.5), 1, 1.5f, (e) =>
                {
                    return e is EntityBlockFalling ebf && ebf.initialPos.Equals(pos);
                });

                if (entity == null)
                {
                    EntityBlockFalling entityblock = new EntityBlockFalling(block, world.BlockAccessor.GetBlockEntity(pos), pos, fallSound, impactDamageMul, true, dustIntensity);
                    world.SpawnEntity(entityblock);
                } else
                {
                    handling = EnumHandling.PreventDefault;
                    failureCode = "entityintersecting";
                    return false;
                }

                handling = EnumHandling.PreventSubsequent;
                return true;
            }

            handling = EnumHandling.PassThrough;
            return false;
        }


        public virtual bool IsAttached(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockPos tmpPos;

            if (attachableFaces == null)    // shorter code path for no attachableFaces specified (common case) - we test only the block below
            {
                tmpPos = pos.DownCopy();
                Block block = blockAccessor.GetBlock(tmpPos);
                return block.CanAttachBlockAt(blockAccessor, this.block, tmpPos, BlockFacing.UP, attachmentAreas[5]);
            }

            tmpPos = new BlockPos();
            for (int i = 0; i < attachableFaces.Length; i++)
            {
                BlockFacing face = attachableFaces[i];

                tmpPos.Set(pos).Add(face);
                Block block = blockAccessor.GetBlock(tmpPos);
                if (block.CanAttachBlockAt(blockAccessor, this.block, tmpPos, face.Opposite, attachmentAreas[face.Index]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsReplacableBeneathAndSideways(IWorldAccessor world, BlockPos pos)
        {
            for (int i = 0; i < 4; i++)
            {
                BlockFacing facing = BlockFacing.HORIZONTALS[i];

                Block nBlock = world.BlockAccessor.GetBlockOrNull(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y, pos.Z + facing.Normali.Z);
                if (nBlock != null && nBlock.Replaceable >= 6000)
                {
                    nBlock = world.BlockAccessor.GetBlockOrNull(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y - 1, pos.Z + facing.Normali.Z);
                    if (nBlock != null && nBlock.Replaceable >= 6000)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsReplacableBeneath(IWorldAccessor world, BlockPos pos)
        {
            Block bottomBlock = world.BlockAccessor.GetBlockBelow(pos);
            return bottomBlock.Replaceable > 6000;
        }
    }
}
