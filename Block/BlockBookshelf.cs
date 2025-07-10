using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;


namespace Vintagestory.GameContent
{
    public class BlockBookshelf : BlockShapeMaterialFromAttributes
    {
        public Dictionary<string, int[]> UsableSlots = null!;

        public override string MeshKey { get; } = "BookshelfMeshes";

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            LoadTypes();
        }

        public override void LoadTypes()
        {
            base.LoadTypes();
            UsableSlots = Attributes["usableSlots"].AsObject<Dictionary<string, int[]>>();
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var beshelf = blockAccessor.GetBlockEntity(pos) as BlockEntityBookshelf;

            if (beshelf?.UsableSlots != null)
            {
                List<Cuboidf> cubs = new List<Cuboidf>
                {
                    new Cuboidf(0, 0, 0, 1, 1, 0.1f),
                    new Cuboidf(0, 0, 0, 1, 1 / 16f, 0.5f),
                    new Cuboidf(0, 15 / 16f, 0, 1, 1, 0.5f),
                    new Cuboidf(0, 0, 0, 1 / 16f, 1, 0.5f),
                    new Cuboidf(15 / 16f, 0, 0, 1, 1, 0.5f)
                };

                for (int i = 0; i < 14; i++)
                {
                    if (!beshelf.UsableSlots.Contains(i)) { 
                        cubs.Add(new Cuboidf());
                        continue;
                    }

                    float x = (i % 7) * 2f / 16f + 1.1f / 16f;
                    float y = (i / 7) * 7.5f / 16f;
                    float z = 6.5f / 16f;
                    var cub = new Cuboidf(x, y + 1f/16f, 1/16f, x + 1.9f/16f, y + 7/16f, z);
                    
                    

                    cubs.Add(cub);
                }

                for (int i = 0; i < cubs.Count; i++) cubs[i] = cubs[i].RotatedCopy(0, (beshelf?.MeshAngleRad ?? 0) * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5));

                return cubs.ToArray();
            }

            return new Cuboidf[] { new Cuboidf(0, 0, 0, 1, 1, 0.5f).RotatedCopy(0, (beshelf?.MeshAngleRad ?? 0) * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5)) };
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var beshelf = blockAccessor.GetBlockEntity(pos) as BlockEntityBookshelf;

            return new Cuboidf[] { new Cuboidf(0, 0, 0, 1, 1, 0.5f).RotatedCopy(0, (beshelf?.MeshAngleRad ?? 0) * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5)) };
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var beb = GetBlockEntity<BlockEntityBookshelf>(pos);
            if (beb != null)
            {
                var mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).RotateY(beb.MeshAngleRad).Translate(-0.5f, -0.5f, -0.5f).Values;
                blockModelData = GetOrCreateMesh(beb.Type, beb.Material).Clone().MatrixTransform(mat);
                decalModelData = GetOrCreateMesh(beb.Type, beb.Material, null, decalTexSource).Clone().MatrixTransform(mat);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var beshelf = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBookshelf;
            if (beshelf != null) return beshelf.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
