using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class CachedMeshRef
    {
        public MeshRef meshref;
        public int TextureId;
    }

    public class ItemWorkItem : Item, IAnvilWorkable
    {
        static int nextMeshRefId = 0;

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            int meshrefId = itemstack.Attributes.GetInt("meshRefId");

            if (!itemstack.Attributes.HasAttribute("meshRefId"))
            {
                meshrefId = ++nextMeshRefId;
            }

            CachedMeshRef cmr = ObjectCacheUtil.GetOrCreate(capi, "" + meshrefId, () =>
            {
                int textureid;
                byte[,,] voxels = GetVoxels(itemstack);
                MeshData mesh = GenMesh(capi, itemstack, voxels, out textureid);

                return new CachedMeshRef()
                {
                    meshref = capi.Render.UploadMesh(mesh),
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

            MeshData workItemMesh = new MeshData(24, 36, false);
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
                tposMetal = capi.BlockTextureAtlas.GetPosition(capi.World.GetBlock(new AssetLocation("ingotpile")), workitemStack.Collectible.LastCodePart());
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

            metalVoxelMesh.XyzFaces = (byte[])CubeMeshUtil.CubeFaceIndices.Clone();
            metalVoxelMesh.XyzFacesCount = 6;

            //metalVoxelMesh.ColorMapIds = new int[6];
            //metalVoxelMesh.TintsCount = 6;

            for (int i = 0; i < metalVoxelMesh.Rgba.Length; i++) metalVoxelMesh.Rgba[i] = 255;
            //metalVoxelMesh.Rgba2 = null;


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



        public static byte[,,] GetVoxels(ItemStack workitemStack)
        {
            return BlockEntityAnvil.deserializeVoxels(workitemStack.Attributes.GetBytes("voxels"));
        }



        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            int recipeId = inSlot.Itemstack.Attributes.GetInt("selectedRecipeId");
            SmithingRecipe recipe = api.World.SmithingRecipes.FirstOrDefault(r => r.RecipeId == recipeId);

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
            stack = GetBaseMaterial(stack);

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
            return new ItemStack(api.World.GetItem(new AssetLocation("ingot-" + Variant["metal"])));
        }

        public EnumHelveWorkableMode GetHelveWorkableMode(ItemStack stack, BlockEntityAnvil beAnvil)
        {
            if (beAnvil.SelectedRecipe.Name.Path == "plate" || beAnvil.SelectedRecipe.Name.Path == "blistersteel") return EnumHelveWorkableMode.TestSufficientVoxelsWorkable;
            return EnumHelveWorkableMode.NotWorkable;
        }
    }
}
