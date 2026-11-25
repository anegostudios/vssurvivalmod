using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public class ElevatorSystem
{
    public EntityElevator? Entity;
    public List<int> ControlPositions = new();
    public bool ShouldUpdate = false;
}

public class EntityElevator : Entity, ICustomInteractionHelpPositioning
{
    public override double FrustumSphereRadius => base.FrustumSphereRadius * 2;

    public override bool ApplyGravity => false;

    public override bool IsInteractable => true;


    public override float MaterialDensity => 100f;

    double swimmingOffsetY;
    public override double SwimmingOffsetY => swimmingOffsetY;

    public bool TransparentCenter => true;

    /// <summary>
    /// The speed this boat can reach at full power
    /// </summary>
    public float SpeedMultiplier = 1.5f;

    ICoreClientAPI capi = null!;
    public ILoadedSound travelSound = null!;
    public ILoadedSound latchSound = null!;

    float accum;

    private bool isMovingUp, isMovingDown;
    private bool legacyGroundCleared;

    public ElevatorSystem? ElevatorSys;

    private int CurrentStopIndex;

    public string? NetworkCode
    {
        get => Attributes.GetString("networkCode");
        set => Attributes.SetString("networkCode", value);
    }

    public bool IsMoving => isMovingUp || isMovingDown;

    private const string UpAp = "UpAP";
    private const string DownAp = "DownAP";

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        swimmingOffsetY = properties.Attributes?["swimmingOffsetY"].AsDouble() ?? 0;

        base.Initialize(properties, api, InChunkIndex3d);

        if (api is ICoreClientAPI clientApi)
        {
            capi = clientApi;
            travelSound = capi.World.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("sounds/effect/gearbox_turn.ogg"),
                ShouldLoop = true,
                RelativePosition = false,
                DisposeOnFinish = false,
                Volume = 0.15f
            });

            latchSound = capi.World.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("sounds/effect/latch.ogg"),
                ShouldLoop = false,
                RelativePosition = false,
                DisposeOnFinish = false
            });
        }

        if (api is ICoreServerAPI serverApi)
        {
            var elevatorModSystem = serverApi.ModLoader.GetModSystem<ModsystemElevator>();
            if (NetworkCode != null)
            {
                ElevatorSys = elevatorModSystem.RegisterElevator(NetworkCode, this);

                // enable max height on old elevators which where already activated
                // if we do not have the control poaints yet, those will also try to update it
                if (Attributes.GetBool("isActivated") && !Attributes.HasAttribute("maxHeight") && ElevatorSys?.ControlPositions.Count > 0)
                {
                    Attributes.SetInt("maxHeight", ElevatorSys.ControlPositions.Last());
                }
            }
        }
    }

    public override void OnGameTick(float dt)
    {
        if (World.Side == EnumAppSide.Server)
        {
            if (!legacyGroundCleared)
            {
                clearLegacyGround(World.BlockAccessor);
                legacyGroundCleared = true;
            }
            updatePosition(dt);
        }

        if (World.Side == EnumAppSide.Client)
        {
            var movingUp = WatchedAttributes.GetBool("isMovingUp");
            var movingDown = WatchedAttributes.GetBool("isMovingDown");
            if (!movingUp && !movingDown && IsMoving)
            {
                StopAnimation("gearturndown");
                StopAnimation("gearturnup");
            }
            isMovingUp = movingUp;
            isMovingDown = movingDown;
            NowInMotion(dt);
        }

        base.OnGameTick(dt);
    }

    protected virtual void updatePosition(float dt)
    {
        // Ignore lag spikes
        dt = Math.Min(0.5f, dt);

        if (ElevatorSys == null || ElevatorSys.ControlPositions.Count == 0) return;

        var elevatorStopHeight = ElevatorSys.ControlPositions[CurrentStopIndex];

        var diff = Math.Abs(Pos.Y - elevatorStopHeight);
        if (diff >= 0.02f)
        {
            var mul = Math.Max(0.5f, Math.Clamp(diff, 0, 1));
            if (Pos.Y < elevatorStopHeight)
            {
                Pos.Y += dt * SpeedMultiplier * mul;
                isMovingUp = true;
            }
            else
            {
                Pos.Y -= dt * SpeedMultiplier * mul;
                isMovingDown = true;
            }

        }
        else
        {
            Pos.Y = elevatorStopHeight;
            isMovingUp = isMovingDown = false;
        }

        WatchedAttributes.SetBool("isMovingUp", isMovingUp);
        WatchedAttributes.SetBool("isMovingDown", isMovingDown);
    }

    // Older saves could potentially have 'invisible' elevator floor blocks.
    private void clearLegacyGround(IBlockAccessor blockAccessor)
    {
        int colliderBlockId = blockAccessor.GetBlock("meta-collider").Id;
        BlockPos origin = Pos.AsBlockPos;
        BlockPos pos = origin.Copy();

        clearLegacyGroundAt(blockAccessor, colliderBlockId, origin, pos, origin.Y);
        clearLegacyGroundAt(blockAccessor, colliderBlockId, origin, pos, (int)Math.Ceiling(Pos.Y));
        if (ElevatorSys == null) return;

        for (int i = 0; i < ElevatorSys.ControlPositions.Count; i++)
        {
            clearLegacyGroundAt(blockAccessor, colliderBlockId, origin, pos, ElevatorSys.ControlPositions[i]);
        }
    }

    private static void clearLegacyGroundAt(IBlockAccessor blockAccessor, int colliderBlockId, BlockPos origin, BlockPos pos, int height)
    {
        for (int x = -1; x < 2; x++)
        {
            for (int z = -1; z < 2; z++)
            {
                pos.Set(origin).Add(x, height - origin.Y, z);
                Block block = blockAccessor.GetBlock(pos);
                if (block.Id == colliderBlockId)
                {
                    blockAccessor.SetBlock(0, pos);
                    continue;
                }
                if (block is not BlockToggleCollisionBox) continue;

                var behavior = blockAccessor.GetBlockEntity<BlockEntityGeneric>(pos)?.GetBehavior<BEBehaviorToggleCollisionBox>();
                if (behavior?.Solid != true) continue;

                behavior.Solid = false;
                behavior.Blockentity.MarkDirty();
            }
        }
    }

    public void NowInMotion(float dt)
    {
        accum += dt;
        if (accum < 0.2) return;
        accum = 0;

        if (isMovingDown || isMovingUp)
        {
            if (isMovingUp && !AnimManager.IsAnimationActive("gearturnup"))
            {
                StopAnimation("gearturndown");
                StartAnimation("gearturnup");
            }

            if (isMovingDown && !AnimManager.IsAnimationActive("gearturndown"))
            {
                StopAnimation("gearturnup");
                StartAnimation("gearturndown");
            }
            if (!travelSound.IsPlaying)
            {
                travelSound.Start();
                travelSound.FadeTo(0.15f, 0.5f, null);
            }

            travelSound.SetPosition((float)Pos.X, (float)Pos.InternalY, (float)Pos.Z);
        }
        else
        {
            if (travelSound.IsPlaying)
            {
                travelSound.FadeTo(0, 0.5f, (s) => travelSound.Stop());
            }
        }
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
    {
        var handled = EnumHandling.PassThrough;
        foreach (EntityBehavior behavior in SidedProperties.Behaviors)
        {
            behavior.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
            if (handled == EnumHandling.PreventSubsequent) break;
        }

        int seleBox = (byEntity as EntityPlayer)?.EntitySelection?.SelectionBoxIndex ?? -1;
        var bhs = GetBehavior<EntityBehaviorSelectionBoxes>();
        if (bhs != null && seleBox > 0)
        {
            var apap = bhs.selectionBoxes[seleBox - 1];
            string apname = apap.AttachPoint.Code;

            if (string.Equals(apname, UpAp))
            {
                if (Api is ICoreServerAPI)
                {
                    if (ElevatorSys != null)
                    {
                        var i = Attributes.GetInt("maxHeight", 0);
                        var indexOf = ElevatorSys.ControlPositions.IndexOf(i);
                        var nextStopIndex = Math.Min(CurrentStopIndex + 1, ElevatorSys.ControlPositions.Count - 1);
                        if (indexOf >= nextStopIndex)
                        {
                            CurrentStopIndex = nextStopIndex;
                        }
                    }
                }
                else
                {
                    StartAnimation("leverUP");
                    latchSound.SetPosition((float)Pos.X, (float)Pos.InternalY, (float)Pos.Z);
                    latchSound.Start();
                }
            }
            else if (string.Equals(apname, DownAp))
            {
                if (Api is ICoreServerAPI)
                {
                    if (ElevatorSys != null)
                    {
                        CurrentStopIndex = Math.Max(CurrentStopIndex - 1, 0);
                    }
                }
                else
                {
                    StartAnimation("leverDOWN");
                    latchSound.SetPosition((float)Pos.X, (float)Pos.InternalY, (float)Pos.Z);
                    latchSound.Start();
                }
            }
        }
    }

    public void CallElevator(BlockPos position, int offset)
    {
        var height = position.Y + offset;
        if (Attributes.GetInt("maxHeight", 0) < height)
        {
            Attributes.SetInt("maxHeight", height);
        }
        var indexOf = ElevatorSys?.ControlPositions.IndexOf(height) ?? -1;
        if (indexOf != -1)
        {
            CurrentStopIndex = indexOf;
        }
    }

    public override bool CanCollect(Entity byEntity)
    {
        return false;
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        travelSound?.Dispose(); // will be null on server
        latchSound?.Dispose();
        base.OnEntityDespawn(despawn);
    }

    public override void FromBytes(ref BinarySpanReader reader, bool isSync)
    {
        base.FromBytes(ref reader, isSync);
        CurrentStopIndex = Attributes.GetInt("currentStopIndex");
    }

    public override void ToBytes(BinaryWriter writer, bool forClient)
    {
        Attributes.SetInt("currentStopIndex", CurrentStopIndex);
        base.ToBytes(writer, forClient);
    }

    public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
    {
        int seleBox = player.Entity.EntitySelection?.SelectionBoxIndex ?? -1;
        var bhs = GetBehavior<EntityBehaviorSelectionBoxes>();
        if (bhs != null && seleBox > 0)
        {
            var apap = bhs.selectionBoxes[seleBox - 1];
            string apname = apap.AttachPoint.Code;

            if (string.Equals(apname, UpAp))
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "elevator-leverup",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
            if (string.Equals(apname, DownAp))
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "elevator-leverdown",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
        }

        return base.GetInteractionHelp(world, es, player);
    }

    public Vec3d? GetInteractionHelpPosition()
    {
        var selection = capi.World.Player.CurrentEntitySelection;
        if (selection == null) return null;

        var selebox = selection.SelectionBoxIndex - 1;
        if (selebox < 0) return null;

        var point = selection.Entity.GetBehavior<EntityBehaviorSelectionBoxes>()?.selectionBoxes[selebox].AttachPoint;
        var offset = 0.5;
        if (UpAp.Equals(point?.Code) || DownAp.Equals(point?.Code))
        {
            offset = 0.1;
        }
        return selection.Entity.GetBehavior<EntityBehaviorSelectionBoxes>()?.GetCenterPosOfBox(selebox)?.Add(0, offset, 0);
    }
}
