using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class BlockScrollRack : Block
    {
        string[] types;
        string[] materials;
        Dictionary<string, CompositeTexture> textures;
        CompositeShape cshape;
        public Cuboidf[] slotsHitBoxes;
        public string[] slotSide;
        public int[] oppositeSlotIndex;

        public Dictionary<string, int[]> slotsBySide = new Dictionary<string, int[]>();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            LoadTypes();
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            var meshRefs = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, "ScrollrackMeshesInventory");
            if (meshRefs?.Count > 0)
            {
                foreach (var (_, meshRef) in meshRefs)
                {
                    meshRef.Dispose();
                }
                ObjectCacheUtil.Delete(api, "ScrollrackMeshesInventory");
            }
            base.OnUnloaded(api);
        }

        public void LoadTypes()
        {
            types = Attributes["types"].AsArray<string>();
            cshape = Attributes["shape"].AsObject<CompositeShape>();
            textures = Attributes["textures"].AsObject<Dictionary<string, CompositeTexture>>(null);
            slotsHitBoxes = Attributes["slotsHitBoxes"].AsObject<Cuboidf[]>(null);
            slotSide = Attributes["slotSide"].AsObject<string[]>(null);
            oppositeSlotIndex = Attributes["oppositeSlotIndex"].AsObject<int[]>(null);
            var grp = Attributes["materials"].AsObject<RegistryObjectVariantGroup>();

            materials = grp.States;
            if (grp.LoadFromProperties != null)
            {
                var prop = api.Assets.TryGet(grp.LoadFromProperties.WithPathPrefixOnce("worldproperties/").WithPathAppendixOnce(".json"))?.ToObject<StandardWorldProperty>();
                materials = prop.Variants.Select(p => p.Code.Path).ToArray().Append(materials);
            }

            for (int i = 0; i < slotSide.Length; i++)
            {
                var side = slotSide[i];
                int[] slots;
                if (slotsBySide.TryGetValue(side, out slots))
                {
                    slots = slots.Append(i);
                } else
                {
                    slots = new int[] { i };
                }

                slotsBySide[side] = slots;
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
                        Attributes = new JsonObject(JToken.Parse("{ \"type\": \"" + type + "\", \"material\": \"" + material + "\" }"))
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
            var beb = GetBlockEntity<BlockEntityScrollRack>(pos);
            return beb?.getOrCreateSelectionBoxes() ?? base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                var bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityScrollRack;
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
            var cMeshes = ObjectCacheUtil.GetOrCreate(api, "ScrollrackMeshes", () => new Dictionary<string, MeshData>());
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

                capi.Tesselator.TesselateShape("Scrollrack block", shape, out mesh, texSource);

                if (overrideTexturesource == null)
                {
                    cMeshes[key] = mesh;
                }
            }

            return mesh;
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var beb = GetBlockEntity<BlockEntityScrollRack>(pos);
            if (beb != null)
            {
                var mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).RotateY(beb.MeshAngleRad).Translate(-0.5f, -0.5f, -0.5f).Values;
                blockModelData = GetOrCreateMesh(beb.Type, beb.Material).Clone().MatrixTransform(mat);
                decalModelData = GetOrCreateMesh(beb.Type, beb.Material, decalTexSource).Clone().MatrixTransform(mat);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            var beb = GetBlockEntity<BlockEntityScrollRack>(pos);
            beb?.clearUsableSlots();

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            Dictionary<string, MultiTextureMeshRef> meshRefs;
            meshRefs = ObjectCacheUtil.GetOrCreate(capi, "ScrollrackMeshesInventory", () => new Dictionary<string, MultiTextureMeshRef>());
            MultiTextureMeshRef meshref;

            string type = itemstack.Attributes.GetString("type", "");
            string material = itemstack.Attributes.GetString("material", "");
            string key = type + "-" + material;

            if (!meshRefs.TryGetValue(key, out meshref))
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
            var beshelf = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityScrollRack;
            if (beshelf != null) return beshelf.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);
            var beshelf = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityScrollRack;
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

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            var drops = base.GetDropsForHandbook(handbookStack, forPlayer);
            drops[0] = drops[0].Clone();
            drops[0].ResolvedItemstack.SetFrom(handbookStack);

            return drops;
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            return Lang.Get("block-scrollrack-" + itemStack.Attributes.GetString("material"));
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            var beshelf = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityScrollRack;
            if (beshelf != null)
            {
                return Lang.Get("block-scrollrack-" + beshelf.Material);
            }

            return base.GetPlacedBlockName(world, pos);
        }
    }
}
