using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public class ElevatorSystem
{
    public EntityElevator Entity;
    public List<int> ControlPositions = new();
}

public class EntityElevator : Entity, ISeatInstSupplier, IMountableListener, ICustomInteractionHelpPositioning
{
    public override double FrustumSphereRadius => base.FrustumSphereRadius * 2;
    public override bool IsCreature => true; // For RepulseAgents behavior to work

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

    public Dictionary<string, string> MountAnimations = new Dictionary<string, string>();

    ICoreClientAPI capi;
    ICoreServerAPI sapi;

    public ILoadedSound travelSound;
    public ILoadedSound latchSound;

    float accum;

    private bool isMovingUp, isMovingDown;
    private int colliderBlockId;

    private int lastStopIndex;

    public ElevatorSystem ElevatorSys;

    private int CurrentStopIndex;

    public string NetworkCode
    {
        get => Attributes.GetString("networkCode");
        set => Attributes.SetString("networkCode", value);
    }

    public bool IsActivated;

    public bool IsMoving => isMovingUp || isMovingDown;

    private const string UpAp = "UpAP";
    private const string DownAp = "DownAP";

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        swimmingOffsetY = properties.Attributes["swimmingOffsetY"].AsDouble();
        MountAnimations = properties.Attributes["mountAnimations"].AsObject<Dictionary<string, string>>();

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
            sapi = serverApi;

            var elevatorModSystem = sapi.ModLoader.GetModSystem<ModsystemElevator>();
            if (NetworkCode != null)
            {
                ElevatorSys = elevatorModSystem.RegisterElevator(NetworkCode, this);
            }

            colliderBlockId = sapi.World.BlockAccessor.GetBlock("meta-collider").Id;
        }
    }

    public override void OnGameTick(float dt)
    {
        if (IsActivated)
        {
            if (World.Side == EnumAppSide.Server)
            {
                updatePosition(dt);
            }
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

        var diff = Math.Abs(ServerPos.Y - elevatorStopHeight);
        if (diff >= 0.02f)
        {
            if (!IsMoving)
            {
                UnSetGround(ServerPos.AsBlockPos, ElevatorSys.ControlPositions[lastStopIndex]);
            }

            var mul = Math.Max(0.5f, Math.Clamp(diff, 0, 1));
            if (ServerPos.Y < elevatorStopHeight)
            {
                ServerPos.Y += dt * SpeedMultiplier * mul;
                isMovingUp = true;
            }
            else
            {
                ServerPos.Y -= dt * SpeedMultiplier * mul;
                isMovingDown = true;
            }

        }
        else
        {
            if (IsMoving)
            {
                lastStopIndex = CurrentStopIndex;
                SetGround(ServerPos.AsBlockPos, elevatorStopHeight);
            }

            isMovingUp = isMovingDown = false;
        }

        WatchedAttributes.SetBool("isMovingUp", isMovingUp);
        WatchedAttributes.SetBool("isMovingDown", isMovingDown);
    }

    private void SetGround(BlockPos pos, int elevatorStopHeight)
    {
        var tmpPos = pos.Copy();

        tmpPos.Y = elevatorStopHeight;
        for (int x = -1; x < 2; x++)
        {
            for (int z = -1; z < 2; z++)
            {
                tmpPos.Set(pos.X + x, elevatorStopHeight, pos.Z + z);
                MakeGroundSolid(tmpPos);
            }
        }
    }

    private void MakeGroundSolid(BlockPos tmpPos)
    {
        Block block;
        block = sapi.World.BlockAccessor.GetBlock(tmpPos);
        if (block.Id == 0)
            sapi.World.BlockAccessor.SetBlock(colliderBlockId, tmpPos);
        else if (block is BlockToggleCollisionBox)
        {
            var blockEntity = sapi.World.BlockAccessor.GetBlockEntity<BlockEntityGeneric>(tmpPos);
            var brtcb = blockEntity.GetBehavior<BEBehaviorToggleCollisionBox>();
            brtcb.Solid = true;
            blockEntity.MarkDirty();
        }
    }

    private void MakeGroundAir(BlockPos tmpPos)
    {
        Block block;
        block = sapi.World.BlockAccessor.GetBlock(tmpPos);
        if (block.Id == colliderBlockId)
            sapi.World.BlockAccessor.SetBlock(0, tmpPos);
        else if (block is BlockToggleCollisionBox)
        {
            var blockEntity = sapi.World.BlockAccessor.GetBlockEntity<BlockEntityGeneric>(tmpPos);
            var brtcb = blockEntity.GetBehavior<BEBehaviorToggleCollisionBox>();
            brtcb.Solid = false;
            blockEntity.MarkDirty();
        }
    }

    private void UnSetGround(BlockPos pos, int elevatorStopHeight)
    {
        var tmpPos = pos.Copy();
        tmpPos.Y = elevatorStopHeight;
        for (int x = -1; x < 2; x++)
        {
            for (int z = -1; z < 2; z++)
            {
                tmpPos.Set(pos.X + x, elevatorStopHeight, pos.Z + z);
                MakeGroundAir(tmpPos);
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

            travelSound.SetPosition((float)SidedPos.X, (float)SidedPos.InternalY, (float)SidedPos.Z);
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
        var bhse = GetBehavior<EntityBehaviorSeatable>();
        if (bhs != null && seleBox > 0 && bhse?.Seats.Any(s => s.Passenger?.EntityId == byEntity.EntityId) == true)
        {
            var apap = bhs.selectionBoxes[seleBox - 1];
            string apname = apap.AttachPoint.Code;

            if (string.Equals(apname, UpAp))
            {
                if (Api is ICoreServerAPI)
                {
                    if (ElevatorSys != null && IsActivated)
                    {
                        CurrentStopIndex = Math.Min(CurrentStopIndex + 1, ElevatorSys.ControlPositions.Count - 1);
                    }
                }
                else
                {
                    StartAnimation("leverUP");
                    latchSound.SetPosition((float)SidedPos.X, (float)SidedPos.InternalY, (float)SidedPos.Z);
                    latchSound.Start();
                }
            }
            else if (string.Equals(apname, DownAp))
            {
                if (Api is ICoreServerAPI)
                {
                    if (ElevatorSys != null && IsActivated)
                    {
                        CurrentStopIndex = Math.Max(CurrentStopIndex - 1, 0);
                    }
                }
                else
                {
                    StartAnimation("leverDOWN");
                    latchSound.SetPosition((float)SidedPos.X, (float)SidedPos.InternalY, (float)SidedPos.Z);
                    latchSound.Start();
                }
            }
        }
    }

    public void CallElevator(BlockPos position, int offset)
    {
        if (!IsActivated) return;
        var indexOf = ElevatorSys.ControlPositions.IndexOf(position.Y + offset);
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
        travelSound?.Dispose();
        base.OnEntityDespawn(despawn);
    }

    public IMountableSeat CreateSeat(IMountable mountable, string seatId, SeatConfig config)
    {
        return new EntityElevatorSeat(mountable, seatId, config);
    }

    public void DidUnnmount(EntityAgent entityAgent)
    {
        MarkShapeModified();
    }

    public void DidMount(EntityAgent entityAgent)
    {
        MarkShapeModified();
    }
    public override void FromBytes(BinaryReader reader, bool isSync)
    {
        base.FromBytes(reader, isSync);
        CurrentStopIndex = Attributes.GetInt("currentStopIndex");
        lastStopIndex = CurrentStopIndex;
        IsActivated = Attributes.GetBool("isActivated");
    }

    public override void ToBytes(BinaryWriter writer, bool forClient)
    {
        Attributes.SetInt("currentStopIndex", CurrentStopIndex);
        Attributes.SetBool("isActivated", IsActivated);
        base.ToBytes(writer, forClient);
    }

    public void DeActivateElevator()
    {
        IsActivated = false;
        Attributes.SetBool("isActivated", IsActivated);
    }

    public void ActivateElevator(BlockPos position, int offset)
    {
        if(IsActivated) return;

        IsActivated = true;
        Attributes.SetBool("isActivated", IsActivated);

        var indexOf = ElevatorSys.ControlPositions.IndexOf(position.Y + offset);
        if (indexOf != -1)
        {
            CurrentStopIndex = indexOf;
            lastStopIndex = indexOf;
        }
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

    public Vec3d GetInteractionHelpPosition()
    {
        var capi = Api as ICoreClientAPI;
        if (capi.World.Player.CurrentEntitySelection == null) return null;

        var selebox = capi.World.Player.CurrentEntitySelection.SelectionBoxIndex - 1;
        if (selebox < 0) return null;

        var point = GetBehavior<EntityBehaviorSelectionBoxes>().selectionBoxes[selebox].AttachPoint;
        var offset = 0.5;
        if (point.Code.Equals(UpAp) || point.Code.Equals(DownAp))
        {
            offset = 0.1;
        }
        return GetBehavior<EntityBehaviorSelectionBoxes>().GetCenterPosOfBox(selebox)?.Add(0, offset, 0);
    }
}
