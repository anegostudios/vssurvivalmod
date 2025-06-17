using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BELantern : BlockEntity
    {
        public string material = "copper";
        public string lining = "plain";
        public string glass = "quartz";


        public float MeshAngle;

        byte[] origlightHsv = new byte[] { 7, 4, 18 };
        byte[] lightHsv = new byte[] { 7, 4, 18 };


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            origlightHsv = Block.LightHsv;
            lightHsv = (byte[])Block.LightHsv;
            setLightColor(origlightHsv, lightHsv, glass);
        }

        public void DidPlace(string material, string lining, string glass)
        {
            this.lining = lining;
            this.material = material;
            this.glass = glass;
            if (glass == null || glass.Length == 0) this.glass = "quartz";
            setLightColor(origlightHsv, lightHsv, glass);
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);

            Api.World.BlockAccessor.RemoveBlockLight(lightHsv, Pos);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            material = tree.GetString("material", "copper");
            lining = tree.GetString("lining", "plain");
            glass = tree.GetString("glass", "quartz");
            // The light color will be set in the Initialize() call which must follow FromTreeAttributes() for any BE in the world: for reliability, it needs to be called only after setting the origLightHsv and LightHsv values

            MeshAngle = tree.GetFloat("meshAngle");

            if (Api != null && Api.Side == EnumAppSide.Client)
            {
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
            Dictionary<string, MeshData> lanternMeshes = ObjectCacheUtil.GetOrCreate(Api, "blockLanternBlockMeshes", () => new Dictionary<string, MeshData>());

            BlockLantern block = Api.World.BlockAccessor.GetBlock(Pos) as BlockLantern;
            if (block == null) return null;

            string orient = block.LastCodePart();

            if (lanternMeshes.TryGetValue(material + "-" + lining + "-" + orient + "-" + glass, out MeshData mesh))
            {
                return mesh;
            }

            return lanternMeshes[material + "-" + lining + "-" + orient + "-" + glass] = block.GenMesh(Api as ICoreClientAPI, material, lining, glass, null, tesselator);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("material", material);
            tree.SetString("lining", lining);
            tree.SetString("glass", glass);
            tree.SetFloat("meshAngle", MeshAngle);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            MeshData mesh = getMesh(tesselator);

            if (mesh == null) return false;

            string part = Block.LastCodePart();
            if (part == "up" || part == "down")
            {
                mesh = mesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0);
            }

            mesher.AddMeshData(mesh);

            return true;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (lining == "plain")
            {
                sb.AppendLine(Lang.Get("lantern-materialwithpanels", Lang.Get("material-" + material), Lang.Get("block-glass-" + glass)));
            } else
            {
                sb.AppendLine(Lang.Get("lantern-materialwithliningandpanels", Lang.Get("material-" + material), Lang.Get("material-" + lining), Lang.Get("block-glass-"+glass)));
            }
        }

        internal bool Interact(IPlayer byPlayer)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty) return false;

            CollectibleObject obj = slot.Itemstack.Collectible;
            if (obj.FirstCodePart() == "glass" && obj.Variant.ContainsKey("color"))
            {
                if (glass != "quartz" && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    ItemStack stack = new ItemStack(Api.World.GetBlock(new AssetLocation("glass-" + glass)));
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0, 0.5));
                    }
                    Api.World.Logger.Audit("{0} Replaced glass {1} with {2} for Lantern at {3}.",
                        byPlayer.PlayerName,
                        stack.Collectible.Code,
                        obj.Code,
                        Pos
                    );
                }

                this.glass = obj.Variant["color"];
                if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative && glass != "quartz") slot.TakeOut(1);

                if (Api.Side == EnumAppSide.Client) (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                Api.World.PlaySoundAt(Api.World.GetBlock(new AssetLocation("glass-" + glass)).Sounds.Place, Pos, -0.4, byPlayer);

                setLightColor(origlightHsv, lightHsv, glass);
                Api.World.BlockAccessor.ExchangeBlock(Block.Id, Pos); // Forces a lighting update

                MarkDirty(true);
                return true;
            }

            if (lining == null || lining == "plain" && obj is ItemMetalPlate && (obj.Variant["metal"] == "gold" || obj.Variant["metal"] == "silver" || obj.Variant["metal"] == "electrum"))
            {
                lining = obj.Variant["metal"];
                if (Api.Side == EnumAppSide.Client) (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/plate"), Pos, -0.4, byPlayer);

                slot.TakeOut(1);
                MarkDirty(true);
                return true;
            }

            return false;
        }


        public static void setLightColor(byte[] origLightHsv, byte[] lightHsv, string color)
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
                case "pink":
                    lightHsv[0] = 54;
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
                    lightHsv[1] = origLightHsv[1];
                    lightHsv[0] = origLightHsv[0];
                    break;
            }
        }
    }
}
