using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class GuiDialogHairStyling : GuiDialogCreateCharacter
    {
        public Dictionary<string, int> hairStylingCost;

        protected override bool AllowClassSelection => false;
        protected override bool AllowKeepCurrent => true;

        Dictionary<string, string> currentSkin = new Dictionary<string, string>();

        GuiComposer chcomposer;
        long entityId;

        protected override bool AllowedSkinPartSelection(string code)
        {
            return code == "hairbase" || code == "hairextra" || code == "mustache" || code == "beard";
        }

        public GuiDialogHairStyling(ICoreClientAPI capi, long entityId, string[] categorycodes, Dictionary<string, int> dictionary) : base(capi, null)
        {
            this.variantCategories = categorycodes;
            this.hairStylingCost = dictionary;
            this.entityId = entityId;

            currentSkin = getCurrentSkin();

            onBeforeCompose = (composer) =>
            {
                this.chcomposer = composer;
                var cancelBounds = ElementBounds.Fixed(0, dlgHeight - 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(12, 6);
                var costBounds = ElementBounds.Fixed(0, dlgHeight - 55, 130, 30).WithAlignment(EnumDialogArea.RightFixed);

                composer.AddSmallButton(Lang.Get("Cancel"), TryClose, cancelBounds, EnumButtonStyle.Normal);
                composer.AddRichtext("Cost: 0 gears", CairoFont.WhiteSmallText(), costBounds, "costline");
            };
        }

        private Dictionary<string, string> getCurrentSkin()
        {
            Dictionary<string, string> currentSkin = new Dictionary<string, string>();
            var skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            foreach (var skinpart in skinMod.AvailableSkinParts)
            {
                if (!AllowedSkinPartSelection(skinpart.Code)) continue;
                string code = skinpart.Code;
                AppliedSkinnablePartVariant appliedVar = skinMod.AppliedSkinParts.FirstOrDefault(sp => sp.PartCode == code);
                currentSkin[code] = appliedVar.Code;
            }

            return currentSkin;
        }

        protected override bool OnNext()
        {
            int money = InventoryTrader.GetPlayerAssets(capi.World.Player.Entity);
            if (getCost() > money)
            {
                capi.TriggerIngameError(this, "notenoughmoney", Lang.Get("Not enough money"));
                return false;
            }

            capi.Network.GetChannel("hairstyling").SendPacket(new PacketHairStyle()
            {
                HairstylingNpcEntityId = entityId,
                Hairstyle = getCurrentSkin()
            });
            didSelect = true;
            TryClose();

            capi.Gui.PlaySound(new AssetLocation("sounds/effect/cashregister"), false, 0.25f);
            //(owningEntity as EntityTradingHumanoid).TalkUtil?.Talk(EnumTalkType.Purchase);

            return true;
        }

        public override void OnGuiClosed()
        {
            if (!didSelect)
            {
                var skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
                foreach (var val in currentSkin)
                {
                    skinMod.selectSkinPart(val.Key, val.Value);
                }
            }
        }

        protected override void onToggleSkinPart(string partCode, string variantCode)
        {
            base.onToggleSkinPart(partCode, variantCode);
            chcomposer.GetRichtext("costline").SetNewText(Lang.Get("Cost: {0} gears", getCost()), CairoFont.WhiteSmallText());
        }

        protected override void onToggleSkinPart(string partCode, int index)
        {
            base.onToggleSkinPart(partCode, index);
            chcomposer.GetRichtext("costline").SetNewText(Lang.Get("Cost: {0} gears", getCost()), CairoFont.WhiteSmallText());
        }

        public int getCost()
        {
            int cost = 0;

            var skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();

            foreach (var skinpart in skinMod.AvailableSkinParts)
            {
                if (!AllowedSkinPartSelection(skinpart.Code)) continue;
                string code = skinpart.Code;
                AppliedSkinnablePartVariant appliedVar = skinMod.AppliedSkinParts.FirstOrDefault(sp => sp.PartCode == code);

                if (currentSkin[code] != appliedVar.Code)
                {
                    cost += hairStylingCost[code];
                }
            }

            return cost;
        }
    }
}
