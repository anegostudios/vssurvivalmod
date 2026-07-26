using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockAxle : BlockMPBase
    {
        public bool IsOrientedTo(BlockFacing facing)
        {
            string dirs = LastCodePart();

            return dirs[0] == facing.Code[0] || (dirs.Length > 1 && dirs[1] == facing.Code[0]);
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            return IsOrientedTo(face);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockPos originalPos = blockSel.Position.Copy();
            BlockFacing originalFace = blockSel.Face;
            bool originalDidOffset = blockSel.DidOffset;
            Block blockAtSelection = world.BlockAccessor.GetBlock(blockSel.Position);

            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                if (failureCode != "notreplaceable") return false;

                if (blockAtSelection is BlockSpurGear selectedGear)
                {
                    BlockFacing gearOrientation = BlockFacing.FromFirstLetter(selectedGear.Variant["orientation"]);
                    BlockFacing hubFace = gearOrientation.Opposite;
                    BlockPos hubAxlePos = originalPos.AddCopy(hubFace);

                    Block blockAtHub = world.BlockAccessor.GetBlock(hubAxlePos);
                    if (blockAtHub != null && blockAtHub.BlockId != 0 && !blockAtHub.IsReplacableBy(this))
                    {
                        failureCode = "notreplaceable";
                        return false;
                    }

                    string axleRotation = (hubFace == BlockFacing.NORTH || hubFace == BlockFacing.SOUTH) ? "ns"
                        : (hubFace == BlockFacing.UP || hubFace == BlockFacing.DOWN) ? "ud" : "we";
                    Block axleVariant = world.GetBlock(new AssetLocation("game", "woodenaxle-" + axleRotation));
                    if (axleVariant == null) axleVariant = world.GetBlock(new AssetLocation("woodenaxle-" + axleRotation));

                    if (axleVariant != null && !BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, axleVariant, hubAxlePos))
                    {
                        failureCode = "axlemusthavesupport";
                        return false;
                    }

                    if (axleVariant != null)
                    {
                        world.BlockAccessor.SetBlock(axleVariant.BlockId, hubAxlePos);
                    }

                    if (selectedGear.TryAddHubAxle(world, originalPos, hubFace))
                    {
                        failureCode = null;
                        return true;
                    }

                    if (axleVariant != null)
                    {
                        world.BlockAccessor.SetBlock(0, hubAxlePos);
                    }
                    return false;
                }

                if (blockAtSelection is BlockAxle selectedAxle
                    && TryPlaceAcrossAdjacentSpurGear(world, blockSel, originalPos, originalFace, originalDidOffset, selectedAxle, ref failureCode))
                {
                    return true;
                }

                return false;
            }

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                BlockPos pos = blockSel.Position.AddCopy(face);

                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                if (block != null)
                {
                    BlockFacing faceOpposite = face.Opposite;
                    if (block.HasMechPowerConnectorAt(world, pos, faceOpposite, this))
                    {
                        AssetLocation loc = CodeWithVariant("rotation", "" + faceOpposite.Code[0] + face.Code[0]);
                        Block toPlaceBlock = world.GetBlock(loc);
                        if (toPlaceBlock == null)
                        {
                            loc = CodeWithVariant("rotation", "" + face.Code[0] + faceOpposite.Code[0]);
                            toPlaceBlock = world.GetBlock(loc);
                        }

                        if (toPlaceBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
                        {
                            block.DidConnectAt(world, pos, faceOpposite);
                            WasPlaced(world, blockSel.Position, face);

                            // Test for connection on opposite side as well
                            pos = blockSel.Position.AddCopy(faceOpposite);
                            block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                            if (block != null && block.HasMechPowerConnectorAt(world, pos, face, this))
                            {
                                block.DidConnectAt(world, pos, face);
                                WasPlaced(world, blockSel.Position, faceOpposite);
                            }

                            return true;
                        }
                    }
                }
            }


            if (base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
            {
                WasPlaced(world, blockSel.Position, null);
                return true;
            }
            return false;
        }

        bool TryPlaceAcrossAdjacentSpurGear(IWorldAccessor world, BlockSelection blockSel, BlockPos originalPos, BlockFacing originalFace, bool originalDidOffset, BlockAxle selectedAxle, ref string failureCode)
        {
            foreach (BlockFacing faceToGear in BlockFacing.ALLFACES)
            {
                BlockPos gearPos = originalPos.AddCopy(faceToGear);
                if (!(world.BlockAccessor.GetBlock(gearPos) is BlockSpurGear gearBlock)) continue;

                BlockFacing axleFaceFromGear = faceToGear.Opposite;
                BlockFacing gearOrientation = BlockFacing.FromFirstLetter(gearBlock.Variant["orientation"]);
                if (axleFaceFromGear != gearOrientation && axleFaceFromGear != gearOrientation.Opposite) continue;

                if (!selectedAxle.HasMechPowerConnectorAt(world, originalPos, faceToGear, gearBlock)) continue;
                if (!gearBlock.HasMechPowerConnectorAt(world, gearPos, axleFaceFromGear, selectedAxle)) continue;

                if (gearBlock.IsHubAxleFace(axleFaceFromGear) && gearBlock.TryAddHubAxle(world, gearPos, axleFaceFromGear))
                {
                    failureCode = null;
                    return true;
                }
            }

            blockSel.Position = originalPos;
            blockSel.Face = originalFace;
            blockSel.DidOffset = originalDidOffset;
            return false;
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BEBehaviorMPAxle bempaxle = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAxle>();
            if (bempaxle != null && !BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, bempaxle.Block, pos))
            {
                bool connected = false;
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    BlockPos npos = pos.AddCopy(face);
                    IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;
                    bool prevConnected = connected;
                    if (block != null && block.HasMechPowerConnectorAt(world, pos, face.Opposite, this) && world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>()?.disconnected == false) connected = true;
                    BlockAngledGears blockagears = block as BlockAngledGears;
                    if (blockagears == null) continue;
                    if (blockagears.Facings.Contains(face.Opposite) && blockagears.Facings.Length == 1)
                    {
                        world.BlockAccessor.BreakBlock(npos, null);
                        connected = prevConnected;  //undo connected = true in this situation
                    }
                }
                if (!connected)
                {
                    world.BlockAccessor.BreakBlock(pos, null);
                }
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }


        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            
        }
    }
}
