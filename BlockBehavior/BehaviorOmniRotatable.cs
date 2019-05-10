// RotateBehavior by Milo Christiansen
//
// To the extent possible under law, the person who associated CC0 with
// this project has waived all copyright and related or neighboring rights
// to this project.
//
// You should have received a copy of the CC0 legalcode along with this
// work.  If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods
{

    class BlockBehaviorOmniRotatable : BlockBehavior
    {
        private bool rotateH = false;
        private bool rotateV = false;
        private bool rotateV4 = false;
        private string facing = "player";
        private bool rotateSides = false;

        public BlockBehaviorOmniRotatable(Block block) : base(block)
        {
            
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;

            AssetLocation blockCode = null;
            if (rotateSides)
            {
                // Simple 6 state rotator.

                if (facing == "block")
                {
                    var x = Math.Abs(blockSel.HitPosition.X - 0.5);
                    var y = Math.Abs(blockSel.HitPosition.Y - 0.5);
                    var z = Math.Abs(blockSel.HitPosition.Z - 0.5);
                    switch (blockSel.Face.Axis)
                    {
                        case EnumAxis.X:
                            if (z < 0.3 && y < 0.3)
                            {
                                blockCode = block.CodeWithParts(blockSel.Face.GetOpposite().Code);
                            }
                            else if (z > y)
                            {
                                blockCode = block.CodeWithParts(blockSel.HitPosition.Z < 0.5 ? "north" : "south");
                            }
                            else
                            {
                                blockCode = block.CodeWithParts(blockSel.HitPosition.Y < 0.5 ? "down" : "up");
                            }
                            break;

                        case EnumAxis.Y:
                            if (z < 0.3 && x < 0.3)
                            {
                                blockCode = block.CodeWithParts(blockSel.Face.GetOpposite().Code);
                            }
                            else if (z > x)
                            {
                                blockCode = block.CodeWithParts(blockSel.HitPosition.Z < 0.5 ? "north" : "south");
                            }
                            else
                            {
                                blockCode = block.CodeWithParts(blockSel.HitPosition.X < 0.5 ? "west" : "east");
                            }
                            break;

                        case EnumAxis.Z:
                            if (x < 0.3 && y < 0.3)
                            {
                                blockCode = block.CodeWithParts(blockSel.Face.GetOpposite().Code);
                            }
                            else if (x > y)
                            {
                                blockCode = block.CodeWithParts(blockSel.HitPosition.X < 0.5 ? "west" : "east");
                            }
                            else
                            {
                                blockCode = block.CodeWithParts(blockSel.HitPosition.Y < 0.5 ? "down" : "up");
                            }
                            break;
                    }
                }
                else
                {
                    if (blockSel.Face.IsVertical)
                    {
                        blockCode = block.CodeWithParts(blockSel.Face.GetOpposite().Code);
                    }
                    else
                    {
                        blockCode = block.CodeWithParts(BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw).Code);
                    }
                }
            }
            else if (rotateH || rotateV)
            {
                // Complex 4/8/16 state rotator.
                string h = "north";
                string v = "up";
                if (blockSel.Face.IsVertical)
                {
                    v = blockSel.Face.Code;
                    h = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw).Code;
                }
                else if (rotateV4)
                {
                    if (facing == "block")
                    {
                        h = blockSel.Face.GetOpposite().Code;
                    }
                    else
                    {
                        // Default to player facing.
                        h = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw).Code;
                    }
                    switch (blockSel.Face.Axis)
                    {
                        case EnumAxis.X:
                            // Find the axis farther from the center.
                            if (Math.Abs(blockSel.HitPosition.Z - 0.5) > Math.Abs(blockSel.HitPosition.Y - 0.5))
                            {
                                v = blockSel.HitPosition.Z < 0.5 ? "left" : "right";
                            }
                            else
                            {
                                v = blockSel.HitPosition.Y < 0.5 ? "up" : "down";
                            }
                            break;

                        case EnumAxis.Z:
                            if (Math.Abs(blockSel.HitPosition.X - 0.5) > Math.Abs(blockSel.HitPosition.Y - 0.5))
                            {
                                v = blockSel.HitPosition.X < 0.5 ? "left" : "right";
                            }
                            else
                            {
                                v = blockSel.HitPosition.Y < 0.5 ? "up" : "down";
                            }
                            break;
                    }
                }
                else
                {
                    v = blockSel.HitPosition.Y < 0.5 ? "up" : "down";
                }

                if (rotateH && rotateV)
                {
                    blockCode = block.CodeWithParts(v, h);
                }
                else if (rotateH)
                {
                    blockCode = block.CodeWithParts(h);
                }
                else if (rotateV)
                {
                    blockCode = block.CodeWithParts(v);
                }
            }

            if (blockCode == null)
            {
                blockCode = this.block.Code;
            }

            Block orientedBlock = world.BlockAccessor.GetBlock(blockCode);
            if (orientedBlock.IsSuitablePosition(world, blockSel.Position, ref failureCode))
            {
                world.BlockAccessor.SetBlock(orientedBlock.BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            ItemStack[] drops = block.GetDrops(world, pos, null);

            return drops != null && drops.Length > 0 ? drops[0] : new ItemStack(block);
        }


        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handling)
        {
            BlockFacing curFacing = BlockFacing.FromCode(block.LastCodePart());
            if (curFacing.IsVertical) return block.Code;

            handling = EnumHandling.PreventDefault;
            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360 - angle) / 90 + curFacing.HorizontalAngleIndex) % 4];
            return block.CodeWithParts(newFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing curFacing = BlockFacing.FromCode(block.LastCodePart());
            if (curFacing.Axis == axis) return block.CodeWithParts(curFacing.GetOpposite().Code);

            curFacing = BlockFacing.FromCode(block.LastCodePart(1));
            if (curFacing != null && curFacing.Axis == axis) return block.CodeWithParts(curFacing.GetOpposite().Code, block.LastCodePart());

            return block.Code;
        }

        public override AssetLocation GetVerticallyFlippedBlockCode(ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing curFacing = BlockFacing.FromCode(block.LastCodePart());
            if (curFacing.IsVertical) return block.CodeWithParts(curFacing.GetOpposite().Code);

            curFacing = BlockFacing.FromCode(block.LastCodePart(1));
            if (curFacing != null && curFacing.IsVertical) return block.CodeWithParts(curFacing.GetOpposite().Code, block.LastCodePart());

            return block.Code;
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            rotateH = properties["rotateH"].AsBool(rotateH);
            rotateV = properties["rotateV"].AsBool(rotateV);
            rotateV4 = properties["rotateV4"].AsBool(rotateV4);
            rotateSides = properties["rotateSides"].AsBool(rotateSides);
            facing = properties["facing"].AsString(facing);
        }
    }
}
