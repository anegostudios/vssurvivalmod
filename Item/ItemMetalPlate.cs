using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

#nullable disable

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

            if (api.ModLoader.GetModSystem<SurvivalCoreSystem>().metalsByCode.TryGetValue(metalcode, out MetalPropertyVariant var))
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
                if (!string.Equals(beAnvil.WorkItemStack.Collectible.Variant["metal"], stack.Collectible.Variant["metal"]))
                {
                    if (api.Side == EnumAppSide.Client)
                        (api as ICoreClientAPI).TriggerIngameError(this, "notequal",
                            Lang.Get("Must be the same metal to add voxels"));
                    return null;
                }

                if (AddVoxels(ref beAnvil.Voxels) == 0)
                {
                    if (api.Side == EnumAppSide.Client) (api as ICoreClientAPI).TriggerIngameError(this, "requireshammering", Lang.Get("Try hammering down before adding additional voxels"));
                    return null;
                }
            }

            return workItemStack;
        }


        public virtual int VoxelCountForHandbook(ItemStack stack) => 81;

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

        public static int AddVoxels(ref byte[,,] voxels)
        {
            int totalAdded = 0;
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
                            totalAdded++;
                        }

                        y++;
                    }
                }
            }
            return totalAdded;
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
