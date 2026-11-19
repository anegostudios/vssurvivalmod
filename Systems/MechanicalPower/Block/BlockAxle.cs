using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockAxle : BlockMPBase
    {
        const float shapeSize = 16f;
        public float StandGearOffset { get; private set; } = 4f / shapeSize;

        BlockFacing[] standSupportFaces;
        float[][] standTransforms;
        Cuboidi[][] standAttachmentAreas;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            StandGearOffset = Attributes?["axleStandGearOffset"].AsFloat(StandGearOffset * shapeSize) / shapeSize ?? StandGearOffset;
            EnumAxis axis = getAxleAxis();
            standSupportFaces = axis switch
            {
                EnumAxis.X => [BlockFacing.DOWN, BlockFacing.UP, BlockFacing.NORTH, BlockFacing.SOUTH],
                EnumAxis.Z => [BlockFacing.DOWN, BlockFacing.UP, BlockFacing.EAST, BlockFacing.WEST],
                _ => BlockFacing.HORIZONTALS
            };
            standTransforms = new float[6][];
            standAttachmentAreas = new Cuboidi[4][];
            for (int i = 0; i < standAttachmentAreas.Length; i++) standAttachmentAreas[i] = new Cuboidi[6];

            Vec3f axleDir = axis switch
            {
                EnumAxis.X => BlockFacing.EAST.Normalf,
                EnumAxis.Y => BlockFacing.UP.Normalf,
                _ => BlockFacing.SOUTH.Normalf
            };
            Cuboidf westFoot = getStandFoot(api, false);
            Cuboidf eastFoot = getStandFoot(api, true);
            foreach (BlockFacing face in standSupportFaces)
            {
                Vec3f up = face.Opposite.Normalf;
                Vec3f across = axleDir.Cross(up);
                float[] transform = [
                    axleDir.X, axleDir.Y, axleDir.Z, 0,
                    up.X, up.Y, up.Z, 0,
                    across.X, across.Y, across.Z, 0,
                    0, 0, 0, 1
                ];
                Mat4f.Translate(transform, -0.5f, -0.5f, -0.5f);
                transform[12] += 0.5f;
                transform[13] += 0.5f;
                transform[14] += 0.5f;
                standTransforms[face.Index] = transform;

                for (int i = 0; i < standAttachmentAreas.Length; i++)
                {
                    Cuboidf foot = (i < 2 ? westFoot : eastFoot).TransformedCopy(transform);
                    if (i % 2 != 0)
                    {
                        float offset = i < 2 ? StandGearOffset : -StandGearOffset;
                        foot.Translate(axleDir.X * offset, axleDir.Y * offset, axleDir.Z * offset);
                    }

                    Cuboidi area = new(
                        (int)MathF.Floor(foot.X1 * shapeSize), (int)MathF.Floor(foot.Y1 * shapeSize), (int)MathF.Floor(foot.Z1 * shapeSize),
                        (int)MathF.Ceiling(foot.X2 * shapeSize) - 1, (int)MathF.Ceiling(foot.Y2 * shapeSize) - 1, (int)MathF.Ceiling(foot.Z2 * shapeSize) - 1
                    );
                    switch (face.Axis)
                    {
                        case EnumAxis.X: area.X1 = area.X2 = 0; break;
                        case EnumAxis.Y: area.Y1 = area.Y2 = 0; break;
                        case EnumAxis.Z: area.Z1 = area.Z2 = 0; break;
                    }
                    standAttachmentAreas[i][face.Index] = area;
                }
            }
        }

        public static AssetLocation GetStandShapeLocation(Block block, bool east)
        {
            string attribute = east ? "axleStandLocEast" : "axleStandLocWest";
            AssetLocation location = block.Attributes?[attribute].AsObject<AssetLocation>(null, block.Code.Domain)
                ?? AssetLocation.Create(east ? "block/wood/mechanics/axle-stand-east" : "block/wood/mechanics/axle-stand-west", block.Code.Domain);
            return location.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
        }

        Cuboidf getStandFoot(ICoreAPI api, bool east)
        {
            AssetLocation location = GetStandShapeLocation(this, east);
            string elementName = Attributes?[east ? "axleStandSupportElementEast" : "axleStandSupportElementWest"].AsString();
            return ObjectCacheUtil.GetOrCreate(api, $"axleStandFoot-{location}-{elementName}", () =>
            {
                Shape shape = API.Common.Shape.TryGet(api, location) ?? throw new InvalidOperationException($"Missing axle stand shape {location}");
                ShapeElement element = elementName == null ? shape.Elements.Single() : shape.GetElementByName(elementName);
                if (element?.From?.Length != 3 || element.To?.Length != 3)
                {
                    throw new InvalidOperationException($"Missing axle stand support element {elementName} in {location}");
                }
                return new Cuboidf(
                    (float)element.From[0] / shapeSize, (float)element.From[1] / shapeSize, (float)element.From[2] / shapeSize,
                    (float)element.To[0] / shapeSize, (float)element.From[1] / shapeSize, (float)element.To[2] / shapeSize
                );
            });
        }

        // Cached block-local transform. Callers must not modify it.
        public float[] GetStandTransform(BlockFacing face) => standTransforms[face.Index];

        // Cached face-voxel footprint. Callers must not modify it.
        public Cuboidi GetStandAttachmentArea(BlockFacing axleEnd, BlockFacing face, bool hasSpurGear)
        {
            return standAttachmentAreas[(axleEnd.Negative ? 0 : 2) + (hasSpurGear ? 1 : 0)][face.Index];
        }

        public BlockFacing GetStandSupportFace(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing axleEnd, bool hasSpurGear)
        {
            BlockPos supportPos = pos.Copy();
            foreach (BlockFacing face in standSupportFaces)
            {
                supportPos.Set(pos).Add(face);
                Block support = blockAccessor.GetBlock(supportPos, BlockLayersAccess.Solid);
                if (support.CanAttachBlockAt(blockAccessor, this, supportPos, face.Opposite, GetStandAttachmentArea(axleEnd, face, hasSpurGear))) return face;
            }
            return null;
        }

        public bool IsOrientedTo(BlockFacing facing)
        {
            string dirs = Variant["rotation"];

            return dirs[0] == facing.Code[0] || (dirs.Length > 1 && dirs[1] == facing.Code[0]);
        }

        EnumAxis getAxleAxis() => BlockFacing.FromFirstLetter(Variant["rotation"][0]).Axis;

        public BlockFacing GetAxleEndFromSelection(BlockPos axlePos, BlockSelection blockSel, BlockFacing fallback)
        {
            if (fallback != null && IsOrientedTo(fallback)) return fallback;
            if (blockSel?.HitPosition == null) return null;

            double x = blockSel.Position.X + blockSel.HitPosition.X - axlePos.X;
            double y = blockSel.Position.Y + blockSel.HitPosition.Y - axlePos.Y;
            double z = blockSel.Position.Z + blockSel.HitPosition.Z - axlePos.Z;

            BlockFacing bestFace = null;
            double bestScore = double.MinValue;
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (!IsOrientedTo(face)) continue;

                Vec3i n = face.Normali;
                double score = n.X > 0 ? x : n.X < 0 ? 1 - x : n.Y > 0 ? y : n.Y < 0 ? 1 - y : n.Z > 0 ? z : 1 - z;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestFace = face;
                }
            }

            return bestFace;
        }

        public static bool TryGetHorizontalEndPlacement(IWorldAccessor world, BlockSelection blockSel, out BlockAxle axle, out BlockPos axlePos, out BlockFacing axleEnd, out BlockSelection placement)
        {
            axle = null;
            axlePos = null;
            axleEnd = null;
            placement = blockSel;
            if (blockSel?.Face == null) return false;

            BlockPos clickedPos = blockSel.Position.AddCopy(blockSel.Face.Opposite);
            axle = world.BlockAccessor.GetBlock(clickedPos) as BlockAxle;
            if (axle == null) return false;

            axleEnd = axle.GetAxleEndFromSelection(clickedPos, blockSel, blockSel.Face);
            if (axleEnd == null || axleEnd.IsVertical)
            {
                axle = null;
                axleEnd = null;
                return false;
            }

            axlePos = clickedPos;
            placement = blockSel.Clone();
            placement.Position = clickedPos.AddCopy(axleEnd);
            return true;
        }

        static bool isHoldingSpurGear(IPlayer player)
        {
            return player?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Block is BlockSpurGear;
        }

        static BlockFacing getVerticalLargeGearRimFacing(IWorldAccessor world, BlockPos axlePos)
        {
            IBlockAccessor blockAccessor = world.BlockAccessor;
            BlockPos rimPos = axlePos.Copy();
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                face.IterateThruFacingOffsets(rimPos);
                if (blockAccessor.GetBlock(rimPos) is not BlockMPMultiblockGear) continue;

                BEMPMultiblock rim = blockAccessor.GetBlockEntity(rimPos) as BEMPMultiblock;
                if (rim?.Principal == null) continue;

                Block largeGear = blockAccessor.GetBlock(rim.Principal);
                if (largeGear is not BlockLargeGear3m) continue;

                BlockFacing stub = BlockLargeGear3m.GetStubFacing(largeGear);
                if (stub.IsVertical) continue;
                BlockFacing ringFacing = rimPos.FacingFrom(rim.Principal);
                if (face == ringFacing.Opposite && ringFacing.IsAdjacent(stub)) return face;
            }

            return null;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            // Right-click adds a gear at the end; Ctrl-right-click mounts it on the axle.
            // This stays face-independent because the HUD only rebuilds when the selection box changes.
            WorldInteraction[] gearInteractions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(world.Api, "mpAxleGearInteractions", () =>
            {
                List<ItemStack> spurStacks = [];
                List<ItemStack> allGearStacks = [];
                foreach (Block block in world.Blocks)
                {
                    if (!block.Variant.TryGetValue("orientation", out string orientation) || orientation != "s") continue;

                    if (block is BlockSpurGear)
                    {
                        ItemStack stack = new ItemStack(block);
                        spurStacks.Add(stack);
                        allGearStacks.Add(stack);
                    }
                    else if (block is BlockAngledGears)
                    {
                        allGearStacks.Add(new ItemStack(block));
                    }
                }

                return
                [
                    new WorldInteraction
                    {
                        ActionLangCode = "blockhelp-axle-addgearonaxle",
                        HotKeyCode = "ctrl",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = spurStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => isHoldingSpurGear((world.Api as ICoreClientAPI)?.World.Player) ? wi.Itemstacks : null
                    },
                    new WorldInteraction
                    {
                        ActionLangCode = "blockhelp-axle-addgear",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = allGearStacks.ToArray()
                    }
                ];
            });

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer).Append(gearInteractions);
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            if (!HasCompatibleConnector(forBlock)) return false;

            if (forBlock is BlockSpurGear spurGear)
            {
                BlockPos gearPos = pos.AddCopy(face);
                BEBehaviorMPAxle axle = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAxle>();
                if (axle?.GetHostedMeshingFace(world, gearPos, spurGear) != null && !axle.IsAxialFace(face))
                {
                    return true;
                }

                if (!IsOrientedTo(face)) return false;

                if (spurGear.HasGearFacingAt(world, gearPos, face.Opposite)) return true;

                // A spur with a through-shaft passes power along its axis.
                return world.BlockAccessor.GetBlockEntity(gearPos)?.GetBehavior<BEBehaviorMPSpurGear>()?.HasThroughShaft == true;
            }

            // Parallel axles with meshing hosted gears connect across the shared face.
            if (forBlock is BlockAxle)
            {
                BEBehaviorMPAxle axle = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAxle>();
                BEBehaviorMPAxle neighAxle = world.BlockAccessor.GetBlockEntity(pos.AddCopy(face))?.GetBehavior<BEBehaviorMPAxle>();
                if (axle?.GetAxleMeshingFace(world, neighAxle, face) != null) return true;
            }

            return IsOrientedTo(face);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Cuboidf[] baseBoxes = base.GetSelectionBoxes(blockAccessor, pos);
            BEBehaviorMPAxle axle = blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAxle>();

            List<Cuboidf> boxes = [];
            if (isHoldingSpurGear((api as ICoreClientAPI)?.World.Player))
            {
                // Split the axle so each half selects its nearest end.
                EnumAxis axis = getAxleAxis();
                foreach (Cuboidf box in baseBoxes) splitAlongAxis(box, axis, boxes);
            }
            else
            {
                boxes.AddRange(baseBoxes);
            }

            if (axle != null)
            {
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    if (axle.HasSpurGear(face)) boxes.Add(BlockSpurGear.GetSelectionBox(face, axle.GetHostedSpurRadius(face)));
                }
            }
            return boxes.ToArray();
        }

        static void splitAlongAxis(Cuboidf b, EnumAxis axis, List<Cuboidf> outBoxes)
        {
            if (axis == EnumAxis.X && b.X1 < 0.5f && b.X2 > 0.5f)
            {
                outBoxes.Add(new Cuboidf(b.X1, b.Y1, b.Z1, 0.5f, b.Y2, b.Z2));
                outBoxes.Add(new Cuboidf(0.5f, b.Y1, b.Z1, b.X2, b.Y2, b.Z2));
            }
            else if (axis == EnumAxis.Y && b.Y1 < 0.5f && b.Y2 > 0.5f)
            {
                outBoxes.Add(new Cuboidf(b.X1, b.Y1, b.Z1, b.X2, 0.5f, b.Z2));
                outBoxes.Add(new Cuboidf(b.X1, 0.5f, b.Z1, b.X2, b.Y2, b.Z2));
            }
            else if (axis == EnumAxis.Z && b.Z1 < 0.5f && b.Z2 > 0.5f)
            {
                outBoxes.Add(new Cuboidf(b.X1, b.Y1, b.Z1, b.X2, b.Y2, 0.5f));
                outBoxes.Add(new Cuboidf(b.X1, b.Y1, 0.5f, b.X2, b.Y2, b.Z2));
            }
            else outBoxes.Add(b);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Cuboidf[] baseBoxes = base.GetCollisionBoxes(blockAccessor, pos);
            BEBehaviorMPAxle axle = blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAxle>();
            if (axle == null || axle.SpurGearFaceCount == 0) return baseBoxes;

            List<Cuboidf> boxes = [..baseBoxes];
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (axle.HasSpurGear(face)) boxes.Add(BlockSpurGear.GetCollisionBox(face, axle.GetHostedSpurRadius(face)));
            }
            return boxes.ToArray();
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            List<ItemStack> drops = [..base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier)];
            BEBehaviorMPAxle axle = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAxle>();
            if (axle != null) drops.AddRange(axle.GetHostedSpurDrops());
            return drops.ToArray();
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var hotbarSlot = byPlayer?.InventoryManager?.ActiveHotbarSlot;
            if (hotbarSlot?.Itemstack?.Block is BlockAngledGears)
            {
                string failureCode = "";
                if (BlockAngledGearMulti.TryPlaceGearOnAxle(world, byPlayer, hotbarSlot.Itemstack, blockSel.Position, blockSel.Face, ref failureCode)
                    && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    hotbarSlot.TakeOut(1);
                    hotbarSlot.MarkDirty();
                }
                return true;
            }

            if (hotbarSlot?.Itemstack?.Block is BlockSpurGear)
            {
                string failureCode = "";
                bool placeHosted = byPlayer.Entity.Controls.CtrlKey;
                if (BlockSpurGear.TryPlaceGearOnAxle(world, hotbarSlot.Itemstack, blockSel.Position, blockSel.Face, ref failureCode, placeHosted, blockSel)
                    && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    hotbarSlot.TakeOut(1);
                    hotbarSlot.MarkDirty();
                }
                return true;
            }

            if (hotbarSlot?.Itemstack?.Block is BlockAxleAttachment attachment)
            {
                string failureCode = "";
                if (!attachment.TryAttachToAxle(world, byPlayer, hotbarSlot.Itemstack, this, blockSel, ref failureCode))
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, failureCode, Lang.Get($"placefailure-{failureCode}"));
                }
                else if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    hotbarSlot.TakeOut(1);
                    hotbarSlot.MarkDirty();
                }
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockFacing largeGearRimFacing = getVerticalLargeGearRimFacing(world, blockSel.Position);
            if (largeGearRimFacing != null)
            {
                string rotation = largeGearRimFacing.Axis switch
                {
                    EnumAxis.X => "we",
                    EnumAxis.Y => "ud",
                    _ => "ns"
                };
                Block toPlaceBlock = world.GetBlock(CodeWithVariant("rotation", rotation));
                if (toPlaceBlock != null && toPlaceBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
                {
                    WasPlaced(world, blockSel.Position, null);
                    return true;
                }
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
                        AssetLocation loc = CodeWithVariant("rotation", $"{faceOpposite.Code[0]}{face.Code[0]}");
                        Block toPlaceBlock = world.GetBlock(loc);
                        if (toPlaceBlock == null)
                        {
                            loc = CodeWithVariant("rotation", $"{face.Code[0]}{faceOpposite.Code[0]}");
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


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BEBehaviorMPAxle bempaxle = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAxle>();
            if (bempaxle != null && !BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, bempaxle.Block, pos))
            {
                // An unsupported axle may still be held by a mechanical connection.
                bool connected = false;
                bool unknown = false;
                BlockPos npos = pos.Copy();
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    face.IterateThruFacingOffsets(npos);
                    if (world.BlockAccessor.GetChunkAtBlockPos(npos) == null)
                    {
                        unknown = true;
                        continue;
                    }

                    IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;
                    if (block != null && block.HasMechPowerConnectorAt(world, npos, face.Opposite, this)
                        && !bempaxle.disconnected)
                    {
                        connected = true;
                        break;
                    }
                }
                if (!connected && !unknown)
                {
                    world.BlockAccessor.BreakBlock(pos, null);
                }
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }


        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }
    }
}
