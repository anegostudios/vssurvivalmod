using System;
using System.Collections.Generic;
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

    public class BEBehaviorMPAxle : BEBehaviorMPBase, IRotatable
    {
        BlockFacing[] orients = new BlockFacing[2];
        ICoreClientAPI capi;
        string orientations;

        // Spur gear discs mounted on the axle ends.
        readonly HostedSpurGears hostedGears = new("spurGear");

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
                axleStandLocWest = BlockAxle.GetStandShapeLocation(Block, false);
                axleStandLocEast = BlockAxle.GetStandShapeLocation(Block, true);
            }


            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
            }
        }

        // Network discovery can reach a neighbouring axle before Initialize runs.
        public override void SetOrientations()
        {
            setAxleAxis(BlockFacing.FromFirstLetter(Block.Variant["rotation"][0]).Axis);
        }

        protected void setAxleAxis(EnumAxis axis)
        {
            switch (axis)
            {
                case EnumAxis.X:
                    orientations = "we";
                    AxisSign = [-1, 0, 0];
                    orients[0] = BlockFacing.WEST;
                    orients[1] = BlockFacing.EAST;
                    break;

                case EnumAxis.Y:
                    orientations = "ud";
                    AxisSign = [0, 1, 0];
                    orients[0] = BlockFacing.DOWN;
                    orients[1] = BlockFacing.UP;
                    break;

                case EnumAxis.Z:
                    orientations = "ns";
                    AxisSign = [0, 0, -1];
                    orients[0] = BlockFacing.NORTH;
                    orients[1] = BlockFacing.SOUTH;
                    break;
            }
        }

        public override float GetResistance()
        {
            return 0.0005f;
        }

        public override float GetRotationalInertia()
        {
            float inertia = base.GetRotationalInertia();
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                BlockSpurGear gear = hostedGears.GetDiscBlock(face);
                if (gear != null) inertia += gear.RotationalInertia;
            }
            return inertia;
        }

        public int SpurGearFaceCount => hostedGears.Count;

        public bool HasSpurGear(BlockFacing face) => hostedGears.Has(face);

        public bool IsAxialFace(BlockFacing face)
        {
            return face != null && (face == orients[0] || face == orients[1]);
        }

        public bool TryAddSpurGear(BlockFacing face, ItemStack gearStack, ref string failureCode)
        {
            if (face == null || gearStack?.Block is not BlockSpurGear gearBlock || !IsAxialFace(face))
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (Block is BlockMPBase axleBlock && !axleBlock.HasCompatibleConnector(gearBlock))
            {
                failureCode = "incompatibleconnector";
                return false;
            }

            if (Block is BlockAxleAttachmentAssembly assembly && !assembly.CanHostSpurGear(face, gearBlock.PitchRadius))
            {
                failureCode = "notenoughspace";
                return false;
            }

            if (!hostedGears.TryAdd(face, gearStack))
            {
                failureCode = "notreplaceable";
                return false;
            }

            updateShape(Api.World);
            Blockentity.MarkDirty(true);
            Api.World.BlockAccessor.MarkBlockDirty(Position);
            return true;
        }

        // The axle-hosted disc face that meshes with the given free-standing spur gear.
        public BlockFacing GetHostedMeshingFace(IWorldAccessor world, BlockPos gearPos, BlockSpurGear gear)
        {
            BEBehaviorMPSpurGear gearBeh = world.BlockAccessor.GetBlockEntity(gearPos)?.GetBehavior<BEBehaviorMPSpurGear>();
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                float radius = gearBeh?.GetExternalSpurRadius(face) ?? (gear.HasGearFacing(face) ? gear.PitchRadius : 0f);
                int teeth = gearBeh?.GetExternalSpurToothCount(face) ?? (gear.HasGearFacing(face) ? gear.ToothCount : 0);
                if (CanHostedGearMesh(face, radius, teeth)) return face;
            }

            return null;
        }

        // Checks whether the hosted disc meshes with the given pitch radius.
        public bool CanHostedGearMesh(BlockFacing gearFace, float otherPitchRadius)
        {
            return hostedGears.Has(gearFace) && BlockSpurGear.CanMesh(hostedGears.GetRadius(gearFace), otherPitchRadius);
        }

        public bool CanHostedGearMesh(BlockFacing gearFace, float otherPitchRadius, int otherToothCount)
        {
            return hostedGears.Has(gearFace) && BlockSpurGear.CanMesh(hostedGears.GetRadius(gearFace), hostedGears.GetToothCount(gearFace), otherPitchRadius, otherToothCount);
        }

        // Two end gears already connect through their own blocks, so this only bridges a hosted gear.
        public BlockFacing GetAxleMeshingFace(IWorldAccessor world, BEBehaviorMPAxle neighbour, BlockFacing radialFace)
        {
            return tryGetAxleSpurMesh(world, neighbour, radialFace, out BlockFacing meshEnd, out _, out _) ? meshEnd : null;
        }

        bool tryGetAxleSpurMesh(IWorldAccessor world, BEBehaviorMPAxle neighbour, BlockFacing radialFace, out BlockFacing meshEnd, out int ownTeeth, out int neighbourTeeth)
        {
            meshEnd = null;
            ownTeeth = 0;
            neighbourTeeth = 0;
            if (neighbour == null || radialFace == null || IsAxialFace(radialFace)) return false;
            if (neighbour.orientations != orientations || disconnected || neighbour.disconnected) return false;

            foreach (BlockFacing end in orients)
            {
                if (!tryGetSpurMeshAtEnd(world, neighbour, end, out ownTeeth, out neighbourTeeth)) continue;

                meshEnd = end;
                return true;
            }

            return false;
        }

        bool tryGetSpurMeshAtEnd(IWorldAccessor world, BEBehaviorMPAxle neighbour, BlockFacing end, out int ownTeeth, out int neighbourTeeth)
        {
            getSpurAtEnd(world, end, out ownTeeth, out float ownRadius, out bool ownHosted);
            neighbour.getSpurAtEnd(world, end, out neighbourTeeth, out float neighbourRadius, out bool neighbourHosted);
            if (ownTeeth <= 0 || neighbourTeeth <= 0 || !ownHosted || !neighbourHosted) return false;
            return BlockSpurGear.CanMesh(ownRadius, ownTeeth, neighbourRadius, neighbourTeeth);
        }

        void getSpurAtEnd(IWorldAccessor world, BlockFacing end, out int teeth, out float radius, out bool hosted)
        {
            teeth = hostedGears.GetToothCount(end);
            radius = hostedGears.GetRadius(end);
            hosted = teeth > 0;
            if (hosted) return;

            var gearPos = new FastBlockPos(Position).Offset(end);
            IBlockAccessor blockAccessor = world.BlockAccessor;
            BlockEntity gearBe = blockAccessor.GetBlockEntity(gearPos);
            Block block = gearBe?.Block ?? blockAccessor.GetBlock(gearPos);
            if (block is not BlockSpurGear gear) return;

            BlockFacing gearFace = end.Opposite;
            BEBehaviorMPSpurGear gearBeh = gearBe?.GetBehavior<BEBehaviorMPSpurGear>();
            if (gearBeh != null)
            {
                gearBeh.TryGetExternalSpur(gearFace, out teeth, out radius);
            }
            else if (gear.HasGearFacing(gearFace))
            {
                teeth = gear.ToothCount;
                radius = gear.PitchRadius;
            }
        }

        public int GetHostedSpurToothCount(BlockFacing face) => hostedGears.GetToothCount(face);

        public float GetHostedSpurRadius(BlockFacing face) => hostedGears.GetRadius(face);

        public ItemStack[] GetHostedSpurDrops() => hostedGears.GetDrops();

        public void GetHostedMaterialUvOffset(ICoreClientAPI capi, BlockFacing face, string textureCode, Vec2f offset) => hostedGears.GetMaterialUvOffset(capi, face, textureCode, offset);

        public void RefreshPowerConnections(BlockFacing preferredFace = null)
        {
            if (Network != null)
            {
                manager?.OnNodeRemoved(this);
                return;
            }

            List<BlockFacing> faces = [];
            if (preferredFace != null)
            {
                faces.AddIfNotPresent(preferredFace);
                faces.AddIfNotPresent(preferredFace.Opposite);
            }
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (IsAxialFace(face)) faces.AddIfNotPresent(face);
            }
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (!IsAxialFace(face)) faces.AddIfNotPresent(face);
            }

            foreach (BlockFacing face in faces)
            {
                if (tryConnect(face)) break;
            }
        }

        public override void SetPropagationDirection(MechPowerPath path)
        {
            if (path?.FromPos != null)
            {
                BlockFacing face = path.FromPos.FacingFrom(Position);
                if (!IsAxialFace(face))
                {
                    IWorldAccessor world = Api.World;
                    IBlockAccessor blockAccessor = world.BlockAccessor;
                    BlockEntity neighbourBe = blockAccessor.GetBlockEntity(path.FromPos);
                    Block block = neighbourBe?.Block ?? blockAccessor.GetBlock(path.FromPos);
                    if (block is BlockSpurGear neighGear)
                    {
                        BlockFacing hostedFace = GetHostedMeshingFace(world, path.FromPos, neighGear);
                        float hostedTeeth = GetHostedSpurToothCount(hostedFace);
                        if (hostedTeeth > 0)
                        {
                            BEBehaviorMPSpurGear neighBeh = neighbourBe?.GetBehavior<BEBehaviorMPSpurGear>();
                            int neighTeeth = neighBeh?.GetExternalSpurToothCount(hostedFace) ?? neighGear.ToothCount;
                            if (neighTeeth > 0) path.gearingRatio *= neighTeeth / hostedTeeth;
                        }
                    }
                    else if (block is BlockAxle
                        && neighbourBe?.GetBehavior<BEBehaviorMPAxle>() is BEBehaviorMPAxle neighAxle)
                    {
                        int myTeeth = 0;
                        int neighTeeth = 0;
                        bool meshed = path.spurGearFace != null
                            && tryGetSpurMeshAtEnd(world, neighAxle, path.spurGearFace, out myTeeth, out neighTeeth);
                        if (!meshed)
                        {
                            meshed = tryGetAxleSpurMesh(world, neighAxle, face, out _, out myTeeth, out neighTeeth);
                        }
                        if (meshed)
                        {
                            path.gearingRatio *= neighTeeth / (float)myTeeth;
                        }
                    }
                }
            }

            base.SetPropagationDirection(path);
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath entryDir)
        {
            List<MechPowerPath> paths = [];

            if (entryDir?.OutFacing != null && IsAxialFace(entryDir.OutFacing))
            {
                paths.Add(entryDir);
                paths.Add(entryDir.PropagatedClone(entryDir.OutFacing.Opposite, !entryDir.invert, fromPos: Position));
            }
            else
            {
                BlockFacing turnDir = propagationDir;
                if (turnDir == null || !IsAxialFace(turnDir)) turnDir = orients[0];

                paths.Add(new MechPowerPath(turnDir, entryDir?.gearingRatio ?? GearedRatio, Position, false, entryDir?.AngleTransform ?? AngleTransform));
                paths.Add(new MechPowerPath(turnDir.Opposite, entryDir?.gearingRatio ?? GearedRatio, Position, true, entryDir?.AngleTransform ?? AngleTransform));
            }

            BlockFacing entrySide = entryDir?.OutFacing?.Opposite;
            BlockFacing entrySpurFace = entryDir?.spurGearFace;
            IWorldAccessor world = Api.World;
            IBlockAccessor blockAccessor = world.BlockAccessor;
            BlockPos npos = Position.Copy();
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                face.IterateThruFacingOffsets(npos);
                if (IsAxialFace(face) || (face == entrySide && entrySpurFace == null)) continue;

                BlockEntity blockEntity = blockAccessor.GetBlockEntity(npos);
                Block block = blockEntity?.Block ?? blockAccessor.GetBlock(npos);
                if (block is BlockSpurGear gear)
                {
                    BEBehaviorMPSpurGear other = blockEntity?.GetBehavior<BEBehaviorMPSpurGear>();
                    foreach (BlockFacing hostedFace in orients)
                    {
                        if (face == entrySide && hostedFace == entrySpurFace) continue;
                        if (!hostedGears.Has(hostedFace)) continue;

                        int otherTeeth = 0;
                        float otherRadius = 0f;
                        if (other != null)
                        {
                            other.TryGetExternalSpur(hostedFace, out otherTeeth, out otherRadius);
                        }
                        else if (gear.HasGearFacing(hostedFace))
                        {
                            otherTeeth = gear.ToothCount;
                            otherRadius = gear.PitchRadius;
                        }
                        int ownTeeth = hostedGears.GetToothCount(hostedFace);
                        if (!BlockSpurGear.CanMesh(hostedGears.GetRadius(hostedFace), ownTeeth, otherRadius, otherTeeth)) continue;

                        float ratio = ownTeeth / (float)otherTeeth;
                        paths.Add(createMeshedPath(face, ratio, entryDir, hostedFace));
                    }
                }
                else if (block is BlockAxle
                    && face != entrySide
                    && blockEntity?.GetBehavior<BEBehaviorMPAxle>() is BEBehaviorMPAxle neighAxle
                    && neighAxle.orientations == orientations && !neighAxle.disconnected)
                {
                    foreach (BlockFacing meshEnd in orients)
                    {
                        if (!tryGetSpurMeshAtEnd(world, neighAxle, meshEnd, out int ownTeeth, out int neighbourTeeth)) continue;

                        float ratio = ownTeeth / (float)neighbourTeeth;
                        paths.Add(createMeshedPath(face, ratio, entryDir, meshEnd));
                    }
                }
            }

            return paths.ToArray();
        }

        MechPowerPath createMeshedPath(BlockFacing outFacing, float ratio, MechPowerPath entryDir, BlockFacing meshingFace)
        {
            return new MechPowerPath(outFacing, (entryDir?.gearingRatio ?? GearedRatio) * ratio, null, !(entryDir?.invert ?? false),
                entryDir?.AngleTransform ?? AngleTransform, propagationDir?.Opposite, meshingFace);
        }

        public override BlockFacing GetPropagatingTurnDir(BlockFacing toFacing)
        {
            if (toFacing != null && !IsAxialFace(toFacing)) return propagationDir?.Opposite;
            return propagationDir;
        }

        protected override CompositeShape GetShape()
        {
            return getAxleShape(Api.World);
        }

        protected override void updateShape(IWorldAccessor worldForResolve)
        {
            Shape = getAxleShape(worldForResolve);
        }

        protected CompositeShape getAxleShape(IWorldAccessor world)
        {
            CompositeShape baseShape = axleshape ?? base.GetShape();
            if (hostedGears.Count == 0) return baseShape;

            CompositeShape shape = baseShape.Clone();
            List<CompositeShape> overlays = [];
            if (shape.Overlays != null) overlays.AddRange(shape.Overlays);

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                CompositeShape gearShape = hostedGears.GetFaceShape(world, face, getHostedDiscPhaseDeg(face));
                if (gearShape != null) overlays.Add(gearShape);
            }

            shape.Overlays = overlays.Count == 0 ? null : overlays.ToArray();
            return shape;
        }

        float getHostedDiscPhaseDeg(BlockFacing face)
        {
            int teeth = hostedGears.GetToothCount(face);
            if (teeth <= 0) return 0f;

            float meshPhase = hostedGears.GetDiscBlock(face)?.MeshPhaseDeg ?? 0f;
            return 180f / teeth * (Position.X % 32 + Position.Y % 32 + Position.Z % 32) + meshPhase;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            hostedGears.FromTreeAttributes(tree, worldAccessForResolve);
            updateShape(worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            hostedGears.ToTreeAttributes(tree);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            hostedGears.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, resolveImports);
            updateShape(worldForNewMappings);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            hostedGears.OnStoreCollectibleMappings(Api.World, blockIdMapping, itemIdMapping);
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            hostedGears.OnTransformed(tree, degreeRotation, flipAxis);
        }


        protected virtual MeshData getStandMesh(string orient)
        {
            if (axleStandLocWest == null) return null;

            BlockMPBase axleBlock = Block as BlockMPBase;
            ItemStack stack = PlacedStack;
            string materialKey = axleBlock?.GetMaterialKey(stack);
            string cacheKey = materialKey == null ? $"{Block.Code}-{orient}-stand" : $"{Block.Code}-{orient}-stand-{materialKey}";
            return ObjectCacheUtil.GetOrCreate(Api, cacheKey, () =>
            {
                Shape shape = API.Common.Shape.TryGet(capi, orient == "west" ? axleStandLocWest : axleStandLocEast);
                MeshData mesh;
                if (materialKey == null)
                {
                    capi.Tesselator.TesselateShape(Block, shape, out mesh);
                }
                else
                {
                    string sourceForLogging = $"{Block.Code} stand";
                    capi.Tesselator.TesselateShape(sourceForLogging, shape, out mesh, axleBlock.GetMaterialTextureSource(capi, shape, sourceForLogging, stack));
                }
                return mesh;
            });
        }

        public static bool IsAttachedToBlock(IBlockAccessor blockaccessor, Block block, BlockPos Position)
        {
            if (block is BlockAxleAttachmentAssembly) return true;
            if (block is not BlockAxle axle) return false;

            BEBehaviorMPAxle behavior = blockaccessor.GetBlockEntity(Position)?.GetBehavior<BEBehaviorMPAxle>();
            BlockFacing end = BlockFacing.FromFirstLetter(block.Variant["rotation"][0]);
            return axle.GetStandSupportFace(blockaccessor, Position, end, behavior?.HasSpurGear(end) == true) != null
                || axle.GetStandSupportFace(blockaccessor, Position, end.Opposite, behavior?.HasSpurGear(end.Opposite) == true) != null;
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

                    mesh = rotStand(mesh, orients[0]);
                    if (mesh != null) mesher.AddMeshData(mesh);
                }

                if (RequiresStand(Api.World, Position, orients[1].Normali))
                {
                    // Add east stand
                    MeshData mesh = getStandMesh("east");
                    mesh = rotStand(mesh, orients[1]);
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
                if (this.Blockentity.GetBehavior<BlockEntityBehaviorCoverable>().WallStack != null) return false;

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

                // An axle with another connector never joins this one, so this end keeps its own stand.
                if (!block.HasCompatibleConnector(Block as BlockMPBase)) return true;
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

        private MeshData rotStand(MeshData mesh, BlockFacing axleEnd)
        {
            if (mesh == null || Block is not BlockAxle axle) return null;

            bool hasSpurGear = HasSpurGear(axleEnd);
            BlockFacing face = axle.GetStandSupportFace(Api.World.BlockAccessor, Position, axleEnd, hasSpurGear);
            if (face == null) return null;

            MeshData stand = mesh.Clone().MatrixTransform(axle.GetStandTransform(face));
            if (hasSpurGear)
            {
                Vec3i n = axleEnd.Normali;
                stand.Translate(-n.X * axle.StandGearOffset, -n.Y * axle.StandGearOffset, -n.Z * axle.StandGearOffset);
            }
            return stand;
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
