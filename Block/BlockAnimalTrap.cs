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

namespace Vintagestory.GameContent
{

    public class BlockAnimalTrap : Block
    {
        protected float rotInterval = GameMath.PIHALF / 4;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreClientAPI capi) AddTrappableHandbookInfo(capi);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                var be = GetBlockEntity<BlockEntityAnimalTrap>(blockSel.Position);
                if (be != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float roundRad = ((int)Math.Round(angleHor / rotInterval)) * rotInterval;

                    be.RotationYDeg = roundRad * GameMath.RAD2DEG;
                    be.MarkDirty(true);
                }
            }

            return val;
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack? byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);

            if (byItemStack?.Attributes.GetBool("destroyed") == true) GetBlockEntity<BlockEntityAnimalTrap>(blockPos).TrapState = EnumTrapState.Destroyed;
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = GetBlockEntity<BlockEntityAnimalTrap>(blockSel.Position);
            if (be != null) return be.Interact(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            bool destroyed = handbookStack.Attributes.GetBool("destroyed");
            ItemStack[] stacks = destroyed ? getDestroyedDrops(api.World, forPlayer.Entity.Pos.XYZ.AsBlockPos, forPlayer) : GetDrops(api.World, forPlayer.Entity.Pos.XYZ.AsBlockPos, forPlayer);
            if (stacks == null) return [];

            return [.. stacks.Select(stack => new BlockDropItemStack(stack))];
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var be = GetBlockEntity<BlockEntityAnimalTrap>(pos);
            if (be?.TrapState == EnumTrapState.Trapped)
            {
                var jstack = Attributes["creatureContainer"].AsObject<JsonItemStack>();
                if (be.Inventory[0].Empty && jstack != null)
                {
                    jstack.Resolve(world, "creature container of " + Code);
                    return [jstack.ResolvedItemstack];
                }
                return [];
            }
            if (be?.TrapState == EnumTrapState.Destroyed) return getDestroyedDrops(world, pos, byPlayer, dropQuantityMultiplier);

            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        private ItemStack[] getDestroyedDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (Attributes?["destroyedDrops"]?.AsObject<BlockDropItemStack[]>() is not BlockDropItemStack[] destroyedDrops) return [];

            List<ItemStack> todrop = new List<ItemStack>();
            for (int i = 0; i < destroyedDrops.Length; i++)
            {
                BlockDropItemStack dstack = destroyedDrops[i];
                dstack.Resolve(world, "Block ", Code);
                ItemStack? stack = dstack.ToRandomItemstackForPlayer(byPlayer, world, dropQuantityMultiplier);
                if (stack != null) todrop.Add(stack);
                if (dstack.LastDrop) break;
            }
            return [.. todrop];
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (itemStack.Attributes.GetBool("destroyed")) return Lang.GetMatching(Code.Domain + AssetLocation.LocationSeparator + "block-" + Code.Path + "-destroyed");

            return base.GetHeldItemName(itemStack);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);

            if (GetBlockEntity<BlockEntityAnimalTrap>(pos)?.TrapState == EnumTrapState.Destroyed) stack.Attributes.SetBool("destroyed", true);

            return stack;
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var be = GetBlockEntity<BlockEntityAnimalTrap>(pos);
            if (be != null)
            {
                blockModelData = be.GetCurrentMesh(null).Clone().Rotate(Vec3f.Half, 0, (be.RotationYDeg-90) * GameMath.DEG2RAD, 0);
                decalModelData = be.GetCurrentMesh(decalTexSource).Clone().Rotate(Vec3f.Half, 0, (be.RotationYDeg-90) * GameMath.DEG2RAD, 0);

                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);

        }

        public bool IsAppetizingBait(ICoreAPI api, ItemStack baitStack)
        {
            var collobj = baitStack.Collectible;

            return (collobj.GetNutritionProperties(api.World, baitStack, null) != null || collobj.Attributes?["foodTags"].Exists == true) &&
                api.World.EntityTypes.Any(type => type.Attributes?["creatureDiet"].AsObject<CreatureDiet>()?.Matches(baitStack, true, 0.5f) == true);
        }

        public bool CanFitBait(ICoreAPI api, ItemStack baitStack)
        {
            var collobj = baitStack.Collectible;

            return Attributes?["excludeFoodTags"].AsArray<string>()?.Any(tag => collobj.Attributes?["foodTags"].AsArray<string>()?.Contains(tag) == true) != true;
        }

        protected virtual void AddTrappableHandbookInfo(ICoreClientAPI capi)
        {
            JToken token;
            ExtraHandbookSection[]? extraHandbookSections = Attributes?["handbook"]?["extraSections"]?.AsObject<ExtraHandbookSection[]>();

            if (extraHandbookSections?.FirstOrDefault(s => s?.Title == "handbook-trappableanimals-title") != null) return;

            if (Attributes?["handbook"].Exists != true)
            {
                if (Attributes == null) Attributes = new JsonObject(JToken.Parse("{ handbook: {} }"));
                else
                {
                    token = Attributes.Token!;
                    token["handbook"] = JToken.Parse("{ }");
                }
            }

            string trapType = Attributes["traptype"].AsString("small");
            HashSet<string> creatureNames = [];
            foreach (var entityType in capi.World.EntityTypes)
            {
                var attr = entityType.Attributes;
                string code = attr?["handbook"]?["trappableGroupCode"]?.AsString() ?? entityType.Code.Domain + ":item-creature-" + entityType.Code.Path;
                if (attr?["trappable"]?[trapType]?["trapChance"]?.AsFloat() > 0) creatureNames.Add(Lang.GetMatching(code));
            }

            ExtraHandbookSection section = new() { Title = "handbook-trappableanimals-title", Text = string.Join(", ", creatureNames) };
            if (extraHandbookSections != null) extraHandbookSections.Append(section);
            else extraHandbookSections = [section];

            token = Attributes["handbook"].Token!;
            token["extraSections"] = JToken.FromObject(extraHandbookSections);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (itemstack.Attributes.GetBool("destroyed"))
            {
                var cshape = Attributes["destroyedShape"].AsObject<CompositeShape>(null, Code.Domain);
                ArgumentNullException.ThrowIfNull(cshape);
                string key = Variant["material"] + "BasketTrap-" + cshape.ToString();
                var meshrefs = ObjectCacheUtil.GetOrCreate(capi, "animalTrapDestroyedMeshRefs", () => new Dictionary<string, MultiTextureMeshRef>());

                if (!meshrefs.TryGetValue(key, out renderinfo.ModelRef))
                {
                    MeshData mesh = ObjectCacheUtil.GetOrCreate(capi, key, () =>
                        capi.TesselatorManager.CreateMesh(
                            "basket trap decal",
                            cshape,
                            (shape, name) => new ShapeTextureSource(capi, shape, name),
                            capi.Tesselator.GetTextureSource(this)
                        ));

                    meshrefs[key] = renderinfo.ModelRef = capi.Render.UploadMultiTextureMesh(mesh);
                }
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }
    }
}
