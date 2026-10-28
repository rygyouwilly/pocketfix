using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PocketMapShuttleLaunchFix
{
    internal static class PatchTargets
    {
        internal static readonly MethodInfo StartChoosingDestination = AccessTools.Method(
            typeof(CompLaunchable),
            "StartChoosingDestination",
            new Type[] { typeof(Action<PlanetTile, TransportersArrivalAction>), typeof(float?) });

        internal static readonly MethodInfo ChoseWorldTarget = AccessTools.Method(
            typeof(CompLaunchable),
            "ChoseWorldTarget",
            new Type[] { typeof(GlobalTargetInfo), typeof(Action<PlanetTile, TransportersArrivalAction>), typeof(float?) });

        internal static readonly MethodInfo TryLaunch = AccessTools.Method(
            typeof(CompLaunchable),
            "TryLaunch",
            new Type[] { typeof(PlanetTile), typeof(TransportersArrivalAction) });

        internal static readonly MethodInfo MaxLaunchDistanceAtFuelLevel = AccessTools.Method(
            typeof(CompLaunchable),
            "MaxLaunchDistanceAtFuelLevel",
            new Type[] { typeof(float) });

        internal static readonly MethodInfo FlyShipLeavingLeaveMap = AccessTools.Method(typeof(FlyShipLeaving), "LeaveMap");

        internal static readonly MethodInfo LoadTransportersRecache = AccessTools.Method(
            typeof(Dialog_LoadTransporters),
            "CalculateAndRecacheTransferables");

        internal static readonly MethodInfo LoadTransportersDraw = AccessTools.Method(
            typeof(Dialog_LoadTransporters),
            "DoWindowContents");

        internal static readonly MethodInfo DrawDaysUntilRot = AccessTools.Method(
            typeof(TransferableOneWayWidget),
            "DrawDaysUntilRot");

        internal static readonly MethodInfo LoadTransportersTilesPerDay = AccessTools.PropertyGetter(
            typeof(Dialog_LoadTransporters),
            "TilesPerDay");

        internal static readonly MethodInfo LoadTransportersDaysWorthOfFood = AccessTools.PropertyGetter(
            typeof(Dialog_LoadTransporters),
            "DaysWorthOfFood");

        internal static readonly MethodInfo GetTransportersFloatMenuOptionsAt = AccessTools.Method(
            typeof(CompLaunchable),
            "GetTransportersFloatMenuOptionsAt");

        internal static readonly MethodInfo CanLandInSpecificCell = AccessTools.Method(
            typeof(TransportersArrivalAction_LandInSpecificCell),
            "CanLandInSpecificCell");

        internal static readonly MethodInfo ShipJobArriveTryStart = AccessTools.Method(
            typeof(ShipJob_Arrive),
            "TryStart");

        internal static readonly MethodBase[] All =
        {
            StartChoosingDestination,
            ChoseWorldTarget,
            TryLaunch,
            MaxLaunchDistanceAtFuelLevel,
            FlyShipLeavingLeaveMap,
            LoadTransportersRecache,
            LoadTransportersDraw,
            DrawDaysUntilRot,
            LoadTransportersTilesPerDay,
            LoadTransportersDaysWorthOfFood,
            GetTransportersFloatMenuOptionsAt,
            CanLandInSpecificCell,
            ShipJobArriveTryStart
        };

        internal static string Describe(MethodBase method)
        {
            return method == null ? "<missing method>" : method.DeclaringType.FullName + "." + method.Name;
        }
    }

    internal static class TranspilerTools
    {
        internal static readonly MethodInfo ThingTileGetter = AccessTools.PropertyGetter(typeof(Thing), "Tile");
        internal static readonly MethodInfo MapTileGetter = AccessTools.PropertyGetter(typeof(Map), "Tile");
        internal static readonly MethodInfo ThingOriginHelper = AccessTools.Method(typeof(PocketMapOriginResolver), "OriginForThingOrVanilla");
        internal static readonly MethodInfo MapOriginHelper = AccessTools.Method(typeof(PocketMapOriginResolver), "OriginForMapOrVanilla");
        internal static readonly MethodInfo SpawnMethod = AccessTools.Method(
            typeof(GenSpawn),
            "Spawn",
            new Type[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(WipeMode) });
        internal static readonly MethodInfo SpawnHelper = AccessTools.Method(typeof(PocketMapOriginResolver), "SpawnAndRememberOrigin");
        internal static readonly MethodInfo ThingMapGetter = AccessTools.PropertyGetter(typeof(Thing), "Map");
        internal static readonly MethodInfo LeavingOriginHelper = AccessTools.Method(typeof(PocketMapOriginResolver), "OriginForLeavingShip");

        internal static IEnumerable<CodeInstruction> ReplaceCalls(
            IEnumerable<CodeInstruction> instructions,
            MethodInfo from,
            MethodInfo to,
            int expected,
            string patchName)
        {
            List<CodeInstruction> list = instructions.ToList();
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(from))
                {
                    list[i].opcode = OpCodes.Call;
                    list[i].operand = to;
                    count++;
                }
            }

            RequireCount(patchName, from.Name, count, expected);
            return list;
        }

        internal static void RequireCount(string patchName, string pattern, int actual, int expected)
        {
            if (actual != expected)
            {
                string message = "[PocketMapShuttleLaunchFix] IL verification failed in " + patchName +
                    ": expected " + expected + " occurrence(s) of " + pattern + ", found " + actual +
                    ". Tested against RimWorld " + ModBootstrap.TestedGameVersion + ".";
                Log.Error(message);
                throw new InvalidOperationException(message);
            }

            Log.Message("[PocketMapShuttleLaunchFix] IL verified: " + patchName + " replaced " + actual + " occurrence(s) of " + pattern + ".");
        }
    }

    [HarmonyPatch]
    internal static class Patch_StartChoosingDestination
    {
        private static MethodBase TargetMethod() { return PatchTargets.StartChoosingDestination; }

        private static bool Prefix(CompLaunchable __instance)
        {
            PlanetTile ignored;
            return PocketMapOriginResolver.ValidateLaunchable(__instance, true, out ignored);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return TranspilerTools.ReplaceCalls(instructions, TranspilerTools.ThingTileGetter,
                TranspilerTools.ThingOriginHelper, 1, "CompLaunchable.StartChoosingDestination");
        }
    }

    [HarmonyPatch]
    internal static class Patch_ChoseWorldTarget
    {
        private static MethodBase TargetMethod() { return PatchTargets.ChoseWorldTarget; }

        private static bool Prefix(CompLaunchable __instance, ref bool __result)
        {
            PlanetTile ignored;
            if (PocketMapOriginResolver.ValidateLaunchable(__instance, true, out ignored))
            {
                return true;
            }

            __result = false;
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return TranspilerTools.ReplaceCalls(instructions, TranspilerTools.ThingTileGetter,
                TranspilerTools.ThingOriginHelper, 1, "CompLaunchable.ChoseWorldTarget(instance)");
        }
    }

    [HarmonyPatch]
    internal static class Patch_TryLaunch
    {
        private static MethodBase TargetMethod() { return PatchTargets.TryLaunch; }

        private static bool Prefix(CompLaunchable __instance, PlanetTile destinationTile)
        {
            PlanetTile ignored;
            if (!PocketMapOriginResolver.ValidateLaunchable(__instance, true, out ignored))
            {
                return false;
            }

            PocketMapOriginResolver.BeginTryLaunch(__instance, destinationTile);
            return true;
        }

        private static void Postfix()
        {
            PocketMapOriginResolver.EndTryLaunch();
        }

        private static Exception Finalizer(Exception __exception)
        {
            PocketMapOriginResolver.EndTryLaunch();
            return __exception;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = instructions.ToList();
            int tileCount = 0;
            int spawnCount = 0;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(TranspilerTools.MapTileGetter))
                {
                    list[i].opcode = OpCodes.Call;
                    list[i].operand = TranspilerTools.MapOriginHelper;
                    tileCount++;
                }
                else if (list[i].Calls(TranspilerTools.SpawnMethod))
                {
                    list[i].opcode = OpCodes.Call;
                    list[i].operand = TranspilerTools.SpawnHelper;
                    spawnCount++;
                }
            }

            TranspilerTools.RequireCount("CompLaunchable.TryLaunch", "Map.get_Tile", tileCount, 1);
            TranspilerTools.RequireCount("CompLaunchable.TryLaunch", "GenSpawn.Spawn(Thing,IntVec3,Map,WipeMode)", spawnCount, 1);
            return list;
        }
    }

    [HarmonyPatch]
    internal static class Patch_MaxLaunchDistanceAtFuelLevel
    {
        private static MethodBase TargetMethod() { return PatchTargets.MaxLaunchDistanceAtFuelLevel; }

        private static bool Prefix(CompLaunchable __instance, ref int __result)
        {
            PlanetTile ignored;
            if (PocketMapOriginResolver.ValidateLaunchable(__instance, false, out ignored))
            {
                return true;
            }

            __result = 0;
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return TranspilerTools.ReplaceCalls(instructions, TranspilerTools.ThingTileGetter,
                TranspilerTools.ThingOriginHelper, 1, "CompLaunchable.MaxLaunchDistanceAtFuelLevel(float)");
        }
    }

    [HarmonyPatch]
    internal static class Patch_FlyShipLeavingLeaveMap
    {
        private static MethodBase TargetMethod() { return PatchTargets.FlyShipLeavingLeaveMap; }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = instructions.ToList();
            List<int> matches = new List<int>();

            for (int i = 0; i + 2 < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Ldarg_0 &&
                    list[i + 1].Calls(TranspilerTools.ThingMapGetter) &&
                    list[i + 2].Calls(TranspilerTools.MapTileGetter))
                {
                    matches.Add(i);
                }
            }

            TranspilerTools.RequireCount("FlyShipLeaving.LeaveMap", "ldarg.0 -> Thing.get_Map -> Map.get_Tile", matches.Count, 1);

            int index = matches[0];
            CodeInstruction replacement = new CodeInstruction(OpCodes.Call, TranspilerTools.LeavingOriginHelper);
            replacement.labels.AddRange(list[index + 1].labels);
            replacement.labels.AddRange(list[index + 2].labels);
            replacement.blocks.AddRange(list[index + 1].blocks);
            replacement.blocks.AddRange(list[index + 2].blocks);
            list[index + 1] = replacement;
            list.RemoveAt(index + 2);
            return list;
        }
    }
}
