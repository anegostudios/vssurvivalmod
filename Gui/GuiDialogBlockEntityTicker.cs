using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class EditTickerPacket
    {
        [ProtoMember(1)]
        public string Interval;
        [ProtoMember(2)]
        public bool Active;
    }

    public class GuiDialogBlockEntityTicker : GuiDialogBlockEntity
    {
        public GuiDialogBlockEntityTicker(BlockPos pos, int tickIntervalMs, bool active, ICoreClientAPI capi) : base("Command block", pos, capi)
        {
            double pad = GuiElementItemSlotGrid.unscaledSlotPadding;

            int spacing = 5;

            double size = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGrid.unscaledSlotPadding;

            double innerWidth = 400;

            // 1.2. Name and Hotkey
            double halfWidth = innerWidth / 2 - 5;
            
            // 1.3. Commands text
            ElementBounds commmandsBounds = ElementBounds.Fixed(0, 34, innerWidth, 30);
            ElementBounds autoNumberBounds = ElementBounds.Fixed(110, 26, 80, 30);

            // 1.4. Clear Macro button
            ElementBounds cancelBounds = ElementBounds.FixedSize(0, 0).FixedUnder(commmandsBounds, 20 + 2 * spacing).WithFixedPadding(10, 2);
            ElementBounds saveBounds = ElementBounds.FixedSize(0, 0).FixedUnder(commmandsBounds, 20 + 2 * spacing).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            if (SingleComposer != null) SingleComposer.Dispose();
            SingleComposer =
                capi.Gui
                .CreateCompo("commandeditordialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Ticker Block", OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddStaticText("Timer (ms)", CairoFont.WhiteSmallText(), commmandsBounds)
                    .AddNumberInput(autoNumberBounds, null, CairoFont.WhiteDetailText(), "automs")
                    .AddSwitch(null, autoNumberBounds.RightCopy(90, 3).WithFixedPadding(0, 0), "onSwitch", 25, 3)
                    .AddStaticText("Active", CairoFont.WhiteSmallText(), autoNumberBounds.RightCopy(120, 6))

                    .AddSmallButton("Cancel", OnCancel, cancelBounds)
                    .AddSmallButton("Save", OnSave, saveBounds)
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetNumberInput("automs").SetValue(tickIntervalMs);
            SingleComposer.GetSwitch("onSwitch").On = active;
            SingleComposer.UnfocusOwnElements();
        }

        private bool OnCancel()
        {
            TryClose();
            return true;
        }

        private bool OnSave()
        {
            var packet = new EditTickerPacket() { 
                Interval = "" + SingleComposer.GetNumberInput("automs").GetValue(),
                Active = SingleComposer.GetSwitch("onSwitch").On
            };

            capi.Network.SendBlockEntityPacket(BlockEntityPosition, 12, SerializerUtil.Serialize(packet));
            TryClose();
            return true;
        }


        private void OnTitleBarClose()
        {
            TryClose();
        }

        
        public override string ToggleKeyCombinationCode
        {
            get { return null; }
        }

    }
}
