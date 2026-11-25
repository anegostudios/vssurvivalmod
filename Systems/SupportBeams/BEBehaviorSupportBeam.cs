using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class BEBehaviorSupportBeam : BlockEntityBehavior, IRotatable, IMaterialExchangeable
    {
        private const float beamHalfWidth = 2 / 16f;
        private const float beamCollisionInset = 1 / 160f;
        private const float beamAnchorHalfWidth = 1 / 6f;
        private const int meshOffsetSeedStride = 100;
        private const int meshOffsetHashRange = 500;
        private const float meshOffsetScale = 1 / 50000f;

        private readonly List<Cuboidd> beamCollisionBoxes = new();
        private readonly List<int> beamSelectionBoxIndices = new();
        private long colliderId;

        public PlacedBeam[] Beams;
        ModSystemSupportBeamPlacer sbp;
        Cuboidf[] collBoxes;
        Cuboidf[] selBoxes;

        int nextPlaceIndex = 0;

        bool dropWhenBroken;

        public BEBehaviorSupportBeam(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            sbp = api.ModLoader.GetModSystem<ModSystemSupportBeamPlacer>();

            if (Beams != null) {
                foreach (var beam in Beams)
                {
                    beam.Block = Api.World.GetBlock(beam.BlockId);
                }
                refreshCollider();
            }

            dropWhenBroken = properties?["dropWhenBroken"].AsBool(true) != false;
        }

        public void AddBeam(Vec3f start, Vec3f end, BlockFacing onFacing, Block block)
        {
            if (Beams == null) Beams = Array.Empty<PlacedBeam>();

            Beams = Beams.Append(new PlacedBeam() {
                Start = start.Clone(),
                End = end.Clone(),
                FacingIndex = onFacing.Index,
                BlockId = block.Id,
                Block = block,
                PlaceIndex = nextPlaceIndex++
            });
            refreshCollider();
            collBoxes = null;
            selBoxes = null;

            sbp.OnBeamAdded(start.ToVec3d().Add(Pos), end.ToVec3d().Add(Pos));
        }

        public Cuboidf[] GetCollisionBoxes()
        {
            if (Api is ICoreClientAPI capi && capi.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible is BlockSupportBeam) return null;

            if (Beams == null) return null;
            if (collBoxes != null) return collBoxes;

            List<Cuboidf> cuboids = new();
            for (int i = 0; i < Beams.Length; i++)
            {
                // Straight beams already have an exact collider in the collision scene.
                if (Beams[i].SlumpPerMeter != 0) cuboids.Add(getBeamAnchorBox(Beams[i]));
            }

            return collBoxes = cuboids.ToArray();
        }

        public Cuboidf[] GetSelectionBoxes()
        {
            if (Api is ICoreClientAPI capi && BlockSupportBeam.IsPlacingThroughBeams(capi.World.Player)) return null;

            if (selBoxes != null) return selBoxes;

            if (Beams == null) return null;

            Cuboidf[] boxes = new Cuboidf[Beams.Length];
            for (int i = 0; i < Beams.Length; i++)
            {
                boxes[i] = getBeamBox(Beams[i], forSelection: true) ?? getBeamAnchorBox(Beams[i]);
            }

            return selBoxes = boxes;
        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            int stver = tree.GetInt("stver");
            var bytes = tree.GetBytes("beams", null);


            if (bytes != null)
            {
                bool refreshCollision = Api != null && worldAccessForResolve != null;
                if (refreshCollision) unregisterCollider();

                Beams = SerializerUtil.Deserialize<PlacedBeam[]>(bytes);
                if (Beams != null)
                {
                    if (stver == 0)
                    {
                        for (int i = 0; i < Beams.Length; i++)
                        {
                            var beam = Beams[i];
                            if (beam.PlaceIndex == 0) beam.PlaceIndex = i;
                            nextPlaceIndex = Math.Max(beam.PlaceIndex, nextPlaceIndex) + 1;
                        }
                    }

                    if (Api != null)
                    {
                        for (int i = 0; i < Beams.Length; i++)
                        {
                            var beam = Beams[i];
                            beam.Block = Api.World.GetBlock(beam.BlockId);
                        }
                        if (refreshCollision) refreshCollider();
                    }
                }

                collBoxes = null;
                selBoxes = null;
            }
        }

        public override void OnBlockRemoved()
        {
            unregisterCollider();
            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            unregisterCollider();
            base.OnBlockUnloaded();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (Beams != null)
            {
                tree.SetBytes("beams", SerializerUtil.Serialize(Beams));
            }

            tree.SetInt("stver", 1);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            if (Beams == null) return;

            for (int i = 0; i < Beams.Length; i++)
            {
                if (oldBlockIdMapping.TryGetValue(Beams[i].BlockId, out AssetLocation code))
                {
                    Block block = worldForNewMappings.GetBlock(code);
                    if (block == null)
                    {
                        worldForNewMappings.Logger.Warning("Cannot load support beam block id mapping @ {1}, block code {0} not found block registry. Will not display correctly.", code, Blockentity.Pos);
                        continue;
                    }

                    Beams[i].BlockId = block.Id;
                    Beams[i].Block = block;
                }
                else
                {
                    worldForNewMappings.Logger.Warning("Cannot load support beam block id mapping @ {1}, block id {0} not found block registry. Will not display correctly.", Beams[i].BlockId, Blockentity.Pos);
                }
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            if (Beams == null) return;

            for (int i = 0; i < Beams.Length; i++)
            {
                Block block = Api.World.GetBlock(Beams[i].BlockId);
                blockIdMapping[block.Id] = block.Code;
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (Beams == null) return true;

            for (int i = 0; i < Beams.Length; i++)
            {
                MeshData mesh = genMesh(i, null, null);

                mesher.AddMeshData(mesh);
            }

            return true;
        }

        public MeshData genMesh(int beamIndex, ITexPositionSource texSource, string texSourceKey)
        {
            var pos = Blockentity.Pos;
            var beam = Beams[beamIndex];
            var start = beam.Start;
            var end = beam.End;
            var meshDatas = sbp.getOrCreateBeamMeshes(beam.Block, (beam.Block as BlockSupportBeam)?.PartialEnds ?? false, texSource, texSourceKey);
            var mesh = ModSystemSupportBeamPlacer.generateMesh(start, end, BlockFacing.ALLFACES[beam.FacingIndex], meshDatas, beam.SlumpPerMeter);
            var offset = getMeshOffset(beam);
            mesh.Translate(offset.X, offset.Y, offset.Z);
            return mesh;
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            FromTreeAttributes(tree, null);
            if (Beams == null) return;

            if (degreeRotation != 0)
            {
                Matrixf mat = new Matrixf();
                mat.Translate(0.5f, 0.5f, 0.5f);
                mat.RotateYDeg(-degreeRotation);
                mat.Translate(-0.5f, -0.5f, -0.5f);

                Vec4f tmpVec = new Vec4f();
                tmpVec.W = 1;

                foreach (var beam in Beams)
                {
                    tmpVec.X = beam.Start.X;
                    tmpVec.Y = beam.Start.Y;
                    tmpVec.Z = beam.Start.Z;
                    var rotatedVec = mat.TransformVector(tmpVec);
                    beam.Start.X = rotatedVec.X;
                    beam.Start.Y = rotatedVec.Y;
                    beam.Start.Z = rotatedVec.Z;


                    tmpVec.X = beam.End.X;
                    tmpVec.Y = beam.End.Y;
                    tmpVec.Z = beam.End.Z;
                    rotatedVec = mat.TransformVector(tmpVec);
                    beam.End.X = rotatedVec.X;
                    beam.End.Y = rotatedVec.Y;
                    beam.End.Z = rotatedVec.Z;
                }
            }
            else if (flipAxis != null)
            {
                foreach (var beam in Beams)
                {
                    switch (flipAxis)
                    {
                        case EnumAxis.X:
                        {
                            beam.Start.X = beam.Start.X * -1 + 1;
                            beam.End.X = beam.End.X * -1 + 1;
                            break;
                        }
                        case EnumAxis.Y:
                        {
                            beam.Start.Y = beam.Start.Y * -1 + 1;
                            beam.End.Y = beam.End.Y * -1 + 1;
                            break;
                        }
                        case EnumAxis.Z:
                        {
                            beam.Start.Z = beam.Start.Z * -1 + 1;
                            beam.End.Z = beam.End.Z * -1 + 1;
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException(nameof(flipAxis), flipAxis, null);
                    }
                }
            }

            ToTreeAttributes(tree);
            if (Api != null) refreshCollider();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (Beams != null && sbp != null)
            {
                for (var i = Beams.Length -1 ; i >= 0; i--)
                {
                    BreakBeam(i, byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative);
                }
            }
            base.OnBlockBroken(byPlayer);
        }

        public void BreakBeamByPlaceIndex(int placeIndex, bool drop = true)
        {
            for (int i = 0; i < Beams.Length; i++)
            {
                if (Beams[i].PlaceIndex == placeIndex)
                {
                    BreakBeam(i, drop);
                    return;
                }
            }            
        }

        

        public void BreakBeam(int beamIndex, bool drop = true)
        {
            if (beamIndex < 0 || beamIndex >= Beams.Length) return;

            var beam = Beams[beamIndex];

            if (drop && dropWhenBroken)
            {
                var beb = beam.Block.BlockEntityBehaviors.FirstOrDefault(beb => beb.Name == "SupportBeam");
                if (beb == null || beb.properties?["dropWhenBroken"]?.AsBool(true) != false)
                {
                    Api.World.SpawnItemEntity(new ItemStack(beam.Block, (int)Math.Ceiling(beam.End.DistanceTo(beam.Start))), Pos);
                }
            }
            sbp.OnBeamRemoved(beam.Start.ToVec3d().Add(Pos), beam.End.ToVec3d().Add(Pos));
            Beams = Beams.RemoveAt(beamIndex);
            refreshCollider();

            if (Beams.Length == 0 && Block is BlockSupportBeam)
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
                return;
            }

            Blockentity.MarkDirty(true);
            collBoxes = null;
            selBoxes = null;
        }

        public bool ExchangeWith(ItemSlot fromSlot, ItemSlot toSlot)
        {
            if (Beams == null || Beams.Length == 0) return false;
            var fromblock = fromSlot.Itemstack.Block;
            var toblock = toSlot.Itemstack.Block;

            bool exchanged = false;

            foreach (var beam in Beams)
            {
                if (beam.BlockId == fromblock.Id)
                {
                    beam.Block = toblock;
                    beam.BlockId = toblock.Id;
                    exchanged = true;
                }
            }

            if (exchanged) refreshCollider();
            Blockentity.MarkDirty(true);

            return exchanged;
        }

        private void refreshCollider()
        {
            beamCollisionBoxes.Clear();
            beamSelectionBoxIndices.Clear();
            if (Beams != null)
            {
                for (int i = 0; i < Beams.Length; i++)
                {
                    Cuboidd collisionBox = getBeamCollisionBox(Beams[i]);
                    if (collisionBox == null) continue;

                    beamCollisionBoxes.Add(collisionBox);
                    beamSelectionBoxIndices.Add(i);
                }
            }

            if (beamCollisionBoxes.Count == 0)
            {
                unregisterCollider();
                return;
            }

            if (colliderId == 0)
            {
                colliderId = Api.World.CollisionScene.RegisterCollider(Pos.dimension, beamCollisionBoxes, Pos, beamSelectionBoxIndices);
            }
            else
            {
                Api.World.CollisionScene.UpdateCollider(colliderId, Pos.dimension, beamCollisionBoxes, Pos, beamSelectionBoxIndices);
            }
        }

        private Cuboidd getBeamCollisionBox(PlacedBeam beam)
        {
            RotatedCuboidf collisionBox = getBeamBox(beam, forSelection: false);
            return collisionBox?.OffsetCopyDouble(Pos.X, Pos.Y, Pos.Z);
        }

        private RotatedCuboidf getBeamBox(PlacedBeam beam, bool forSelection)
        {
            // A single rotated box cannot follow a curved beam.
            if (beam.SlumpPerMeter != 0) return null;

            FastVec3f offset = forSelection ? getMeshOffset(beam) : default;
            var start = beam.Start.AddCopy(offset.X, offset.Y, offset.Z);
            float length = beam.Start.DistanceTo(beam.End);
            if (length <= 0) return null;

            ModSystemSupportBeamPlacer.GetBeamTransform(
                beam.End.X - beam.Start.X,
                beam.End.Y - beam.Start.Y,
                beam.End.Z - beam.Start.Z,
                out float yaw,
                out float pitch,
                out float endExtension
            );
            float[] rotation = Quaternionf.Create();
            Quaternionf.RotateY(rotation, rotation, yaw);
            Quaternionf.RotateZ(rotation, rotation, pitch);

            // Match the cross section and model origin used by generateMesh().
            Cuboidd modelBounds = (beam.Block as BlockSupportBeam)?.BeamShapeBounds;
            float minY = modelBounds == null ? -beamHalfWidth : (float)modelBounds.Y1 - 0.125f;
            float maxY = modelBounds == null ? beamHalfWidth : (float)modelBounds.Y2 - 0.125f;
            float minZ = modelBounds == null ? -beamHalfWidth : (float)modelBounds.Z1 - 0.5f;
            float maxZ = modelBounds == null ? beamHalfWidth : (float)modelBounds.Z2 - 0.5f;
            float inset = forSelection ? 0 : beamCollisionInset;
            float insetY = Math.Min(inset, (maxY - minY) / 4);
            float insetZ = Math.Min(inset, (maxZ - minZ) / 4);
            float insetX = Math.Min(inset, (length + 2 * endExtension) / 4);

            return new RotatedCuboidf(
                start.X - length - endExtension + insetX,
                start.Y + minY + insetY,
                start.Z + minZ + insetZ,
                start.X + endExtension - insetX,
                start.Y + maxY - insetY,
                start.Z + maxZ - insetZ
            ).SetRotationCenter(start).SetRotationQuaternion(rotation);
        }

        private Cuboidf getBeamAnchorBox(PlacedBeam beam)
        {
            float size = beamAnchorHalfWidth;
            var box = new Cuboidf(beam.Start.X - size, beam.Start.Y - size, beam.Start.Z - size, beam.Start.X + size, beam.Start.Y + size, beam.Start.Z + size);

            for (int i = 0; i < 3; i++)
            {
                if (box[i] < 0)
                {
                    box[i] = -size;
                    box[i + 3] = size;
                }
                if (box[i] > 1)
                {
                    box[i] = 1 - size;
                    box[i + 3] = 1 + size;
                }
            }

            return box;
        }

        private FastVec3f getMeshOffset(PlacedBeam beam)
        {
            int seedOffset = beam.PlaceIndex * meshOffsetSeedStride;
            return new(
                GameMath.MurmurHash3Mod(Pos.X + seedOffset, Pos.Y + seedOffset, Pos.Z + seedOffset, meshOffsetHashRange) * meshOffsetScale,
                GameMath.MurmurHash3Mod(Pos.X - seedOffset, Pos.Y + seedOffset, Pos.Z + seedOffset, meshOffsetHashRange) * meshOffsetScale,
                GameMath.MurmurHash3Mod(Pos.X + seedOffset, Pos.Y + seedOffset, Pos.Z - seedOffset, meshOffsetHashRange) * meshOffsetScale
            );
        }

        private void unregisterCollider()
        {
            if (colliderId == 0) return;

            Api.World.CollisionScene.UnregisterCollider(colliderId);
            colliderId = 0;
        }
    }
}
