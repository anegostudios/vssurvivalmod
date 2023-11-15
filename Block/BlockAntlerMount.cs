using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class BlockAntlerMount : Block
    {
        string[] types;
        string[] materials;
        Dictionary<string, CompositeTexture> textures;
        CompositeShape cshape;

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

            materials = grp.States;
            if (grp.LoadFromProperties != null)
            {
                var prop = api.Assets.TryGet(grp.LoadFromProperties.WithPathPrefixOnce("worldproperties/").WithPathAppendixOnce(".json"))?.ToObject<StandardWorldProperty>();
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
            var bect = blockAccessor.GetBlockEntity(pos) as BlockEntityAntlerMount;
            float degY = bect == null ? 0 : bect.MeshAngleRad * GameMath.RAD2DEG;
            return new Cuboidf[] { SelectionBoxes[0].RotatedCopy(0, degY, 0, new Vec3d(0.5, 0.5, 0.5)) };
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            // Prefer selected block face
            if (blockSel.Face.IsHorizontal)
            {
                if (TryAttachTo(world, byPlayer, blockSel, itemstack, ref failureCode)) return true;
                if (failureCode == "entityintersecting") return false;
            }

            // Otherwise attach to any possible face
            BlockFacing[] faces = BlockFacing.HORIZONTALS;
            blockSel = blockSel.Clone();
            for (int i = 0; i < faces.Length; i++)
            {
                blockSel.Face = faces[i];
                if (TryAttachTo(world, byPlayer, blockSel, itemstack, ref failureCode)) return true;
            }

            failureCode = "requirehorizontalattachable";

            return false;
        }


        bool TryAttachTo(IWorldAccessor world, IPlayer player, BlockSelection blockSel, ItemStack itemstack, ref string failureCode)
        {
            BlockFacing oppositeFace = blockSel.Face.Opposite;

            BlockPos attachingBlockPos = blockSel.Position.AddCopy(oppositeFace);
            Block attachingBlock = world.BlockAccessor.GetBlock(attachingBlockPos);

            if (attachingBlock.CanAttachBlockAt(world.BlockAccessor, this, attachingBlockPos, blockSel.Face, null) && CanPlaceBlock(world, player, blockSel, ref failureCode))
            {
                DoPlaceBlock(world, player, blockSel, itemstack);
                return true;
            }

            return false;
        }

        bool CanBlockStay(IWorldAccessor world, BlockPos pos)
        {
            var bect = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityAntlerMount;
            var facing = BlockFacing.HorizontalFromAngle((bect?.MeshAngleRad ?? 0) + GameMath.PIHALF);
            Block attachingblock = world.BlockAccessor.GetBlock(pos.AddCopy(facing));

            return attachingblock.CanAttachBlockAt(world.BlockAccessor, this, pos.AddCopy(facing), facing.Opposite);
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            return false;
        }




        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                var bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityAntlerMount;
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

        public Shape GetOrCreateShape(string type, string material)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            var rcshape = this.cshape.Clone();
            rcshape.Base.Path = rcshape.Base.Path.Replace("{type}", type).Replace("{material}", material);
            rcshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            return capi.Assets.TryGet(rcshape.Base)?.ToObject<Shape>();
        }

        public MeshData GetOrCreateMesh(string type, string material, string cachekeyextra=null, ITexPositionSource overrideTexturesource = null)
        {
            var cMeshes = ObjectCacheUtil.GetOrCreate(api, "AntlerMountMeshes", () => new Dictionary<string, MeshData>());
            ICoreClientAPI capi = api as ICoreClientAPI;

            string key = type + "-" + material + cachekeyextra;
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

                capi.Tesselator.TesselateShape("AntlerMount block", shape, out mesh, texSource);

                if (overrideTexturesource == null)
                {
                    cMeshes[key] = mesh;
                }
            }

            return mesh;
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var beb = GetBlockEntity<BlockEntityAntlerMount>(pos);
            if (beb != null)
            {
                var mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).RotateY(beb.MeshAngleRad).Translate(-0.5f, -0.5f, -0.5f).Values;
                blockModelData = GetOrCreateMesh(beb.Type, beb.Material).Clone().MatrixTransform(mat);
                decalModelData = GetOrCreateMesh(beb.Type, beb.Material, null, decalTexSource).Clone().MatrixTransform(mat);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (!CanBlockStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            Dictionary<string, MultiTextureMeshRef> meshRefs;
            meshRefs = ObjectCacheUtil.GetOrCreate(capi, "AntlerMountMeshesInventory", () => new Dictionary<string, MultiTextureMeshRef>());
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



        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var beshelf = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityAntlerMount;
            if (beshelf != null) return beshelf.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);
            var beshelf = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityAntlerMount;
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
            string type = itemStack.Attributes.GetString("type", "square");
            return Lang.Get("block-antlermount-" + type);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            var bemount = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityAntlerMount;
            if (bemount == null) return base.GetPlacedBlockName(world, pos);

            return Lang.Get("block-antlermount-" + bemount.Type);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            var bemount = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityAntlerMount;
            if (bemount == null) return base.GetPlacedBlockInfo(world, pos, forPlayer);

            return base.GetPlacedBlockInfo(world, pos, forPlayer) + "\n" + Lang.Get("Material: {0}", Lang.Get("material-" + bemount.Material));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string wood = inSlot.Itemstack.Attributes.GetString("material", "oak");
            dsc.AppendLine(Lang.Get("Material: {0}", Lang.Get("material-" + wood)));
        }
    }
}
