using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BEBehaviorJonasBoilerDoor : BlockEntityBehavior
    {
        ModSystemControlPoints modSys;
        AnimationMetaData animData;
        ControlPoint cp;

        bool on;
        float heatAccum;

        public BEBehaviorJonasBoilerDoor(BlockEntity blockentity) : base(blockentity)
        {

        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            api.Event.OnTestBlockAccess += Event_OnTestBlockAccess;

            if (api.Side == EnumAppSide.Server)
            {
                Blockentity.RegisterGameTickListener(checkFireServer, 1000, 12);
            }

            animData = properties["animData"].AsObject<AnimationMetaData>();

            var controlpointcode = AssetLocation.Create(properties["controlpointcode"].ToString(), Block.Code.Domain);

            modSys = api.ModLoader.GetModSystem<ModSystemControlPoints>();
            cp = modSys[controlpointcode];
            cp.ControlData = animData;

            animData.AnimationSpeed = on ? 1 : 0;
            cp.Trigger();   
        }

        private EnumWorldAccessResponse Event_OnTestBlockAccess(IPlayer player, BlockSelection blockSel, EnumBlockAccessFlags accessType, string claimant, EnumWorldAccessResponse response)
        {
            var facing = BlockFacing.FromCode(this.Block.Variant["side"]);
            var a = Pos.AddCopy(facing);
            var b = blockSel.Position.UpCopy();
            if ((a == b || a == blockSel.Position) && player.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible is ItemCoal)
            {
                return EnumWorldAccessResponse.Granted;
            }

            return response;
        }

        private void checkFireServer(float dt)
        {
            var facing = BlockFacing.FromCode(this.Block.Variant["side"]);
            var be = Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(facing));

            if (be is BlockEntityCoalPile bec && bec.IsBurning)
            {
                heatAccum = Math.Min(10, heatAccum + dt);
            }
            else
            {
                heatAccum = Math.Max(0, heatAccum - dt);
            }

            if (!on && heatAccum >= 9.9f)
            {
                on = true;
                animData.AnimationSpeed = on ? 1 : 0;
                cp.Trigger();
                Blockentity.MarkDirty(true);
                return;
            }
            
            if (on && heatAccum <= 0)
            {
                on = false;
                animData.AnimationSpeed = on ? 1 : 0;
                cp.Trigger();
                Blockentity.MarkDirty(true);
                return;
            }
        }

        internal void Interact(IPlayer byPlayer, BlockSelection blockSel)
        {
         //   on = !on;
         //   animData.AnimationSpeed = on ? 1 : 0;
          //  cp.Trigger();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            on = tree.GetBool("on");
            heatAccum = tree.GetFloat("heatAccum");

            if (Api != null && worldAccessForResolve.Side == EnumAppSide.Client)
            {
                animData.AnimationSpeed = on ? 1 : 0;
                cp.Trigger();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("on", on);
            tree.SetFloat("heatAccum", heatAccum);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Api is ICoreClientAPI capi)
            {
                if (capi.Settings.Bool["extendedDebugInfo"] == true)
                {
                    dsc.AppendLine("animspeed: " + animData.AnimationSpeed);
                }
            }
        }
    }
}
