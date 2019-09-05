using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BELantern : BlockEntity, IBlockShapeSupplier
    {
        public string material = "copper";
        public string lining = "plain";
        public string glass = "quartz";

        MeshData currentMesh;

        byte[] origlightHsv = new byte[] { 7, 4, 18 };
        byte[] lightHsv = new byte[] { 7, 4, 18 };

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Block block = api.World.BlockAccessor.GetBlock(pos);
            origlightHsv = block.LightHsv;
        }

        public void DidPlace(string material, string lining)
        {
            this.lining = lining;
            this.material = material;
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            material = tree.GetString("material", "copper");
            lining = tree.GetString("lining", "plain");
            glass = tree.GetString("glass", "quartz");
            setLightColor(glass);

            if (api != null && api.Side == EnumAppSide.Client)
            {
                currentMesh = null;
                MarkDirty(true);
            }
        }

        internal byte[] GetLightHsv()
        {
            lightHsv[2] = lining != "plain" ? (byte)(origlightHsv[2] + 3) : origlightHsv[2];
            return lightHsv;
        }

        private MeshData getMesh(ITesselatorAPI tesselator)
        {
            Dictionary<string, MeshData> lanternMeshes = ObjectCacheUtil.GetOrCreate(api, "blockLanternBlockMeshes", () => new Dictionary<string, MeshData>());
            
            MeshData mesh = null;
            BlockLantern block = api.World.BlockAccessor.GetBlock(pos) as BlockLantern;
            if (block == null) return null;

            string orient = block.LastCodePart();

            if (lanternMeshes.TryGetValue(material + "-" + lining + "-" + orient + "-" + glass, out mesh))
            {
                return mesh;
            }

            return lanternMeshes[material + "-" + lining + "-" + orient + "-" + glass] = block.GenMesh(api as ICoreClientAPI, material, lining, glass, null, tesselator);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("material", material);
            tree.SetString("lining", lining);
            tree.SetString("glass", glass);
        }


        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (currentMesh == null)
            {
                currentMesh = getMesh(tesselator);
                if (currentMesh == null) return false;
            }

            mesher.AddMeshData(currentMesh);
            return true;
        }

        public override string GetBlockInfo(IPlayer forPlayer)
        {
            return Lang.Get("{0} with {1} lining and {2} glass panels", material.UcFirst(), lining.UcFirst(), glass);
        }

        internal void Interact(IPlayer byPlayer)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty) return;

            CollectibleObject obj = slot.Itemstack.Collectible;
            if (obj.FirstCodePart() == "glass" && obj.Variant.ContainsKey("color"))
            {
                if (glass != "quartz" && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    ItemStack stack = new ItemStack(api.World.GetBlock(new AssetLocation("glass-" + glass)));
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        api.World.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0, 0.5));
                    }
                }

                this.glass = obj.Variant["color"];
                if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative && glass != "quartz") slot.TakeOut(1);

                if (api.Side == EnumAppSide.Client) (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                Vec3d soundpos = pos.ToVec3d().Add(0.5, 0, 0.5);
                api.World.PlaySoundAt(api.World.GetBlock(new AssetLocation("glass-" + glass)).Sounds.Place, soundpos.X, soundpos.Y, soundpos.Z, byPlayer);

                setLightColor(glass);

                MarkDirty(true);
            }
        }


        void setLightColor(string color)
        {
            switch (color)
            {
                case "green":
                    lightHsv[0] = 20;
                    lightHsv[1] = 4;
                    break;
                case "blue":
                    lightHsv[0] = 42;
                    lightHsv[1] = 4;
                    break;
                case "violet":
                    lightHsv[0] = 48;
                    lightHsv[1] = 4;
                    break;
                case "red":
                    lightHsv[0] = 0;
                    lightHsv[1] = 4;
                    break;
                case "yellow":
                    lightHsv[0] = 11;
                    lightHsv[1] = 4;
                    break;
                case "brown":
                    lightHsv[0] = 5;
                    lightHsv[1] = 4;
                    break;

                default:
                    lightHsv[1] = 3;
                    lightHsv[0] = 7;
                    break;
            }
        }
    }
}
