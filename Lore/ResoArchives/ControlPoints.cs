using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ControlPoint
    {
        public AssetLocation Code;
        public object ControlData;
        public event Action<ControlPoint> Activate;

        public void Trigger()
        {
            Activate?.Invoke(this);
        }
    }

    public class ControlPointListener
    {
        public AssetLocation Code;
        public Action Activate;
    }

    public class ModSystemControlPoints : ModSystem
    {
        protected Dictionary<AssetLocation, ControlPoint> controlPoints = new Dictionary<AssetLocation, ControlPoint>();

        public override bool ShouldLoad(EnumAppSide side) => true;
        public override double ExecuteOrder() => 0;

        public ControlPoint this[AssetLocation code]
        {
            get
            {
                if (!controlPoints.TryGetValue(code, out var cpoint))
                {
                    cpoint = controlPoints[code] = new ControlPoint();
                }
                return cpoint;
            }
            set
            {
                controlPoints[code] = value;
            }
        }
    }
}
