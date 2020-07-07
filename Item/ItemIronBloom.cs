using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemIronBloom : Item
    {

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            /*if (itemstack.Attributes.HasAttribute("hashCode"))
            {
                int hashcode = itemstack.Attributes.GetInt("hashCode");

                renderinfo.ModelRef = ObjectCacheUtil.GetOrCreate(capi, "ironbloom-" + hashcode, () =>
                {
                    MeshData mesh = GenMesh(capi, itemstack);
                    return capi.Render.UploadMesh(mesh);
                });
            }*/
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (itemStack.Attributes.HasAttribute("voxels"))
            {
                return Lang.Get("Partially worked iron bloom");
            }
            return base.GetHeldItemName(itemStack);
        }

        public MeshData GenMesh(ICoreClientAPI capi, ItemStack stack)
        {
            return null;
        }

        public int GetWorkItemHashCode(ItemStack stack)
        {
            return stack.Attributes.GetHashCode();
        }
    }
}
