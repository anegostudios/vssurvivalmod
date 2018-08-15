using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public enum EnumAccumType
    {
        Sum, Max, NoAccum
    }

    class EmotionState
    {
        public string Code = "";
        public float Duration = 0;
        public float Chance = 0;
        public int Slot = 0;
        public float Priority = 0;
        public EnumAccumType AccumType = EnumAccumType.Max;  // sum, max or noaccum

        public string[] NotifyEntityCodes = new string[0];
        public float NotifyChances = 0;
        public float NotifyRange = 0;
    }

    public class EntityBehaviorEmotionStates : EntityBehavior
    {
        Dictionary<string, EmotionState> availableStates = new Dictionary<string, EmotionState>();
        Dictionary<string, float> activeStates = new Dictionary<string, float>();

        TreeAttribute entityAttr;

        int timer = 0;

        public EntityBehaviorEmotionStates(Entity entity) : base(entity)
        {
            try
            {
                if (entity.Attributes.HasAttribute("emotionstates"))
                {
                    entityAttr = (TreeAttribute)entity.Attributes["emotionstates"];

                    foreach (var val in entityAttr)
                    {
                        activeStates[val.Key] = (float)val.Value.GetValue();
                    }

                }
                else
                {
                    entity.Attributes["emotionstates"] = entityAttr = new TreeAttribute();
                }

            } catch
            {
                entity.Attributes["emotionstates"] = entityAttr = new TreeAttribute();
            }
        }


        public override void Initialize(EntityType entityType, JsonObject typeAttributes)
        {
            base.Initialize(entityType, typeAttributes);

            JsonObject[] availStates = typeAttributes["states"].AsArray();
            
            foreach (JsonObject obj in availStates)
            {
                EmotionState state = obj.AsObject<EmotionState>();
                availableStates[state.Code] = state;
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, float damage)
        {
            if (TryTriggerState("aggressiveondamage"))
            {
                if (TryTriggerState("aggressivealarmondamage"))
                {

                }

                return;
            }

            if (TryTriggerState("fleeondamage"))
            {
                if (TryTriggerState("fleealarmondamage"))
                {

                }
                return;
            }

        }



        public bool TryTriggerState(string statecode)
        {
            EmotionState newstate;
            if (!availableStates.TryGetValue(statecode, out newstate)) return false;
            if (entity.World.Rand.NextDouble() > newstate.Chance) return false;

            float activedur = 0;

            foreach (string activestatecode in activeStates.Keys)
            {
                if (activestatecode == statecode)
                {
                    activedur = activeStates[activestatecode];
                    continue;
                }

                EmotionState activestate = availableStates[activestatecode];

                if (activestate.Slot == newstate.Slot)
                {
                    // Active state has priority over this one
                    if (activestate.Priority > newstate.Priority) return false;
                    else
                    {
                        // New state has priority
                        activeStates.Remove(activestatecode);
                        entityAttr.RemoveAttribute(activestatecode);
                        break;
                    }
                }
            }

            float newDuration = 0;
            if (newstate.AccumType == EnumAccumType.Sum) newDuration = activedur + newstate.Duration;
            if (newstate.AccumType == EnumAccumType.Max) newDuration = Math.Max(activedur, newstate.Duration);
            if (newstate.AccumType == EnumAccumType.NoAccum) newDuration = activedur > 0 ? activedur : newstate.Duration;

            activeStates[statecode] = newDuration;
            entityAttr.SetFloat(statecode, newDuration);

            return true;
        }


        public override void OnGameTick(float deltaTime)
        {
            timer++;
            if (timer % 10 != 0) return;

            List<string> active = activeStates.Keys.ToList();

            foreach (string statecode in active) 
            {
                activeStates[statecode] -= 10*deltaTime;
                float leftDur = activeStates[statecode];
                if (leftDur <= 0)
                {
                    activeStates.Remove(statecode);
                    entityAttr.RemoveAttribute(statecode);
                    continue;
                }

                entityAttr.SetFloat(statecode, leftDur);
            }

            if (entity.World.EntityDebugMode) {
                entity.DebugAttributes.SetString("emotionstates", string.Join(", ", active));
            }
        }


        public override string PropertyName()
        {
            return "emotionstates";
        }


        
    }
}
