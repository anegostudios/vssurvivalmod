﻿using System;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// A block behavior for a door. Also requires the "door" block entity behavior type on a block to work.
    /// Defined with the "Door" code.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
    ///	{ "name": "Door" }
    ///]
    ///</code><code lang="json">
    ///"attributes": {
    ///	"widthByType": {
    ///		"*": 1
    ///	},
    ///	"heightByType": {
    ///		"*": 2
    ///	},
    ///	"openSoundByType": {
    ///		"*": "sounds/block/cokeovendoor-open"
    ///	},
    ///	"closeSoundByType": {
    ///		"*": "sounds/block/cokeovendoor-close"
    ///	}
    ///}
    ///</code></example>
    [DocumentAsJson]
    [AddDocumentationProperty("TriggerSound", "Sets both OpenSound & CloseSound.", "Vintagestory.API.Common.AssetLocation", "Optional", "sounds/block/door", true)]
    public class BlockBehaviorDoor : StrongBlockBehavior, IMultiBlockColSelBoxes, IMultiBlockBlockProperties
    {
        /// <summary>
        /// The sound to play when the door is opened.
        /// </summary>
        [DocumentAsJson("Optional", "sounds/block/door", true)]
        public AssetLocation OpenSound;

        /// <summary>
        /// The sound to play when the door is closed.
        /// </summary>
        [DocumentAsJson("Optional", "sounds/block/door", true)]
        public AssetLocation CloseSound;

        /// <summary>
        /// The width of the multiblock instance for the door.
        /// </summary>
        [DocumentAsJson("Optional", "1", true)]
        public int width;

        /// <summary>
        /// The height of the multiblock instance for the door.
        /// </summary>
        [DocumentAsJson("Optional", "1", true)]
        public int height;
        
        /// <summary>
        /// Can this door be opened by hand?
        /// </summary>
        [DocumentAsJson("Optional", "True", true)]
        public bool handopenable;

        /// <summary>
        /// Is this door airtight?
        /// </summary>
        [DocumentAsJson("Optional", "True", true)]
        public bool airtight;

        ICoreAPI api;
        public MeshData animatableOrigMesh;
        public Shape animatableShape;
        public string animatableDictKey;

        public BlockBehaviorDoor(Block block) : base(block)
        {
            airtight = block.Attributes["airtight"].AsBool(true);
            width = block.Attributes["width"].AsInt(1);
            height = block.Attributes["height"].AsInt(1);
            handopenable = block.Attributes["handopenable"].AsBool(true);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
            OpenSound = CloseSound = AssetLocation.Create(block.Attributes["triggerSound"].AsString("sounds/block/door"));

            if (block.Attributes["openSound"].Exists) OpenSound = AssetLocation.Create(block.Attributes["openSound"].AsString("sounds/block/door"));
            if (block.Attributes["closeSound"].Exists) CloseSound = AssetLocation.Create(block.Attributes["closeSound"].AsString("sounds/block/door"));

            base.OnLoaded(api);
        }


        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs, ref EnumHandling handled)
        {
            var beh = world.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorDoor>();

            bool opened = !beh.Opened;
            if (activationArgs != null)
            {
                opened = activationArgs.GetBool("opened", opened);
            }

            if (beh.Opened != opened)
            {
                beh.ToggleDoorState(null, opened);
            }
        }




        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            var beh = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>();
            if (beh != null)
            {
                decalMesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, beh.RotateYRad, 0);
            }
        }

        public static BEBehaviorDoor getDoorAt(IWorldAccessor world, BlockPos pos)
        {
            var door = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>();
            if (door != null) return door;

            var blockMb = world.BlockAccessor.GetBlock(pos) as BlockMultiblock;
            if (blockMb != null)
            {
                door = world.BlockAccessor.GetBlockEntity(pos.AddCopy(blockMb.OffsetInv))?.GetBehavior<BEBehaviorDoor>();
                return door;
            }

            return null;
        }


        public static bool HasCombinableLeftDoor(IWorldAccessor world, float RotateYRad, BlockPos pos, int width, out BEBehaviorDoor leftDoor, out int leftOffset)
        {
            leftOffset = 0;
            leftDoor = null;
            BlockFacing leftFacing = BlockFacing.HorizontalFromYaw(RotateYRad).GetCW();

            BlockPos leftPos = pos.AddCopy(leftFacing);
            leftDoor = getDoorAt(world, leftPos);

            if (width > 1)
            {
                if (leftDoor == null)
                {
                    for (int i = 2; i <= width; i++)
                    {
                        leftPos = pos.AddCopy(leftFacing, i);
                        leftDoor = getDoorAt(world, leftPos);
                        if (leftDoor != null) break;
                    }
                }

                if (leftDoor != null)
                {
                    BlockPos offsetPos = leftDoor.Pos.AddCopy(leftFacing.Opposite, leftDoor.InvertHandles ? width : (width + leftDoor.doorBh.width - 1));
                    leftOffset = (int)pos.DistanceTo(offsetPos);

                    if ((leftDoor.facingWhenClosed.Axis == EnumAxis.X && leftPos.X != leftDoor.Pos.X) ||
                        (leftDoor.facingWhenClosed.Axis == EnumAxis.Z && leftPos.Z != leftDoor.Pos.Z))
                    {
                        leftDoor = null;
                        leftOffset = 0;
                    }
                }
            }

            if (leftDoor != null && leftDoor.LeftDoor == null && leftDoor.RightDoor == null && leftDoor.facingWhenClosed == BlockFacing.HorizontalFromYaw(RotateYRad))
            {
                return true;
            }

            return false;
        }

        public static bool HasCombinableRightDoor(IWorldAccessor world, float RotateYRad, BlockPos pos, int width, out BEBehaviorDoor rightDoor, out int rightOffset)
        {
            rightOffset = 0;
            rightDoor = null;
            BlockFacing rightFacing = BlockFacing.HorizontalFromYaw(RotateYRad).GetCCW();

            BlockPos rightPos = pos.AddCopy(rightFacing);
            rightDoor = getDoorAt(world, rightPos);

            if (width > 1)
            {
                if (rightDoor == null)
                {
                    for (int i = 2; i <= width; i++)
                    {
                        rightPos = pos.AddCopy(rightFacing, i);
                        rightDoor = getDoorAt(world, rightPos);
                        if (rightDoor != null) break;
                    }
                }

                if (rightDoor != null)
                {
                    BlockPos offsetPos = rightDoor.Pos.AddCopy(rightFacing.Opposite, !rightDoor.InvertHandles ? width : width + rightDoor.doorBh.width - 1);
                    rightOffset = (int)pos.DistanceTo(offsetPos);

                    if ((rightDoor.facingWhenClosed.Axis == EnumAxis.X && rightPos.X != rightDoor.Pos.X) ||
                        (rightDoor.facingWhenClosed.Axis == EnumAxis.Z && rightPos.Z != rightDoor.Pos.Z))
                    {
                        rightDoor = null;
                        rightOffset = 0;
                    }
                }
            }

            if (rightDoor != null && rightDoor.RightDoor == null && rightDoor.LeftDoor == null && rightDoor.facingWhenClosed == BlockFacing.HorizontalFromYaw(RotateYRad))
            {
                return true;
            }

            return false;
        }


        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            BlockPos pos = blockSel.Position.Copy();
            var rotRad = BEBehaviorDoor.getRotateYRad(byPlayer, blockSel);
            BlockFacing facing = BlockFacing.HorizontalFromYaw(rotRad);
            bool blocked = false;

            bool invertHandle = HasCombinableLeftDoor(world, rotRad, blockSel.Position, width, out BEBehaviorDoor otherDoor, out int offset);

            if (invertHandle && width > 1 && offset != 0)
            {
                pos.Add(facing.GetCCW(), offset);
            }

            if (!invertHandle && HasCombinableRightDoor(world, rotRad, blockSel.Position, width, out otherDoor, out offset) && width > 1 && offset != 0)
            {
                pos.Add(facing.GetCW(), offset);
            }

            IterateOverEach(pos, rotRad, invertHandle, (mpos) =>
            {
                Block mblock = world.BlockAccessor.GetBlock(mpos, BlockLayersAccess.Solid);
                if (!mblock.IsReplacableBy(block))
                {
                    blocked = true;
                    return false;
                }

                return true;
            });

            if (blocked)
            {
                handling = EnumHandling.PreventDefault;
                failureCode = "notenoughspace";
                return false;
            }

            return base.CanPlaceBlock(world, byPlayer, blockSel, ref handling, ref failureCode);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;
            BlockPos pos = blockSel.Position.Copy();
            IBlockAccessor ba = world.BlockAccessor;

            if (block.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                var rotRad = BEBehaviorDoor.getRotateYRad(byPlayer, blockSel);
                BlockFacing facing = BlockFacing.HorizontalFromYaw(rotRad);
                if (HasCombinableLeftDoor(world, rotRad, blockSel.Position, width, out BEBehaviorDoor otherDoor, out int offset))
                {
                    if (width > 1 && offset != 0)
                    {
                        pos.Add(facing.GetCCW(), offset);
                    }
                }
                else if (HasCombinableRightDoor(world, rotRad, blockSel.Position, width, out otherDoor, out offset))
                {
                    if (width > 1 && offset != 0)
                    {
                        pos.Add(facing.GetCW(), offset);
                    }
                }

                return placeDoor(world, byPlayer, itemstack, blockSel, pos, ba);
            }

            return false;
        }

        public bool placeDoor(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, BlockPos pos, IBlockAccessor ba)
        {
            ba.SetBlock(block.BlockId, pos);
            var bh = ba.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>();
            bh.OnBlockPlaced(itemstack, byPlayer, blockSel);

            if (world.Side == EnumAppSide.Server)
            {
                placeMultiblockParts(world, pos);
            }

            return true;
        }

        public void placeMultiblockParts(IWorldAccessor world, BlockPos pos)
        {
            var beh = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>();
            float rotRad = beh?.RotateYRad ?? 0;

            IterateOverEach(pos, rotRad, beh?.InvertHandles ?? false, (mpos) =>
            {
                if (mpos == pos) return true;
                int dx = mpos.X - pos.X;
                int dy = mpos.Y - pos.Y;
                int dz = mpos.Z - pos.Z;

                string sdx = (dx < 0 ? "n" : (dx > 0 ? "p" : "")) + Math.Abs(dx);
                string sdy = (dy < 0 ? "n" : (dy > 0 ? "p" : "")) + Math.Abs(dy);
                string sdz = (dz < 0 ? "n" : (dz > 0 ? "p" : "")) + Math.Abs(dz);

                AssetLocation loc = new AssetLocation("multiblock-monolithic-" + sdx + "-" + sdy + "-" + sdz);
                Block block = world.GetBlock(loc);
                world.BlockAccessor.SetBlock(block.Id, mpos);
                if (world.Side == EnumAppSide.Server) world.BlockAccessor.TriggerNeighbourBlockUpdate(mpos);
                return true;
            });
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            if (world.Side == EnumAppSide.Client) return;
            var beh = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>();

            var rotRad = beh?.RotateYRad ?? 0;

            IterateOverEach(pos, rotRad, beh?.InvertHandles ?? false, (mpos) =>
            {
                if (mpos == pos) return true;

                Block mblock = world.BlockAccessor.GetBlock(mpos);
                if (mblock is BlockMultiblock)
                {
                    world.BlockAccessor.SetBlock(0, mpos);
                    if (world.Side == EnumAppSide.Server) world.BlockAccessor.TriggerNeighbourBlockUpdate(mpos);
                }

                return true;
            });

            base.OnBlockRemoved(world, pos, ref handling);
        }

        public void IterateOverEach(BlockPos pos, float yRotRad, bool invertHandle, ActionConsumable<BlockPos> onBlock)
        {
            BlockPos tmpPos = new BlockPos(pos.dimension);

            for (int dx = 0; dx < width; dx++)
            {
                for (int dy = 0; dy < height; dy++)
                {
                    for (int dz = 0; dz < width; dz++)
                    {
                        var offset = BEBehaviorDoor.getAdjacentOffset(dx, dz, dy, yRotRad, invertHandle);
                        tmpPos.Set(pos.X + offset.X, pos.Y + offset.Y, pos.Z + offset.Z);

                        if (!onBlock(tmpPos)) return;
                    }
                }
            }
        }


        public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return getColSelBoxes(blockAccessor, pos, offset);
        }
        public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return getColSelBoxes(blockAccessor, pos, offset);
        }

        private static Cuboidf[] getColSelBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            var beh = blockAccessor.GetBlockEntity(pos.AddCopy(offset.X, offset.Y, offset.Z))?.GetBehavior<BEBehaviorDoor>();
            if (beh == null) return null;

            // Works only for 1 and 2 wide doors
            // Would need to loop across width to make n-width doors

            var rightBackOffset = beh.getAdjacentOffset(-1, -1);
            if (offset.X == rightBackOffset.X && offset.Z == rightBackOffset.Z)
            {
                return null;
            }
            if (beh.Opened)
            {
                var rightOffset = beh.getAdjacentOffset(-1, 0);
                if (offset.X == rightOffset.X && offset.Z == rightOffset.Z) return null;
            } else
            {
                var backOffset = beh.getAdjacentOffset(0, -1);
                if (offset.X == backOffset.X && offset.Z == backOffset.Z) return null;
            }

            return beh.ColSelBoxes;
        }


        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventSubsequent;
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>()?.ColSelBoxes ?? null;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventSubsequent;
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>()?.ColSelBoxes ?? null;
        }

        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventSubsequent;
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>()?.ColSelBoxes ?? null;
        }

        public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing, ref EnumHandling handled)
        {
            return base.GetParticleBreakBox(blockAccess, pos, facing, ref handled);
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData, ref EnumHandling handled)
        {
            var beh = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>();

            if (beh.Opened)
            {
                float rot = beh.InvertHandles ? 90 : -90;
                decalModelData = decalModelData.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, rot * GameMath.DEG2RAD, 0);
                if (!beh.InvertHandles) decalModelData = decalModelData.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 1, 1f, -1);
            }
            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData, ref handled);
        }

        public override void GetHeldItemName(StringBuilder sb, ItemStack itemStack)
        {
            doorNameWithMaterial(sb);
        }

        public override void GetPlacedBlockName(StringBuilder sb, IWorldAccessor world, BlockPos pos)
        {
            // Already set in Block.GetPlacedBlockName()
        }

        private void doorNameWithMaterial(StringBuilder sb)
        {
            if (block.Variant.ContainsKey("wood"))
            {
                string doorname = sb.ToString();
                sb.Clear();
                sb.Append(Lang.Get("doorname-with-material", doorname, Lang.Get("material-" + block.Variant["wood"])));
            }
        }

        public override float GetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            var beh = block.GetBEBehavior<BEBehaviorDoor>(pos);
            if (beh == null) return 0f;

            if (!beh.IsSideSolid(face)) return 0f;

            if (block.Variant["style"] == "sleek-windowed") return 1.0f;

            if (!airtight) return 0f;

            return 1f;
        }

        public float MBGetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos, Vec3i offset)
        {
            var beh = block.GetBEBehavior<BEBehaviorDoor>(pos.AddCopy(offset.X, offset.Y, offset.Z));
            if (beh == null) return 0f;
            if (!beh.IsSideSolid(face)) return 0f;

            if (block.Variant["style"] == "sleek-windowed") return offset.Y == -1 ? 0.0f : 1.0f;

            if (!airtight) return 0f;

            return 1f;
        }

        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            var beh = block.GetBEBehavior<BEBehaviorDoor>(pos);
            if (beh == null) return 0;

            if (type == EnumRetentionType.Sound) return beh.IsSideSolid(facing) ? 3 : 0;

            if (!airtight) return 0;
            if (api.World.Config.GetBool("openDoorsNotSolid", false)) return beh.IsSideSolid(facing) ? getInsulation(pos) : 0;
            return (beh.IsSideSolid(facing) || beh.IsSideSolid(facing.Opposite)) ? getInsulation(pos) : 3; // Also check opposite so the door can be facing inwards or outwards.
        }


        public int MBGetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type, Vec3i offset)
        {
            var beh = block.GetBEBehavior< BEBehaviorDoor>(pos.AddCopy(offset.X, offset.Y, offset.Z));
            if (beh == null) return 0;
            if (type == EnumRetentionType.Sound) return beh.IsSideSolid(facing) ? 3 : 0;

            if (!airtight) return 0;
            if (api.World.Config.GetBool("openDoorsNotSolid", false)) return beh.IsSideSolid(facing) ? getInsulation(pos) : 0;
            return (beh.IsSideSolid(facing) || beh.IsSideSolid(facing.Opposite)) ? getInsulation(pos) : 3; // Also check opposite so the door can be facing inwards or outwards.
        }

        private int getInsulation(BlockPos pos)
        {
            var mat = block.GetBlockMaterial(api.World.BlockAccessor, pos);
            if (mat == EnumBlockMaterial.Ore || mat == EnumBlockMaterial.Stone || mat == EnumBlockMaterial.Soil || mat == EnumBlockMaterial.Ceramic)
            {
                return -1;
            }
            return 1;
        }

        public bool MBCanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea, Vec3i offsetInv)
        {
            return false;
        }

        public JsonObject MBGetAttributes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return null;
        }
    }
}
