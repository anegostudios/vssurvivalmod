using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockEntityWindmillRotor : BEMPBase
    {
        WeatherSystem weatherSystem;
        double windSpeed;
        double torqueNow;

        int sailLength = 0;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            string orientation = Block.Variant["side"];
            switch (orientation)
            {
                case "north":
                    AxisMapping = new int[] { 0, 1, 2 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;

                case "east":
                    AxisMapping = new int[] { 2, 1, 0 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;

                case "south":
                    AxisMapping = new int[] { 0, 1, 2 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;

                case "west":
                    AxisMapping = new int[] { 2, 1, 0 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;
            }

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(CheckWindSpeed, 1000);

                weatherSystem = api.ModLoader.GetModSystem<WeatherSystem>();
            } else
            {
                updateShape();
            }

        }

        private void CheckWindSpeed(float dt)
        {
            windSpeed = weatherSystem.GetWindSpeed(pos);
        }

        public override float GetResistance()
        {
            return torqueNow - network.Speed <= 0 ? 0.003f : 0;
        }

        public override float GetTorque()
        {
            torqueNow += (windSpeed - torqueNow) / 20.0;

            return Math.Max(0, (float)torqueNow - network.Speed) * sailLength / 3f;
        }

        public override void OnBlockBroken()
        {
            if (sailLength > 0)
            {
                ItemStack stacks = new ItemStack(api.World.GetItem(new AssetLocation("sail")), sailLength * 4);
                api.World.SpawnItemEntity(stacks, pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            base.OnBlockBroken();
        }

        public override EnumTurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            return EnumTurnDirection.Clockwise;
        }

        internal bool OnInteract(IPlayer byPlayer)
        {
            if (sailLength >= 3) return false;

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack sailStack = new ItemStack(api.World.GetItem(new AssetLocation("sail")));
            if (slot.Empty || !slot.Itemstack.Equals(api.World, sailStack)) return false;

            if (slot.StackSize < 4) return false;

            slot.TakeOut(4);
            sailLength++;
            updateShape();
            return true;
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            sailLength = tree.GetInt("sailLength");
            
            if (worldAccessForResolve.Side == EnumAppSide.Client && sailLength > 0 && Block != null)
            {
                updateShape();
            }

            base.FromTreeAtributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetInt("sailLength", sailLength);
            base.ToTreeAttributes(tree);
        }


        void updateShape()
        {
            Shape = new CompositeShape()
            {
                Base = new AssetLocation("block/wood/windmill-" + sailLength + "blade"),
                rotateY = Block.Shape.rotateY
            };
            manager.RemoveDeviceForRender(this);
            manager.AddDeviceForRender(this);
        }
    }
}
