﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockFenceGate : BlockBaseDoor
    {

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            CanStep = false;
        }

        public override string GetKnobOrientation()
        {
            return GetKnobOrientation(this);
        }

        public override BlockFacing GetDirection()
        {
            return BlockFacing.FromFirstLetter(Variant["type"]);
        }

        public string GetKnobOrientation(Block block)
        {
            return Variant["knobOrientation"];
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);

                string face = (horVer[0] == BlockFacing.NORTH || horVer[0] == BlockFacing.SOUTH) ? "n" : "w";

                string knobOrientation = GetSuggestedKnobOrientation(world.BlockAccessor, blockSel.Position, horVer[0], out bool neighbourOpen);
                AssetLocation newCode = CodeWithVariants(new string[] { "type", "state", "knobOrientation" }, new string[] { face, neighbourOpen ? "opened" : "closed", knobOrientation });

                world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(newCode).BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        private string GetSuggestedKnobOrientation(IBlockAccessor ba, BlockPos pos, BlockFacing facing, out bool neighbourOpen)
        {
            string leftOrRight = "left";

            Block nBlock1 = ba.GetBlock(pos.AddCopy(facing.GetCW()));
            Block nBlock2 = ba.GetBlock(pos.AddCopy(facing.GetCCW()));
            bool invert = facing == BlockFacing.EAST || facing == BlockFacing.SOUTH;   //doors and gates assets are setup for n or w orientations

            bool isDoor1 = IsSameDoor(nBlock1);
            bool isDoor2 = IsSameDoor(nBlock2);
            if (isDoor1 && isDoor2)
            {
                leftOrRight = "left";
                neighbourOpen = (nBlock1 as BlockBaseDoor).IsOpened();
            }
            else
            {
                if (isDoor1)
                {
                    if (GetKnobOrientation(nBlock1) == "right")   //facing gate: the gate to the right, has its hinge away from us
                    {
                        leftOrRight = invert ? "left" : "right";
                        neighbourOpen = false;
                    }
                    else
                    {
                        leftOrRight = invert ? "right" : "left";
                        neighbourOpen = (nBlock1 as BlockBaseDoor).IsOpened();
                    }
                }
                else if (isDoor2)
                {
                    if (GetKnobOrientation(nBlock2) == "right")   //facing gate: the gate to the left, has its hinge towards us ("knob" is hinge apparently)
                    {
                        leftOrRight = invert ? "right" : "left";
                        neighbourOpen = false;
                    }
                    else
                    {
                        leftOrRight = invert ? "left" : "right";
                        neighbourOpen = (nBlock2 as BlockBaseDoor).IsOpened();
                    }
                }
                else
                {
                    //no neighbouring gate, but some AI for trying to guess which side the player wants the hinge (including potentially the first gate to be placed of a double gate)
                    neighbourOpen = false;
                    if (nBlock1.Attributes?.IsTrue("isFence") == true ^ nBlock2.Attributes?.IsTrue("isFence") == true)
                    {
                        leftOrRight = (invert ^ nBlock2.Attributes?.IsTrue("isFence") == true) ? "left" : "right";
                    }
                    else if (nBlock2.Replaceable >= 6000 && nBlock1.Replaceable < 6000)  //empty space on left, place hinge on right
                    {
                        leftOrRight = invert ? "left" : "right";
                    }
                    else if (nBlock1.Replaceable >= 6000 && nBlock2.Replaceable < 6000)  //empty space on right, place hinge on left
                    {
                        leftOrRight = invert ? "right" : "left";
                    }
                }
            }
            return leftOrRight;
        }

        protected override void Open(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
        {
            AssetLocation newCode = CodeWithVariant("state", IsOpened() ? "closed" : "opened");

            world.BlockAccessor.ExchangeBlock(world.BlockAccessor.GetBlock(newCode).BlockId, pos);
        }

        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs)
        {
            if (activationArgs != null && activationArgs.HasAttribute("opened")) 
            {
                if (activationArgs.GetBool("opened") == IsOpened()) return;   // do nothing if already in the required state
            }
            Open(world, caller.Player, blockSel.Position);
        }

        protected override BlockPos TryGetConnectedDoorPos(BlockPos pos)
        {
            string knob = GetKnobOrientation();
            BlockFacing dir = GetDirection();
            return knob == "right" ? pos.AddCopy(dir.GetCCW()) : pos.AddCopy(dir.GetCW());
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariants(new string[] { "type", "state", "knobOrientation", "cover" }, new string[] { "n", "closed", "left", "free" }));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariants(new string[] { "type", "state", "knobOrientation", "cover" }, new string[] { "n", "closed", "left", "free" }));
            return new ItemStack(block);
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing nowFacing = BlockFacing.FromFirstLetter(Variant["type"]);
            BlockFacing rotatedFacing = BlockFacing.HORIZONTALS_ANGLEORDER[(nowFacing.HorizontalAngleIndex + angle / 90) % 4];

            string prevType = Variant["type"];
            string newType = prevType;
            if (nowFacing.Axis != rotatedFacing.Axis)
            {
                newType = (prevType == "n" ? "w" : "n");
            }

            var knowbOr = Variant["knobOrientation"];
            if (prevType == "n" && newType == "w" && knowbOr == "right" && angle == 90)
            {
                knowbOr = "left";
            }
            else if (prevType == "n" && newType == "w" && knowbOr == "left" && angle == 90)
            {
                knowbOr = "right";
            }
            else if (prevType == "w" && newType == "n" && knowbOr == "right" && angle == 270)
            {
                knowbOr = "left";
            }
            else if (prevType == "w" && newType == "n" && knowbOr == "left" && angle == 270)
            {
                knowbOr = "right";
            }

            return CodeWithVariants(new string[] { "type", "knobOrientation" }, new string[] { newType , knowbOr});
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }
    }
}
