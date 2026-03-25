using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

#nullable disable

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
            return api.GetSmithingRecipes()
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
            // Already occupied anvil
            if (beAnvil.WorkItemStack != null) return null;

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



        public virtual int VoxelCountForHandbook(ItemStack stack) => 42;

        private void CreateVoxelsFromIronBloom(ref byte[,,] voxels)
        {
            ItemIngot.CreateVoxelsFromIngot(api, ref voxels);

            Random rand = api.World.Rand;

            int length = 9;
            int height = 5;
            int width = 6;

            int metalVoxels = 0;
            for (int x = 3; x < 12; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 5; z < 11; z++)
                    {
                        if (y == 0 && voxels[x, y, z] == (byte)EnumVoxelMaterial.Metal)
                        {
                            metalVoxels += 1;
                            continue;
                        }

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

                        if (voxels[x, y, z] == (byte)EnumVoxelMaterial.Metal)
                        {
                            metalVoxels += 1;
                        }
                    }
                }
            }

            // Add metal voxels until there are definitely enough to make an ingot, by shifting up from the bottom
            for (int tries = 0; metalVoxels < ItemIngot.VoxelCount && tries < 1000; ++tries)
            {
                int n = rand.Next(length * width);
                int x = n / width;
                int z = n % width;
                x += 3;
                z += 5;

                if (voxels[x, height, z] == (byte)EnumVoxelMaterial.Metal)
                {
                    continue;
                }
                for (int y = height; y > 0; --y)
                {
                    voxels[x, y, z] = voxels[x, y-1, z];
                }
                voxels[x, 0, z] = (byte)EnumVoxelMaterial.Metal;
                metalVoxels += 1;
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
