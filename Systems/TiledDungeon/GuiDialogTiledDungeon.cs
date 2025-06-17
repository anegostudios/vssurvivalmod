using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.ServerMods;

public class GuiDialogTiledDungeon : GuiDialogGeneric
{
    private bool save;

    public GuiDialogTiledDungeon(string dialogTitle, string constraint, ICoreClientAPI capi) : base(dialogTitle, capi)
    {
        var pad = GuiElementItemSlotGrid.unscaledSlotPadding;

        var slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, pad, 45 + pad, 10, 1).FixedGrow(2 * pad, 2 * pad);

        var chanceInputBounds = ElementBounds.Fixed(3, 0, 48, 30).FixedUnder(slotBounds, -4);

        var leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);
        var rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);

        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;


        var dialogBounds = ElementStdBounds
            .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);


        SingleComposer = capi.Gui
                .CreateCompo("tiledungeon", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(dialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                .AddTextInput(slotBounds, OnTextChanged, CairoFont.TextInput(), "constraints")
                .AddButton("Close", OnCloseClicked, leftButton.FixedUnder(chanceInputBounds, 25))
                .AddButton("Save", OnSaveClicked, rightButton.FixedUnder(chanceInputBounds, 25))
                .EndChildElements()
                .Compose()
            ;

        var inp = SingleComposer.GetTextInput("constraints");
        inp.SetValue(constraint);
    }

    public override ITreeAttribute Attributes
    {
        get
        {
            TreeAttribute tree = new TreeAttribute();
            tree.SetInt("save", save ? 1 : 0);
            var inp = SingleComposer.GetTextInput("constraints");
            tree.SetString("constraints", inp.GetText());

            return tree;
        }
    }

    private void OnTextChanged(string obj)
    {
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }

    private bool OnSaveClicked()
    {
        save = true;
        TryClose();
        return true;
    }

    private bool OnCloseClicked()
    {
        TryClose();
        return true;
    }
}
