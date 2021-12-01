using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;


/*
 * 1. Procedeurally make horizontal and vertical branch ends use the bark texture not the treetrunk texture
2. [~] Always rotate the last horizontal branch 20-30 degrees upwards
3. [x] No tree trunk telescoping, i.e. no stem block
4. [x] But horizontal branches should be thinner
s */

namespace Vintagestory.GameContent
{
    public class BlockEntityFruitTreeFoliage: BlockEntityFruitTreePart
    {
        public override void Initialize(ICoreAPI api)
        {
            blockFoliage = Block as BlockFruitTreeFoliage;
            string code = Block.Attributes?["branchBlock"]?.AsString();
            if (code == null) { api.World.BlockAccessor.SetBlock(0, Pos); return; }
            blockBranch = api.World.GetBlock(AssetLocation.Create(code, Block.Code.Domain)) as BlockFruitTreeBranch;

            base.Initialize(api);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            GenMesh();
        }

        public override void GenMesh()
        {
            base.GenFoliageMesh(true, out leavesMesh, out sticksMesh);
        }



        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (Api.World.EntityDebugMode)
            {
                dsc.AppendLine("TreeType: " + TreeType);
                dsc.AppendLine("FoliageState: " + FoliageState);
                dsc.AppendLine("Growthdir: " + GrowthDir);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            var prevState = FoliageState;

            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                GenMesh();
                if (prevState != FoliageState)
                {
                    MarkDirty(true);
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (sticksMesh == null) return true;

            mesher.AddMeshData(leavesMesh);

            mesher.AddMeshData(CopyRndSticksMesh(sticksMesh));
            return true;
        }


        MeshData CopyRndSticksMesh(MeshData mesh)
        {
            float rndoffx = (GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 100) - 50) / 500f;
            float rndoffy = (GameMath.MurmurHash3Mod(Pos.X, -Pos.Y, Pos.Z, 100) - 50) / 500f;
            float rndoffz = (GameMath.MurmurHash3Mod(Pos.X, Pos.Y, -Pos.Z, 100) - 50) / 500f;

            float rndrotx = (GameMath.MurmurHash3Mod(-Pos.X, -Pos.Y, Pos.Z, 100) - 50f) / 150f;
            float rndrotz = (GameMath.MurmurHash3Mod(-Pos.X, -Pos.Y, -Pos.Z, 100) - 50f) / 150f;

            float branchAngle1 = rndrotx;
            float branchAngle2 = rndrotz;

            Vec3f origin = null;

            switch (GrowthDir.Index)
            {
                case 0: // N
                    origin = new Vec3f(0.5f, 4 / 16f, 21f / 16f);
                    branchAngle1 = -rndrotx;
                    break;
                case 1: // E
                    branchAngle1 = rndrotz;
                    branchAngle2 = -rndrotx;
                    origin = new Vec3f(-5f / 16f, 4 / 16f, 0.5f);
                    break;
                case 2: // S
                    //origin = new Vec3f(21f / 16f, 4 / 16f, 0.5f);
                    origin = new Vec3f(0.5f, 4 / 16f, -5f / 16f);
                    break;
                case 3: // W
                    origin = new Vec3f(21f / 16f, 4 / 16f, 0.5f);
                    branchAngle1 = 0;
                    branchAngle2 = rndrotx;
                    break;
                case 4: // U
                    origin = new Vec3f(0.5f, 0f, 0.5f);
                    break;

            }

            return mesh?.Clone().Translate(rndoffx, rndoffy, rndoffz).Rotate(origin, branchAngle1, 0, branchAngle2);
        }
             


    }
}
