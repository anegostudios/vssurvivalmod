#nullable disable

using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockWaterWheel : BlockWindmillRotor
    {
        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            return face == powerOutFacing || face == powerOutFacing.Opposite;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return
                base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)
                .Where((wi) => wi.ActionLangCode != "heldhelp-addsails")
                .ToArray()
            ;
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            // For water wheels we want to check there is no other wheel oriented so that it will collide with this one or overlap on the same stream blocks - but adjacent same-aligned water wheels are possible

            BlockFacing facing = GetFacingForPlacement(world, blockSel.Position, false, out bool invalid);
            if (invalid) return false;

            // facing is null if there is no adjacent MP block such as an axle, in that case we take our facing from the player's orientation
            if (facing == null)
            {
                var facings = Block.SuggestedHVOrientation(byPlayer, blockSel);
                if (facings != null && facings.Length > 0) facing = facings[0];
            }

            if (facing != null)
            {
                BlockPos pos = blockSel.Position.Copy();
                int xMin = 0;
                int xMax = 0;
                int zMin = 0;
                int zMax = 0;
                if (facing.IsAxisNS)
                {
                    xMin = -2;    // Note for now only coded for small size
                    xMax = 2;
                }
                else
                {
                    zMin = -2;    // Note for now only coded for small size
                    zMax = 2;
                }

                for (int x = xMin; x <= xMax; x++)
                {
                    for (int z = zMin; z <= zMax; z++)
                    {
                        for (int y = -2; y <= 2; y++)    // Note for now only coded for small size
                        {
                            if (x == 0 && y == 0 && z == 0) continue;

                            bool beyondRadius = Math.Abs(x) > 1 || Math.Abs(y) > 1 || Math.Abs(z) > 1;

                            // Check if any mechanical power block at all in our wheel's rotation path
                            // or if there is a rotor even 1 block beyond the rotation path but oriented like us so its blades would overlap with our blades

                            pos.Set(blockSel.Position.X + x, blockSel.Position.Y + y, blockSel.Position.Z + z);
                            if (world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Solid) is IMPPowered MPblock)
                            {
                                if (!beyondRadius || (MPblock is BlockWindmillRotor rotor && rotor.GetFacing()?.Axis == facing.Axis))
                                {
                                    failureCode = "rotationblocked";
                                    return false;
                                }
                            }

                            // Do not do the front/behind checks outside our own radius
                            if (beyondRadius) continue;

                            // Now check if in range of 90-degree angled rotor in front or behind

                            pos.Add(facing.Opposite);
                            Block b1 = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Solid);
                            if (b1 is BlockWindmillRotor rotor1 && rotor1.GetFacing()?.Axis != facing.Axis)
                            {
                                failureCode = "rotationblocked";
                                return false;
                            }

                            pos.Add(facing);
                            pos.Add(facing);
                            Block b2 = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Solid);
                            if (b2 is BlockWindmillRotor rotor2 && rotor2.GetFacing()?.Axis != facing.Axis)
                            {
                                failureCode = "rotationblocked";
                                return false;
                            }
                        }
                    }
                }
            }

            return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);
        }
    }
}
