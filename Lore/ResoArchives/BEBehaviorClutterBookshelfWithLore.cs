using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BEBehaviorClutterBookshelfWithLore : BEBehaviorClutterBookshelf
    {
        public string LoreCode;

        public BEBehaviorClutterBookshelfWithLore(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            LoreCode = tree.GetString("loreCode");
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack != null)
            {
                LoreCode = byItemStack?.Attributes.GetString("loreCode", "jonas");
            }

            base.OnBlockPlaced(byItemStack);
        }


        string[] colors = new string[] { "aged-orangebrown", "aged-orange", "aged-darkgreen", "aged-darkgray", "aged-cherryred", "aged-brickred", "aged-darkolive", "aged-darkbeige", "aged-olive", "aged-purpleorange", "aged-gray", "rotten-gray", "rotten-brown", "rotten-rust", "rotten-purple", "rotten-green" };
        public bool OnInteract(IPlayer byPlayer)
        {
            if (LoreCode == null) return false;

            string rndColor = colors[GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, colors.Length)];
            var stack = new ItemStack(Api.World.GetItem(new AssetLocation("lore-book-" + rndColor)));
            stack.Attributes.SetString("category", LoreCode);

            if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                var plrpos = byPlayer.Entity.Pos;
                Api.World.SpawnItemEntity(stack, plrpos.XYZ);
            }

            Api.World.Logger.Audit("{0} Took 1x{1} from {2} at {3}.",
                byPlayer.PlayerName,
                stack.Collectible.Code,
                Block.Code,
                Pos
            );

            LoreCode = null;
            Blockentity.MarkDirty(true);

            return true;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (LoreCode != null) tree.SetString("loreCode", LoreCode);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            MaybeInitialiseMesh_OffThread();

            if (LoreCode != null)
            {
                mesher.AddMeshData(genMesh(new AssetLocation("shapes/block/clutter/"+ Type +"-book.json")));
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Api is ICoreClientAPI capi && capi.Settings.Bool["extendedDebugInfo"] == true)
            {
                dsc.AppendLine("lore code:" + LoreCode);
            }
        }

        private MeshData genMesh(AssetLocation assetLocation)
        {
            return ObjectCacheUtil.GetOrCreate(Api, "bookshelflorebook-" + Variant + " - " + assetLocation.Path + "-" + rotateX + "-" + rotateY + "-" + rotateZ, () =>
            {
                var shape = Api.Assets.TryGet(assetLocation).ToObject<Shape>();
                (Api as ICoreClientAPI).Tesselator.TesselateShape(Block, shape, out var mesh, new Vec3f(rotateX * GameMath.RAD2DEG, rotateY * GameMath.RAD2DEG, rotateZ * GameMath.RAD2DEG));

                if (Variant == "doublesidedold" || Variant == "full")
                {
                    mesh.Translate(0.5f, 0, 0);
                }

                return mesh;
            });
        }
    }
}
