using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

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

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                itemslot.MarkDirty();
                return;
            }

            BlockIngotPile block = byEntity.World.GetBlock(new AssetLocation("ingotpile")) as BlockIngotPile;
            if (block == null) return;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityIngotPile)
            {
                BlockEntityIngotPile pile = (BlockEntityIngotPile)be;
                if (pile.OnPlayerInteract(byPlayer))
                {
                    handHandling = EnumHandHandling.PreventDefault;
                }
                return;
            }

            if (be is BlockEntityAnvil)
            {
                return;
            }

            BlockPos pos = blockSel.Position.AddCopy(blockSel.Face);
            if (byEntity.World.BlockAccessor.GetBlock(pos).Replaceable < 6000) return;

            be = byEntity.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityIngotPile)
            {
                BlockEntityIngotPile pile = (BlockEntityIngotPile)be;
                if (pile.OnPlayerInteract(byPlayer))
                {
                    handHandling = EnumHandHandling.PreventDefault;
                }
                return;
            }


            if (block.Construct(itemslot, byEntity.World, blockSel.Position.AddCopy(blockSel.Face), byPlayer))
            {
                handHandling = EnumHandHandling.PreventDefault;
            }
            
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    HotKeyCode = "sneak",
                    ActionLangCode = "heldhelp-place",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
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
            return api.World.SmithingRecipes
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

            ItemStack workItemStack = new ItemStack(api.World.GetItem(new AssetLocation("workitem-" + Variant["metal"])));
            workItemStack.Collectible.SetTemperature(api.World, workItemStack, stack.Collectible.GetTemperature(api.World, stack));

            if (beAnvil.WorkItemStack == null)
            {
                CreateVoxelsFromIngot(api, ref beAnvil.Voxels, isBlisterSteel);
            } else
            {
                if (isBlisterSteel) return null;
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
