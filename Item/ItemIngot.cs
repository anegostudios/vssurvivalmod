using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemIngot : Item, IAnvilWorkable
    {
        bool isBlisterSteel;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            isBlisterSteel = Variant["metal"] == "blistersteel";
        }



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
            return api.GetSmithingRecipes()
                .Where(r => r.Ingredient.SatisfiesAsIngredient(stack))
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

            Item item = api.World.GetItem(new AssetLocation("workitem-" + Variant["metal"]));
            if (item == null) return null;

            ItemStack workItemStack = new ItemStack(item);
            workItemStack.Collectible.SetTemperature(api.World, workItemStack, stack.Collectible.GetTemperature(api.World, stack));

            if (beAnvil.WorkItemStack == null)
            {
                CreateVoxelsFromIngot(api, ref beAnvil.Voxels, isBlisterSteel);
            } else
            {
                if (isBlisterSteel) return null;

                IAnvilWorkable workable = beAnvil.WorkItemStack.Collectible as IAnvilWorkable;

                if (!workable.GetBaseMaterial(beAnvil.WorkItemStack).Equals(api.World, GetBaseMaterial(stack), GlobalConstants.IgnoredStackAttributes))
                {
                    if (api.Side == EnumAppSide.Client) (api as ICoreClientAPI).TriggerIngameError(this, "notequal", Lang.Get("Must be the same metal to add voxels"));
                    return null;
                }

                AddVoxelsFromIngot(api, ref beAnvil.Voxels);
            }

            return workItemStack;
        }


        public static void CreateVoxelsFromIngot(ICoreAPI api, ref byte[,,] voxels, bool isBlisterSteel = false)
        {
            voxels = new byte[16, 6, 16];

            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        voxels[4 + x, y, 6 + z] = (byte)EnumVoxelMaterial.Metal;

                        if (isBlisterSteel)
                        {
                            if (api.World.Rand.NextDouble() < 0.5)
                            {
                                voxels[4 + x, y + 1, 6 + z] = (byte)EnumVoxelMaterial.Metal;
                            }
                            if (api.World.Rand.NextDouble() < 0.5)
                            {
                                voxels[4 + x, y + 1, 6 + z] = (byte)EnumVoxelMaterial.Slag;
                            }
                        }
                    }

                }
            }
        }

        public static void AddVoxelsFromIngot(ICoreAPI api, ref byte[,,] voxels, bool isBlisterSteel = false)
        {
            for (int x = 0; x < 7; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    int y = 0;
                    int added = 0;
                    while (y < 6 && added < 2)
                    {
                        if (voxels[4 + x, y, 6 + z] == (byte)EnumVoxelMaterial.Empty)
                        {
                            voxels[4 + x, y, 6 + z] = (byte)EnumVoxelMaterial.Metal;
                            added++;
                        }

                        y++;
                    }

                    if (isBlisterSteel && y < 6)
                    {
                        if (api.World.Rand.NextDouble() < 0.5)
                        {
                            voxels[4 + x, y + 1, 6 + z] = (byte)EnumVoxelMaterial.Metal;
                        }
                        if (api.World.Rand.NextDouble() < 0.5)
                        {
                            voxels[4 + x, y + 1, 6 + z] = (byte)EnumVoxelMaterial.Slag;
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
            return EnumHelveWorkableMode.NotWorkable;
        }
    }
}
