using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Concept:
    /// 10 slots vertically stacked
    /// on the right side 2 text inputs: Min chance / max chance in %
    /// right top: 2 extra text inputs: "min/max no loot chance"
    /// </summary>
    public class GuiDialogItemStackRandomizer : GuiDialogGeneric
    {
        bool save = false;

        public GuiDialogItemStackRandomizer(float totalChance, ICoreClientAPI capi) : base("Item Stack Randomizer", capi)
        {
            ElementBounds chanceInputBounds = ElementBounds.Fixed(0, 70, 60, 30);

            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;


            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);


            SingleComposer = capi.Gui
                .CreateCompo("itemstackrandomizer", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar("Item Stack Randomizer", OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddStaticText("Chance for any loot to appear:", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, 30, 250, 30))
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.FlatCopy(), null, CairoFont.WhiteDetailText(), "chance")
                    .AddButton("Close", OnCloseClicked, leftButton.FixedUnder(chanceInputBounds, 25))
                    .AddButton("Save", OnSaveClicked, rightButton.FixedUnder(chanceInputBounds, 25))


                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetNumberInput("chance").SetValue("" + (totalChance*100));
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
        

        private void OnTitleBarClose()
        {
            TryClose();
        }

        public void ReloadValues()
        {

        }
        
        public override string ToggleKeyCombinationCode
        {
            get { return null; }
        }

        public override ITreeAttribute Attributes
        {
            get
            {
                GuiElementNumberInput inp = SingleComposer.GetNumberInput("chance");

                TreeAttribute tree = new TreeAttribute();
                tree.SetInt("save", save ? 1 : 0);
                tree.SetFloat("totalChance", (float)inp.GetValue()/100);
                return tree;
            }
        }
    }
}
