﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class CachedMeshRef
    {
        public MultiTextureMeshRef meshref;
        public int TextureId;
    }

    public class ItemWorkItem : Item, IAnvilWorkable
    {
        static int nextMeshRefId = 0;
        public bool isBlisterSteel;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            isBlisterSteel = Variant["metal"] == "blistersteel";
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (!itemstack.Attributes.HasAttribute("voxels"))
            {
                // Probably displayed in the hand book, which calls .Clone() each frame - cannot store any meshref id here
                CachedMeshRef ccmr = ObjectCacheUtil.GetOrCreate(capi, "clearWorkItem" + Variant["metal"], () =>
                {
                    byte[,,] voxels = new byte[16, 6, 16];
                    ItemIngot.CreateVoxelsFromIngot(capi, ref voxels);
                    MeshData mesh = GenMesh(capi, itemstack, voxels, out int textureid);

                    return new CachedMeshRef()
                    {
                        meshref = capi.Render.UploadMultiTextureMesh(mesh),
                        TextureId = textureid
                    };
                });

                renderinfo.ModelRef = ccmr.meshref;
                renderinfo.TextureId = ccmr.TextureId;

                base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
                return;
            }

            int meshrefId = itemstack.Attributes.GetInt("meshRefId", -1);  // NextMeshRefId commenced at 0, so -1 is an impossible actual meshrefId

            if (meshrefId == -1)
            {
                meshrefId = ++nextMeshRefId;
            }

            CachedMeshRef cmr = ObjectCacheUtil.GetOrCreate(capi, "" + meshrefId, () =>
            {
                byte[,,] voxels = GetVoxels(itemstack);
                MeshData mesh = GenMesh(capi, itemstack, voxels, out int textureid);

                return new CachedMeshRef()
                {
                    meshref = capi.Render.UploadMultiTextureMesh(mesh),
                    TextureId = textureid
                };
            });

            renderinfo.ModelRef = cmr.meshref;
            renderinfo.TextureId = cmr.TextureId;

            itemstack.Attributes.SetInt("meshRefId", meshrefId);

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }


        public static MeshData GenMesh(ICoreClientAPI capi, ItemStack workitemStack, byte[,,] voxels, out int textureId)
        {
            textureId = 0;
            if (workitemStack == null) return null;

            MeshData workItemMesh = new MeshData(24, 36, false, true);
            workItemMesh.CustomBytes = new CustomMeshDataPartByte()
            {
                Conversion = DataConversion.NormalizedFloat,
                Count = workItemMesh.VerticesCount,
                InterleaveSizes = new int[] { 1 },
                Instanced = false,
                InterleaveOffsets = new int[] { 0 },
                InterleaveStride = 1,
                Values = new byte[workItemMesh.VerticesCount]
            };

            TextureAtlasPosition tposMetal;
            TextureAtlasPosition tposSlag;


            if (workitemStack.Collectible.FirstCodePart() == "ironbloom")
            {
                tposSlag = capi.BlockTextureAtlas.GetPosition(capi.World.GetBlock(new AssetLocation("anvil-copper")), "ironbloom");
                tposMetal = capi.BlockTextureAtlas.GetPosition(capi.World.GetBlock(new AssetLocation("ingotpile")), "iron");
            }
            else
            {
                tposMetal = capi.BlockTextureAtlas.GetPosition(capi.World.GetBlock(new AssetLocation("ingotpile")), workitemStack.Collectible.Variant["metal"]);
                tposSlag = tposMetal;
            }

            MeshData metalVoxelMesh = CubeMeshUtil.GetCubeOnlyScaleXyz(1 / 32f, 1 / 32f, new Vec3f(1 / 32f, 1 / 32f, 1 / 32f));
            CubeMeshUtil.SetXyzFacesAndPacketNormals(metalVoxelMesh);
            metalVoxelMesh.CustomBytes = new CustomMeshDataPartByte()
            {
                Conversion = DataConversion.NormalizedFloat,
                Count = metalVoxelMesh.VerticesCount,
                Values = new byte[metalVoxelMesh.VerticesCount]
            };

            textureId = tposMetal.atlasTextureId;
            for (int i = 0; i < 6; i++) metalVoxelMesh.AddTextureId(textureId);

            metalVoxelMesh.XyzFaces = (byte[])CubeMeshUtil.CubeFaceIndices.Clone();
            metalVoxelMesh.XyzFacesCount = 6;
            metalVoxelMesh.Rgba.Fill((byte)255);


            MeshData slagVoxelMesh = metalVoxelMesh.Clone();

            for (int i = 0; i < metalVoxelMesh.Uv.Length; i++)
            {
                if (i % 2 > 0)
                {
                    metalVoxelMesh.Uv[i] = tposMetal.y1 + metalVoxelMesh.Uv[i] * 2f / capi.BlockTextureAtlas.Size.Height;

                    slagVoxelMesh.Uv[i] = tposSlag.y1 + slagVoxelMesh.Uv[i] * 2f / capi.BlockTextureAtlas.Size.Height;
                }
                else
                {
                    metalVoxelMesh.Uv[i] = tposMetal.x1 + metalVoxelMesh.Uv[i] * 2f / capi.BlockTextureAtlas.Size.Width;

                    slagVoxelMesh.Uv[i] = tposSlag.x1 + slagVoxelMesh.Uv[i] * 2f / capi.BlockTextureAtlas.Size.Width;
                }
            }

            MeshData metVoxOffset = metalVoxelMesh.Clone();
            MeshData slagVoxOffset = slagVoxelMesh.Clone();

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 6; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        EnumVoxelMaterial mat = (EnumVoxelMaterial)voxels[x, y, z];
                        if (mat == EnumVoxelMaterial.Empty) continue;

                        float px = x / 16f;
                        float py = 10 / 16f + y / 16f;
                        float pz = z / 16f;

                        MeshData mesh = mat == EnumVoxelMaterial.Metal ? metalVoxelMesh : slagVoxelMesh;
                        MeshData meshVoxOffset = mat == EnumVoxelMaterial.Metal ? metVoxOffset : slagVoxOffset;

                        for (int i = 0; i < mesh.xyz.Length; i += 3)
                        {
                            meshVoxOffset.xyz[i] = px + mesh.xyz[i];
                            meshVoxOffset.xyz[i + 1] = py + mesh.xyz[i + 1];
                            meshVoxOffset.xyz[i + 2] = pz + mesh.xyz[i + 2];
                        }

                        float textureSize = 32f / capi.BlockTextureAtlas.Size.Width;

                        float offsetX = px * textureSize;
                        float offsetY = (py * 32f) / capi.BlockTextureAtlas.Size.Width;
                        float offsetZ = pz * textureSize;

                        for (int i = 0; i < mesh.Uv.Length; i += 2)
                        {
                            meshVoxOffset.Uv[i] = mesh.Uv[i] + GameMath.Mod(offsetX + offsetY, textureSize);
                            meshVoxOffset.Uv[i + 1] = mesh.Uv[i + 1] + GameMath.Mod(offsetZ + offsetY, textureSize);
                        }

                        for (int i = 0; i < meshVoxOffset.CustomBytes.Values.Length; i++)
                        {
                            byte glowSub = (byte)GameMath.Clamp(10 * (Math.Abs(x - 8) + Math.Abs(z - 8) + Math.Abs(y - 2)), 100, 250);
                            meshVoxOffset.CustomBytes.Values[i] = (mat == EnumVoxelMaterial.Metal) ? (byte)0 : glowSub;
                        }

                        workItemMesh.AddMeshData(meshVoxOffset);
                    }
                }
            }

            return workItemMesh;
        }



        public virtual int VoxelCountForHandbook(ItemStack stack) => GetVoxels(stack).Cast<byte>().Count(voxel => voxel == (byte)EnumVoxelMaterial.Metal);

        public static byte[,,] GetVoxels(ItemStack workitemStack)
        {
            return BlockEntityAnvil.deserializeVoxels(workitemStack.Attributes.GetBytes("voxels"));
        }



        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            int recipeId = inSlot.Itemstack.Attributes.GetInt("selectedRecipeId");
            SmithingRecipe recipe = api.GetSmithingRecipes().FirstOrDefault(r => r.RecipeId == recipeId);

            if (recipe == null)
            {
                dsc.AppendLine("Unknown work item");
                return;
            }

            dsc.AppendLine(Lang.Get("Unfinished {0}", recipe.Output.ResolvedItemstack.GetName()));
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
            stack = GetBaseMaterial(stack);

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
            if (beAnvil.WorkItemStack != null) return null;

            try
            {
                beAnvil.Voxels = BlockEntityAnvil.deserializeVoxels(stack.Attributes.GetBytes("voxels"));
                beAnvil.SelectedRecipeId = stack.Attributes.GetInt("selectedRecipeId");
            }
            catch (Exception)
            {

            }

            return stack.Clone();
        }


        public ItemStack GetBaseMaterial(ItemStack stack)
        {
            Item item = api.World.GetItem(AssetLocation.Create("ingot-" + Variant["metal"], Attributes?["baseMaterialDomain"].AsString("game")));
            if (item == null)
            {
                throw new Exception(string.Format("Base material for {0} not found, there is no item with code 'ingot-{1}'", stack.Collectible.Code, Variant["metal"]));
            }
            return new ItemStack(item);
        }

        public EnumHelveWorkableMode GetHelveWorkableMode(ItemStack stack, BlockEntityAnvil beAnvil)
        {
            if (beAnvil.SelectedRecipe.Name.Path == "plate" || beAnvil.SelectedRecipe.Name.Path == "blistersteel") return EnumHelveWorkableMode.TestSufficientVoxelsWorkable;
            return EnumHelveWorkableMode.NotWorkable;
        }
    }
}
