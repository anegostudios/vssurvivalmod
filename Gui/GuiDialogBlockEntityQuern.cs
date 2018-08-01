using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class GuiDialogBlockEntityQuern : GuiDialogGeneric
    {
        InventoryBase inventory;
        BlockPos blockEntityPos;


        SyncedTreeAttribute attributes = new SyncedTreeAttribute();

        long lastRedrawMs;
        bool isduplicate = false;

        public override ITreeAttribute Attributes
        {
            get { return attributes; }
        }

        public GuiDialogBlockEntityQuern(string DialogTitle, InventoryBase inventory, BlockPos blockEntityPos, SyncedTreeAttribute tree, ICoreClientAPI capi) : base(DialogTitle, capi)
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

            attributes.OnModified.Add(new TreeModifiedListener() { listener = OnAttributesModified });

            inventory.SlotModified += OnInventorySlotModified;

            capi.World.Player.InventoryManager.OpenInventory(inventory);

            SetupDialog();
        }

        private void OnInventorySlotModified(int slotid)
        {
            SetupDialog();
        }

        void SetupDialog()
        {
            ItemSlot hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (hoveredSlot != null && hoveredSlot.Inventory == inventory)
            {
                capi.Input.TriggerOnMouseLeaveSlot(hoveredSlot);
            }
            else
            {
                hoveredSlot = null;
            }

            ElementBounds quernBounds = ElementBounds.Fixed(0, 0, 250, 90);
            
            ElementBounds inputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 20, 30, 1, 1);
            ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 173, 30, 1, 1);

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(ElementGeometrics.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(quernBounds);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-ElementGeometrics.DialogToScreenPadding, 0);


            SingleComposer = capi.Gui
                .CreateCompo("blockentitymillstone", dialogBounds, false)
                .AddDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddDynamicCustomDraw(quernBounds, OnBgDraw, "symbolDrawer")
                    .AddItemSlotGrid(inventory, SendInvPacket, 1, new int[] { 0 }, inputSlotBounds, "inputSlot")
                    .AddItemSlotGrid(inventory, SendInvPacket, 1, new int[] { 1 }, outputSlotBounds, "outputslot")
                .EndChildElements()
                .Compose()
            ;

            lastRedrawMs = capi.ElapsedMilliseconds;

            if (hoveredSlot != null)
            {
                SingleComposer.OnMouseMove(capi, new MouseEvent() { X = capi.Input.MouseX, Y = capi.Input.MouseY });
            }
        }


        private void OnAttributesModified()
        {
            if (!IsOpened()) return;

            if (capi.ElapsedMilliseconds - lastRedrawMs > 500)
            {
                if (SingleComposer != null) SingleComposer.GetCustomDraw("symbolDrawer").Redraw();
                lastRedrawMs = capi.ElapsedMilliseconds;
            }
        }



        private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            double top = 30;

            // Arrow Right
            ctx.Save();
            Matrix m = ctx.Matrix;
            m.Translate(GuiElement.scaled(83), GuiElement.scaled(top + 2));
            m.Scale(GuiElement.scaled(0.6), GuiElement.scaled(0.6));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawArrowRight(ctx, 2);

            double dx = attributes.GetFloat("inputGrindTime") / attributes.GetFloat("maxGrindTime", 1);


            ctx.Rectangle(GuiElement.scaled(5), 0, GuiElement.scaled(125 * dx), GuiElement.scaled(100));
            ctx.Clip();
            LinearGradient gradient = new LinearGradient(0, 0, GuiElement.scaled(200), 0);
            gradient.AddColorStop(0, new Color(0, 0.4, 0, 1));
            gradient.AddColorStop(1, new Color(0.2, 0.6, 0.2, 1));
            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawArrowRight(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();
        }




        private void SendInvPacket(object p)
        {
            capi.Network.SendBlockEntityPacket(blockEntityPos.X, blockEntityPos.Y, blockEntityPos.Z, p);
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
        }

        public override void OnGuiClosed()
        {
            inventory.Close(capi.World.Player);
            capi.World.Player.InventoryManager.CloseInventory(inventory);

            SingleComposer.GetSlotGrid("inputSlot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputslot").OnGuiClosed(capi);

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
                PositionDialogAbove(new Vec3d(blockEntityPos.X + 0.5, blockEntityPos.Y + 1.5, blockEntityPos.Z + 0.5));
            }

            base.OnRender2D(deltaTime);
        }


        public override bool DisableWorldInteract()
        {
            return false;
        }
    }
}
