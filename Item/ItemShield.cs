using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cairo;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemShield : Item, ITexPositionSource, IContainedMeshSource
    {
        float offY;
        float curOffY = 0;
        ICoreClientAPI capi;

        ITextureAtlasAPI targetAtlas;

        public Size2i AtlasSize => targetAtlas.Size;

        Dictionary<string, AssetLocation> tmpTextures = new Dictionary<string, AssetLocation>();

        Dictionary<int, MeshRef> meshrefs => ObjectCacheUtil.GetOrCreate(api, "shieldmeshrefs", () => new Dictionary<int, MeshRef>());

        public string Construction => Variant["construction"];

        Dictionary<string, Dictionary<string, int>> durabilityGains;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                return getOrCreateTexPos(tmpTextures[textureCode]);
            }
        }

        protected TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
        {
            TextureAtlasPosition texpos = targetAtlas[texturePath];

            if (texpos == null)
            {
                IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (texAsset != null)
                {
                    targetAtlas.GetOrInsertTexture(texturePath, out _, out texpos, () => texAsset.ToBitmap(capi));
                }
                else
                {
                    capi.World.Logger.Warning("For render in shield {0}, require texture {1}, but no such texture found.", Code, texturePath);
                }
            }

            return texpos;
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            curOffY = offY = FpHandTransform.Translation.Y;
            capi = api as ICoreClientAPI;

            durabilityGains = Attributes["durabilityGains"].AsObject<Dictionary<string, Dictionary<string, int>>>();

            AddAllTypesToCreativeInventory();
        }

        public override int GetMaxDurability(ItemStack itemstack)
        {
            int gain = 0;

            foreach (var val in durabilityGains)
            {
                string mat = itemstack.Attributes.GetString(val.Key);
                if (mat != null)
                {
                    val.Value.TryGetValue(mat, out var matgain);
                    gain += matgain;
                }
            }

            return base.GetMaxDurability(itemstack) + gain;
        }

        public void AddAllTypesToCreativeInventory()
        {
            if (Construction == "crude" || Construction == "blackguard") return;

            List<JsonItemStack> stacks = new List<JsonItemStack>();

            var vg = Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>();

            foreach (var metal in vg["metal"])
            {
                switch (Construction)
                {
                    case "woodmetal":
                        foreach (var wood in vg["wood"])
                        {
                            stacks.Add(genJstack(string.Format("{{ wood: \"{0}\", metal: \"{1}\", deco: \"none\" }}", wood, metal)));
                        }
                        break;

                    case "woodmetalleather":
                        foreach (var color in vg["color"])
                        {
                            stacks.Add(genJstack(string.Format("{{ wood: \"{0}\", metal: \"{1}\", color: \"{2}\", deco: \"none\" }}", "generic", metal, color)));
                            if (color != "redblack") stacks.Add(genJstack(string.Format("{{ wood: \"{0}\", metal: \"{1}\", color: \"{2}\", deco: \"ornate\" }}", "generic", metal, color)));
                        }
                        break;

                    case "metal":
                        stacks.Add(genJstack(string.Format("{{ wood: \"{0}\", metal: \"{1}\", deco: \"none\" }}", "generic", metal)));

                        foreach (var color in vg["color"])
                        {
                            
                            if (color != "redblack") stacks.Add(genJstack(string.Format("{{ wood: \"{0}\", metal: \"{1}\", color: \"{2}\", deco: \"ornate\" }}", "generic", metal, color)));
                        }

                        break;
                }
            }

            this.CreativeInventoryStacks = new CreativeTabAndStackList[]
            {
                new CreativeTabAndStackList() { Stacks = stacks.ToArray(), Tabs = new string[]{ "general", "items", "tools" } }
            };
        }


        private JsonItemStack genJstack(string json)
        {
            var jstack = new JsonItemStack()
            {
                Code = this.Code,
                Type = EnumItemClass.Item,
                Attributes = new JsonObject(JToken.Parse(json))
            };

            jstack.Resolve(api.World, "shield type");

            return jstack;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (target == EnumItemRenderTarget.HandFp)
            {
                bool sneak = capi.World.Player.Entity.Controls.Sneak;

                curOffY += ((sneak ? 0.4f : offY) - curOffY) * renderinfo.dt * 8;

                renderinfo.Transform.Translation.X = curOffY;
                renderinfo.Transform.Translation.Y = curOffY * 1.2f;
                renderinfo.Transform.Translation.Z = curOffY * 1.2f;
            }

            int meshrefid = itemstack.TempAttributes.GetInt("meshRefId");
            if (meshrefid == 0 || !meshrefs.TryGetValue(meshrefid, out renderinfo.ModelRef))
            {
                int id = meshrefs.Count+1;
                var modelref = capi.Render.UploadMesh(GenMesh(itemstack, capi.ItemTextureAtlas));
                renderinfo.ModelRef = meshrefs[id] = modelref;

                itemstack.TempAttributes.SetInt("meshRefId", id);
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }



        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas)
        {
            this.targetAtlas = targetAtlas;

            MeshData mesh;
            tmpTextures.Clear();

            string wood = itemstack.Attributes.GetString("wood");
            string metal = itemstack.Attributes.GetString("metal");
            string color = itemstack.Attributes.GetString("color");
            string deco = itemstack.Attributes.GetString("deco");

            ITexPositionSource texSource = this;

            if (wood == null && metal == null && Construction != "crude" && Construction != "blackguard") return new MeshData();

            if (wood == null || wood == "") wood = "generic";

            tmpTextures["front"] = tmpTextures["back"] = tmpTextures["handle"] = new AssetLocation("block/wood/planks/generic.png");

            var shape = capi.TesselatorManager.GetCachedShape(this.Shape.Base);

            foreach (var ctex in shape.Textures)
            {
                tmpTextures[ctex.Key] = ctex.Value;
            }

            switch (Construction)
            {
                case "crude":
                    break;
                case "blackguard":
                    break;
                case "woodmetal":
                    if (wood != "generic")
                    {
                        tmpTextures["handle"] = tmpTextures["back"] = tmpTextures["front"] = new AssetLocation("block/wood/debarked/" + wood + ".png");
                    }
                    tmpTextures["rim"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");


                    if (deco == "ornate")
                    {
                        tmpTextures["front"] = new AssetLocation("item/tool/shield/ornate/" + color + ".png");
                    }
                    break;
                case "woodmetalleather":
                    if (wood != "generic")
                    {
                        tmpTextures["handle"] = tmpTextures["back"] = tmpTextures["front"] = new AssetLocation("block/wood/debarked/" + wood + ".png");
                    }
                    tmpTextures["front"] = new AssetLocation("item/tool/shield/leather/" + color + ".png");
                    tmpTextures["rim"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");

                    if (deco == "ornate")
                    {
                        tmpTextures["front"] = new AssetLocation("item/tool/shield/ornate/" + color + ".png");
                    }

                    break;
                case "metal":
                    tmpTextures["rim"] = tmpTextures["handle"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");
                    tmpTextures["front"] = tmpTextures["back"] = new AssetLocation("block/metal/plate/" + metal + ".png");

                    if (deco == "ornate")
                    {
                        tmpTextures["front"] = new AssetLocation("item/tool/shield/ornate/" + color + ".png");
                    }
                    break;
            }

            capi.Tesselator.TesselateItem(this, out mesh, texSource);

            return mesh;
        }

         
        public override string GetHeldItemName(ItemStack itemStack)
        {
            bool ornate = itemStack.Attributes.GetString("deco") == "ornate";
            string metal = itemStack.Attributes.GetString("metal");
            string wood = itemStack.Attributes.GetString("wood");
            string color = itemStack.Attributes.GetString("color");

            switch (Construction)
            {
                case "crude":
                    return Lang.Get("Crude shield");
                case "woodmetal":
                    if (wood == "generic")
                    {
                        return ornate ? Lang.Get("Ornate wooden shield") : Lang.Get("Wooden shield");
                    }
                    if (wood == "aged")
                    {
                        return ornate ? Lang.Get("Aged ornate shield") : Lang.Get("Aged wooden shield");
                    }
                    return ornate ? Lang.Get("Ornate {0} shield", Lang.Get("material-" + wood)) : Lang.Get("{0} shield", Lang.Get("material-" + wood));
                case "woodmetalleather":
                    return ornate ? Lang.Get("Ornate leather reinforced wooden shield") : Lang.Get("Leather reinforced wooden shield");
                case "metal":
                    return ornate ? Lang.Get("shield-ornatemetal", Lang.Get("color-" + color), Lang.Get("material-" + metal)) : Lang.Get("shield-withmaterial", Lang.Get("material-" + metal));
                case "blackguard":
                    return Lang.Get("Blackguard shield");
            }

            return base.GetHeldItemName(itemStack);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            var attr = inSlot.Itemstack?.ItemAttributes?["shield"];
            if (attr == null || !attr.Exists) return;

            float acdmgabsorb = attr["damageAbsorption"]["active"].AsFloat(0);
            float acchance = attr["protectionChance"]["active"].AsFloat(0);

            float padmgabsorb = attr["damageAbsorption"]["passive"].AsFloat(0);
            float pachance = attr["protectionChance"]["passive"].AsFloat(0);

            dsc.AppendLine(Lang.Get("shield-stats", (int)(100*acchance), (int)(100*pachance), acdmgabsorb, padmgabsorb));

            switch (Construction)
            {
                case "woodmetal":
                    dsc.AppendLine("Wood: " + inSlot.Itemstack.Attributes.GetString("wood"));
                    dsc.AppendLine("Metal: " + inSlot.Itemstack.Attributes.GetString("metal"));
                    break;

                case "woodmetalleather":
                    dsc.AppendLine("Metal: " + inSlot.Itemstack.Attributes.GetString("metal"));
                    break;

            }

            
        }

        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
        {
            return GenMesh(itemstack, targetAtlas);
        }

        public string GetMeshCacheKey(ItemStack itemstack)
        {
            string wood = itemstack.Attributes.GetString("wood");
            string metal = itemstack.Attributes.GetString("metal");
            string color = itemstack.Attributes.GetString("color");
            string deco = itemstack.Attributes.GetString("deco");

            return Code.ToShortString() + "-" + wood + "-" + metal + "-" + color + "-" + deco;
        }
    }
}
