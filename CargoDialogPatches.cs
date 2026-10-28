using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;

namespace PocketMapShuttleLaunchFix
{
    [HarmonyPatch(typeof(Dialog_LoadTransporters), "CalculateAndRecacheTransferables")]
    internal static class Patch_LoadTransporters_CalculateAndRecacheTransferables
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return TranspilerTools.ReplaceCalls(
                instructions,
                TranspilerTools.MapTileGetter,
                TranspilerTools.MapOriginHelper,
                2,
                "Dialog_LoadTransporters.CalculateAndRecacheTransferables");
        }
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters), "DoWindowContents")]
    internal static class Patch_LoadTransporters_DoWindowContents
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return TranspilerTools.ReplaceCalls(
                instructions,
                TranspilerTools.MapTileGetter,
                TranspilerTools.MapOriginHelper,
                2,
                "Dialog_LoadTransporters.DoWindowContents");
        }
    }

    [HarmonyPatch(typeof(TransferableOneWayWidget), "DrawDaysUntilRot")]
    internal static class Patch_TransferableOneWayWidget_DrawDaysUntilRot
    {
        private static bool Prefix(PlanetTile ___tile)
        {
            if (___tile.Valid)
            {
                return true;
            }

            ModBootstrap.DebugLog("Skipped days-until-rot rendering because the cargo widget has no valid world tile.");
            return false;
        }
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters), "get_TilesPerDay")]
    internal static class Patch_LoadTransporters_TilesPerDay
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return TranspilerTools.ReplaceCalls(
                instructions,
                TranspilerTools.MapTileGetter,
                TranspilerTools.MapOriginHelper,
                3,
                "Dialog_LoadTransporters.get_TilesPerDay");
        }
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters), "get_DaysWorthOfFood")]
    internal static class Patch_LoadTransporters_DaysWorthOfFood
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return TranspilerTools.ReplaceCalls(
                instructions,
                TranspilerTools.MapTileGetter,
                TranspilerTools.MapOriginHelper,
                2,
                "Dialog_LoadTransporters.get_DaysWorthOfFood");
        }
    }
}
