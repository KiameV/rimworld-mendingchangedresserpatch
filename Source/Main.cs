using ChangeDresser;
using Harmony;
using RimWorld;
using System.Reflection;
using Verse;
using Verse.AI;

namespace MendingChangeDresserPatch
{
    [StaticConstructorOnStartup]
    class Main
    {
        static Main()
        {
            var harmony = HarmonyInstance.Create("com.mendingchangedresserpatch.rimworld.mod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message("MendingChangeDresserPatch: Adding Harmony Postfix to Mending.WorkGiver_DoBill.TryFindBestBillIngredients");
        }
    }

    [HarmonyPatch(typeof(Mending.WorkGiver_DoBill), "TryFindBestBillIngredients")]
    static class Pawn_GetGizmos
    {
        public static void Postfix(ref bool __result, Bill bill, Pawn pawn, Thing billGiver, bool ignoreHitPoints, ref Thing chosen)
        {
            if (__result == false)
            {
                foreach (Building_Dresser dresser in WorldComp.DressersToUse)
                {
                    if (dresser.Spawned && dresser.Map == pawn.Map)
                    {
                        foreach (Apparel a in dresser.Apparel)
                        {
                            if ((ignoreHitPoints || a.HitPoints < a.MaxHitPoints && a.HitPoints > 0) &&
                                bill.recipe.fixedIngredientFilter.Allows(a) &&
                                bill.ingredientFilter.Allows(a) &&
                                bill.recipe.ingredients.Any((IngredientCount ingNeed) => ingNeed.filter.Allows(a)) &&
                                pawn.CanReserve(a, 1) &&
                                (!bill.CheckIngredientsIfSociallyProper || a.IsSociallyProper(pawn)))
                            {
                                dresser.Remove(a, false);
                                if (a.Spawned == false)
                                {
                                    Log.Error("Failed to spawn apparel-to-mend [" + a.Label + "] from dresser [" + dresser.Label + "].");
                                    __result = false;
                                    chosen = null;
                                }
                                else
                                {
                                    __result = true;
                                    chosen = a;
                                }
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}