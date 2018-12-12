using System;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class GuiDialogBlockEntityFirepit : GuiDialogBlockEntity
    {
        bool haveCookingContainer;
        string currentOutputText;

        ElementBounds cookingSlotsSlotBounds;

        long lastRedrawMs;

        protected override double FloatyDialogPosition => 0.6;
        protected override double FloatyDialogAlign => 0.8;

        public GuiDialogBlockEntityFirepit(string dialogTitle, InventoryBase Inventory, BlockPos BlockEntityPosition,
                                           SyncedTreeAttribute tree, ICoreClientAPI capi)
            : base(dialogTitle, Inventory, BlockEntityPosition, capi)
        {
            if (IsDuplicate) return;
            tree.OnModified.Add(new TreeModifiedListener() { listener = OnAttributesModified } );
            Attributes = tree;

            SetupDialog();
        }

        private void OnInventorySlotModified(int slotid)
        {
            //Console.WriteLine("slot modified: {0}", slotid);
            SetupDialog();
        }

        void SetupDialog()
        {
            ItemSlot hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (hoveredSlot != null && hoveredSlot.Inventory == Inventory)
            {
                capi.Input.TriggerOnMouseLeaveSlot(hoveredSlot);
            } else
            {
                hoveredSlot = null;
            }

            string newOutputText = Attributes.GetString("outputText", ""); // Inventory.GetOutputText();
            bool newHaveCookingContainer = Attributes.GetInt("haveCookingContainer") > 0; //Inventory.HaveCookingContainer;

            GuiElementDynamicText outputTextElem;

            if (haveCookingContainer == newHaveCookingContainer && SingleComposer != null)
            {
                outputTextElem = SingleComposer.GetDynamicText("outputText");
                outputTextElem.SetNewText(newOutputText, true);
                SingleComposer.GetCustomDraw("symbolDrawer").Redraw();

                haveCookingContainer = newHaveCookingContainer;
                currentOutputText = newOutputText;

                outputTextElem.Bounds.fixedOffsetY = 0;

                if (outputTextElem.QuantityTextLines > 2)
                {
                    outputTextElem.Bounds.fixedOffsetY = -outputTextElem.Font.GetFontExtents().Height;
                }
                outputTextElem.Bounds.CalcWorldBounds();

                //Console.WriteLine("new text is now " + newOutputText + ", lines = " + outputTextElem.QuantityTextLines);

                return;
            }

            //ClearComposers();


            haveCookingContainer = newHaveCookingContainer;
            currentOutputText = newOutputText;

            int qCookingSlots = Attributes.GetInt("quantityCookingSlots");

            ElementBounds stoveBounds = ElementBounds.Fixed(0, 0, 250, 250);

            cookingSlotsSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 20, 30 + 45, 4, qCookingSlots / 4);
            cookingSlotsSlotBounds.fixedHeight += 10;

            double top = cookingSlotsSlotBounds.fixedHeight + cookingSlotsSlotBounds.fixedY;

            ElementBounds inputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 20, top, 1, 1);
            ElementBounds fuelSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 20, 120 + top, 1, 1);
            ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 173, top, 1, 1);

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(stoveBounds);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);


            int openedFirepits = capi.OpenedGuis.OfType<GuiDialogBlockEntityFirepit>().Count();

            if (!capi.Settings.Bool["immersiveMouseMode"])
            {
             //   if (openedFirepits % 3 == 1) dialogBounds.fixedOffsetY -= stoveBounds.fixedHeight + 100;
                //if (openedFirepits % 3 == 2) dialogBounds.fixedOffsetY += stoveBounds.fixedHeight + 100;
            }


            int[] cookingSlotIds = new int[qCookingSlots];
            for (int i = 0; i < qCookingSlots; i++) cookingSlotIds[i] = 3 + i;

            SingleComposer = capi.Gui
                .CreateCompo("blockentitystove"+BlockEntityPosition, dialogBounds)
                .AddDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddDynamicCustomDraw(stoveBounds, OnBgDraw, "symbolDrawer")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, ElementBounds.Fixed(15, 30, 235, 45), "outputText")
                    .AddIf(haveCookingContainer)
                        .AddItemSlotGrid(Inventory, SendInvPacket, 4, cookingSlotIds, cookingSlotsSlotBounds, "ingredientSlots")
                    .EndIf()
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 0 }, fuelSlotBounds, "fuelslot")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, fuelSlotBounds.RightCopy(20, 15), "fueltemp")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 1 }, inputSlotBounds, "oreslot")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, inputSlotBounds.RightCopy(30, 15), "oretemp")

                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 2 }, outputSlotBounds, "outputslot")
                .EndChildElements()
                .Compose();

            lastRedrawMs = capi.ElapsedMilliseconds;

            if (hoveredSlot != null)
            {
                SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));
            }

            outputTextElem = SingleComposer.GetDynamicText("outputText");
            outputTextElem.SetNewText(currentOutputText, true);
            outputTextElem.Bounds.fixedOffsetY = 0;
            
            if (outputTextElem.QuantityTextLines > 2)
            {
                outputTextElem.Bounds.fixedOffsetY = -outputTextElem.Font.GetFontExtents().Height;
            }
            outputTextElem.Bounds.CalcWorldBounds();


        }


        private void OnAttributesModified()
        {
            if (!IsOpened()) return;

            string fuelTemp = Attributes.GetFloat("furnaceTemperature").ToString("#");
            string oreTemp = Attributes.GetFloat("oreTemperature").ToString("#");

            fuelTemp += fuelTemp.Length > 0 ? "°C" : "";
            oreTemp += oreTemp.Length > 0 ? "°C" : "";

            SingleComposer.GetDynamicText("fueltemp").SetNewText(fuelTemp);
            SingleComposer.GetDynamicText("oretemp").SetNewText(oreTemp);

            if (capi.ElapsedMilliseconds - lastRedrawMs > 500)
            {
                if (SingleComposer != null) SingleComposer.GetCustomDraw("symbolDrawer").Redraw();
                lastRedrawMs = capi.ElapsedMilliseconds;
            }
        }



        private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            double top = cookingSlotsSlotBounds.fixedHeight + cookingSlotsSlotBounds.fixedY;

            // 1. Fire
            ctx.Save();
            Matrix m = ctx.Matrix;
            m.Translate(GuiElement.scaled(25), GuiElement.scaled(60 + top));
            m.Scale(GuiElement.scaled(0.25), GuiElement.scaled(0.25));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawFlame(ctx);

            double dy = 210 - 210 * (Attributes.GetFloat("fuelBurnTime", 0) / Attributes.GetFloat("maxFuelBurnTime", 1));
            ctx.Rectangle(0, dy, 200, 210 - dy);
            ctx.Clip();
            LinearGradient gradient = new LinearGradient(0, GuiElement.scaled(250), 0, 0);
            gradient.AddColorStop(0, new Color(1, 1, 0, 1));
            gradient.AddColorStop(1, new Color(1, 0, 0, 1));
            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawFlame(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();


            // 2. Arrow Right
            ctx.Save();
            m = ctx.Matrix;
            m.Translate(GuiElement.scaled(83), GuiElement.scaled(top + 2));
            m.Scale(GuiElement.scaled(0.6), GuiElement.scaled(0.6));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawArrowRight(ctx, 2);

            double dx = Attributes.GetFloat("oreCookingTime") / Attributes.GetFloat("maxOreCookingTime", 1);

            
            ctx.Rectangle(GuiElement.scaled(5), 0, GuiElement.scaled(125 * dx), GuiElement.scaled(100));
            ctx.Clip();
            gradient = new LinearGradient(0, 0, GuiElement.scaled(200), 0);
            gradient.AddColorStop(0, new Color(0, 0.4, 0, 1));
            gradient.AddColorStop(1, new Color(0.2, 0.6, 0.2, 1));
            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawArrowRight(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();
        }



        private void SendInvPacket(object packet)
        {
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, packet);
        }


        private void OnTitleBarClose()
        {
            TryClose();
        }


        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnInventorySlotModified;
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnInventorySlotModified;

            SingleComposer.GetSlotGrid("fuelslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("oreslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("ingredientSlots")?.OnGuiClosed(capi);

            base.OnGuiClosed();
        }
    }
}
