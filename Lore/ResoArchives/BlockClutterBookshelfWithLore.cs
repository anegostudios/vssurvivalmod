using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockClutterBookshelfWithLore : BlockClutterBookshelf
    {
        public BlockClutterBookshelfWithLore()
        {
        }

        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "bookshelfWithLoreInteractions", () =>
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-takelorebook",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            });
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            var meshref = genCombinedMesh(itemstack);
            if (meshref != null)
            {
                renderinfo.ModelRef = meshref;
            }
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        private MultiTextureMeshRef genCombinedMesh(ItemStack itemstack)
        {
            var cachedRefs = ObjectCacheUtil.GetOrCreate(api, "combinedBookShelfWithLoreMeshRef", () => new Dictionary<string, MultiTextureMeshRef>());

            string type = itemstack.Attributes.GetString("type", itemstack.Attributes.GetString("type1"));
            if (type == null) return null;

            if (cachedRefs.TryGetValue(type, out var meshref)) return meshref;
            
            var mesh = GetOrCreateMesh(GetTypeProps(type, itemstack, null));
            var loc = new AssetLocation("shapes/block/clutter/" + type + "-book.json");
            var shape = api.Assets.TryGet(loc).ToObject<Shape>();
            (api as ICoreClientAPI).Tesselator.TesselateShape(this, shape, out var bookmesh);

            mesh.AddMeshData(bookmesh);

            return cachedRefs[type] = (api as ICoreClientAPI).Render.UploadMultiTextureMesh(mesh);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var be = GetBEBehavior<BEBehaviorClutterBookshelfWithLore>(pos);
            var stack = base.OnPickBlock(world, pos);
            if (be != null) stack.Attributes.SetString("loreCode", be.LoreCode);
            return stack;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            var loreCode = inSlot.Itemstack.Attributes.GetString("loreCode");
            if (loreCode != null)
            {
                dsc.AppendLine("lore code:" + loreCode);
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = GetBEBehavior<BEBehaviorClutterBookshelfWithLore>(blockSel.Position);
            if (be != null) return be.OnInteract(byPlayer);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
