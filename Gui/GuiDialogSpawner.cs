using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
        HashSet<string> codes;

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

            ElementBounds creatureTextBounds = ElementBounds.Fixed(0, 30, 400, 25);

            ElementBounds dropDownBounds = ElementBounds.Fixed(0, 0, 400, 28).FixedUnder(creatureTextBounds, 0);


            ElementBounds areaTextBounds = ElementBounds.Fixed(0, 30, 400, 25).FixedUnder(dropDownBounds, 0);

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
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-20, 0);

            List<string> entityCodes = new List<string>();
            List<string> entityNames = new List<string>();
            

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
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .AddStaticText("Entities to spawn", CairoFont.WhiteDetailText(), creatureTextBounds)
                .AddMultiSelectDropDown(entityCodes.ToArray(), entityNames.ToArray(), 0, didSelectEntity, dropDownBounds, "entityCode")

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

                .AddStaticText("Interval (ingame hours)", CairoFont.WhiteDetailText(), tmpBoundsDim3 = dropDownBounds.FlatCopy().WithFixedSize(400, 30).FixedUnder(tmpBoundsDim2, -40))
                .AddNumberInput(tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(100, 29), OnIntervalChanged, CairoFont.WhiteDetailText(), "interval")

                .AddStaticText("Max concurrent entities to spawn", CairoFont.WhiteDetailText(), tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, 10).WithFixedSize(400, 30))
                .AddNumberInput(tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(100, 29), OnMaxChanged, CairoFont.WhiteDetailText(), "maxentities")

                .AddStaticText("Spawn 'x' entities, then remove block (0 for infinite)", CairoFont.WhiteDetailText(), tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, 10).WithFixedSize(400, 30))
                .AddNumberInput(tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(100, 29), OnCountChanged, CairoFont.WhiteDetailText(), "spawncount")

                .AddStaticText("Begin spawning only after being imported", CairoFont.WhiteDetailText(), tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, 10).WithFixedSize(400, 30))
                .AddSwitch(onTogglePrimer, tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10), "primerSwitch", 20)


                .AddSmallButton("Close", OnButtonClose, closeButtonBounds.FixedUnder(tmpBoundsDim3, 20))
                .AddSmallButton("Save", OnButtonSave, saveButtonBounds.FixedUnder(tmpBoundsDim3, 20))
                .Compose()
            ;

            UpdateFromServer(this.spawnerData);
        }

        private void onTogglePrimer(bool on)
        {
            spawnerData.SpawnOnlyAfterImport = on;
        }

        private void OnMaxChanged(string t1)
        {
            float max = SingleComposer.GetNumberInput("maxentities").GetValue();
            spawnerData.MaxCount = (int)max;
        }

        private void OnCountChanged(string t1)
        {
            float count = SingleComposer.GetNumberInput("spawncount").GetValue();
            spawnerData.RemoveAfterSpawnCount = (int)count;
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
            
            capi.World.HighlightBlocks(capi.World.Player, (int)EnumHighlightSlot.Spawner, new List<BlockPos>() { spawnerData.SpawnArea.Start.AsBlockPos.Add(blockEntityPos), spawnerData.SpawnArea.End.AsBlockPos.Add(blockEntityPos) }, EnumHighlightBlocksMode.Absolute, API.Common.EnumHighlightShape.Cube);
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

        private void didSelectEntity(string code, bool selected)
        {
            if (selected)
            {
                codes.Add(code);
            } else
            {
                codes.Remove(code);
            }

            spawnerData.EntityCodes = codes.ToArray();
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
            SingleComposer.GetNumberInput("spawncount").SetValue(data.RemoveAfterSpawnCount);

            SingleComposer.GetSwitch("primerSwitch").SetValue(data.SpawnOnlyAfterImport);

            SingleComposer.GetNumberInput("interval").SetValue(data.InGameHourInterval);
            SingleComposer.GetDropDown("entityCode").SetSelectedValue(data.EntityCodes);

            if (spawnerData.EntityCodes == null)
            {
                codes = new HashSet<string>();
            } else
            {
                codes = new HashSet<string>(spawnerData.EntityCodes);
            }
            

            capi.World.HighlightBlocks(capi.World.Player, (int)EnumHighlightSlot.Spawner, new List<BlockPos>() {
                spawnerData.SpawnArea.Start.AsBlockPos.Add(blockEntityPos), spawnerData.SpawnArea.End.AsBlockPos.Add(blockEntityPos)
            }, EnumHighlightBlocksMode.Absolute, API.Common.EnumHighlightShape.Cube);

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

            capi.World.HighlightBlocks(capi.World.Player, (int)EnumHighlightSlot.Spawner, new List<BlockPos>() {});
        }

    }
}
