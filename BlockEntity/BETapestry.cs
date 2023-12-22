using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityTapestry : BlockEntity
    {
        bool rotten;
        string type;
        MeshData meshData;

        bool needsToDie;
        bool didInitialize;

        public string Type => type;
        public bool Rotten => rotten;

        string[][] tapestryGroups = new string[][]
        {
            new string[] { "rot1" },
            new string[] { "rot2" },
            new string[] { "rot3" },

            new string[] { "holy1", "schematic-c1", "schematic-c-bloody1", "forlorn1" },
            new string[] { "holy2", "schematic-c2", "schematic-c-bloody2", "forlorn2" },

            new string[] { "salvation11", "schematic-a11", "schematic-b11", "schematic-d11", "rotbeast11", "blackguard11" },
            new string[] { "salvation12", "schematic-a12", "schematic-b12", "schematic-d12", "rotbeast12", "blackguard12" },
            new string[] { "salvation21", "schematic-a21", "schematic-b21", "schematic-d21", "rotbeast21", "blackguard21" },
            new string[] { "salvation22", "schematic-a22", "schematic-b22", "schematic-d22", "rotbeast22", "blackguard22" },
        };

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
                type = byItemStack?.Attributes?.GetString("type");
            }

            genMesh();
        }

        void genMesh()
        {
            if (Api.Side == EnumAppSide.Server) return;

            int rotVariant = 1 + GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 4);
            BlockTapestry tBlock = Block as BlockTapestry;
            meshData = tBlock?.genMesh(rotten, type, rotVariant).Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, Block.Shape.rotateY * GameMath.DEG2RAD, 0);
        }
        

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            rotten = tree.GetBool("rotten");
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
            tree.SetString("type", type);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if(!resolveImports) return;
            bool found = false;

            double val = ((double)(uint)schematicSeed / uint.MaxValue);

            for (int i = 0; !found && i < tapestryGroups.Length; i++)
            {
                for (int j = 0; !found && j < tapestryGroups[i].Length; j++)
                {
                    if (tapestryGroups[i][j] == type)
                    {
                        int rnd = GameMath.oaatHashMany(schematicSeed + ((i >= 3) ? 87987 : 0), 20);
            
                        uint seed2 = GameMath.Mod((uint)schematicSeed + (uint)rnd, uint.MaxValue);
            
                        val = ((double)seed2 / uint.MaxValue);
            
                        int len = tapestryGroups[i].Length;
                        int pos = GameMath.oaatHashMany(j + schematicSeed, 20);
            
                        type = tapestryGroups[i][GameMath.Mod(pos, len)];
                        found = true;
            
                    }
                }
            }

            if (val < 0.6)
            {
                needsToDie = true;
                if (didInitialize) {
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                }
                return;
            }

            rotten = worldForNewMappings.Rand.NextDouble() < 0.75;
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
