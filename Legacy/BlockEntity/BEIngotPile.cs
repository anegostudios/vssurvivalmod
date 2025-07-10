﻿using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityIngotPile : BlockEntityItemPile, ITexPositionSource
    {
        Dictionary<string, MeshData[]> meshesByType
        {
            get
            {
                Api.ObjectCache.TryGetValue("ingotpile-meshes", out object value);
                return (Dictionary<string, MeshData[]>)value;
            }
            set { Api.ObjectCache["ingotpile-meshes"] = value; }
        }
        

        Block tmpBlock;
        string tmpMetalCode;
        ITexPositionSource tmpTextureSource;
        ICoreClientAPI capi;

        internal AssetLocation soundLocation = new AssetLocation("sounds/block/ingot");

        public override AssetLocation SoundLocation { get { return soundLocation; } }
        public override string BlockCode { get { return "ingotpile"; } }
        public override int MaxStackSize { get { return 64; } }

        internal void EnsureMeshExists()
        {
            if (meshesByType == null) meshesByType = new Dictionary<string, MeshData[]>();
            if (MetalType == null || meshesByType.ContainsKey(MetalType)) return;
            if (Api.Side != EnumAppSide.Client) return;

            tmpBlock = Api.World.BlockAccessor.GetBlock(Pos);
            tmpTextureSource = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(tmpBlock);

            Shape shape = ObjectCacheUtil.GetOrCreate(Api, "ingotpileshape", () => API.Common.Shape.TryGet(Api, "shapes/block/metal/ingotpile.json"));
            if (shape == null) return;

            foreach (var textureCode in Block.Textures.Keys)
            {
                ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;
                MeshData[] meshes = new MeshData[65];

                tmpMetalCode = textureCode;

                for (int j = 0; j <= 64; j++)
                {
                    var mesh = meshes[j];
                    mesher.TesselateShape("ingotPile", shape, out mesh, this, null, 0, 0, 0, j);
                }

                meshesByType[tmpMetalCode] = meshes;
            }

            tmpTextureSource = null;
            tmpMetalCode = null;
            tmpBlock = null;
        }

        public override bool TryPutItem(IPlayer player)
        {
            bool result = base.TryPutItem(player);
            return result;
        }

        

        public TextureAtlasPosition this[string textureCode]
        {
            get { return tmpTextureSource[tmpMetalCode]; }
        }


        public BlockEntityIngotPile() : base()
        {
            inventory = new InventoryGeneric(1, BlockCode, null, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.ResolveBlocksOrItems();

            capi = api as ICoreClientAPI;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
        }

        public string MetalType
        {
            get { return inventory?[0]?.Itemstack?.Collectible?.LastCodePart(); }
        }

        public override bool OnTesselation(ITerrainMeshPool meshdata, ITesselatorAPI tesselator)
        {
            lock (inventoryLock)
            {
                if (inventory[0].Itemstack == null) return true;

                EnsureMeshExists();
                if (MetalType != null && meshesByType.TryGetValue(MetalType, out MeshData[] mesh))
                {
                    meshdata.AddMeshData(mesh[inventory[0].StackSize]);
                }
            }

            return true;
        }
    }
}
