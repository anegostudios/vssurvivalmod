using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

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
        List<string> codes = new List<string>();

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

            var entityProps = capi.World
                .SearchItems(new AssetLocation("*", "creature*"))
                .Select(item => new AssetLocation(item.Code.Domain, item.CodeEndWithoutParts(1)))
                .Select(location => capi.World.GetEntityType(location))
                .OfType<EntityProperties>()
                .OrderBy(type => Lang.Get("item-creature-" + type.Code.Path))
                .OrderBy(type => type.Code.FirstCodePart())
            ;

            foreach (var type in entityProps)
            {
                // Quick very ugly hack for now: We have like 150 butterflies, but the drop down system is not designed for large lists. Lets ignore them for now
                if (type.Code.Path.Contains("butterfly")) continue;

                entityCodes.Add(type.Code.ToString());
                entityNames.Add(Lang.Get("item-creature-" + type.Code.Path));
            }

            if (entityNames.Count == 0) return;


            ElementBounds tmpBoundsDim1;
            ElementBounds tmpBoundsDim2;
            ElementBounds tmpBoundsDim3;

            SingleComposer = capi.Gui
                .CreateCompo("spawnwerconfig", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .AddStaticText("Entities to spawn", CairoFont.WhiteDetailText(), creatureTextBounds)
                .AddMultiSelectDropDown(entityCodes.ToArray(), entityNames.ToArray(), 0, null, dropDownBounds, "entityCode")

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

                .AddStaticText("Player range mode", CairoFont.WhiteDetailText(), tmpBoundsDim3 = dropDownBounds.FlatCopy().WithFixedSize(400, 30).FixedUnder(tmpBoundsDim2, -40))
                .AddDropDown(
                    new string[] { "0", "1", "2", "3" }, 
                    new string[] { "Ignore player", "Spawn only when player is within minum range", "Spawn only when player is outside maximum range", "Spawn only when player is outside minimum range but within maximum range" }, 
                    0, null, tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(300, 29), CairoFont.WhiteDetailText(), "playerRangeMode"
                )

                .AddStaticText("Minimum player range", CairoFont.WhiteDetailText(), tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0,10).WithFixedSize(200, 30))
                .AddStaticText("Maximum player range", CairoFont.WhiteDetailText(), tmpBoundsDim3.RightCopy(20, 0).WithFixedSize(200, 30))

                .AddNumberInput(tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(100, 29), null, CairoFont.WhiteDetailText(), "minPlayerRange")
                .AddNumberInput(tmpBoundsDim3.RightCopy(120, 0).WithFixedSize(100, 29), null, CairoFont.WhiteDetailText(), "maxPlayerRange")

                .AddStaticText("Spawn Interval (ingame hours)", CairoFont.WhiteDetailText(), tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, 10).WithFixedSize(400, 30))
                .AddNumberInput(tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(100, 29), null, CairoFont.WhiteDetailText(), "interval")

                .AddStaticText("Max concurrent entities to spawn", CairoFont.WhiteDetailText(), tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, 10).WithFixedSize(400, 30))
                .AddNumberInput(tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(100, 29), null, CairoFont.WhiteDetailText(), "maxentities")

                .AddStaticText("Spawn 'x' entities, then remove block (0 for infinite)", CairoFont.WhiteDetailText(), tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, 10).WithFixedSize(400, 30))
                .AddNumberInput(tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(100, 29), null, CairoFont.WhiteDetailText(), "spawncount")

                .AddSwitch(null, tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, 10), "primerSwitch", 20)
                .AddStaticText("Begin spawning only after being imported", CairoFont.WhiteDetailText(), tmpBoundsDim3.RightCopy(10, 0).WithFixedSize(400, 30))

                .AddSwitch(null, tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, 10), "rechargeMode", 20)
                .AddStaticText("Slowly recharge before spawning more entities", CairoFont.WhiteDetailText(), tmpBoundsDim3.RightCopy(10, 0).WithFixedSize(400, 30))

                .AddStaticText("Max charge", CairoFont.WhiteDetailText(), tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, 10).WithFixedSize(100, 30))
                .AddStaticText("Recharge rate per hour", CairoFont.WhiteDetailText(), tmpBoundsDim3.RightCopy(75, 0).WithFixedSize(200, 30))

                .AddNumberInput(tmpBoundsDim3 = tmpBoundsDim3.BelowCopy(0, -10).WithFixedSize(75, 29), null, CairoFont.WhiteDetailText(), "chargeCapacity")
                .AddNumberInput(tmpBoundsDim3.RightCopy(100, 0).WithFixedSize(75, 29), null, CairoFont.WhiteDetailText(), "rechargePerHour")

                .AddSmallButton("Close", OnButtonClose, closeButtonBounds.FixedUnder(tmpBoundsDim3, 20))
                .AddSmallButton("Save", OnButtonSave, saveButtonBounds.FixedUnder(tmpBoundsDim3, 20))
                .Compose()
            ;

            UpdateFromServer(this.spawnerData);
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
            SingleComposer.GetNumberInput("minPlayerRange").SetValue(data.MinPlayerRange);
            SingleComposer.GetNumberInput("maxPlayerRange").SetValue(data.MaxPlayerRange);

            SingleComposer.GetSwitch("primerSwitch").SetValue(data.SpawnOnlyAfterImport);

            SingleComposer.GetNumberInput("interval").SetValue(data.InGameHourInterval);
            SingleComposer.GetDropDown("entityCode").SetSelectedValue(data.EntityCodes);

            SingleComposer.GetSwitch("rechargeMode").On = data.InternalCapacity > 0;
            SingleComposer.GetNumberInput("chargeCapacity").SetValue(data.InternalCapacity);
            SingleComposer.GetNumberInput("rechargePerHour").SetValue((float)data.RechargePerHour);
            SingleComposer.GetDropDown("playerRangeMode").SetSelectedIndex((int)data.SpawnRangeMode);

            capi.World.HighlightBlocks(capi.World.Player, (int)EnumHighlightSlot.Spawner, new List<BlockPos>() {
                spawnerData.SpawnArea.Start.AsBlockPos.Add(blockEntityPos), spawnerData.SpawnArea.End.AsBlockPos.Add(blockEntityPos)
            }, EnumHighlightBlocksMode.Absolute, API.Common.EnumHighlightShape.Cube);

            updating = false;
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
            spawnerData.SpawnArea.X1 = (int)SingleComposer.GetNumberInput("x1").GetValue();
            spawnerData.SpawnArea.Y1 = (int)SingleComposer.GetNumberInput("y1").GetValue();
            spawnerData.SpawnArea.Z1 = (int)SingleComposer.GetNumberInput("z1").GetValue();
            spawnerData.SpawnArea.X2 = (int)SingleComposer.GetNumberInput("x2").GetValue();
            spawnerData.SpawnArea.Y2 = (int)SingleComposer.GetNumberInput("y2").GetValue();
            spawnerData.SpawnArea.Z2 = (int)SingleComposer.GetNumberInput("z2").GetValue();
            spawnerData.MinPlayerRange = (int)SingleComposer.GetNumberInput("minPlayerRange").GetValue();
            spawnerData.MaxPlayerRange = (int)SingleComposer.GetNumberInput("maxPlayerRange").GetValue();
            spawnerData.MaxCount = (int)SingleComposer.GetNumberInput("maxentities").GetValue();
            spawnerData.RemoveAfterSpawnCount = (int)SingleComposer.GetNumberInput("spawncount").GetValue();
            spawnerData.SpawnOnlyAfterImport = SingleComposer.GetSwitch("primerSwitch").On;
            spawnerData.InGameHourInterval = (float)SingleComposer.GetNumberInput("interval").GetValue();
            bool rechargeOn = SingleComposer.GetSwitch("rechargeMode").On;
            spawnerData.InternalCapacity = rechargeOn ? (int)SingleComposer.GetNumberInput("chargeCapacity").GetValue() : 0;
            spawnerData.RechargePerHour = SingleComposer.GetNumberInput("rechargePerHour").GetValue();
            spawnerData.SpawnRangeMode = (EnumSpawnRangeMode)SingleComposer.GetDropDown("playerRangeMode").SelectedValue.ToInt();
            spawnerData.EntityCodes = SingleComposer.GetDropDown("entityCode").SelectedValues;

            byte[] data = SerializerUtil.Serialize(spawnerData);
            capi.Network.SendBlockEntityPacket(blockEntityPos, 1001, data);
            
            return true;
        }

        public override bool CaptureAllInputs()
        {
            return false;
        }

        public override bool PrefersUngrabbedMouse => false;

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();

            capi.World.HighlightBlocks(capi.World.Player, (int)EnumHighlightSlot.Spawner, new List<BlockPos>() {});
        }

    }
}
