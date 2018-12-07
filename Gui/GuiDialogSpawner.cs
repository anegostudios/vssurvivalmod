using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    // We need a spawner block that spawns exactly 1 trader withing a configurable radius on solid ground
    // If the trader is killed, any subsequent spawning trader should forever hate that player 
    // => Allow the entity to have access to the block they spawned from, so they can modify it during runtime!
    public class GuiDialogSpawner : GuiDialogGeneric
    {
        public BESpawnerData spawnerData = new BESpawnerData();
        BlockPos blockEntityPos;
        bool updating;

        public GuiDialogSpawner(BlockPos blockEntityPos, ICoreClientAPI capi) : base("Spawner config", capi)
        {
            this.blockEntityPos = blockEntityPos;
        }


        public override void OnGuiOpened()
        {
            Compose();
        }

        private void Compose()
        {
            ClearComposers();

            ElementBounds creatureTextBounds = ElementBounds.Fixed(0, 30, 300, 25);

            ElementBounds dropDownBounds = ElementBounds.Fixed(0, 0, 300, 28).FixedUnder(creatureTextBounds, 0);


            ElementBounds areaTextBounds = ElementBounds.Fixed(0, 30, 300, 25).FixedUnder(dropDownBounds, 0);

            ElementBounds closeButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(20, 4)
            ;

            ElementBounds saveButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(20, 4)
            ;


            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(dropDownBounds, creatureTextBounds, closeButtonBounds, saveButtonBounds);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            List<string> entityCodes = new List<string>();
            List<string> entityNames = new List<string>();
            entityCodes.Add("");
            entityNames.Add("-");

            foreach (EntityProperties type in capi.World.EntityTypes)
            {
                entityCodes.Add(type.Code.ToString());
                entityNames.Add(Lang.Get("item-creature-" + type.Code.Path));
            }


            ElementBounds tmpBoundsDim1;
            ElementBounds tmpBoundsDim2;
            ElementBounds tmpBoundsDim3;

            SingleComposer = capi.Gui
                .CreateCompo("spawnwerconfig", dialogBounds)
                .AddDialogBG(bgBounds, true)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .AddStaticText("Entity to spawn", CairoFont.WhiteDetailText(), creatureTextBounds)
                .AddDropDown(entityCodes.ToArray(), entityNames.ToArray(), 0, didSelectEntity, dropDownBounds, "entityCode")

                .AddStaticText("Spawn area dimensions", CairoFont.WhiteDetailText(), tmpBoundsDim1 = dropDownBounds.BelowCopy(0, 10))
                .AddStaticText("X1", CairoFont.WhiteDetailText(), tmpBoundsDim1 = tmpBoundsDim1.BelowCopy(0, 7).WithFixedSize(20, 29))
                .AddNumberInput(tmpBoundsDim1 = tmpBoundsDim1.RightCopy(5, -7).WithFixedSize(60, 29), OnDimensionsChanged, CairoFont.WhiteDetailText(), "x1")
                .AddStaticText("Y1", CairoFont.WhiteDetailText(), tmpBoundsDim1 = tmpBoundsDim1.RightCopy(10, 7).WithFixedSize(20, 29))
                .AddNumberInput(tmpBoundsDim1 = tmpBoundsDim1.RightCopy(5, -7).WithFixedSize(60, 29), OnDimensionsChanged, CairoFont.WhiteDetailText(), "y1")
                .AddStaticText("Z1", CairoFont.WhiteDetailText(), tmpBoundsDim1 = tmpBoundsDim1.RightCopy(10, 7).WithFixedSize(20, 29))
                .AddNumberInput(tmpBoundsDim1 = tmpBoundsDim1.RightCopy(5, -7).WithFixedSize(60, 29), OnDimensionsChanged, CairoFont.WhiteDetailText(), "z1")

                .AddStaticText("X2", CairoFont.WhiteDetailText(), tmpBoundsDim2 = dropDownBounds.FlatCopy().WithFixedSize(20, 29).FixedUnder(tmpBoundsDim1, -40))
                .AddNumberInput(tmpBoundsDim2 = tmpBoundsDim2.RightCopy(5, -7).WithFixedSize(60, 29), OnDimensionsChanged, CairoFont.WhiteDetailText(), "x2")
                .AddStaticText("Y2", CairoFont.WhiteDetailText(), tmpBoundsDim2 = tmpBoundsDim2.RightCopy(10, 7).WithFixedSize(20, 29))
                .AddNumberInput(tmpBoundsDim2 = tmpBoundsDim2.RightCopy(5, -7).WithFixedSize(60, 29), OnDimensionsChanged, CairoFont.WhiteDetailText(), "y2")
                .AddStaticText("Z2", CairoFont.WhiteDetailText(), tmpBoundsDim2 = tmpBoundsDim2.RightCopy(10, 7).WithFixedSize(20, 29))
                .AddNumberInput(tmpBoundsDim2 = tmpBoundsDim2.RightCopy(5, -7).WithFixedSize(60, 29), OnDimensionsChanged, CairoFont.WhiteDetailText(), "z2")

                .AddStaticText("Interval (ingame hours)", CairoFont.WhiteDetailText(), tmpBoundsDim3 = dropDownBounds.FlatCopy().WithFixedSize(300, 30).FixedUnder(tmpBoundsDim2, -40))
                .AddNumberInput(tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(100, 29), OnIntervalChanged, CairoFont.WhiteDetailText(), "interval")

                .AddStaticText("Max concurrent entities to spawn", CairoFont.WhiteDetailText(), tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, 10).WithFixedSize(300, 30))
                .AddNumberInput(tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(100, 29), OnMaxChanged, CairoFont.WhiteDetailText(), "maxentities")


                .AddSmallButton("Close", OnButtonClose, closeButtonBounds.FixedUnder(tmpBoundsDim3, 20))
                .AddSmallButton("Save", OnButtonSave, saveButtonBounds.FixedUnder(tmpBoundsDim3, 20))
                .Compose()
            ;

            UpdateFromServer(this.spawnerData);
        }

        private void OnMaxChanged(string t1)
        {
            float max = SingleComposer.GetNumberInput("maxentities").GetValue();
            spawnerData.MaxCount = (int)max;
        }

        private void OnIntervalChanged(string t1)
        {
            float interval = SingleComposer.GetNumberInput("interval").GetValue();
            spawnerData.InGameHourInterval = interval;
            
        }

        private void OnDimensionsChanged(string val)
        {
            if (updating) return;
            spawnerData.SpawnArea = ParseDimensions();
            
            capi.World.HighlightBlocks(capi.World.Player, new List<BlockPos>() { spawnerData.SpawnArea.Start.AsBlockPos.Add(blockEntityPos), spawnerData.SpawnArea.End.AsBlockPos.Add(blockEntityPos) }, EnumHighlightBlocksMode.Absolute, API.Common.EnumHighlightShape.Cube);
        }

        private Cuboidi ParseDimensions()
        {
            float x1 = SingleComposer.GetNumberInput("x1").GetValue();
            float y1 = SingleComposer.GetNumberInput("y1").GetValue();
            float z1 = SingleComposer.GetNumberInput("z1").GetValue();

            float x2 = SingleComposer.GetNumberInput("x2").GetValue();
            float y2 = SingleComposer.GetNumberInput("y2").GetValue();
            float z2 = SingleComposer.GetNumberInput("z2").GetValue();

            return new Cuboidi(
                (int)x1, (int)y1, (int)z1, (int)x2, (int)y2, (int)z2
            );
        }

        private void didSelectEntity(string code)
        {
            spawnerData.EntityCode = code;
        }

        public void UpdateFromServer(BESpawnerData data)
        {
            updating = true;
            this.spawnerData = data;
            SingleComposer.GetNumberInput("x1").SetValue(data.SpawnArea.X1);
            SingleComposer.GetNumberInput("y1").SetValue(data.SpawnArea.Y1);
            SingleComposer.GetNumberInput("z1").SetValue(data.SpawnArea.Z1);
            SingleComposer.GetNumberInput("x2").SetValue(data.SpawnArea.X2);
            SingleComposer.GetNumberInput("y2").SetValue(data.SpawnArea.Y2);
            SingleComposer.GetNumberInput("z2").SetValue(data.SpawnArea.Z2);
            SingleComposer.GetNumberInput("maxentities").SetValue(data.MaxCount);
            SingleComposer.GetNumberInput("interval").SetValue(data.InGameHourInterval);
            SingleComposer.GetDropDown("entityCode").SetSelectedValue(data.EntityCode);

            capi.World.HighlightBlocks(capi.World.Player, new List<BlockPos>() { spawnerData.SpawnArea.Start.AsBlockPos.Add(blockEntityPos), spawnerData.SpawnArea.End.AsBlockPos.Add(blockEntityPos) }, EnumHighlightBlocksMode.Absolute, API.Common.EnumHighlightShape.Cube);
            updating = false;
        }



        private void OnTextChanged(string value)
        {
            GuiElementDynamicText logtextElem = SingleComposer.GetDynamicText("text");
            SingleComposer.GetScrollbar("scrollbar").SetNewTotalHeight((float)logtextElem.Bounds.fixedHeight);
        }

        private void OnNewScrollbarvalue(float value)
        {
            GuiElementDynamicText logtextElem = SingleComposer.GetDynamicText("text");

            logtextElem.Bounds.fixedY = 3 - value;
            logtextElem.Bounds.CalcWorldBounds();
        }


        private void OnTitleBarClose()
        {
            OnButtonClose();
        }

        private bool OnButtonClose()
        {
            TryClose();
            return true;
        }

        private bool OnButtonSave()
        {
            capi.Network.SendBlockEntityPacket(blockEntityPos.X, blockEntityPos.Y, blockEntityPos.Z, 1001, SerializerUtil.Serialize(spawnerData));
            return true;
        }

        public override bool CaptureAllInputs()
        {
            return false;
        }

        public override bool RequiresUngrabbedMouse()
        {
            return false;
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();

            capi.World.HighlightBlocks(capi.World.Player, new List<BlockPos>() {});
        }

    }
}
