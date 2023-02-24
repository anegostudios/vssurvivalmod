using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class PlacedBeam
    {
        [ProtoMember(1)]
        public Vec3f Start;
        [ProtoMember(2)]
        public Vec3f End;
        [ProtoMember(3)]
        public int BlockId;
        [ProtoMember(4)]
        public int FacingIndex;

        public Block block;
    }

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

            if (api.Side == EnumAppSide.Client)
            {
                sbp = api.ModLoader.GetModSystem<ModSystemSupportBeamPlacer>();
            }

            if (Beams != null) {
                foreach (var beam in Beams)
                {
                    beam.block = Api.World.GetBlock(beam.BlockId);
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
                block = block 
            });
            collBoxes = null;
        }

        public Cuboidf[] GetCollisionBoxes()
        {
            if (Api is ICoreClientAPI capi && capi.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible is BlockSupportBeam) return null;

            if (Beams == null) return null;
            if (collBoxes != null) return collBoxes;

            Cuboidf[] cuboids = new Cuboidf[Beams.Length * 2];
            for (int i = 0; i < Beams.Length; i++)
            {
                float size = 1 / 8f;
                var beam = Beams[i];
                cuboids[2 * i] = new Cuboidf(beam.Start.X - size, beam.Start.Y - size, beam.Start.Z - size, beam.Start.X + size, beam.Start.Y + size, beam.Start.Z + size);
                cuboids[2 * i + 1] = new Cuboidf(beam.End.X - size, beam.End.Y - size, beam.End.Z - size, beam.End.X + size, beam.End.Y + size, beam.End.Z + size);
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
                        beam.block = Api.World.GetBlock(beam.BlockId);
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

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);
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
                    Beams[i].block = block;
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

            var pos = Blockentity.Pos;
            for (int i = 0; i < Beams.Length; i++)
            {
                var beam = Beams[i];
                var start = beam.Start;
                var end = beam.End;
                var meshData = sbp.getOrCreateBeamMesh(beam.block);
                var mesh = ModSystemSupportBeamPlacer.generateMesh(start, end, BlockFacing.ALLFACES[beam.FacingIndex], meshData);

                float x = GameMath.MurmurHash3Mod(pos.X + i * 100, pos.Y + i * 100, pos.Z + i * 100, 500) / 50000f;
                float y = GameMath.MurmurHash3Mod(pos.X - i * 100, pos.Y + i * 100, pos.Z + i * 100, 500) / 50000f;
                float z = GameMath.MurmurHash3Mod(pos.X + i * 100, pos.Y + i * 100, pos.Z - i * 100, 500) / 50000f;
                mesh.Translate(x, y, z);

                mesher.AddMeshData(mesh);
            }

            return true;
        }

        public void OnTransformed(ITreeAttribute tree, int degreeRotation, EnumAxis? flipAxis)
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

        public ItemStack[] GetDrops(IPlayer byPlayer)
        {
            List<ItemStack> drops = new List<ItemStack>();
            foreach (var beam in Beams)
            {
                drops.Add(new ItemStack(beam.block, (int)Math.Ceiling(beam.End.DistanceTo(beam.Start))));
            }

            return drops.ToArray();
        }

        public void ExchangeWith(ItemSlot fromSlot, ItemSlot toSlot)
        {
            if (Beams == null || Beams.Length == 0) return;
            var fromblock = fromSlot.Itemstack.Block;
            var toblock = toSlot.Itemstack.Block;

            foreach (var beam in Beams)
            {
                if (beam.BlockId == fromblock.Id)
                {
                    beam.block = toblock;
                    beam.BlockId = toblock.Id;
                }
            }

            Blockentity.MarkDirty(true);
        }
    }
}
