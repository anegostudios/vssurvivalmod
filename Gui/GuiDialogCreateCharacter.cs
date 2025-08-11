using System;
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

    public class GuiDialogCreateCharacter : GuiDialog
    {
        protected bool didSelect = false;
        protected IInventory characterInv;
        protected ElementBounds insetSlotBounds;

        protected Action<GuiComposer> onBeforeCompose;

        CharacterSystem modSys;
        int currentClassIndex = 0;

        int curTab = 0;
        int rows = 7;

        float charZoom = 1f;
        bool charNaked = true;

        protected int dlgHeight = 433 + 80;

        public GuiDialogCreateCharacter(ICoreClientAPI capi, CharacterSystem modSys) : base(capi)
        {
            this.modSys = modSys;
        }

        public string[] variantCategories = ["standard"];

        protected virtual bool AllowClassSelection => true;
        protected virtual bool AllowKeepCurrent => false;
        protected virtual bool AllowedSkinPartSelection(string code) => true;

        protected void ComposeGuis()
        {
            double pad = GuiElementItemSlotGridBase.unscaledSlotPadding;
            double slotsize = GuiElementPassiveItemSlot.unscaledSlotSize;

            characterInv = capi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

            ElementBounds tabBounds = ElementBounds.Fixed(0, -25, 450, 25);

            double ypos = 20 + pad;

            ElementBounds bgBounds = ElementBounds.FixedSize(717, dlgHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);

            ElementBounds dialogBounds = ElementBounds.FixedSize(757, dlgHeight+40).WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);


            GuiTab[] tabs = new GuiTab[] {
                new GuiTab() { Name = Lang.Get("tab-skinandvoice"), DataInt = 0 },
                new GuiTab() { Name = Lang.Get("tab-charclass"), DataInt = 1 },
            };

            GuiComposer createCharacterComposer;
            Composers["createcharacter"] = createCharacterComposer =
                capi.Gui
                .CreateCompo("createcharacter", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(curTab == 0 ? Lang.Get("Customize Skin") : (curTab == 1 ? Lang.Get("Select character class") : Lang.Get("Select your outfit")), OnTitleBarClose)
                .AddIf(AllowClassSelection)
                    .AddHorizontalTabs(tabs, tabBounds, onTabClicked, CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), "tabs")
                .EndIf()
                .BeginChildElements(bgBounds)
            ;

            var bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
            bh.hideClothing = false;

            if (curTab == 0)
            {
                var skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();

                bh.hideClothing = charNaked;

                var essr = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
                essr.TesselateShape();

                CairoFont smallfont = CairoFont.WhiteSmallText();
                var textExt = smallfont.GetTextExtents(Lang.Get("Show dressed"));
                int colorIconSize = 22;

                ElementBounds leftColBounds = ElementBounds.Fixed(0, ypos, 204, dlgHeight - 59).FixedGrow(2 * pad, 2 * pad);

                insetSlotBounds = ElementBounds.Fixed(0, ypos + 2, 265, leftColBounds.fixedHeight - 2 * pad - 10).FixedRightOf(leftColBounds, 10);
                ElementBounds rightColBounds = ElementBounds.Fixed(0, ypos, 54, dlgHeight - 59).FixedGrow(2 * pad, 2 * pad).FixedRightOf(insetSlotBounds, 10);
                ElementBounds toggleButtonBounds = ElementBounds.Fixed(
                        (int)insetSlotBounds.fixedX + insetSlotBounds.fixedWidth / 2 - textExt.Width / RuntimeEnv.GUIScale / 2 - 12,
                        0,
                        textExt.Width / RuntimeEnv.GUIScale + 1,
                        textExt.Height / RuntimeEnv.GUIScale
                    )
                    .FixedUnder(insetSlotBounds, 4).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(12, 6)
                ;

                ElementBounds bounds=null;
                ElementBounds prevbounds = null;

                double leftX = 0;

                foreach (var skinpart in skinMod.AvailableSkinParts)
                {
                    bounds = ElementBounds.Fixed(leftX, (prevbounds == null || prevbounds.fixedY == 0) ? -10 : prevbounds.fixedY + 8, colorIconSize, colorIconSize);
                    if (!AllowedSkinPartSelection(skinpart.Code)) continue;

                    string code = skinpart.Code;

                    AppliedSkinnablePartVariant appliedVar = skinMod.AppliedSkinParts.FirstOrDefault(sp => sp.PartCode == code);

                    var variants = skinpart.Variants.Where(p => variantCategories.Contains(p.Category) || (AllowKeepCurrent && p.Code == appliedVar.Code)).ToArray();

                    if (skinpart.Type == EnumSkinnableType.Texture && !skinpart.UseDropDown)
                    {
                        var colors = variants.Select(p => p.Color).ToArray();
                        int selectedIndex = 0;

                        createCharacterComposer.AddRichtext(Lang.Get("skinpart-"+code), CairoFont.WhiteSmallText(), bounds = bounds.BelowCopy(0, 10).WithFixedSize(210, 22));
                        createCharacterComposer.AddColorListPicker(colors, (index) => onToggleSkinPart(code, index), bounds = bounds.BelowCopy(0, 0).WithFixedSize(colorIconSize, colorIconSize), 180, "picker-" + code);

                        for (int i = 0; i < variants.Length; i++)
                        {
                            if (variants[i].Code == appliedVar?.Code) selectedIndex = i;
                            var picker = createCharacterComposer.GetColorListPicker("picker-" + code + "-" + i);
                            picker.ShowToolTip = true;
                            picker.TooltipText = Lang.Get("color-" + variants[i].Code);
#if DEBUG
                            if (!Lang.HasTranslation("color-" + variants[i].Code))
                            {
                                System.Diagnostics.Debug.WriteLine("\"" + Lang.Get("color-" + skinpart.Variants[i].Code) + "\": \"" + skinpart.Variants[i].Code + "\",");
                            }
#endif

                        }

                        createCharacterComposer.ColorListPickerSetValue("picker-" + code, selectedIndex);
                    }
                    else
                    {
                        int selectedIndex = Math.Max(0, variants.IndexOf(v => v.Code == appliedVar?.Code));
                        string[] names = variants.Select(v => Lang.Get("skinpart-" + code + "-" + v.Code)).ToArray();
                        string[] values = variants.Select(v => v.Code).ToArray();
#if DEBUG
                        for (int i = 0; i < names.Length; i++)
                        {
                            var v = variants[i];
                            if (!Lang.HasTranslation("skinpart-" + code + "-" + v.Code))
                            {
                                System.Diagnostics.Debug.WriteLine("\"" + names[i] + "\": \"" + v.Code + "\",");
                            }
                        }
#endif

                        createCharacterComposer.AddRichtext(Lang.Get("skinpart-" + code), CairoFont.WhiteSmallText(), bounds = bounds.BelowCopy(0, 10).WithFixedSize(210, 22));

                        string tooltip = Lang.GetIfExists("skinpartdesc-" + code);
                        if (tooltip != null)
                        {
                            createCharacterComposer.AddHoverText(tooltip, CairoFont.WhiteSmallText(), 300, bounds = bounds.FlatCopy());
                        }

                        createCharacterComposer.AddDropDown(values, names, selectedIndex, (variantcode, selected) => onToggleSkinPart(code, variantcode), bounds = bounds.BelowCopy(0, 0).WithFixedSize(200, 25), "dropdown-" + code);
                    }

                    prevbounds = bounds.FlatCopy();

                    if (skinpart.Colbreak)
                    {
                        leftX = insetSlotBounds.fixedX + insetSlotBounds.fixedWidth + 22;
                        prevbounds.fixedY = 0;
                    }
                }

                createCharacterComposer
                    .AddInset(insetSlotBounds, 2)
                    .AddToggleButton(Lang.Get("Show dressed"), smallfont, OnToggleDressOnOff, toggleButtonBounds, "showdressedtoggle")
                    .AddIf(modSys != null)
                        .AddButton(Lang.Get("Randomize"), () => { return OnRandomizeSkin(new Dictionary<string, string>()); }, ElementBounds.Fixed(0, dlgHeight - 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(8, 6), CairoFont.WhiteSmallText(), EnumButtonStyle.Small)
                    .EndIf()
                    .AddIf(modSys != null && capi.Settings.String.Exists("lastSkinSelection"))
                        .AddButton(Lang.Get("Last selection"), () => { return OnRandomizeSkin(modSys.getPreviousSelection()); }, ElementBounds.Fixed(130, dlgHeight - 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(8, 6), CairoFont.WhiteSmallText(), EnumButtonStyle.Small)
                    .EndIf()
                    .AddSmallButton(Lang.Get("Confirm Skin"), OnNext, ElementBounds.Fixed(0, dlgHeight - 25).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6), EnumButtonStyle.Normal)
                ;

                createCharacterComposer.GetToggleButton("showdressedtoggle").SetValue(!charNaked);

                onBeforeCompose?.Invoke(createCharacterComposer);
            }

            if (curTab == 1)
            {
                var essr = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
                essr.TesselateShape();

                ypos -= 10;

                ElementBounds leftColBounds = ElementBounds.Fixed(0, ypos, 0, dlgHeight - 47).FixedGrow(2 * pad, 2 * pad);
                insetSlotBounds = ElementBounds.Fixed(0, ypos + 25, 190, leftColBounds.fixedHeight - 2 * pad + 10).FixedRightOf(leftColBounds, 10);

                ElementBounds rightSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, ypos, 1, rows).FixedGrow(2 * pad, 2 * pad).FixedRightOf(insetSlotBounds, 10);
                ElementBounds prevButtonBounds = ElementBounds.Fixed(0, ypos + 25, 35, slotsize - 4).WithFixedPadding(2).FixedRightOf(insetSlotBounds, 20);
                ElementBounds centerTextBounds = ElementBounds.Fixed(0, ypos + 25, 200, slotsize - 4 - 8).FixedRightOf(prevButtonBounds, 20);

                ElementBounds charclasssInset = centerTextBounds.ForkBoundingParent(4, 4, 4, 4);

                ElementBounds nextButtonBounds = ElementBounds.Fixed(0, ypos + 25, 35, slotsize - 4).WithFixedPadding(2).FixedRightOf(charclasssInset, 20);

                CairoFont font = CairoFont.WhiteMediumText();
                centerTextBounds.fixedY += (centerTextBounds.fixedHeight - font.GetFontExtents().Height / RuntimeEnv.GUIScale) / 2;

                ElementBounds charTextBounds = ElementBounds.Fixed(0, 0, 480, 100).FixedUnder(prevButtonBounds, 20).FixedRightOf(insetSlotBounds, 20);

                createCharacterComposer
                    .AddInset(insetSlotBounds, 2)

                    .AddIconButton("left", (on) => changeClass(-1), prevButtonBounds.FlatCopy())
                    .AddInset(charclasssInset, 2)
                    .AddDynamicText("Commoner", font.Clone().WithOrientation(EnumTextOrientation.Center), centerTextBounds, "className")
                    .AddIconButton("right", (on) => changeClass(1), nextButtonBounds.FlatCopy())

                    .AddRichtext("", CairoFont.WhiteDetailText(), charTextBounds, "characterDesc")
                    .AddSmallButton(Lang.Get("Confirm Class"), OnConfirm, ElementBounds.Fixed(0, dlgHeight - 25).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6), EnumButtonStyle.Normal)
                ;

                changeClass(0);
            }

            var tabElem = createCharacterComposer.GetHorizontalTabs("tabs");
            if (tabElem != null)
            {
                tabElem.unscaledTabSpacing = 20;
                tabElem.unscaledTabPadding = 10;
                tabElem.activeElement = curTab;
            }
            createCharacterComposer.Compose();
        }

        private bool OnRandomizeSkin(Dictionary<string, string> preselection)
        {
            var entity = capi.World.Player.Entity;

            var bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
            bh.doReloadShapeAndSkin = false;

            modSys.randomizeSkin(entity, preselection);
            var skinMod = entity.GetBehavior<EntityBehaviorExtraSkinnable>();

            foreach (var appliedPart in skinMod.AppliedSkinParts)
            {
                string partcode = appliedPart.PartCode;

                var skinPart = skinMod.AvailableSkinParts.FirstOrDefault(part => part.Code == partcode);
                int index = skinPart.Variants.IndexOf(part => part.Code == appliedPart.Code);

                if (skinPart.Type == EnumSkinnableType.Texture && !skinPart.UseDropDown)
                {
                    Composers["createcharacter"].ColorListPickerSetValue("picker-" + partcode, index);
                }
                else
                {
                    Composers["createcharacter"].GetDropDown("dropdown-" + partcode).SetSelectedIndex(index);
                }
            }

            bh.doReloadShapeAndSkin = true;
            reTesselate();

            return true;
        }

        private void OnToggleDressOnOff(bool on)
        {
            charNaked = !on;
            var bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
            bh.hideClothing = charNaked;

            if (modSys != null)
            {
                var charclass = capi.World.Player.Entity.WatchedAttributes.GetString("characterClass", modSys.characterClasses[0].Code);
                modSys.setCharacterClass(capi.World.Player.Entity, charclass);
            }
            reTesselate();
        }

        protected virtual void onToggleSkinPart(string partCode, string variantCode)
        {
            var skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            skinMod.selectSkinPart(partCode, variantCode);
        }

        protected virtual void onToggleSkinPart(string partCode, int index)
        {
            var skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            string variantCode = skinMod.AvailableSkinPartsByCode[partCode].Variants[index].Code;
            skinMod.selectSkinPart(partCode, variantCode);
        }

        protected virtual bool OnNext()
        {
            if (AllowClassSelection)
            {
                curTab = 1;
                ComposeGuis();
            } else
            {
                didSelect = true;
                TryClose();
            }
            return true;
        }

        private void onTabClicked(int tabid)
        {
            curTab = tabid;
            ComposeGuis();
        }

        public override void OnGuiOpened()
        {
            string charclass = capi.World.Player.Entity.WatchedAttributes.GetString("characterClass");
            if (AllowClassSelection)
            {
                if (charclass != null)
                {
                    modSys.setCharacterClass(capi.World.Player.Entity, charclass, true);
                }
                else
                {
                    modSys.setCharacterClass(capi.World.Player.Entity, modSys.characterClasses[0].Code, true);
                }
            }

            ComposeGuis();
            var essr = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
            essr.TesselateShape();

            if (capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Guest || capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Survival)
            {
                if (characterInv != null) characterInv.Open(capi.World.Player);
            }
        }


        public override void OnGuiClosed()
        {
            if (characterInv != null)
            {
                characterInv.Close(capi.World.Player);
                Composers["createcharacter"].GetSlotGrid("leftSlots")?.OnGuiClosed(capi);
                Composers["createcharacter"].GetSlotGrid("rightSlots")?.OnGuiClosed(capi);
            }

            if (modSys != null)
            {
                CharacterClass chclass = modSys.characterClasses[currentClassIndex];
                modSys.ClientSelectionDone(characterInv, chclass.Code, didSelect);
            }

            var bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
            bh.hideClothing = false;
            reTesselate();
        }




        private bool OnConfirm()
        {
            didSelect = true;
            TryClose();
            return true;
        }

        protected virtual void OnTitleBarClose()
        {
            TryClose();
        }
        protected void SendInvPacket(object packet)
        {
            capi.Network.SendPacketClient(packet);
        }







        void changeClass(int dir)
        {
            currentClassIndex = GameMath.Mod(currentClassIndex + dir, modSys.characterClasses.Count);

            CharacterClass chclass = modSys.characterClasses[currentClassIndex];
            Composers["createcharacter"].GetDynamicText("className").SetNewText(Lang.Get("characterclass-" + chclass.Code));

            StringBuilder fulldesc = new StringBuilder();
            StringBuilder attributes = new StringBuilder();

            fulldesc.AppendLine(Lang.Get("characterdesc-" + chclass.Code));
            fulldesc.AppendLine();
            fulldesc.AppendLine(Lang.Get("traits-title"));

            var chartraits = chclass.Traits.Select(code => modSys.TraitsByCode[code]).OrderBy(trait => (int)trait.Type);

            foreach (var trait in chartraits)
            {
                attributes.Clear();
                foreach (var val in trait.Attributes)
                {
                    if (attributes.Length > 0) attributes.Append(", ");
                    attributes.Append(Lang.Get(string.Format(GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", val.Key, val.Value)));
                }

                if (attributes.Length > 0)
                {
                    fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), attributes));
                } else
                {
                    string desc = Lang.GetIfExists("traitdesc-" + trait.Code);
                    if (desc != null)
                    {
                        fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), desc));
                    } else
                    {
                        fulldesc.AppendLine(Lang.Get("trait-" + trait.Code));
                    }


                }
            }

            if (chclass.Traits.Length == 0)
            {
                fulldesc.AppendLine(Lang.Get("No positive or negative traits"));
            }

            Composers["createcharacter"].GetRichtext("characterDesc").SetNewText(fulldesc.ToString(), CairoFont.WhiteDetailText());

            modSys.setCharacterClass(capi.World.Player.Entity, chclass.Code, true);

            reTesselate();
        }

        protected void reTesselate()
        {
            var essr = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
            essr.TesselateShape();
        }

        public void PrepAndOpen()
        {
            TryOpen();
        }

        public override bool CaptureAllInputs()
        {
            return IsOpened();
        }


        public override string ToggleKeyCombinationCode
        {
            get { return null; }
        }



        public override void OnMouseWheel(MouseWheelEventArgs args)
        {
            base.OnMouseWheel(args);

            if (insetSlotBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY) && curTab == 0)
            {
                charZoom = GameMath.Clamp(charZoom + args.deltaPrecise / 5f, 0.5f, 1f);
            }
        }

        public override bool PrefersUngrabbedMouse => true;


        #region Character render
        protected float yaw = -GameMath.PIHALF + 0.3f;
        protected bool rotateCharacter;
        public override void OnMouseDown(MouseEvent args)
        {
            base.OnMouseDown(args);

            rotateCharacter = insetSlotBounds.PointInside(args.X, args.Y);
        }

        public override void OnMouseUp(MouseEvent args)
        {
            base.OnMouseUp(args);

            rotateCharacter = false;
        }

        public override void OnMouseMove(MouseEvent args)
        {
            base.OnMouseMove(args);

            if (rotateCharacter) yaw -= args.DeltaX / 100f;
        }


        Vec4f lighPos = new Vec4f(-1, -1, 0, 0).NormalizeXYZ();
        Matrixf mat = new Matrixf();

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            if (capi.IsGamePaused)
            {
                capi.World.Player.Entity.talkUtil.OnGameTick(deltaTime);
            }

            capi.Render.GlPushMatrix();

            if (focused) { capi.Render.GlTranslate(0, 0, 150); }

            capi.Render.GlRotate(-14, 1, 0, 0);

            mat.Identity();
            mat.RotateXDeg(-14);
            Vec4f lightRot = mat.TransformVector(lighPos);
            double pad = GuiElement.scaled(GuiElementItemSlotGridBase.unscaledSlotPadding);

            capi.Render.CurrentActiveShader.Uniform("lightPosition", new Vec3f(lightRot.X, lightRot.Y, lightRot.Z));
            capi.Render.PushScissor(insetSlotBounds);

            double posX = insetSlotBounds.renderX + pad - GuiElement.scaled(195) * charZoom + GuiElement.scaled(115 * (1 - charZoom));
            double posY = insetSlotBounds.renderY + pad + GuiElement.scaled(10 * (1 - charZoom));
            double posZ = (float)GuiElement.scaled(230);
            float size = (float)GuiElement.scaled(330 * charZoom);

            if (curTab == 1)
            {
                posX = insetSlotBounds.renderX + pad - GuiElement.scaled(110);
                posY = insetSlotBounds.renderY + pad - GuiElement.scaled(15);
                size = (float)GuiElement.scaled(205);
            }

            capi.Render.RenderEntityToGui(deltaTime, capi.World.Player.Entity, posX, posY, posZ, yaw, size, ColorUtil.WhiteArgb);
            capi.Render.PopScissor();
            capi.Render.CurrentActiveShader.Uniform("lightPosition", new Vec3f(1, -1, 0).Normalize());
            capi.Render.GlPopMatrix();
        }
        #endregion


        public override float ZSize
        {
            get { return (float)GuiElement.scaled(280); }
        }
    }
}
