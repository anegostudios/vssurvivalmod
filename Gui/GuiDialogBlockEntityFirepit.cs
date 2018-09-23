using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class GuiDialogBlockEntityFirepit : GuiDialogGeneric
    {
        InventoryBase inventory;
        BlockPos blockEntityPos;

        bool haveCookingContainer;
        string currentOutputText;

        ElementBounds cookingSlotsSlotBounds;

        SyncedTreeAttribute attributes = new SyncedTreeAttribute();

        long lastRedrawMs;
        bool isduplicate = false;

        public override ITreeAttribute Attributes
        {
            get { return attributes; }
        }

        public GuiDialogBlockEntityFirepit(string DialogTitle, InventoryBase inventory, BlockPos blockEntityPos, SyncedTreeAttribute tree, ICoreClientAPI capi) : base(DialogTitle, capi)
        {
            foreach (var val in capi.World.Player.InventoryManager.Inventories)
            {
                if (val.Value == inventory)
                {
                    isduplicate = true;
                    return;
                }
            }

            this.inventory = inventory;
            this.blockEntityPos = blockEntityPos;
            this.attributes = tree;

            attributes.OnModified.Add(new TreeModifiedListener() { listener = OnAttributesModified } );

            

            capi.World.Player.InventoryManager.OpenInventory(inventory);

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
            if (hoveredSlot != null && hoveredSlot.Inventory == inventory)
            {
                capi.Input.TriggerOnMouseLeaveSlot(hoveredSlot);
            } else
            {
                hoveredSlot = null;
            }

            string newOutputText = attributes.GetString("outputText", ""); // inventory.GetOutputText();
            bool newHaveCookingContainer = attributes.GetInt("haveCookingContainer") > 0; //inventory.HaveCookingContainer;

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

            int qCookingSlots = attributes.GetInt("quantityCookingSlots");

            ElementBounds stoveBounds = ElementBounds.Fixed(0, 0, 250, 250);

            cookingSlotsSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 20, 30 + 45, 4, qCookingSlots / 4);
            cookingSlotsSlotBounds.fixedHeight += 10;

            double top = cookingSlotsSlotBounds.fixedHeight + cookingSlotsSlotBounds.fixedY;

            ElementBounds inputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 20, top, 1, 1);
            ElementBounds fuelSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 20, 120 + top, 1, 1);
            ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 173, top, 1, 1);

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(ElementGeometrics.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(stoveBounds);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-ElementGeometrics.DialogToScreenPadding, 0);


            int openedFirepits = 0;
            foreach (var val in capi.OpenedGuis)
            {
                if (val is GuiDialogBlockEntityFirepit)
                {
                    openedFirepits++;
                }
            }

            if (!capi.Settings.Bool["floatyGuis"])
            {
             //   if (openedFirepits % 3 == 1) dialogBounds.fixedOffsetY -= stoveBounds.fixedHeight + 100;
                //if (openedFirepits % 3 == 2) dialogBounds.fixedOffsetY += stoveBounds.fixedHeight + 100;
            }


            int[] cookingSlotIds = new int[qCookingSlots];
            for (int i = 0; i < qCookingSlots; i++) cookingSlotIds[i] = 3 + i;

            SingleComposer = capi.Gui
                .CreateCompo("blockentitystove"+blockEntityPos, dialogBounds, false)
                .AddDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddDynamicCustomDraw(stoveBounds, OnBgDraw, "symbolDrawer")
                    .AddDynamicText(currentOutputText, CairoFont.WhiteDetailText(), EnumTextOrientation.Left, ElementBounds.Fixed(15, 30, 235, 45), 1, "outputText")
                    .AddIf(haveCookingContainer)
                        .AddItemSlotGrid(inventory, SendInvPacket, 4, cookingSlotIds, cookingSlotsSlotBounds, "ingredientSlots")
                    .EndIf()
                    .AddItemSlotGrid(inventory, SendInvPacket, 1, new int[] { 0 }, fuelSlotBounds, "fuelslot")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, fuelSlotBounds.RightCopy(20, 15), 1, "fueltemp")
                    .AddItemSlotGrid(inventory, SendInvPacket, 1, new int[] { 1 }, inputSlotBounds, "oreslot")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, inputSlotBounds.RightCopy(30, 15), 1, "oretemp")

                    .AddItemSlotGrid(inventory, SendInvPacket, 1, new int[] { 2 }, outputSlotBounds, "outputslot")
                .EndChildElements()
                .Compose()
            ;

            lastRedrawMs = capi.ElapsedMilliseconds;

            if (hoveredSlot != null)
            {
                SingleComposer.OnMouseMove(capi, new MouseEvent() { X = capi.Input.MouseX, Y = capi.Input.MouseY });
            }

            outputTextElem = SingleComposer.GetDynamicText("outputText");
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

            string fuelTemp = attributes.GetFloat("furnaceTemperature").ToString("#");
            string oreTemp = attributes.GetFloat("oreTemperature").ToString("#");

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

            double dy = 210 - 210 * (attributes.GetFloat("fuelBurnTime", 0) / attributes.GetFloat("maxFuelBurnTime", 1));
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

            double dx = attributes.GetFloat("oreCookingTime") / attributes.GetFloat("maxOreCookingTime", 1);

            
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
            capi.Network.SendBlockEntityPacket(blockEntityPos.X, blockEntityPos.Y, blockEntityPos.Z, packet);
        }


        private void OnTitleBarClose()
        {
            TryClose();
        }


        public override bool TryOpen()
        {
            if (isduplicate) return false;
            return base.TryOpen();
        }

        public override void OnGuiOpened()
        {
            inventory.Open(capi.World.Player);
            inventory.SlotModified += OnInventorySlotModified;
        }

        public override void OnGuiClosed()
        {
            inventory.SlotModified -= OnInventorySlotModified;
            inventory.Close(capi.World.Player);
            capi.World.Player.InventoryManager.CloseInventory(inventory);

            SingleComposer.GetSlotGrid("fuelslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("oreslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("ingredientSlots")?.OnGuiClosed(capi);

            capi.Network.SendBlockEntityPacket(blockEntityPos.X, blockEntityPos.Y, blockEntityPos.Z, (int)EnumBlockContainerPacketId.CloseInventory);
        }

        public void ReloadValues()
        {
            
        }

        public override void OnFinalizeFrame(float dt)
        {
            base.OnFinalizeFrame(dt);

            if (!IsInRangeOfBlock(blockEntityPos))
            {
                // Because we cant do it in here
                capi.Event.RegisterCallback((deltatime) => TryClose(), 0);
            }
        }


        public override void OnRender2D(float deltaTime)
        {
            if (capi.Settings.Bool["floatyGuis"])
            {

                EntityPlayer entityPlayer = capi.World.Player.Entity;
                Vec3d aboveHeadPos = new Vec3d(blockEntityPos.X + 0.5, blockEntityPos.Y + 1.5, blockEntityPos.Z + 0.5);
                Vec3d pos = MatrixToolsd.Project(aboveHeadPos, capi.Render.PerspectiveProjectionMat, capi.Render.PerspectiveViewMat, capi.Render.FrameWidth, capi.Render.FrameHeight);

                // Z negative seems to indicate that the name tag is behind us \o/
                if (pos.Z < 0)
                {
                    return;
                }

                SingleComposer.Bounds.Alignment = EnumDialogArea.None;
                SingleComposer.Bounds.fixedOffsetX = 0;
                SingleComposer.Bounds.fixedOffsetY = 0;
                SingleComposer.Bounds.absFixedX = pos.X - SingleComposer.Bounds.OuterWidth / 2;
                SingleComposer.Bounds.absFixedY = capi.Render.FrameHeight - pos.Y;
                SingleComposer.Bounds.absMarginX = 0;
                SingleComposer.Bounds.absMarginY = 0;
            }

            base.OnRender2D(deltaTime);
        }


        public override bool DisableWorldInteract()
        {
            return false;
        }
    }
}
