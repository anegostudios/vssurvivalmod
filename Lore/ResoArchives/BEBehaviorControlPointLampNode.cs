using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public interface INetworkedLight
    {
        void setNetwork(string networkCode);
    }

    public class BEBehaviorControlPointLampNode : BEBehaviorShapeFromAttributes, INetworkedLight
    {
        ModSystemControlPoints modSys;
        protected string networkCode;

        public BEBehaviorControlPointLampNode(BlockEntity blockentity) : base(blockentity)
        {

        }

        public void setNetwork(string networkCode)
        {
            string prevNet = networkCode;
            this.networkCode = networkCode;
            registerToControlPoint(prevNet);
            Blockentity.MarkDirty(true);
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            registerToControlPoint(null);
        }

        void registerToControlPoint(string previousNetwork)
        {
            if (Api.Side != EnumAppSide.Server) return;

            modSys = Api.ModLoader.GetModSystem<ModSystemControlPoints>();

            if (previousNetwork != null)
            {
                modSys[AssetLocation.Create(networkCode, Block.Code.Domain)].Activate -= BEBehaviorControlPointLampNode_Activate;
            }

            if (networkCode == null) return;

            var controlpointcode = AssetLocation.Create(networkCode, Block.Code.Domain);
            modSys[controlpointcode].Activate += BEBehaviorControlPointLampNode_Activate;

            BEBehaviorControlPointLampNode_Activate(modSys[controlpointcode]);
        }

        private void BEBehaviorControlPointLampNode_Activate(ControlPoint cpoint)
        {
            if (cpoint.ControlData == null) return;

            bool on = (bool)cpoint.ControlData;
            string newState = on ? "on/" : "off/";
            string newType = Type.Replace("off/", newState).Replace("on/", newState);

            if (Type != newType)
            {
                var oldType = Type;
                this.Type = newType;
                Blockentity.MarkDirty(true);
                initShape();
                relight(oldType);   // Possible performance issue, this will duplicate relighting on the client because the Blockentity.MarkDirty will also cause a full update client-side
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            networkCode = tree.GetString("networkCode");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("networkCode", networkCode);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Api is ICoreClientAPI capi)
            {
                if (capi.Settings.Bool["extendedDebugInfo"] == true) dsc.AppendLine("network code: " + networkCode);
            }
        }
    }
}
