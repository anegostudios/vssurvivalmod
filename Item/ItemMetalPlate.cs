using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemMetalPlate : Item, IAnvilWorkable
    {
        public string GetMetalType()
        {
            return LastCodePart();
        }


        public int GetRequiredAnvilTier(ItemStack stack)
        {
            string metalcode = Variant["metal"];
            int tier = 0;

            MetalPropertyVariant var;
            if (api.ModLoader.GetModSystem<SurvivalCoreSystem>().metalsByCode.TryGetValue(metalcode, out var))
            {
                tier = var.Tier - 1;
            }

            if (stack.Collectible.Attributes?["requiresAnvilTier"].Exists == true)
            {
                tier = stack.Collectible.Attributes["requiresAnvilTier"].AsInt(tier);
            }

            return tier;
        }


        public List<SmithingRecipe> GetMatchingRecipes(ItemStack stack)
        {
            ItemStack basemat = new ItemStack(api.World.GetItem(new AssetLocation("ingot-" + Variant["metal"])));

            return api.GetSmithingRecipes()
                .Where(r => r.Ingredient.SatisfiesAsIngredient(basemat))
                .Where(r => r.Output.ResolvedItemstack.Collectible.Code != stack.Collectible.Code)
                .OrderBy(r => r.Output.ResolvedItemstack.Collectible.Code) // Cannot sort by name, thats language dependent!
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
            if (!CanWork(stack)) return null;

            ItemStack workItemStack = new ItemStack(api.World.GetItem(new AssetLocation("workitem-" + Variant["metal"])));
            workItemStack.Collectible.SetTemperature(api.World, workItemStack, stack.Collectible.GetTemperature(api.World, stack));

            if (beAnvil.WorkItemStack == null)
            {
                CreateVoxels(ref beAnvil.Voxels);
            }
            else
            {
                IAnvilWorkable workable = beAnvil.WorkItemStack.Collectible as IAnvilWorkable;
                if (!workable.GetBaseMaterial(beAnvil.WorkItemStack).Equals(api.World, GetBaseMaterial(stack), GlobalConstants.IgnoredStackAttributes))
                {
                    if (api.Side == EnumAppSide.Client) (api as ICoreClientAPI).TriggerIngameError(this, "notequal", Lang.Get("Must be the same metal to add voxels"));
                    return null;
                }

                AddVoxels(ref beAnvil.Voxels);
            }

            return workItemStack;
        }


        public static void CreateVoxels(ref byte[,,] voxels)
        {
            voxels = new byte[16, 6, 16];

            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 1; y++)
                {
                    for (int z = 0; z < 9; z++)
                    {
                        voxels[3 + x, y, 3 + z] = (byte)EnumVoxelMaterial.Metal;
                    }

                }
            }
        }

        public static void AddVoxels(ref byte[,,] voxels)
        {
            for (int x = 0; x < 9; x++)
            {
                for (int z = 0; z < 9; z++)
                {
                    int y = 0;
                    int added = 0;
                    while (y < 6 && added < 1)
                    {
                        if (voxels[3 + x, y, 3 + z] == (byte)EnumVoxelMaterial.Empty)
                        {
                            voxels[3 + x, y, 3 + z] = (byte)EnumVoxelMaterial.Metal;
                            added++;
                        }

                        y++;
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
            return EnumHelveWorkableMode.NotWorkable;
        }
    }
}
