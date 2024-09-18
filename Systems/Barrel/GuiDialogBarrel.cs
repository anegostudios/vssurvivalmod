using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class GuiDialogBarrel : GuiDialogBlockEntity
    {
        EnumPosFlag screenPos;
        ElementBounds inputSlotBounds;

        protected override double FloatyDialogPosition => 0.6;
        protected override double FloatyDialogAlign => 0.8;

        public override double DrawOrder => 0.2;


        public GuiDialogBarrel(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (IsDuplicate) return;
            
        }

        void SetupDialog()
        {
            ElementBounds barrelBoundsLeft = ElementBounds.Fixed(0, 30, 150, 200);
            ElementBounds barrelBoundsRight = ElementBounds.Fixed(170, 30, 150, 200);

            inputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30, 1, 1);
            inputSlotBounds.fixedHeight += 10;

            double top = inputSlotBounds.fixedHeight + inputSlotBounds.fixedY;


            ElementBounds fullnessMeterBounds = ElementBounds.Fixed(100, 30, 40, 200);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(barrelBoundsLeft, barrelBoundsRight);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithFixedAlignmentOffset(IsRight(screenPos) ? -GuiStyle.DialogToScreenPadding : GuiStyle.DialogToScreenPadding, 0)
                .WithAlignment(IsRight(screenPos) ? EnumDialogArea.RightMiddle : EnumDialogArea.LeftMiddle)
            ;


            SingleComposer = capi.Gui
                .CreateCompo("blockentitybarrel" + BlockEntityPosition, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 0 }, inputSlotBounds, "inputSlot")
                    .AddSmallButton(Lang.Get("barrel-seal"), onSealClick, ElementBounds.Fixed(0, 100, 80, 25), EnumButtonStyle.Normal)

                    .AddInset(fullnessMeterBounds.ForkBoundingParent(2,2,2,2), 2)
                    .AddDynamicCustomDraw(fullnessMeterBounds, fullnessMeterDraw, "liquidBar")

                    .AddDynamicText(getContentsText(), CairoFont.WhiteDetailText(), barrelBoundsRight, "contentText")

                .EndChildElements()
            .Compose();
        }


        string getContentsText()
        {
            string contents = Lang.Get("Contents:");

            if (Inventory[0].Empty && Inventory[1].Empty) contents +="\n" + Lang.Get("nobarrelcontents");
            else
            {
                if (!Inventory[1].Empty)
                {
                    ItemStack stack = Inventory[1].Itemstack;
                    WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(stack);

                    if (props != null)
                    {
                        string incontainername = Lang.Get(stack.Collectible.Code.Domain + ":incontainer-" + stack.Class.ToString().ToLowerInvariant() + "-" + stack.Collectible.Code.Path);
                        contents += "\n" + Lang.Get(props.MaxStackSize > 0 ? "barrelcontents-items" : "barrelcontents-liquid", (float)stack.StackSize / props.ItemsPerLitre, incontainername);
                    }
                    else
                    {
                        contents += "\n" + Lang.Get("barrelcontents-items", stack.StackSize, stack.GetName());
                    }
                }

                if (!Inventory[0].Empty)
                {
                    ItemStack stack = Inventory[0].Itemstack;
                    contents += "\n" + Lang.Get("barrelcontents-items", stack.StackSize, stack.GetName());
                }

                BlockEntityBarrel bebarrel = capi.World.BlockAccessor.GetBlockEntity(BlockEntityPosition) as BlockEntityBarrel;
                if (bebarrel.CurrentRecipe != null)
                {
                    ItemStack outStack = bebarrel.CurrentRecipe.Output.ResolvedItemstack;
                    WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(outStack);

                    string timeText = bebarrel.CurrentRecipe.SealHours > 24 ? Lang.Get("{0} days", Math.Round(bebarrel.CurrentRecipe.SealHours / capi.World.Calendar.HoursPerDay, 1)) : Lang.Get("{0} hours", bebarrel.CurrentRecipe.SealHours);

                    if (props != null)
                    {
                        string incontainername = Lang.Get(outStack.Collectible.Code.Domain + ":incontainer-" + outStack.Class.ToString().ToLowerInvariant() + "-" + outStack.Collectible.Code.Path);
                        float litres = (float)bebarrel.CurrentOutSize / props.ItemsPerLitre;
                        contents += "\n\n" + Lang.Get("Will turn into {0} litres of {1} after {2} of sealing.", litres, incontainername, timeText);
                    }
                    else
                    {
                        contents += "\n\n" + Lang.Get("Will turn into {0}x {1} after {2} of sealing.", bebarrel.CurrentOutSize, outStack.GetName(), timeText);
                    }

                    
                }
            }

            return contents;
        }

        public void UpdateContents()
        {
            SingleComposer.GetCustomDraw("liquidBar").Redraw();
            SingleComposer.GetDynamicText("contentText").SetNewText(getContentsText());
        }

        private void fullnessMeterDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            ItemSlot liquidSlot = Inventory[1];
            if (liquidSlot.Empty) return;

            BlockEntityBarrel bebarrel = capi.World.BlockAccessor.GetBlockEntity(BlockEntityPosition) as BlockEntityBarrel;
            float itemsPerLitre = 1f;
            int capacity = bebarrel.CapacityLitres;

            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(liquidSlot.Itemstack);
            if (props != null)
            {
                itemsPerLitre = props.ItemsPerLitre;
                capacity = Math.Max(capacity, props.MaxStackSize);
            }

            float fullnessRelative = liquidSlot.StackSize / itemsPerLitre / capacity;

            double offY = (1 - fullnessRelative) * currentBounds.InnerHeight;

            ctx.Rectangle(0, offY, currentBounds.InnerWidth, currentBounds.InnerHeight - offY);

            CompositeTexture tex = props?.Texture ?? liquidSlot.Itemstack.Collectible.Attributes?["inContainerTexture"].AsObject<CompositeTexture>(null, liquidSlot.Itemstack.Collectible.Code.Domain);
            if (tex != null)
            {
                ctx.Save();
                Matrix m = ctx.Matrix;
                m.Scale(GuiElement.scaled(3), GuiElement.scaled(3));
                ctx.Matrix = m;

                AssetLocation loc = tex.Base.Clone().WithPathAppendixOnce(".png");
                GuiElement.fillWithPattern(capi, ctx, loc, true, false, tex.Alpha);

                ctx.Restore();
            }
        }



        private bool onSealClick()
        {
            BlockEntityBarrel bebarrel = capi.World.BlockAccessor.GetBlockEntity(BlockEntityPosition) as BlockEntityBarrel;
            if (bebarrel == null || bebarrel.Sealed) return true;

            if (!bebarrel.CanSeal) return true;

            bebarrel.SealBarrel();

            capi.Network.SendBlockEntityPacket(BlockEntityPosition, 1337);
            capi.World.PlaySoundAt(new AssetLocation("sounds/player/seal"), BlockEntityPosition, 0.4, null);

            TryClose();

            return true;
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

            screenPos = GetFreePos("smallblockgui");
            OccupyPos("smallblockgui", screenPos);
            SetupDialog();
        }

        public override void OnGuiClosed()
        {
            SingleComposer.GetSlotGrid("inputSlot").OnGuiClosed(capi);

            base.OnGuiClosed();

            FreePos("smallblockgui", screenPos);
        }



    }
}
