using HarmonyLib;
using Vintagestory.API.Common;
namespace ImprovedQuenching
{
    public class ImprovedQuenchingModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            var harmony = new Harmony("improvedquenching");
            harmony.PatchAll();
            api.Logger.Event("[ImprovedQuenching] Mod started and patches applied.");
        }
        public override void Dispose()
        {
            new Harmony("improvedquenching").UnpatchAll("improvedquenching");
            base.Dispose();
        }
    }
}