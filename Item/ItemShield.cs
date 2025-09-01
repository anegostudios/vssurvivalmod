using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModSystemStopRaiseShieldAnim : ModSystem
    {
        ICoreClientAPI capi;
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.AfterActiveSlotChanged += Event_AfterActiveSlotChanged;
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            capi.World.Player.InventoryManager.GetHotbarInventory().SlotModified += ModSystemStopRaiseShieldAnim_SlotModified;
        }

        private void ModSystemStopRaiseShieldAnim_SlotModified(int slotid)
        {
            maybeStopRaiseShield();
        }
        private void Event_AfterActiveSlotChanged(ActiveSlotChangeEventArgs obj)
        {
            maybeStopRaiseShield();
        }

        private void maybeStopRaiseShield()
        {
            var eplr = capi.World.Player.Entity;
            if (!(eplr.RightHandItemSlot.Itemstack?.Item is ItemShield) && eplr.AnimManager.IsAnimationActive("raiseshield-right"))
            {
                eplr.AnimManager.StopAnimation("raiseshield-right");
            }
        }
    }

    public class ItemShield : Item, IContainedMeshSource, IAttachableToEntity
    {
        float offY;
        float curOffY = 0;
        ICoreClientAPI capi;

        Dictionary<int, MultiTextureMeshRef> meshrefs => ObjectCacheUtil.GetOrCreate(api, "shieldmeshrefs", () => new Dictionary<int, MultiTextureMeshRef>());
        Dictionary<string, Dictionary<string, int>> durabilityGains;
        public string Construction => Variant["construction"];


        IAttachableToEntity attrAtta;
        #region IAttachableToEntity
        public int RequiresBehindSlots { get; set; } = 0;
        string IAttachableToEntity.GetCategoryCode(ItemStack stack) => attrAtta?.GetCategoryCode(stack);
        CompositeShape IAttachableToEntity.GetAttachedShape(ItemStack stack, string slotCode) => attrAtta.GetAttachedShape(stack, slotCode);
        string[] IAttachableToEntity.GetDisableElements(ItemStack stack) => attrAtta.GetDisableElements(stack);
        string[] IAttachableToEntity.GetKeepElements(ItemStack stack) => attrAtta.GetKeepElements(stack);
        string IAttachableToEntity.GetTexturePrefixCode(ItemStack stack)
        {
            string[] attributes = ["wood", "metal", "color", "deco"];
            attributes = [.. attributes.Select(code => stack.Attributes.GetString(code))];
            return attrAtta.GetTexturePrefixCode(stack) + "-" + string.Join("-", attributes);
        }

        void IAttachableToEntity.CollectTextures(ItemStack itemstack, Shape intoShape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        {
            ContainedTextureSource cnts = genTextureSource(itemstack, null);
            if (cnts == null)
            {
                string wood = itemstack.Attributes.GetString("wood");
                string metal = itemstack.Attributes.GetString("metal");
                string color = itemstack.Attributes.GetString("color");
                string deco = itemstack.Attributes.GetString("deco");

                api.Logger.Warning(GetHeldItemName(itemstack) + " (" + Code.ToShortString() + "-" + wood + "-" + metal + "-" + color + "-" + deco + ") could not create textures, may render with unknown textures");
                return;
            }

            foreach (var val in cnts.Textures)
            {
                intoShape.Textures[val.Key] = val.Value;
            }
        }

        public bool IsAttachable(Entity toEntity, ItemStack itemStack) => true;
        #endregion


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            capi = api as ICoreClientAPI;
            if (capi != null) curOffY = offY = FpHandTransform.Translation.Y;

            durabilityGains = Attributes["durabilityGains"].AsObject<Dictionary<string, Dictionary<string, int>>>();

            AddAllTypesToCreativeInventory();

            attrAtta = IAttachableToEntity.FromAttributes(this);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (api.ObjectCache.ContainsKey("shieldmeshrefs") && meshrefs.Count > 0)
            {
                foreach (var (_, meshRef) in meshrefs)
                {
                    meshRef.Dispose();
                }

                ObjectCacheUtil.Delete(api, "shieldmeshrefs");
            }
            base.OnUnloaded(api);
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
            int meshrefid = itemstack.TempAttributes.GetInt("meshRefId");
            if (meshrefid == 0 || !meshrefs.TryGetValue(meshrefid, out renderinfo.ModelRef))
            {
                int id = meshrefs.Count+1;
                var modelref = capi.Render.UploadMultiTextureMesh(GenMesh(itemstack, capi.ItemTextureAtlas));
                renderinfo.ModelRef = meshrefs[id] = modelref;

                itemstack.TempAttributes.SetInt("meshRefId", id);
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }


        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            string onhand = (byEntity.LeftHandItemSlot == slot) ? "left" : "right";
            string notonhand = (byEntity.LeftHandItemSlot == slot) ? "right" : "left";

            if (byEntity.Controls.Sneak && !byEntity.Controls.RightMouseDown)
            {
                if (!byEntity.AnimManager.IsAnimationActive("raiseshield-" + onhand))
                {
                    byEntity.AnimManager.StartAnimation("raiseshield-" + onhand);
                }
            } else
            {
                if (byEntity.AnimManager.IsAnimationActive("raiseshield-" + onhand)) byEntity.AnimManager.StopAnimation("raiseshield-" + onhand);
            }

            if (byEntity.AnimManager.IsAnimationActive("raiseshield-" + notonhand)) byEntity.AnimManager.StopAnimation("raiseshield-" + notonhand);

            base.OnHeldIdle(slot, byEntity);
        }



        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas)
        {
            ContainedTextureSource cnts = genTextureSource(itemstack, targetAtlas);
            if (null == cnts) return new MeshData();

            capi.Tesselator.TesselateItem(this, out MeshData mesh, cnts);
            return mesh;
        }

        private ContainedTextureSource genTextureSource(ItemStack itemstack, ITextureAtlasAPI targetAtlas)
        {
            var cnts = new ContainedTextureSource(api as ICoreClientAPI, targetAtlas, new Dictionary<string, AssetLocation>(), string.Format("For render in shield {0}", Code));

            cnts.Textures.Clear();

            string wood = itemstack.Attributes.GetString("wood");
            string metal = itemstack.Attributes.GetString("metal");
            string color = itemstack.Attributes.GetString("color");
            string deco = itemstack.Attributes.GetString("deco");

            if (wood == null && metal == null && Construction != "crude" && Construction != "blackguard") return null;

            if (wood == null || wood == "") wood = "generic";

            cnts.Textures["front"] = cnts.Textures["back"] = cnts.Textures["handle"] = new AssetLocation("block/wood/planks/generic.png");

            var shape = capi.TesselatorManager.GetCachedShape(this.Shape.Base);

            foreach (var ctex in shape.Textures)
            {
                cnts.Textures[ctex.Key] = ctex.Value;
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
                        cnts.Textures["handle"] = cnts.Textures["back"] = cnts.Textures["front"] = new AssetLocation("block/wood/debarked/" + wood + ".png");
                    }
                    cnts.Textures["rim"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");


                    if (deco == "ornate")
                    {
                        cnts.Textures["front"] = new AssetLocation("item/tool/shield/ornate/" + color + ".png");
                    }
                    break;
                case "woodmetalleather":
                    if (wood != "generic")
                    {
                        cnts.Textures["handle"] = cnts.Textures["back"] = cnts.Textures["front"] = new AssetLocation("block/wood/debarked/" + wood + ".png");
                    }
                    cnts.Textures["front"] = new AssetLocation("item/tool/shield/leather/" + color + ".png");
                    cnts.Textures["rim"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");

                    if (deco == "ornate")
                    {
                        cnts.Textures["front"] = new AssetLocation("item/tool/shield/ornate/" + color + ".png");
                    }

                    break;
                case "metal":
                    cnts.Textures["rim"] = cnts.Textures["handle"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");
                    cnts.Textures["front"] = cnts.Textures["back"] = new AssetLocation("block/metal/plate/" + metal + ".png");

                    if (deco == "ornate")
                    {
                        cnts.Textures["front"] = new AssetLocation("item/tool/shield/ornate/" + color + ".png");
                    }
                    break;
            }

            return cnts;
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

            if (attr["protectionChance"]["active-projectile"].Exists)
            {
                float pacchance = attr["protectionChance"]["active-projectile"].AsFloat(0);
                float ppachance = attr["protectionChance"]["passive-projectile"].AsFloat(0);
                float pflatdmgabsorb = attr["projectileDamageAbsorption"].AsFloat(0);
                dsc.AppendLine("<strong>" + Lang.Get("Projectile protection") + "</strong>");
                dsc.AppendLine(Lang.Get("shield-stats", (int)(100 * pacchance), (int)(100 * ppachance), pflatdmgabsorb));
                dsc.AppendLine();
            }

            float flatdmgabsorb = attr["damageAbsorption"].AsFloat(0);
            float acchance = attr["protectionChance"]["active"].AsFloat(0);
            float pachance = attr["protectionChance"]["passive"].AsFloat(0);

            dsc.AppendLine("<strong>" + Lang.Get("Melee attack protection") + "</strong>");
            dsc.AppendLine(Lang.Get("shield-stats", (int)(100 * acchance), (int)(100 * pachance), flatdmgabsorb));
            dsc.AppendLine();

            switch (Construction)
            {
                case "woodmetal":
                    dsc.AppendLine(Lang.Get("shield-woodtype", Lang.Get("material-" + inSlot.Itemstack.Attributes.GetString("wood"))));
                    dsc.AppendLine(Lang.Get("shield-metaltype", Lang.Get("material-" + inSlot.Itemstack.Attributes.GetString("metal"))));
                    break;

                case "woodmetalleather":
                    dsc.AppendLine(Lang.Get("shield-metaltype", Lang.Get("material-" + inSlot.Itemstack.Attributes.GetString("metal"))));
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
