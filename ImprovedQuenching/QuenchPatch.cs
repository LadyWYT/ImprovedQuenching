using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ImprovedQuenching
{
    public class QuenchedState
    {
        public bool wasClayQuenched;
    }

    [HarmonyPatch(typeof(CollectibleBehaviorQuenchable))]
    [HarmonyPatch("applyQuenchedStats")]
    public static class QuenchPatch
    {
        //Runs before vanilla's quenching methods and should cancel the quench if it returns false
        public static bool Prefix(CollectibleBehaviorQuenchable __instance, 
            IWorldAccessor world,
            ItemStack itemstack,
            ref QuenchedState __state)
        {
            __state = new QuenchedState();
            int timesQuenched =itemstack.Attributes.GetInt("quenchIteration");
            int timesTempered = itemstack.Attributes.GetInt("temperIteration");
            //Check to see if item has been quenched or tempered and cancel if yes
            if (timesQuenched >= 1 || timesTempered >= 1)
            {
                return false; //This tells the game not to run the quench
            }
            //Now we read the maximum recorded temperature during the cooling process
            float peakTemp =itemstack.Attributes.GetFloat("improvedquenching:peakQuenchTemp", 0f);
            //Temperature brackets for the quenching bonus
            //Low: 815-925 for a modest bonus
            //Medium: 925-975 for a good bonus
            //High: 975+ for maximum bonus with risk of shattering the item due to the stress
            float bonusMultiplier;
            if (peakTemp >= 975f)
            {
                bonusMultiplier = 5.0f;
                //float currentShatter = itemstack.Attributes.GetFloat("shatterchance", 0.05f);
                //itemstack.Attributes.SetFloat("shatterchance", currentShatter + 0.15f);
            }
            else if (peakTemp >= 925f)
            {
                bonusMultiplier = 2.5f;
                //itemstack.Attributes.SetFloat("shatterchance", 0f);
            }
            else
            {
                bonusMultiplier = 1.0f;
                //itemstack.Attributes.SetFloat("shatterchance", 0f);
            }
            //Store the multiplier so it's usable after the game applies the appropriate buff
            itemstack.Attributes.SetFloat("improvedquenching:bonusMultiplier", bonusMultiplier);
            //Check to see if the item is clay covered and apply the appropriate bonuses and SKIP the original method
            if (itemstack.Attributes.GetBool("clayCovered"))
            {
                float clayBonusPower = 0.05f; //25% bonus on clay covered items -- change as needed
                float clayBonusDuration = 0.04f; //25% bonus on clay covered items -- change as needed
                float currentPower = __instance.GetPowerValue(world, itemstack);
                __instance.SetPowerValue(world, itemstack, currentPower + clayBonusPower);
                float currentDuration = __instance.GetDurationBonus(world, itemstack);
                __instance.SetDurationBonus(world, itemstack, currentDuration + clayBonusDuration);
                itemstack.Attributes.SetInt("quenchIteration", 1);
                itemstack.Attributes.SetBool("clayCovered", false);
                var buffable = __instance.collObj.GetBehavior<CollectibleBehaviorBuffable>();
                if (buffable != null)
                {
                    float newDuration = __instance.GetDurationBonus(world, itemstack);
                    float newPower = __instance.GetPowerValue(world, itemstack);
                    float percentBroken = (float)__instance.collObj.GetRemainingDurability(itemstack)
                                         / (float)__instance.collObj.GetMaxDurability(itemstack);

                    buffable.ApplyBuffs(itemstack, new List<AppliedCollectibleBuff>
                    {
                        new AppliedCollectibleBuff
                        {
                            Code       = "hardened",
                            Multiplier = 1f + newDuration,
                            StatCode   = "maxdurability"
                        }
                    }, EnumBuffAddType.ReplaceOnDuplicate);

                                    buffable.ApplyBuffs(itemstack, new List<AppliedCollectibleBuff>
                    {
                        new AppliedCollectibleBuff
                        {
                            Code       = "hardened",
                            Multiplier = 1f + newPower,
                            StatCode   = "attackpower"
                        }
                    }, EnumBuffAddType.ReplaceOnDuplicate);

                                    buffable.ApplyBuffs(itemstack, new List<AppliedCollectibleBuff>
                    {
                        new AppliedCollectibleBuff
                        {
                            Code       = "hardened",
                            Multiplier = 1f + newPower,
                            StatCode   = "miningspeed"
                        }
                    }, EnumBuffAddType.ReplaceOnDuplicate);

                    // Rescale remaining durability to the new max (same as vanilla does)
                    __instance.collObj.SetDurability(itemstack,
                        (int)(percentBroken * (float)__instance.collObj.GetMaxDurability(itemstack)));
                }
                return false; //Setting this to false means the game does NOT run the original method.
            }
            return true; //Setting this to true should mean the game runs the original method as well
        }
        //This part runs after the vanilla method
        //This part is supposed to scale the power value added by the above
        public static void Postfix(CollectibleBehaviorQuenchable __instance,
            IWorldAccessor world,
            ItemStack itemstack,
            QuenchedState __state)
        {
            if (__state.wasClayQuenched)
            {
                itemstack.Attributes.RemoveAttribute("improvedquenching:bonusMultiplier");
                return;
            }
            float multiplier = itemstack.Attributes.GetFloat("improvedquenching:bonusMultiplier", 1f);
            if (multiplier != 1f)
            {
                float currentPower = __instance.GetPowerValue(world, itemstack);
                __instance.SetPowerValue(world, itemstack, currentPower * multiplier);
                float currentDuration = __instance.GetDurationBonus(world, itemstack);
                __instance.SetDurationBonus(world, itemstack, currentDuration * multiplier);
                var buffable = __instance.collObj.GetBehavior<CollectibleBehaviorBuffable>();
                if (buffable != null)
                {
                    float newPower = __instance.GetPowerValue(world, itemstack);
                    buffable.ApplyBuffs(itemstack, new List<AppliedCollectibleBuff>
                    {
                        new AppliedCollectibleBuff
                        {
                            Code       = "hardened",
                            Multiplier = 1f + newPower,
                            StatCode   = "attackpower"
                        },
                        new AppliedCollectibleBuff
                        {
                            Code       = "hardened",
                            Multiplier = 1f + newPower,
                            StatCode   = "miningspeed"
                        }
                    }, EnumBuffAddType.ReplaceOnDuplicate);
                }
            }
            //Clean up the temp attribute
            itemstack.Attributes.RemoveAttribute("improvedquenching:bonusMultiplier");
            }        

        }
    [HarmonyPatch(typeof(CollectibleBehaviorQuenchable))]
    [HarmonyPatch("IsGettingCooled")]
    public static class QuenchTempTrackerPatch
    {
        //Should run each tick while item is cooling down
        //Records highest temperature during quench for the above method
        public static void Prefix(CollectibleBehaviorQuenchable __instance,
            IWorldAccessor world, ItemSlot slot, Vec3d pos, float dt, float temperature)
        {
            if (slot?.Itemstack == null) return;
            float storedPeak = slot.Itemstack.Attributes.GetFloat("improvedquenching:peakQuenchTemp", 0f);
            if (temperature > storedPeak)
            {
                slot.Itemstack.Attributes.SetFloat("improvedquenching:peakQuenchTemp", temperature);
            }
        }
    }
    [HarmonyPatch(typeof(CollectibleBehaviorQuenchable))]
    [HarmonyPatch("applyTemperedStats")]
    public static class TemperPatch
    {
        //This does like the Quench Patch, but for Tempering
        public static bool Prefix(CollectibleBehaviorQuenchable __instance,
            IWorldAccessor world, ItemStack itemstack)
        {
            //Get tempered iteration from itemstack
            int temperIteration = itemstack.Attributes.GetInt("temperIteration");
            int quenchIteration = itemstack.Attributes.GetInt("quenchIteration");

            // Gate: only allow tempering once
            if (temperIteration >= 1 || quenchIteration >= 1)
            {
                return false; // skip original, do nothing
            }

            // Pick a durability bonus amount (tune this number to taste)
            float durabilityBonus = 0.4f; // +40% max durability

            float current = __instance.GetDurationBonus(world, itemstack);
            __instance.SetDurationBonus(world, itemstack, current + durabilityBonus);

            // Mark as tempered so this can't fire again
            itemstack.Attributes.SetInt("temperIteration", temperIteration + 1);

            var buffable = __instance.collObj.GetBehavior<CollectibleBehaviorBuffable>();
            if (buffable != null)
            {
                float newDuration = __instance.GetDurationBonus(world, itemstack);
                float percentBroken = (float)__instance.collObj.GetRemainingDurability(itemstack)
                                    / (float)__instance.collObj.GetMaxDurability(itemstack);

                buffable.ApplyBuffs(itemstack, new List<AppliedCollectibleBuff>
                {
                    new AppliedCollectibleBuff
                    {
                        Code       = "hardened",
                        Multiplier = 1f + newDuration,
                        StatCode   = "maxdurability"
                    }
                }, EnumBuffAddType.ReplaceOnDuplicate);

                __instance.collObj.SetDurability(itemstack,
                    (int)(percentBroken * (float)__instance.collObj.GetMaxDurability(itemstack)));
            }

            return false; // never run the original method... EVER
        }
    }

    [HarmonyPatch(typeof(CollectibleBehaviorQuenchable))]
    [HarmonyPatch("SetTemperature")]
    public static class QuenchShatterPreviewPatch
    {
        public static void Postfix(CollectibleBehaviorQuenchable __instance,
            IWorldAccessor world, ItemStack itemstack, float temperature)
        {
            if (itemstack == null) return;

            int quenchIteration = itemstack.Attributes.GetInt("quenchIteration");
            int temperIteration = itemstack.Attributes.GetInt("temperIteration");

            // If the item has already been heat-treated, applyQuenchedStats owns shatterchance.
            if (quenchIteration >= 1 || temperIteration >= 1) return;

            string state = __instance.GetState(itemstack);

            if (state == "quench")
            {
                // Track peak temp during forge heating (cooling ticks are tracked by QuenchTempTrackerPatch).
                float peakTemp = itemstack.Attributes.GetFloat("improvedquenching:peakQuenchTemp", 0f);
                if (temperature > peakTemp)
                {
                    peakTemp = temperature;
                    itemstack.Attributes.SetFloat("improvedquenching:peakQuenchTemp", temperature);
                }
                //sets shatter chance according to temperature
                float newShatter = peakTemp >= 975f ? 0.20f : 0f;
                __instance.SetShatterChance(world, itemstack, newShatter);
            }
            else if (state == "settled")
            {
                // Item settled without completing a quench — clear the preview so the
                // tooltip falls back to vanilla's default BreakChancePerQuench (5%).
                itemstack.Attributes.RemoveAttribute("shatterchance");
                itemstack.Attributes.RemoveAttribute("improvedquenching:peakQuenchTemp");
            }
            else if (state == "overheat")
            {
                // Item is overheated. Quenching it now will shatter it instantly. Whoops!
                __instance.SetShatterChance(world, itemstack, 1f);
            }
        }
    }
}