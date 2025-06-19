using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public record GaitMeta
    {
        public string Code { get; set; } // Unique identifier for the gait, ideally matched with rideable controls
        public float TurnRadius { get; set; } = 3.5f;
        public float MoveSpeed { get; set; } = 0f;
        public bool Backwards { get; set; } = false;
        public float StaminaCost { get; set; } = 0f;
        public string FallbackGaitCode { get; set; } // Gait to slow down to such as when fatiguing
        public bool IsSprint { get; set; } // Used to toggle entity.Controls.Sprint from rideable, consider alternatives?
        public AssetLocation Sound { get; set; }
    }

    public class EntityBehaviorGait : EntityBehavior
    {
        public override string PropertyName()
        {
            return "gait";
        }

        public readonly FastSmallDictionary<string, GaitMeta> Gaits = new(1);
        public GaitMeta CurrentGait
        {
            get => Gaits[entity.WatchedAttributes.GetString("currentgait")];
            set => entity.WatchedAttributes.SetString("currentgait", value.Code);
        }

        public GaitMeta IdleGait;
        public GaitMeta FallbackGait => CurrentGait.FallbackGaitCode is null ? IdleGait : Gaits[CurrentGait.FallbackGaitCode];

        public float GetTurnRadius() => CurrentGait?.TurnRadius ?? 3.5f; // Default turn radius if not set

        public void SetIdle() => CurrentGait = IdleGait;
        public bool IsIdle => CurrentGait == IdleGait;
        public bool IsBackward => CurrentGait.Backwards;
        public bool IsForward => !CurrentGait.Backwards && CurrentGait != IdleGait;        
        public GaitMeta CascadingFallbackGait(int n)
        {
            var result = CurrentGait;

            while (n > 0)
            {
                if (result.FallbackGaitCode is null) return IdleGait;
                result = Gaits[result.FallbackGaitCode];
                n--;
            }

            return result;
        }

        protected ICoreAPI api;
        protected ICoreClientAPI capi;

        public EntityBehaviorGait(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            api = entity.Api;
            capi = api as ICoreClientAPI;

            GaitMeta[] gaitarray = attributes["gaits"].AsArray<GaitMeta>();
            foreach (GaitMeta gait in gaitarray)
            {
                Gaits[gait.Code] = gait;
            }

            string idleGaitCode = attributes["idleGait"].AsString("idle");
            IdleGait = Gaits[idleGaitCode];
            CurrentGait = IdleGait; // Set initial gait to Idle
        }
    }
}
