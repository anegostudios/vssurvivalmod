using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class GuiDialogBlockEntityRecipeSelector : GuiDialogGeneric
    {
        BlockPos blockEntityPos;

        int prevSlotOver = -1;
        Dictionary<int, SkillItem> skillItems;
        bool didSelect = false;
        API.Common.Action<int> onSelectedRecipe;
        API.Common.Action onCancelSelect;

        

        public GuiDialogBlockEntityRecipeSelector(string DialogTitle, ItemStack[] recipeOutputs, API.Common.Action<int> onSelectedRecipe, API.Common.Action onCancelSelect, BlockPos blockEntityPos, ICoreClientAPI capi) : base(DialogTitle, capi)
        {
            this.blockEntityPos = blockEntityPos;
            this.onSelectedRecipe = onSelectedRecipe;
            this.onCancelSelect = onCancelSelect;

            skillItems = new Dictionary<int, SkillItem>();

            double size = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGrid.unscaledSlotPadding;

            for (int i = 0; i < recipeOutputs.Length; i++)
            {
                ItemStack stack = recipeOutputs[i];
                ItemSlot dummySlot = new DummySlot(stack);

                string key = GetCraftDescKey(stack);
                string desc = Lang.GetMatching(key);
                if (desc == key) desc = "";

                skillItems.Add(i, new SkillItem()
                {
                    Code = stack.Collectible.Code.Clone(),
                    Name = stack.GetName(),
                    Description = desc,
                    RenderHandler = (AssetLocation code, float dt, double posX, double posY) => {
                        // No idea why the weird offset and size multiplier
                        double scsize = GuiElement.scaled(size - 5);

                        capi.Render.RenderItemstackToGui(dummySlot, posX + scsize/2, posY + scsize / 2, 100, (float)GuiElement.scaled(GuiElementPassiveItemSlot.unscaledItemSize), ColorUtil.WhiteArgb);
                    }
                });
            }


            
            SetupDialog();
        }


        public string GetCraftDescKey(ItemStack stack)
        {
            string name = "";
            string type = stack.Class == EnumItemClass.Block ? "block" : "item";

            name = stack.Collectible.Code?.Domain + AssetLocation.LocationSeparator + type + "craftdesc-" + stack.Collectible.Code?.Path;

            return name;
        }

        void SetupDialog()
        {
            int cnt = Math.Max(1, skillItems.Count);

            int cols = Math.Min(cnt, 7);

            int rows = (int)Math.Ceiling(cnt / (float)cols);
            
            double size = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGrid.unscaledSlotPadding;
            double innerWidth = Math.Max(300, cols * size);
            ElementBounds skillGridBounds = ElementBounds.Fixed(0, 30, innerWidth, rows * size);

            ElementBounds textBounds = ElementBounds.Fixed(0, rows*size + 50, innerWidth, 33);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;


            SingleComposer =
                capi.Gui
                .CreateCompo("toolmodeselect", ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("Select Recipe"), OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddSkillItemGrid(skillItems, cols, rows, OnSlotClick, skillGridBounds, "skillitemgrid")
                    .AddDynamicText("", CairoFont.WhiteSmallishText(), EnumTextOrientation.Left, textBounds, "name")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, textBounds.BelowCopy(0,10,0,0), "desc")
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetSkillItemGrid("skillitemgrid").OnSlotOver = OnSlotOver;
        }

        private void OnSlotOver(int num)
        {
            if (num >= skillItems.Count) return;

            if (num != prevSlotOver)
            {
                prevSlotOver = num;
                SingleComposer.GetDynamicText("name").SetNewText(skillItems[num].Name);
                SingleComposer.GetDynamicText("desc").SetNewText(skillItems[num].Description);
            }
        }

        private void OnSlotClick(int num)
        {
            onSelectedRecipe(num);

            didSelect = true;

            TryClose();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();

            if (!didSelect)
            {
                onCancelSelect();
            }
        }

        public override bool TryClose()
        {
            ///capi.Logger.Notification("Call to GuiDialogBlockEntityRecipeSelector.TryClose");
            return base.TryClose();
        }

        public override bool TryOpen()
        {
            //capi.Logger.Notification("Call to GuiDialogBlockEntityRecipeSelector.TryOpen");
            return base.TryOpen();
        }


        private void SendInvPacket(object packet)
        {
            
        }
        

        private void OnTitleBarClose()
        {
            TryClose();
        }


        // Begin floaty dialog code, activated by Immersive Mouse Mode.
        // Makes the tool mode selection dialog float above its block.
        // See GuiDialogBlockEntity for documentation.

        private readonly double floatyDialogPosition = 0.5;
        private readonly double floatyDialogAlign = 0.75;

        public override bool PrefersUngrabbedMouse => false;

        public override void OnRenderGUI(float deltaTime)
        {
            if (capi.Settings.Bool["immersiveMouseMode"])
            {
                Vec3d aboveHeadPos = new Vec3d(blockEntityPos.X + 0.5, blockEntityPos.Y + floatyDialogPosition, blockEntityPos.Z + 0.5);
                Vec3d pos = MatrixToolsd.Project(aboveHeadPos, capi.Render.PerspectiveProjectionMat, capi.Render.PerspectiveViewMat, capi.Render.FrameWidth, capi.Render.FrameHeight);
                if (pos.Z < 0) return;

                SingleComposer.Bounds.Alignment = EnumDialogArea.None;
                SingleComposer.Bounds.fixedOffsetX = 0;
                SingleComposer.Bounds.fixedOffsetY = 0;
                SingleComposer.Bounds.absFixedX = pos.X - SingleComposer.Bounds.OuterWidth / 2;
                SingleComposer.Bounds.absFixedY = capi.Render.FrameHeight - pos.Y - SingleComposer.Bounds.OuterHeight * floatyDialogAlign;
                SingleComposer.Bounds.absMarginX = 0;
                SingleComposer.Bounds.absMarginY = 0;
            }

            base.OnRenderGUI(deltaTime);
        }
    }
}
