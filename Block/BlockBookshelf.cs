﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockBookshelf : Block
    {
        string[] types;
        string[] materials;
        Dictionary<string, CompositeTexture> textures;
        CompositeShape cshape;
        public Dictionary<string, int[]> UsableSlots;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            LoadTypes();
        }

        public void LoadTypes()
        {
            types = Attributes["types"].AsArray<string>();
            cshape = Attributes["shape"].AsObject<CompositeShape>();
            textures = Attributes["textures"].AsObject<Dictionary<string, CompositeTexture>>(null);
            var grp = Attributes["materials"].AsObject<RegistryObjectVariantGroup>();

            UsableSlots = Attributes["usableSlots"].AsObject<Dictionary<string, int[]>>();

            materials = grp.States;
            if (grp.LoadFromProperties != null)
            {
                var prop = api.Assets.TryGet(grp.LoadFromProperties.WithPathPrefixOnce("worldproperties/").WithPathAppendixOnce(".json"))?.ToObject< StandardWorldProperty>();
                materials = prop.Variants.Select(p => p.Code.Path).ToArray().Append(materials);
            }

            List<JsonItemStack> stacks = new List<JsonItemStack>();

            foreach (var type in types)
            {
                foreach (var material in materials)
                {
                    var jstack = new JsonItemStack()
                    {
                        Code = this.Code,
                        Type = EnumItemClass.Block,
                        Attributes = new JsonObject(JToken.Parse("{ \"type\": \"" + type + "\", \"material\": \""+material+"\" }"))
                    };

                    jstack.Resolve(api.World, Code + " type");
                    stacks.Add(jstack);
                }
            }

            this.CreativeInventoryStacks = new CreativeTabAndStackList[]
            {
                new CreativeTabAndStackList() { Stacks = stacks.ToArray(), Tabs = new string[]{ "general", "decorative" } }
            };
        }


        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var beshelf = blockAccessor.GetBlockEntity(pos) as BlockEntityBookshelf;

            if (beshelf?.UsableSlots != null)
            {
                List<Cuboidf> cubs = new List<Cuboidf>
                {
                    new Cuboidf(0, 0, 0, 1, 1, 0.1f),
                    new Cuboidf(0, 0, 0, 1, 1 / 16f, 0.5f),
                    new Cuboidf(0, 15 / 16f, 0, 1, 1, 0.5f),
                    new Cuboidf(0, 0, 0, 1 / 16f, 1, 0.5f),
                    new Cuboidf(15 / 16f, 0, 0, 1, 1, 0.5f)
                };

                for (int i = 0; i < 14; i++)
                {
                    if (!beshelf.UsableSlots.Contains(i)) { 
                        cubs.Add(new Cuboidf());
                        continue;
                    }

                    float x = (i % 7) * 2f / 16f + 1.1f / 16f;
                    float y = (i / 7) * 7.5f / 16f;
                    float z = 6.5f / 16f;
                    var cub = new Cuboidf(x, y + 1f/16f, 1/16f, x + 1.9f/16f, y + 7/16f, z);
                    
                    

                    cubs.Add(cub);
                }

                for (int i = 0; i < cubs.Count; i++) cubs[i] = cubs[i].RotatedCopy(0, (beshelf?.MeshAngleRad ?? 0) * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5));

                return cubs.ToArray();
            }

            return new Cuboidf[] { new Cuboidf(0, 0, 0, 1, 1, 0.5f).RotatedCopy(0, (beshelf?.MeshAngleRad ?? 0) * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5)) };
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var beshelf = blockAccessor.GetBlockEntity(pos) as BlockEntityBookshelf;

            return new Cuboidf[] { new Cuboidf(0, 0, 0, 1, 1, 0.5f).RotatedCopy(0, (beshelf?.MeshAngleRad ?? 0) * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5)) };
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                BlockEntityBookshelf bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBookshelf;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float intervalRad = GameMath.PIHALF;
                    float roundRad = ((int)Math.Round(angleHor / intervalRad)) * intervalRad;
                    bect.MeshAngleRad = roundRad;
                    bect.OnBlockPlaced(byItemStack); // call again to regen mesh
                }
            }

            return val;
        }

        public virtual MeshData GetOrCreateMesh(string type, string material, ITexPositionSource overrideTexturesource = null)
        {
            var cMeshes = ObjectCacheUtil.GetOrCreate(api, "BookshelfMeshes", () => new Dictionary<string, MeshData>());
            ICoreClientAPI capi = api as ICoreClientAPI;
            
            string key = type + "-" + material;
            if (overrideTexturesource != null || !cMeshes.TryGetValue(key, out var mesh))
            {
                mesh = new MeshData(4, 3);

                var rcshape = this.cshape.Clone();
                rcshape.Base.Path = rcshape.Base.Path.Replace("{type}", type).Replace("{material}", material);
                rcshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");

                var shape = capi.Assets.TryGet(rcshape.Base)?.ToObject<Shape>();

                var texSource = overrideTexturesource;
                if (texSource == null)
                {
                    var stexSource = new ShapeTextureSource(capi, shape, rcshape.Base.ToString());
                    texSource = stexSource;
                    foreach (var val in textures)
                    {
                        var ctex = val.Value.Clone();
                        ctex.Base.Path = ctex.Base.Path.Replace("{type}", type).Replace("{material}", material);
                        ctex.Bake(capi.Assets);

                        stexSource.textures[val.Key] = ctex;
                    }
                }
                if (shape == null) return mesh;

                capi.Tesselator.TesselateShape("Bookshelf block", shape, out mesh, texSource);

                /*if (cprops.texPos == null)
                {
                    api.Logger.Warning("No texture previously loaded for clutter block " + key);
                    cprops.texPos = texSource.firstTexPos;
                    cprops.texPos.RndColors = new int[TextureAtlasPosition.RndColorsLength];
                }*/

                if (overrideTexturesource == null)
                {
                    cMeshes[key] = mesh;
                }
            }

            return mesh;
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var beb = GetBlockEntity<BlockEntityBookshelf>(pos);
            if (beb != null)
            {
                var mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).RotateY(beb.MeshAngleRad).Translate(-0.5f, -0.5f, -0.5f).Values;
                blockModelData = GetOrCreateMesh(beb.Type, beb.Material).Clone().MatrixTransform(mat);
                decalModelData = GetOrCreateMesh(beb.Type, beb.Material, decalTexSource).Clone().MatrixTransform(mat);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }



        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            Dictionary<string, MultiTextureMeshRef> meshRefs;
            meshRefs = ObjectCacheUtil.GetOrCreate(capi, "BookshelfMeshesInventory", () => new Dictionary<string, MultiTextureMeshRef>());

            string type = itemstack.Attributes.GetString("type", "");
            string material = itemstack.Attributes.GetString("material", "");
            string key = type + "-" + material;

            if (!meshRefs.TryGetValue(key, out MultiTextureMeshRef meshref))
            {
                MeshData mesh = GetOrCreateMesh(type, material);
                meshref = capi.Render.UploadMultiTextureMesh(mesh);
                meshRefs[key] = meshref;
            }

            renderinfo.ModelRef = meshref;
        }



        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var beshelf = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBookshelf;
            if (beshelf != null) return beshelf.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);
            var beshelf = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBookshelf;
            if (beshelf != null)
            {
                stack.Attributes.SetString("type", beshelf.Type);
                stack.Attributes.SetString("material", beshelf.Material);
            }

            return stack;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[] { OnPickBlock(world, pos) };
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);

            var meshRefs = ObjectCacheUtil.TryGet< Dictionary<string, MultiTextureMeshRef>>(api, "BookshelfMeshesInventory");
            if (meshRefs != null)
            {
                foreach (var val in meshRefs) val.Value?.Dispose();
            }
        }
    }
}
