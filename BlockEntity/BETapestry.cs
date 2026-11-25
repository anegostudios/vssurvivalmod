using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityTapestry : BlockEntity
    {
        bool rotten;
        bool preserveType;
        bool preserve;
        string type;
        MeshData meshData;

        bool needsToDie;
        bool didInitialize;

        public string Type => type;
        public bool Rotten => rotten;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            didInitialize = true;

            if (needsToDie)
            {
                RegisterDelayedCallback((dt) => api.World.BlockAccessor.SetBlock(0, Pos), 50);
                return;
            }

            if (type != null) genMesh();
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (byItemStack != null)
            {
                type = byItemStack.Attributes?.GetString("type");
                preserveType = byItemStack.Attributes?.GetBool("preserveType") ?? false;
                preserve = byItemStack.Attributes?.GetBool("preserve") ?? false;
            }

            genMesh();
        }

        void genMesh()
        {
            if (Api.Side == EnumAppSide.Server) return;

            int rotVariant = 1 + GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 4);
            BlockTapestry tBlock = Block as BlockTapestry;
            meshData = tBlock?.genMesh(rotten, type, rotVariant).Rotate(0, Block.Shape.rotateY * GameMath.DEG2RAD, 0);
        }
        

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            rotten = tree.GetBool("rotten");
            preserveType = tree.GetBool("preserveType");
            preserve = tree.GetBool("preserve");
            type = tree.GetString("type");
            if (worldForResolving.Side == EnumAppSide.Client && Api != null && type != null)
            {
                genMesh();
                MarkDirty(true);
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("rotten", rotten);
            tree.SetBool("preserveType", preserveType);
            tree.SetBool("preserve", preserve);
            tree.SetString("type", type);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if(!resolveImports || preserve) return;
            
            bool wallcarving = Block.FirstCodePart() == "wallcarving";

            if (!preserveType)
            {
                bool found = false;
                double val = (double)(uint)schematicSeed / uint.MaxValue;
                var blockGroups = wallcarving ? BlockTapestry.wallcarvingGroups : BlockTapestry.tapestryGroups;

                for (int i = 0; !found && i < blockGroups.Length; i++)
                {
                    for (int j = 0; !found && j < blockGroups[i].Length; j++)
                    {
                        if (blockGroups[i][j] == type)
                        {
                            int rnd = GameMath.oaatHashMany(schematicSeed + ((i >= 3) ? 87987 : 0), 20);
                
                            uint seed2 = GameMath.Mod((uint)schematicSeed + (uint)rnd, uint.MaxValue);
                
                            val = (double)seed2 / uint.MaxValue;
                
                            int len = blockGroups[i].Length;
                            int pos = GameMath.oaatHashMany(j + schematicSeed, 20);
                
                            type = blockGroups[i][GameMath.Mod(pos, len)];
                            found = true;
                        }
                    }
                }

                if (val < 0.6)
                {
                    needsToDie = true;
                    if (didInitialize) Api.World.BlockAccessor.SetBlock(0, Pos);
                    return;
                }
            }
            
            if (!wallcarving) rotten = worldForNewMappings.Rand.NextDouble() < 0.75;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mesher.AddMeshData(meshData);
            return true;
        }

        
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Rotten) dsc.AppendLine(Lang.Get("Will fall apart when broken"));
        }
    }
}
