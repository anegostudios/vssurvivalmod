using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemIronBloom : Item, IAnvilWorkable
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


        public int GetRequiredAnvilTier(ItemStack stack)
        {
            return 2;
        }


        public List<SmithingRecipe> GetMatchingRecipes(ItemStack stack)
        {
            return api.World.SmithingRecipes
                .Where(r => r.Ingredient.SatisfiesAsIngredient(stack))
                .OrderBy(r => r.Output.ResolvedItemstack.Collectible.Code)
                .ToList()
            ;
        }

        public bool CanWork(ItemStack stack)
        {
            float temperature = stack.Collectible.GetTemperature(api.World, stack);
            float meltingpoint = stack.Collectible.GetMeltingPoint(api.World, null, new DummySlot(stack));

            if (stack.Collectible.Attributes?["workableTemperature"].Exists == true)
            {
                return stack.Collectible.Attributes["workableTemperature"].AsFloat(meltingpoint / 2) <= temperature;
            }

            return temperature >= meltingpoint / 2;
        }

        public ItemStack TryPlaceOn(ItemStack stack, BlockEntityAnvil beAnvil)
        {
            if (stack.Attributes.HasAttribute("voxels"))
            {
                try
                {
                    beAnvil.Voxels = BlockEntityAnvil.deserializeVoxels(stack.Attributes.GetBytes("voxels"));
                    beAnvil.SelectedRecipeId = stack.Attributes.GetInt("selectedRecipeId");
                }
                catch (Exception)
                {
                    CreateVoxelsFromIronBloom(ref beAnvil.Voxels);
                }
            }
            else
            {
                CreateVoxelsFromIronBloom(ref beAnvil.Voxels);
            }


            ItemStack workItemStack = stack.Clone();
            workItemStack.StackSize = 1;
            workItemStack.Collectible.SetTemperature(api.World, workItemStack, stack.Collectible.GetTemperature(api.World, stack));

            return workItemStack.Clone();
        }





        private void CreateVoxelsFromIronBloom(ref byte[,,] voxels)
        {
            ItemIngot.CreateVoxelsFromIngot(api, ref voxels);

            Random rand = api.World.Rand;

            for (int dx = -1; dx < 8; dx++)
            {
                for (int y = 0; y < 5; y++)
                {
                    for (int dz = -1; dz < 5; dz++)
                    {
                        int x = 4 + dx;
                        int z = 6 + dz;

                        if (y == 0 && voxels[x, y, z] == (byte)EnumVoxelMaterial.Metal) continue;

                        float dist = Math.Max(0, Math.Abs(x - 7) - 1) + Math.Max(0, Math.Abs(z - 8) - 1) + Math.Max(0, y - 1f);

                        if (rand.NextDouble() < dist / 3f - 0.4f + (y - 1.5f) / 4f)
                        {
                            continue;
                        }

                        if (rand.NextDouble() > dist / 2f)
                        {
                            voxels[x, y, z] = (byte)EnumVoxelMaterial.Metal;
                        }
                        else
                        {
                            voxels[x, y, z] = (byte)EnumVoxelMaterial.Slag;
                        }
                    }
                }
            }
        }


        public ItemStack GetBaseMaterial(ItemStack stack)
        {
            return stack;
        }

        public EnumHelveWorkableMode GetHelveWorkableMode(ItemStack stack, BlockEntityAnvil beAnvil)
        {
            return EnumHelveWorkableMode.FullyWorkable;
        }
    }
}
