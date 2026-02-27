using System;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.ServerMods;

public class GuiDialogTiledDungeon : GuiDialogGeneric
{
    private bool save;

    public GuiDialogTiledDungeon(string dialogTitle, string name, string target, ICoreClientAPI capi) : base(dialogTitle, capi)
    {
        var pad = GuiElementItemSlotGrid.unscaledSlotPadding;

        var slotBounds = ElementBounds.Fixed(EnumDialogArea.None, 0, 30, 350, 30);

        var leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);
        var rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);

        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        var dialogBounds = ElementStdBounds
            .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);


        var textInput = CairoFont.TextInput();
        var text = CairoFont.WhiteSmallishText();
        SingleComposer = capi.Gui
            .CreateCompo("tiledungeon", dialogBounds)
            .AddShadedDialogBG(bgBounds, true)
            .AddDialogTitleBar(dialogTitle, OnTitleBarClose)
            .BeginChildElements(bgBounds)

            .AddStaticText("Name", text, slotBounds = slotBounds.FlatCopy().WithFixedWidth(100))
            .AddTextInput(slotBounds = slotBounds.RightCopy(5, -3).WithFixedWidth(350), null, textInput, "name")

            .AddStaticText("Target", text, slotBounds = slotBounds.BelowCopy(0, 15).WithFixedX(0).WithFixedWidth(100))
            .AddTextInput(slotBounds = slotBounds.RightCopy(5, -3).WithFixedWidth(350), null, textInput, "target")

            .AddStaticText("Empty target means it only accepts connections but does not actively connect to anything. The opposite happens when name field is empty.", CairoFont.WhiteSmallText(), slotBounds = slotBounds.BelowCopy(0, 20).WithFixedX(0).WithFixedWidth(500))

            .AddButton("Save", OnSaveClicked, rightButton.FixedUnder(slotBounds, 40))
            .AddButton("Close", OnCloseClicked, leftButton.FixedUnder(slotBounds, 40))
            .EndChildElements()
            .Compose()
        ;

        var inp = SingleComposer.GetTextInput("target");
        inp.Enabled = target != null;
        inp.SetValue(target);

        inp = SingleComposer.GetTextInput("name");
        inp.Enabled = name != null;
        inp.SetValue(name);
    }


    public override ITreeAttribute Attributes
    {
        get
        {
            TreeAttribute tree = new TreeAttribute();
            tree.SetInt("save", save ? 1 : 0);
            tree.SetString("target", SingleComposer.GetTextInput("target").GetText());
            tree.SetString("name", SingleComposer.GetTextInput("name").GetText());
            return tree;
        }
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
