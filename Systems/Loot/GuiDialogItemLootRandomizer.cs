using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class GuiDialogItemLootRandomizer : GuiDialogGeneric
    {
        InventoryBase inv;
        bool save = false;

        public GuiDialogItemLootRandomizer(InventoryBase inv, float[] chances, ICoreClientAPI capi, string title = "Item Loot Randomizer") : base(title, capi)
        {
            this.inv = inv;
            createDialog(chances, title);
        }

        public GuiDialogItemLootRandomizer(ItemStack[] stacks, float[] chances, ICoreClientAPI capi, string title = "Item Loot Randomizer") : base(title, capi)
        {
            inv = new InventoryGeneric(10, "lootrandomizer-1", capi, null);
            for (int i = 0; i < 10; i++) inv[i].Itemstack = stacks[i];

            createDialog(chances, title);
        }

        private void createDialog(float[] chances, string title)
        {
            double pad = GuiElementItemSlotGrid.unscaledSlotPadding;

            ElementBounds slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, pad, 45 + pad, 10, 1).FixedGrow(2 * pad, 2 * pad);

            ElementBounds chanceInputBounds = ElementBounds.Fixed(3, 0, 48, 30).FixedUnder(slotBounds, -4);

            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);
            ElementBounds chanceTextBounds = ElementBounds.Fixed(EnumDialogArea.CenterFixed, 0, 0, 150, 30).WithFixedPadding(10, 1);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;



            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);

            float totalChance = chances.Sum();
            string text = "Total chance: " + (int)totalChance + "%";

            SingleComposer = capi.Gui
                .CreateCompo("itemlootrandomizer", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(title, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddItemSlotGrid(inv, SendInvPacket, 10, slotBounds, "slots")
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.FlatCopy(), (t) => OnTextChanced(0), CairoFont.WhiteDetailText(), "chance1")
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.RightCopy(3), (t) => OnTextChanced(1), CairoFont.WhiteDetailText(), "chance2")
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.RightCopy(3), (t) => OnTextChanced(2), CairoFont.WhiteDetailText(), "chance3")
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.RightCopy(3), (t) => OnTextChanced(3), CairoFont.WhiteDetailText(), "chance4")
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.RightCopy(3), (t) => OnTextChanced(4), CairoFont.WhiteDetailText(), "chance5")
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.RightCopy(3), (t) => OnTextChanced(5), CairoFont.WhiteDetailText(), "chance6")
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.RightCopy(3), (t) => OnTextChanced(6), CairoFont.WhiteDetailText(), "chance7")
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.RightCopy(3), (t) => OnTextChanced(7), CairoFont.WhiteDetailText(), "chance8")
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.RightCopy(3), (t) => OnTextChanced(8), CairoFont.WhiteDetailText(), "chance9")
                    .AddNumberInput(chanceInputBounds = chanceInputBounds.RightCopy(3), (t) => OnTextChanced(9), CairoFont.WhiteDetailText(), "chance10")

                    .AddButton("Close", OnCloseClicked, leftButton.FixedUnder(chanceInputBounds, 25))
                    .AddDynamicText(text, CairoFont.WhiteDetailText(), chanceTextBounds.FixedUnder(chanceInputBounds, 25), "totalchance")
                    .AddButton("Save", OnSaveClicked, rightButton.FixedUnder(chanceInputBounds, 25))


                .EndChildElements()
                .Compose()
            ;

            for (int i = 0; i < 10; i++)
            {
                GuiElementNumberInput inp = SingleComposer.GetNumberInput("chance" + (i + 1));
                inp.SetValue("" + chances[i]);
            }

            SingleComposer.GetSlotGrid("slots").CanClickSlot = OnCanClickSlot;
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

        bool updating = false;

        private void OnTextChanced(int index)
        {
            if (updating) return;

            UpdateRatios(index);
        }


        public void UpdateRatios(int forceUnchanged = -1)
        {
            updating = true;

            int quantityFilledSlots = 0;
            float totalChance = 0;

            for (int i = 0; i < 10; i++)
            {
                ItemSlot slot = inv[i];

                quantityFilledSlots += (slot.Itemstack != null) ? 1 : 0;

                GuiElementNumberInput inp = SingleComposer.GetNumberInput("chance" + (i + 1));
                totalChance += inp.GetValue();
            }

            float scaleValue = 100f / totalChance;

            int totalNew = 0;

            for (int i = 0; i < 10; i++)
            {
                GuiElementNumberInput inp = SingleComposer.GetNumberInput("chance" + (i + 1));
                ItemSlot slot = inv[i];
                if (slot.Itemstack == null)
                {
                    inp.SetValue("");
                    continue;
                }

                int newVal = (int)(inp.GetValue() * scaleValue);

                if (inp.GetText().Length != 0)
                {
                    if ((i != forceUnchanged || (int)(inp.GetValue()) > 100) && totalChance > 100)
                    {
                        inp.SetValue("" + newVal);
                        totalNew += newVal;
                    } else
                    {
                        totalNew += (int)inp.GetValue();
                    }
                    
                }
            }

            updating = false;

            SingleComposer.GetDynamicText("totalchance").SetNewText("Total chance: " + (int)totalNew + "%");
        }


        private bool OnCanClickSlot(int slotID)
        {
            ItemStack mousestack = capi.World.Player.InventoryManager.MouseItemSlot.Itemstack;

            if (mousestack == null)
            {
                inv[slotID].Itemstack = null;
            } else
            {
                inv[slotID].Itemstack = mousestack.Clone();
                
            }

            inv[slotID].MarkDirty();
            UpdateRatios();

            return false;
        }



        private void SendInvPacket(object t1)
        {
            UpdateRatios();
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }


        public override string ToggleKeyCombinationCode
        {
            get { return null; }
        }

        public override ITreeAttribute Attributes
        {
            get
            {
                TreeAttribute tree = new TreeAttribute();
                tree.SetInt("save", save ? 1 : 0);

                int num = 0;
                for (int i = 0; i < 10; i++)
                {
                    ItemStack stack = inv[i].Itemstack;
                    if (stack == null) continue;

                    GuiElementNumberInput inp = SingleComposer.GetNumberInput("chance" + (i + 1));
                    TreeAttribute subtree = new TreeAttribute();
                    subtree.SetItemstack("stack", stack.Clone());
                    subtree.SetFloat("chance", inp.GetValue());

                    tree["stack" + (num++)] = subtree;
                }

                return tree;
            }
        }
    }
}
