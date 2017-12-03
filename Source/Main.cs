using ChangeDresser;
using Harmony;
using RimWorld;
using System.Collections.Generic;
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

            Log.Message("MendingChangeDresserPatch: Adding Harmony Postfix to WorkGiver_DoBill.TryFindBestBillIngredients");
            Log.Message("MendingChangeDresserPatch: Adding Harmony Postfix to Game.Game_FinalizeInit");
        }

        private static LinkedList<Building_Dresser> changeDressers = null;
        private static bool initialized = false;
        public static IEnumerable<Building_Dresser> GetChangeDressers()
        {
            if (!initialized)
            {
                bool mendingFound = false;
                foreach (ModContentPack pack in LoadedModManager.RunningMods)
                {
                    foreach (Assembly assembly in pack.assemblies.loadedAssemblies)
                    {
                        if (!mendingFound &&
                            assembly.GetName().Name.Equals("Mending"))
                        {
                            mendingFound = true;
                        }
                        else if (
                            changeDressers == null &&
                            assembly.GetName().Name.Equals("ChangeDresser"))
                        {
                            System.Type type = assembly.GetType("ChangeDresser.WorldComp");
                            PropertyInfo fi = type?.GetProperty("DressersToUse", BindingFlags.Public | BindingFlags.Static);
                            changeDressers = (LinkedList<Building_Dresser>)fi?.GetValue(null, null);
                        }
                    }
                    if (mendingFound && changeDressers != null)
                    {
                        break;
                    }
                }
                if (!mendingFound)
                {
                    Log.Error("Failed to initialize MendingChangeDresserPatch, mending not found.");
                }
                else if (changeDressers == null)
                {
                    Log.Error("Failed to initialize MendingChangeDresserPatch, changeDressers could not be initialized.");
                }
                initialized = true;
            }
            return changeDressers;
        }
    }

    [HarmonyPatch(typeof(Verse.Game), "FinalizeInit")]
    static class Patch_Game_FinalizeInit
    {
        static void Postfix()
        {
            Main.GetChangeDressers();
        }
    }

    [HarmonyPatch(typeof(Mending.WorkGiver_DoBill), "TryFindBestBillIngredients")]
    static class Patch_WorkGiver_DoBill_TryFindBestBillIngredients
    {
        static void Postfix(ref bool __result, Bill bill, Pawn pawn, Thing billGiver, bool ignoreHitPoints, ref Thing chosen)
        {
            if (__result == false)
            {
                IEnumerable<Building_Dresser> dressers = Main.GetChangeDressers();
                if (dressers == null)
                {
                    Log.Warning("MendingChangeDresserPatch failed to retrieve ChangeDressers");
                    return;
                }

                foreach (Building_Dresser dresser in dressers)
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