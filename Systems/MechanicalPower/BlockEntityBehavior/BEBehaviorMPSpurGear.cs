using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPSpurGear : BEBehaviorMPBase
    {
        public BlockFacing Facing => BlockFacing.FromFirstLetter(Block.Variant["orientation"]);

        float angleOffset;
        int discFaces;

        public int DiscFaces => discFaces;

        public int DiscCount
        {
            get
            {
                int count = 0;
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    if (HasDisc(face)) count++;
                }

                return count;
            }
        }

        public bool HasDisc(BlockFacing face)
        {
            return face != null && (discFaces & (1 << face.Index)) != 0;
        }

        public void SetDisc(BlockFacing face, bool enabled)
        {
            if (face == null) return;

            int bit = 1 << face.Index;
            int next = enabled ? discFaces | bit : discFaces & ~bit;
            if (next == discFaces) return;

            discFaces = next;
            Blockentity.MarkDirty(true);
        }

        public bool IsMultiBlock => IsMultiVariant();

        bool IsHubVariant()
        {
            return Block?.Code?.PathStartsWith("spurgearhub-") == true;
        }

        bool IsMultiVariant()
        {
            return Block?.Code?.PathStartsWith("spurgearmulti-") == true;
        }

        public override float AngleRad
        {
            get
            {
                if (!IsHubVariant()) return base.AngleRad + angleOffset;

                MechanicalNetwork net = Network;
                if (net == null) return base.AngleRad + angleOffset;

                float a = (net.AngleRad * GearedRatio) % GameMath.TWOPI;
                return IsRotationReversed() ? GameMath.TWOPI - a : a;
            }
        }

        public BEBehaviorMPSpurGear(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            // Makes it correct in most cases. Whoever reads this - feel free to make it perfect
            angleOffset = 11.25f * GameMath.DEG2RAD * (Pos.X % 32 + Pos.Y % 32 + Pos.Z % 32);

            AxisSign = new int[3] { 0, 0, 0 };
            switch (Facing.Index)
            {
                case 0: // N
                    AxisSign[2] = -1;
                    break;
                case 1: // E
                    AxisSign[0] = -1;
                    break;
                case 2: // S
                    AxisSign[2] = -1;
                    break;
                case 3: // W
                    AxisSign[0] = -1;
                    break;
                case 4: // U
                    AxisSign[1] = 1;
                    break;
                case 5: // D
                    AxisSign[1] = 1;
                    break;
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            discFaces = tree.GetInt("discFaces", 0);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("discFaces", discFaces);
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath entryDir)
        {
            if (IsMultiVariant())
            {
                // Derive every exit's sense from the entry path's true rotation sense
                // (NetworkDir works for both the tryConnect and the spread calling
                // conventions), expressed via the invert flag. Co-axial discs continue
                // the shaft unchanged; perpendicular discs mesh with the entry disc at
                // the edge shared by the two faces, so their sense depends on WHICH
                // face, not just the axis (see ExitSense).
                BlockFacing netDir = entryDir.NetworkDir();

                List<MechPowerPath> multiPaths = new List<MechPowerPath>();
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    if (!HasDisc(face)) continue;

                    if (face.Axis == netDir.Axis)
                    {
                        multiPaths.Add(new MechPowerPath(face, entryDir.gearingRatio, Position, face != netDir));
                    }
                    else
                    {
                        BlockFacing exitSense = ExitSense(face, netDir);
                        multiPaths.Add(new MechPowerPath(face, entryDir.gearingRatio, null, exitSense == face.Opposite));
                    }
                }

                return multiPaths.ToArray();
            }

            BlockFacing left, right, above, below;

            if (Facing.IsHorizontal)
            {
                left = entryDir.OutFacing.Opposite == Facing ? entryDir.OutFacing.GetCW() : Facing.GetCW();
                right = entryDir.OutFacing.Opposite == Facing ? entryDir.OutFacing.GetCCW() : Facing.GetCCW();
                above = BlockFacing.UP;
                below = BlockFacing.DOWN;
            }
            else
            {
                left = BlockFacing.WEST;
                right = BlockFacing.EAST;
                above = BlockFacing.NORTH;
                below = BlockFacing.SOUTH;
            }

            BlockPos tmpPos = Pos.Copy();
            bool doLeft = IsMatchingSpurGear(Api.World.BlockAccessor.GetBlock(tmpPos.Add(left)));
            bool doRight = IsMatchingSpurGear(Api.World.BlockAccessor.GetBlock(tmpPos.Set(Pos).Add(right)));
            bool doAbove = IsMatchingSpurGear(Api.World.BlockAccessor.GetBlock(tmpPos.Set(Pos).Add(above)));
            bool doBelow = IsMatchingSpurGear(Api.World.BlockAccessor.GetBlock(tmpPos.Set(Pos).Add(below)));

            SmallBoolArray bools = new SmallBoolArray();
            bools[0] = doLeft;
            bools[1] = doRight;
            bools[2] = doAbove;
            bools[3] = doBelow;

            MechPowerPath axial = entryDir.OutFacing.Opposite == Facing ? entryDir : entryDir.PropagatedClone(Facing, entryDir.invert, propagationDir);

            MechPowerPath side = null;
            if (bools == 1) side = entryDir.PropagatedClone(left, !entryDir.invert, propagationDir.Opposite);
            if (bools == 2) side = entryDir.PropagatedClone(right, !entryDir.invert, propagationDir.Opposite);
            if (bools == 4) side = entryDir.PropagatedClone(above, !entryDir.invert, propagationDir.Opposite);
            if (bools == 8) side = entryDir.PropagatedClone(below, !entryDir.invert, propagationDir.Opposite);

            if (!IsHubVariant())
            {
                if (bools == 0) return new MechPowerPath[] { axial };
                if (side != null) return new MechPowerPath[] { axial, side };

                List<MechPowerPath> plainPaths = new List<MechPowerPath>() { axial };
                if (doLeft) plainPaths.Add(entryDir.PropagatedClone(left, !entryDir.invert, propagationDir.Opposite));
                if (doRight) plainPaths.Add(entryDir.PropagatedClone(right, !entryDir.invert, propagationDir.Opposite));
                if (doAbove) plainPaths.Add(entryDir.PropagatedClone(above, !entryDir.invert, propagationDir.Opposite));
                if (doBelow) plainPaths.Add(entryDir.PropagatedClone(below, !entryDir.invert, propagationDir.Opposite));

                return plainPaths.ToArray();
            }

            MechPowerPath throughAxle = new MechPowerPath(axial.OutFacing.Opposite, axial.gearingRatio, Position, !axial.invert);
            if (bools == 0) return new MechPowerPath[] { axial, throughAxle };
            if (side != null) return new MechPowerPath[] { axial, throughAxle, side };

            List<MechPowerPath> paths = new List<MechPowerPath>() { axial, throughAxle };
            if (doLeft) paths.Add(entryDir.PropagatedClone(left, !entryDir.invert, propagationDir.Opposite));
            if (doRight) paths.Add(entryDir.PropagatedClone(right, !entryDir.invert, propagationDir.Opposite));
            if (doAbove) paths.Add(entryDir.PropagatedClone(above, !entryDir.invert, propagationDir.Opposite));
            if (doBelow) paths.Add(entryDir.PropagatedClone(below, !entryDir.invert, propagationDir.Opposite));

            return paths.ToArray();
        }

        public override BlockFacing GetPropagatingTurnDir(BlockFacing toFacing)
        {
            // For the multi variant the plain-gear answer below (propagationDir.Opposite,
            // the counter-rotation of a PARALLEL meshed gear) is the wrong sense - and
            // even the wrong axis - for through and perpendicular exits, so answer with
            // the same sense GetMechPowerExits propagates through that exit face.
            if (IsMultiVariant())
            {
                if (toFacing.Axis == propagationDir.Axis) return propagationDir;
                return ExitSense(toFacing, propagationDir);
            }

            // The hub couples the two ends of its own axis like a plain axle: no turnDir
            // remap, the invert flag handles the sense.
            if (IsHubVariant() && (toFacing == Facing || toFacing == Facing.Opposite))
            {
                return null;
            }

            // Plain spur gear: a neighbour meshing in parallel counter-rotates.
            return propagationDir.Opposite;
        }

        /// <summary>
        /// For the multi variant, a neighbour connecting on a perpendicular face computes
        /// its invert flag from this answer (tryConnect), so it must reproduce the same
        /// face-dependent sense GetMechPowerExits propagates through that face. test
        /// points from the neighbour INTO this gear, so the exit face is test.Opposite.
        /// </summary>
        public override bool IsPropagationDirection(BlockPos fromPos, BlockFacing test)
        {
            if (IsMultiVariant() && propagationDir != null && test.Axis != propagationDir.Axis)
            {
                return ExitSense(test.Opposite, propagationDir) == test;
            }

            return base.IsPropagationDirection(fromPos, test);
        }

        // ---- rotation-sense algebra for the multi variant ----
        //
        // Two perpendicular meshed discs must satisfy, at the edge shared by their two
        // faces:   spinOut = -spinIn * FaceSign(entryFace) * FaceSign(exitFace)
        // (bevel-gear kinematics: rim velocities match at the contact edge, so the sign
        // flips with the geometric side of each face).
        //
        // SpinOf maps a propagation facing to the on-screen spin sign of the axle
        // renderer for that axis, in arbitrary but mutually consistent units. The ns
        // axle renders with the opposite handedness from we/ud, hence SOUTH grouping
        // with UP/WEST. Calibrated against meshed rigs in game.

        static int SpinOf(BlockFacing facing)
        {
            return facing == BlockFacing.UP || facing == BlockFacing.SOUTH || facing == BlockFacing.WEST ? 1 : -1;
        }

        static int FaceSign(BlockFacing face)
        {
            return face == BlockFacing.EAST || face == BlockFacing.UP || face == BlockFacing.SOUTH ? 1 : -1;
        }

        static BlockFacing PositiveFacing(EnumAxis axis)
        {
            if (axis == EnumAxis.X) return BlockFacing.EAST;
            if (axis == EnumAxis.Y) return BlockFacing.UP;
            return BlockFacing.SOUTH;
        }

        static BlockFacing FacingWithSpin(EnumAxis axis, int spin)
        {
            BlockFacing positive = PositiveFacing(axis);
            return SpinOf(positive) == spin ? positive : positive.Opposite;
        }

        /// <summary>
        /// The face whose disc feeds this gear: the single disc face on the propagation
        /// axis. With discs on both faces (through-shaft) one mesh edge is physically
        /// overconstrained whatever we pick; use the positive face so the outcome is
        /// deterministic and independent of placement order.
        /// </summary>
        BlockFacing EntryFaceOnAxis(EnumAxis axis)
        {
            BlockFacing positive = PositiveFacing(axis);
            if (HasDisc(positive) == HasDisc(positive.Opposite)) return positive;
            return HasDisc(positive) ? positive : positive.Opposite;
        }

        BlockFacing ExitSense(BlockFacing exitFace, BlockFacing netDir)
        {
            BlockFacing entryFace = EntryFaceOnAxis(netDir.Axis);
            int outSpin = -SpinOf(netDir) * FaceSign(entryFace) * FaceSign(exitFace);
            return FacingWithSpin(exitFace.Axis, outSpin);
        }

        /// <summary>
        /// Called by BlockSpurGearMulti when a disc is added or removed. A disc change on
        /// the propagation axis changes which face EntryFaceOnAxis resolves to, flipping
        /// the sense every perpendicular exit should carry - but senses already spread to
        /// neighbours are never revisited (JoinAndSpreadNetworkToNeighbours early-returns
        /// for nodes already in the network). Rebuild the network from its power source
        /// so every node re-derives its sense from the final disc set, the same recovery
        /// run when a node is removed. Keeps the outcome a function of the final
        /// configuration instead of the placement order.
        /// </summary>
        public void OnDiscsChanged(BlockFacing changedFace)
        {
            if (Api?.Side != EnumAppSide.Server || network == null) return;
            if (!IsMultiVariant()) return;
            if (changedFace.Axis != propagationDir?.Axis) return;

            manager.RebuildNetwork(network);
        }

        public override float GetResistance()
        {
            return IsMultiVariant() ? 0.0005f * DiscCount : 0.0005f;
        }

        bool IsMatchingSpurGear(Block block)
        {
            if (!(block is BlockSpurGear)) return false;
            if (block.Variant?["orientation"] != Block.Variant?["orientation"]) return false;

            string path = block.Code?.Path;
            return path != null && (path.StartsWith("spurgear-") || path.StartsWith("spurgearhub-"));
        }
    }
}
