using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent;

public class BlockEntityBeeHiveKiln : BlockEntity, IRotatable
{
    public BlockFacing Orientation;
    private MultiblockStructure structure;
    private MultiblockStructure highlightedStructure;

    private BlockPos CenterPos;

    private bool receivesHeat;
    private float receivesHeatSmooth; // 0..1
    public double TotalHoursLastUpdate;
    public double TotalHoursHeatReceived;
    public bool StructureComplete;
    private int tickCounter;
    private bool wasNotProcessing;

    private BlockPos[] particlePositions;
    private BEBehaviorDoor beBehaviorDoor;

    /// <summary>
    /// After this time the kiln gets heat damage from usage
    /// </summary>
    public static int KilnBreakAfterHours = 168;
    /// <summary>
    /// The time it takes to burn an item after it has been heated to at least ItemBurnTemperature
    /// </summary>
    public static int ItemBurnTimeHours = 9;
    /// <summary>
    /// Temperature (°C) needed to start the burning process convert from raw to burned
    /// </summary>
    public static int ItemBurnTemperature = 950;
    /// <summary>
    /// The max temperature (°C) the kiln and items will reach
    /// </summary>
    public static int ItemMaxTemperature = 1200;
    /// <summary>
    /// The temmperature gain per item per hour.
    /// It takes 2 hours to get from 0 to 1000 °C
    /// </summary>
    public static int ItemTemperatureGainPerHour = 500;

    private static AdvancedParticleProperties smokeParticles = new AdvancedParticleProperties()
    {
        HsvaColor =
            new NatFloat[] { NatFloat.createUniform(0, 0), NatFloat.createUniform(0, 0), NatFloat.createUniform(40, 30), NatFloat.createUniform(220, 50) },
        OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -16),
        GravityEffect = NatFloat.createUniform(0, 0),
        Velocity = new NatFloat[] { NatFloat.createUniform(0f, 0.05f), NatFloat.createUniform(0.2f, 0.3f), NatFloat.createUniform(0f, 0.05f) },
        Size = NatFloat.createUniform(0.3f, 0.05f),
        Quantity = NatFloat.createUniform(0.25f, 0),
        SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, 1.5f),
        LifeLength = NatFloat.createUniform(4.5f, 0),
        ParticleModel = EnumParticleModel.Quad,
        SelfPropelled = true,
    };

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        structure = Block.Attributes["multiblockStructure"].AsObject<MultiblockStructure>();
        if (Orientation != null)
        {
            Init();
        }
    }

    public override void OnPlacementBySchematic(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos pos, Dictionary<int, Dictionary<int, int>> replaceBlocks, int centerrockblockid,
        Block layerBlock, bool resolveImports)
    {
        base.OnPlacementBySchematic(api, blockAccessor, pos, replaceBlocks, centerrockblockid, layerBlock, resolveImports);
        // needed when a spawned in by wgen or we
        if (Orientation != null && CenterPos == null)
        {
            Init();
        }
    }

    public void Init()
    {
        if (Api.Side == EnumAppSide.Client)
        {
            RegisterGameTickListener(OnClientTick50ms, 50);
        }
        else
        {
            RegisterGameTickListener(OnServerTick1s, 1000);
        }

        int rotYDeg = 0;

        switch (Orientation.Code)
        {
            case "east":
            {
                rotYDeg = 270;
                break;
            }
            case "west":
            {
                rotYDeg = 90;
                break;
            }
            case "south":
            {
                rotYDeg = 180;
                break;
            }
        }

        structure.InitForUse(rotYDeg);

        CenterPos = Pos.AddCopy(Orientation.Normali * 2);

        particlePositions = new BlockPos[10];
        var downCenter = CenterPos.Down();
        particlePositions[0] = downCenter;
        particlePositions[1] = downCenter.AddCopy(Orientation.Opposite);
        particlePositions[2] = downCenter.AddCopy(Orientation);

        particlePositions[3] = downCenter.AddCopy(Orientation.GetCW());
        particlePositions[4] = downCenter.AddCopy(Orientation.GetCW()).Add(Orientation.Opposite);
        particlePositions[5] = downCenter.AddCopy(Orientation.GetCW()).Add(Orientation);

        particlePositions[6] = downCenter.AddCopy(Orientation.GetCCW());
        particlePositions[7] = downCenter.AddCopy(Orientation.GetCCW()).Add(Orientation.Opposite);
        particlePositions[8] = downCenter.AddCopy(Orientation.GetCCW()).Add(Orientation);
        particlePositions[9] = downCenter.UpCopy(3);

        beBehaviorDoor = GetBehavior<BEBehaviorDoor>();
    }

    private void OnClientTick50ms(float dt)
    {
        receivesHeatSmooth = GameMath.Clamp(receivesHeatSmooth + (receivesHeat ? dt / 10 : -dt / 3), 0, 1);

        if (receivesHeatSmooth == 0) return;

        var rnd = Api.World.Rand;
        for (int i = 0; i < Entity.FireParticleProps.Length; i++)
        {
            int index = Math.Min(Entity.FireParticleProps.Length - 1, Api.World.Rand.Next(Entity.FireParticleProps.Length + 1));

            for (int j = 0; j < particlePositions.Length; j++)
            {
                var particles = Entity.FireParticleProps[index];
                var pos = particlePositions[j];

                if (j == 9)
                {
                    particles = smokeParticles;
                    particles.Quantity.avg = 0.2f;
                    particles.basePos.Set(pos.X + 0.5, pos.InternalY + 0.75, pos.Z + 0.5);
                    particles.Velocity[1].avg = (float)(0.3 + 0.3 * rnd.NextDouble()) * 2;
                    particles.PosOffset[1].var = 0.2f;
                    particles.Velocity[0].avg = (float)(rnd.NextDouble() - 0.5) / 4;
                    particles.Velocity[2].avg = (float)(rnd.NextDouble() - 0.5) / 4;
                }
                else
                {
                    particles.Quantity.avg = GameMath.Sqrt(0.5f * (index == 0 ? 0.5f : (index == 1 ? 5 : 0.6f))) / 4f;
                    particles.basePos.Set(pos.X + 0.5, pos.InternalY + 0.5, pos.Z + 0.5);
                    particles.Velocity[1].avg = (float)(0.5 + 0.5 * rnd.NextDouble()) * 2;
                    particles.PosOffset[1].var = 1;
                    particles.Velocity[0].avg = (float)(rnd.NextDouble() - 0.5);
                    particles.Velocity[2].avg = (float)(rnd.NextDouble() - 0.5);
                }

                particles.PosOffset[0].var = 0.49f;
                particles.PosOffset[2].var = 0.49f;

                Api.World.SpawnParticles(particles);
            }
        }
    }

    private void OnServerTick1s(float dt)
    {
        if (receivesHeat)
        {
            Vec3d pos = CenterPos.ToVec3d().Add(0.5, 0, 0.5);
            Entity[] entities = Api.World.GetEntitiesAround(pos, 1.75f, 3, e => e.Alive && e is EntityAgent);

            foreach (var entity in entities)
                entity.ReceiveDamage(new DamageSource()
                {
                    DamageTier = 1,
                    SourcePos = pos,
                    SourceBlock = Block,
                    Type = EnumDamageType.Fire
                }, 4);
        }

        if (++tickCounter % 3 == 0) OnServerTick3s();
    }

    private void OnServerTick3s()
    {
        var markDirty = false;

        var minHeatHours = float.MaxValue;
        var beforeReceiveHeat = receivesHeat;
        var beforeStructureComplete = StructureComplete;

        if (!receivesHeat)
        {
            TotalHoursLastUpdate = Api.World.Calendar.TotalHours;
        }

        receivesHeat = true;
        for (int j = 0; j < 9; j++)
        {
            var pos = particlePositions[j].DownCopy();
            var blockEntity = Api.World.BlockAccessor.GetBlockEntity(pos);
            float heatHours = 0;
            if (blockEntity is BlockEntityCoalPile becp && becp.IsBurning)
            {
                heatHours = becp.GetHoursLeft(TotalHoursLastUpdate);
            }
            else if (blockEntity is BlockEntityGroundStorage gs && gs.IsBurning)
            {
                heatHours = gs.GetHoursLeft(TotalHoursLastUpdate);
            }

            minHeatHours = Math.Min(minHeatHours, heatHours);
            receivesHeat &= heatHours > 0;
        }

        StructureComplete = structure.InCompleteBlockCount(Api.World, Pos) == 0;
        if (beforeReceiveHeat != receivesHeat || beforeStructureComplete != StructureComplete)
        {
            markDirty = true;
        }

        if (receivesHeat)
        {
            if (!StructureComplete || beBehaviorDoor.Opened)
            {
                wasNotProcessing = true;
                TotalHoursLastUpdate = Api.World.Calendar.TotalHours;
                MarkDirty();
                return;
            }

            // pause the process time while structure is incomplete or door is open (heat loss)
            if (wasNotProcessing)
            {
                wasNotProcessing = false;
                TotalHoursLastUpdate = Api.World.Calendar.TotalHours;
            }

            var hoursPassed = Api.World.Calendar.TotalHours - TotalHoursLastUpdate;

            var heatHoursReceived = Math.Max(0, GameMath.Min((float)hoursPassed, minHeatHours));
            TotalHoursHeatReceived += heatHoursReceived;
            UpdateGroundStorage(heatHoursReceived);
            TotalHoursLastUpdate = Api.World.Calendar.TotalHours;
            markDirty = true;
        }

        // check damage every 7 * 24h = 168 ingame hours to damage - same time as cementation furnace
        if (TotalHoursHeatReceived >= KilnBreakAfterHours)
        {
            TotalHoursHeatReceived = 0;
            structure.WalkMatchingBlocks(Api.World, Pos, (block, pos) =>
            {
                var heatResistance = block.Attributes?["heatResistance"].AsFloat(1) ?? 1;

                if (Api.World.Rand.NextDouble() > heatResistance)
                {
                    var nowBlock = Api.World.GetBlock(block.CodeWithVariant("state", "damaged"));
                    Api.World.BlockAccessor.SetBlock(nowBlock.Id, pos);
                    StructureComplete = false;
                    markDirty = true;
                }
            });
        }

        if (markDirty)
        {
            MarkDirty();
        }
    }

    private void UpdateGroundStorage(float hoursHeatReceived)
    {
        var doorOpen = GetOpenDoors();

        for (int j = 0; j < 9; j++)
        {
            for (int i = 1; i < 4; i++)
            {
                var pos = particlePositions[j].UpCopy(i);
                var groundStorage = Api.World.BlockAccessor.GetBlockEntity<BlockEntityGroundStorage>(pos);
                if (groundStorage == null) continue;

                for (var index = 0; index < groundStorage.Inventory.Count; index++)
                {
                    var itemSlot = groundStorage.Inventory[index];
                    if (itemSlot.Empty) continue;
                    float itemHoursHeatReceived = 0;
                    var collectible = itemSlot.Itemstack.Collectible;

                    if (collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack.Block?.BlockMaterial == EnumBlockMaterial.Ceramic
                        ||  collectible.CombustibleProps?.SmeltingType == EnumSmeltType.Fire
                        || collectible.Attributes?["beehivekiln"].Exists == true)
                    {
                        var temp = itemSlot.Itemstack.Collectible.GetTemperature(Api.World, itemSlot.Itemstack, hoursHeatReceived);
                        var tempGain = hoursHeatReceived * ItemTemperatureGainPerHour;
                        var hoursHeatingUp = (ItemBurnTemperature - temp) / ItemTemperatureGainPerHour;
                        if (hoursHeatingUp < 0)
                        {
                            hoursHeatingUp = 0;
                        }
                        if (temp < ItemMaxTemperature)
                        {
                            temp = GameMath.Min(ItemMaxTemperature, temp + tempGain);
                            collectible.SetTemperature(Api.World, itemSlot.Itemstack, temp);
                        }

                        var heatReceived = (hoursHeatReceived - hoursHeatingUp);
                        if(temp >= ItemBurnTemperature && heatReceived > 0)
                        {
                            itemHoursHeatReceived = itemSlot.Itemstack.Attributes.GetFloat("hoursHeatReceived") + heatReceived;
                            itemSlot.Itemstack.Attributes.SetFloat("hoursHeatReceived", itemHoursHeatReceived);
                        }
                        itemSlot.MarkDirty();
                    }

                    if (itemHoursHeatReceived >= ItemBurnTimeHours)
                    {
                        ConvertItemToBurned(groundStorage, itemSlot, doorOpen);
                    }
                }

                groundStorage.MarkDirty();
            }
        }
    }

    private void ConvertItemToBurned(BlockEntityGroundStorage groundStorage, ItemSlot itemSlot, int doorOpen)
    {
        groundStorage.forceStorageProps = true;

        if (itemSlot != null && !itemSlot.Empty)
        {
            var rawStack = itemSlot.Itemstack;
            var firedStack = rawStack.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;
            var kiln = rawStack.Collectible.Attributes?["beehivekiln"];
            var stack = kiln?[doorOpen.ToString()]?.AsObject<JsonItemStack>();
            var temp = itemSlot.Itemstack.Collectible.GetTemperature(Api.World, itemSlot.Itemstack);
            if (kiln?.Exists == true && stack?.Resolve(Api.World, "beehivekiln-burn") == true)
            {
                itemSlot.Itemstack = stack.ResolvedItemstack.Clone();
                itemSlot.Itemstack.StackSize = rawStack.StackSize / rawStack.Collectible.CombustibleProps.SmeltedRatio;
            }
            else if (firedStack != null)
            {
                itemSlot.Itemstack = firedStack.Clone();
                itemSlot.Itemstack.StackSize = rawStack.StackSize / rawStack.Collectible.CombustibleProps.SmeltedRatio;
            }
            itemSlot.Itemstack.Collectible.SetTemperature(Api.World, itemSlot.Itemstack, temp);
            itemSlot.MarkDirty();
        }

        groundStorage.MarkDirty(true);
    }

    private int GetOpenDoors()
    {
        var backDoorPos = CenterPos.AddCopy(Orientation.Normali * 2).Up();
        var rightDoorPos = CenterPos.AddCopy(Orientation.GetCW().Normali * 2).Up();
        var leftDoorPos = CenterPos.AddCopy(Orientation.GetCCW().Normali * 2).Up();
        var doorOpen = 0;
        foreach (var doorPos in new[] { backDoorPos, rightDoorPos, leftDoorPos })
        {
            var door = Api.World.BlockAccessor.GetBlock(doorPos);

            if (door.Variant["state"] != null && door.Variant["state"] == "opened")
            {
                doorOpen++;
            }
        }

        return doorOpen;
    }

    public void Interact(IPlayer byPlayer)
    {
        if (Api is not ICoreClientAPI capi) return;

        bool sneaking = byPlayer.WorldData.EntityControls.ShiftKey;

        int damagedTiles = 0;
        int wrongTiles = 0;
        int incompleteCount = 0;
        BlockPos posMain = Pos;

        // set up incompleteCount (etc) for both orientations and pick whichever is more complete
        if (!sneaking)
        {
            incompleteCount = structure.InCompleteBlockCount(Api.World, Pos,
                (haveBlock, wantLoc) =>
                {
                    var firstCodePart = haveBlock.FirstCodePart();
                    if ((firstCodePart == "refractorybricks" || firstCodePart == "claybricks" || firstCodePart == "refractorybrickgrating") && haveBlock.Variant["state"] == "damaged")
                    {
                        damagedTiles++;
                    }
                    else wrongTiles++;
                }
            );

            if (incompleteCount > 0)
                highlightedStructure = structure;
        }

        if (!sneaking && incompleteCount > 0)
        {
            if (wrongTiles > 0 && damagedTiles > 0)
            {
                capi.TriggerIngameError(this, "incomplete",
                    Lang.Get("Structure is not complete, {0} blocks are missing or wrong, {1} tiles are damaged!", wrongTiles, damagedTiles));
            }
            else
            {
                if (wrongTiles > 0)
                {
                    capi.TriggerIngameError(this, "incomplete",
                        Lang.Get("Structure is not complete, {0} blocks are missing or wrong!", wrongTiles));
                }
                else
                {
                    if (damagedTiles == 1)
                    {
                        capi.TriggerIngameError(this, "incomplete",
                            Lang.Get("Structure is not complete, {0} tile is damaged!", damagedTiles));
                    }
                    else
                    {
                        capi.TriggerIngameError(this, "incomplete",
                            Lang.Get("Structure is not complete, {0} tiles are damaged!", damagedTiles));
                    }
                }
            }

            highlightedStructure.HighlightIncompleteParts(Api.World, byPlayer, posMain);
            return;
        }

        highlightedStructure?.ClearHighlights(Api.World, byPlayer);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        receivesHeat = tree.GetBool("receivesHeat");
        TotalHoursLastUpdate = tree.GetDouble("totalHoursLastUpdate");
        StructureComplete = tree.GetBool("structureComplete");
        Orientation = BlockFacing.FromFirstLetter(tree.GetString("orientation"));
        TotalHoursHeatReceived = tree.GetDouble("totalHoursHeatReceived");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetBool("receivesHeat", receivesHeat);
        tree.SetDouble("totalHoursLastUpdate", TotalHoursLastUpdate);
        tree.SetBool("structureComplete", StructureComplete);
        tree.SetString("orientation", Orientation.Code);
        tree.SetDouble("totalHoursHeatReceived", TotalHoursHeatReceived);
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();

        if (Api is ICoreClientAPI capi)
        {
            highlightedStructure?.ClearHighlights(Api.World, capi.World.Player);
        }
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();

        if (Api is ICoreClientAPI capi)
        {
            highlightedStructure?.ClearHighlights(Api.World, capi.World.Player);
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        if (GetBehavior<BEBehaviorDoor>().Opened)
        {
            dsc.AppendLine(Lang.Get("Door must be closed for firing!"));
        }

        if (!StructureComplete)
        {
            dsc.AppendLine(Lang.Get("Structure incomplete! Can't get hot enough, paused."));
            return;
        }

        if (receivesHeat)
        {
            dsc.AppendLine(Lang.Get("Okay! Receives heat!"));
        }
        else
        {
            dsc.AppendLine(Lang.Get("Ready to be fired. Ignite 3x3 piles of coal below. (progress will proceed once all 9 piles have ignited)"));
        }

        if (TotalHoursHeatReceived > 0)
        {
            dsc.AppendLine(Lang.Get("Firing: for {0:0.##} hours", TotalHoursHeatReceived));
        }
    }

    public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping,
        EnumAxis? flipAxis)
    {
        var orientation = BlockFacing.FromFirstLetter(tree.GetString("orientation"));
        var horizontalRotated = orientation.GetHorizontalRotated(-degreeRotation - 180);
        tree.SetString("orientation", horizontalRotated.Code);

        var rotateYRad = tree.GetFloat("rotateYRad");
        rotateYRad = (rotateYRad - degreeRotation * GameMath.DEG2RAD) % GameMath.TWOPI;
        tree.SetFloat("rotateYRad", rotateYRad);
    }
}
