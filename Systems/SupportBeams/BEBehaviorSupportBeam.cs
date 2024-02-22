using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class BEBehaviorSupportBeam : BlockEntityBehavior, IRotatable, IMaterialExchangeable
    {
        public PlacedBeam[] Beams;
        ModSystemSupportBeamPlacer sbp;
        Cuboidf[] collBoxes;

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
            }
        }

        public void AddBeam(Vec3f start, Vec3f end, BlockFacing onFacing, Block block)
        {
            if (Beams == null) Beams = new PlacedBeam[0];

            Beams = Beams.Append(new PlacedBeam() { 
                Start = start.Clone(), 
                End = end.Clone(),
                FacingIndex = onFacing.Index,
                BlockId = block.Id,
                Block = block
            });
            collBoxes = null;

            sbp.OnBeamAdded(start.ToVec3d().Add(Pos), end.ToVec3d().Add(Pos));
        }

        public Cuboidf[] GetCollisionBoxes()
        {
            if (Api is ICoreClientAPI capi && capi.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible is BlockSupportBeam) return null;

            if (Beams == null) return null;
            if (collBoxes != null) return collBoxes;

            Cuboidf[] cuboids = new Cuboidf[Beams.Length];
            for (int i = 0; i < Beams.Length; i++)
            {
                float size = 1 / 8f;
                var beam = Beams[i];
                cuboids[i] = new Cuboidf(beam.Start.X - size, beam.Start.Y - size, beam.Start.Z - size, beam.Start.X + size, beam.Start.Y + size, beam.Start.Z + size);

                // This does not work when the beam extends into another block
                //cuboids[2 * i + 1] = new Cuboidf(beam.End.X - size, beam.End.Y - size, beam.End.Z - size, beam.End.X + size, beam.End.Y + size, beam.End.Z + size);
            }

            return collBoxes = cuboids;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            var bytes = tree.GetBytes("beams", null);
            if (bytes != null)
            {
                Beams = SerializerUtil.Deserialize<PlacedBeam[]>(bytes);

                if (Api != null && Beams != null)
                {
                    foreach (var beam in Beams)
                    {
                        beam.Block = Api.World.GetBlock(beam.BlockId);
                    }
                }

                collBoxes = null;
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (Beams != null)
            {
                tree.SetBytes("beams", SerializerUtil.Serialize(Beams));
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            if (Beams == null) return;

            for (int i = 0; i < Beams.Length; i++)
            {
                AssetLocation code;
                if (oldBlockIdMapping.TryGetValue(Beams[i].BlockId, out code))
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
            var meshData = sbp.getOrCreateBeamMesh(beam.Block, texSource, texSourceKey);
            var mesh = ModSystemSupportBeamPlacer.generateMesh(start, end, BlockFacing.ALLFACES[beam.FacingIndex], meshData, beam.SlumpPerMeter);

            float x = GameMath.MurmurHash3Mod(pos.X + beamIndex * 100, pos.Y + beamIndex * 100, pos.Z + beamIndex * 100, 500) / 50000f;
            float y = GameMath.MurmurHash3Mod(pos.X - beamIndex * 100, pos.Y + beamIndex * 100, pos.Z + beamIndex * 100, 500) / 50000f;
            float z = GameMath.MurmurHash3Mod(pos.X + beamIndex * 100, pos.Y + beamIndex * 100, pos.Z - beamIndex * 100, 500) / 50000f;
            mesh.Translate(x, y, z);
            return mesh;
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation,Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            FromTreeAttributes(tree, null);
            if (Beams == null) return;

            Matrixf mat = new Matrixf();
            mat.Translate(0.5f, 0.5f, 0.5f);
            mat.RotateYDeg(-degreeRotation);
            mat.Translate(-0.5f, -0.5f, -0.5f);
            
            Vec4f tmpVec = new Vec4f();
            tmpVec.W = 1;

            for (int i = 0; i < Beams.Length; i++)
            {
                var beam = Beams[i];

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

            ToTreeAttributes(tree);
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

        public void BreakBeam(int beamIndex, bool drop = true)
        {
            if (beamIndex < 0 || beamIndex >= Beams.Length) return;

            var beam = Beams[beamIndex];

            if (drop)
            {
                Api.World.SpawnItemEntity(new ItemStack(beam.Block, (int)Math.Ceiling(beam.End.DistanceTo(beam.Start))), Pos.ToVec3d().Add(0.5));
            }
            sbp.OnBeamRemoved(beam.Start.ToVec3d().Add(Pos), beam.End.ToVec3d().Add(Pos));
            Beams = Beams.RemoveEntry(beamIndex);
            Blockentity.MarkDirty(true);
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

            Blockentity.MarkDirty(true);

            return exchanged;
        }
    }
}
