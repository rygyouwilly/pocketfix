using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PocketMapShuttleLaunchFix
{
    internal static class ExplicitPocketArrivalScope
    {
        [ThreadStatic]
        private static int depth;

        internal static void Enter()
        {
            depth++;
        }

        internal static void Exit()
        {
            if (depth > 0)
            {
                depth--;
            }
        }

        internal static PocketMapParent PocketParentForVanillaRedirect(MapParent mapParent)
        {
            return depth > 0 ? null : mapParent as PocketMapParent;
        }
    }

    [HarmonyPatch(typeof(ShipJob_Arrive), "TryStart")]
    internal static class Patch_ShipJob_Arrive_TryStart
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            MethodInfo helper = AccessTools.Method(
                typeof(ExplicitPocketArrivalScope),
                "PocketParentForVanillaRedirect");
            int count = 0;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Isinst && list[i].operand as Type == typeof(PocketMapParent))
                {
                    list[i].opcode = OpCodes.Call;
                    list[i].operand = helper;
                    count++;
                }
            }

            if (count != 1)
            {
                string message = "ShipJob_Arrive.TryStart expected exactly 1 PocketMapParent redirect check, found " + count + ".";
                Log.Error("[PocketMapShuttleLaunchFix] Patch error: " + message);
                throw new InvalidOperationException(message);
            }

            Log.Message("[PocketMapShuttleLaunchFix] IL verified: ShipJob_Arrive.TryStart replaced 1 PocketMapParent redirect check.");
            return list;
        }
    }

    [HarmonyPatch(typeof(TransportersArrivalAction_LandInSpecificCell), "CanLandInSpecificCell")]
    internal static class Patch_LandInSpecificCell_CanLandInSpecificCell
    {
        private static bool Prefix(MapParent mapParent, ref bool __result)
        {
            PocketMapParent pocketParent = mapParent as PocketMapParent;
            if (pocketParent == null)
            {
                return true;
            }

            Map map = pocketParent.HasMap ? pocketParent.Map : null;
            __result = Current.Game != null &&
                Find.World != null &&
                Find.World.pocketMaps.Contains(pocketParent) &&
                map != null &&
                Current.Game.Maps.Contains(map);

            return false;
        }
    }

    [HarmonyPatch(typeof(CompLaunchable), "GetTransportersFloatMenuOptionsAt")]
    internal static class Patch_CompLaunchable_GetTransportersFloatMenuOptionsAt
    {
        private static readonly MethodInfo TransportersInGroupGetter = AccessTools.PropertyGetter(
            typeof(CompLaunchable),
            "TransportersInGroup");

        private static readonly FieldInfo LandingCellField = AccessTools.Field(
            typeof(TransportersArrivalAction_LandInSpecificCell),
            "cell");

        private static readonly FieldInfo LandingRotationField = AccessTools.Field(
            typeof(TransportersArrivalAction_LandInSpecificCell),
            "rotation");

        private static void Postfix(
            CompLaunchable __instance,
            PlanetTile tile,
            Action<PlanetTile, TransportersArrivalAction> launchAction,
            bool isShuttle,
            ref IEnumerable<FloatMenuOption> __result)
        {
            __result = AppendPocketMapOptions(__result, __instance, tile, launchAction, isShuttle);
        }

        private static IEnumerable<FloatMenuOption> AppendPocketMapOptions(
            IEnumerable<FloatMenuOption> vanilla,
            CompLaunchable launchable,
            PlanetTile destinationTile,
            Action<PlanetTile, TransportersArrivalAction> launchAction,
            bool isShuttle)
        {
            if (vanilla != null)
            {
                foreach (FloatMenuOption option in vanilla)
                {
                    yield return option;
                }
            }

            if (!isShuttle || launchable == null || launchAction == null || Current.Game == null)
            {
                yield break;
            }

            List<PocketMapParent> candidates = FindTargetPocketMaps(destinationTile);
            if (candidates.Count == 0)
            {
                yield break;
            }

            IEnumerable<IThingHolder> transporters = TransportersInGroupGetter.Invoke(launchable, null)
                as IEnumerable<IThingHolder>;
            if (transporters == null)
            {
                Log.Error("[PocketMapShuttleLaunchFix] Could not read CompLaunchable.TransportersInGroup while adding PocketMap landing options.");
                yield break;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                PocketMapParent pocketParent = candidates[i];
                Action<PlanetTile, TransportersArrivalAction> resolvedLaunch = delegate(
                    PlanetTile ignored,
                    TransportersArrivalAction arrivalAction)
                {
                    TransportersArrivalAction_LandInSpecificCell landing =
                        arrivalAction as TransportersArrivalAction_LandInSpecificCell;
                    if (landing == null)
                    {
                        Log.Error("[PocketMapShuttleLaunchFix] Pocket Map landing did not produce a LandInSpecificCell action; launch was cancelled safely.");
                        return;
                    }

                    IntVec3 cell = (IntVec3)LandingCellField.GetValue(landing);
                    Rot4 rotation = (Rot4)LandingRotationField.GetValue(landing);
                    PocketMapLandInSpecificCellAction pocketAction = new PocketMapLandInSpecificCellAction(
                        pocketParent,
                        cell,
                        rotation,
                        destinationTile);

                    Log.Message("[PocketMapShuttleLaunchFix] Pocket landing selected: parent=" +
                        pocketParent.ID + ", surfaceTile=" + destinationTile + ", cell=" + cell + ".");
                    launchAction(destinationTile, pocketAction);
                };

                string mapDefName = pocketParent.MapGeneratorDef?.defName;
                bool isInsectLair = mapDefName == "InsectLair";

                foreach (FloatMenuOption option in pocketParent.GetShuttleFloatMenuOptions(transporters, resolvedLaunch))
                {
                    if (isInsectLair)
                    {
                        option.Label = candidates.Count == 1
                            ? "PocketMapShuttleLaunchFix_LandInInsectLair".Translate()
                            : "PocketMapShuttleLaunchFix_LandInInsectLairNumbered".Translate(i + 1);
                    }
                    else
                    {
                        option.Label = candidates.Count == 1
                            ? "PocketMapShuttleLaunchFix_LandInAncientStockpile".Translate()
                            : "PocketMapShuttleLaunchFix_LandInAncientStockpileNumbered".Translate(i + 1);
                    }
                    yield return option;
                }
            }
        }

        private static List<PocketMapParent> FindTargetPocketMaps(PlanetTile destinationTile)
        {
            List<PocketMapParent> result = new List<PocketMapParent>();
            List<Map> maps = Current.Game.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map == null || !map.IsPocketMap)
                {
                    continue;
                }

                PocketMapParent parent = map.PocketMapParent;
                if (parent == null || parent.MapGeneratorDef == null)
                {
                    continue;
                }

                string defName = parent.MapGeneratorDef.defName;
                if (defName != "AncientStockpile" && defName != "InsectLair")
                {
                    continue;
                }

                PlanetTile resolvedTile;
                string failure;
                if (PocketMapOriginResolver.TryResolvePocketOrigin(map, out resolvedTile, out failure) &&
                    resolvedTile == destinationTile)
                {
                    result.Add(parent);
                }
            }

            return result;
        }
    }

    public sealed class PocketMapLandInSpecificCellAction : TransportersArrivalAction_LandInSpecificCell
    {
        private PocketMapParent pocketParent;
        private PlanetTile surfaceTile = PlanetTile.Invalid;

        public PocketMapLandInSpecificCellAction()
        {
        }

        internal PocketMapLandInSpecificCellAction(
            PocketMapParent pocketParent,
            IntVec3 cell,
            Rot4 rotation,
            PlanetTile surfaceTile)
            : base(pocketParent, cell, rotation, true)
        {
            this.pocketParent = pocketParent;
            this.surfaceTile = surfaceTile;
        }

        public override FloatMenuAcceptanceReport StillValid(
            IEnumerable<IThingHolder> pods,
            PlanetTile destinationTile)
        {
            bool parentRegistered = pocketParent != null &&
                Find.World != null &&
                Find.World.pocketMaps.Contains(pocketParent);
            Map map = parentRegistered && pocketParent.HasMap ? pocketParent.Map : null;
bool mapLoaded = map != null && Current.Game != null && Current.Game.Maps.Contains(map);
PlanetTile resolvedTile;
string failure;
bool resolved = mapLoaded &&
PocketMapOriginResolver.TryResolvePocketOrigin(map, out resolvedTile, out failure) &&
resolvedTile == surfaceTile;
bool destinationMatches = destinationTile == surfaceTile;
bool canLand = parentRegistered && mapLoaded &&
TransportersArrivalAction_LandInSpecificCell.CanLandInSpecificCell(pods, pocketParent);
bool valid = parentRegistered && mapLoaded && resolved && destinationMatches && canLand;
Log.Message("[PocketMapShuttleLaunchFix] Pocket landing validation: valid=" + valid +
", parentRegistered=" + parentRegistered +
", mapLoaded=" + mapLoaded +
", resolved=" + resolved +
", destinationMatches=" + destinationMatches +
", canLand=" + canLand + ".");
return valid;
}
public override void Arrived(List transporters, PlanetTile destinationTile)
{
ExplicitPocketArrivalScope.Enter();
try
{
Log.Message("[PocketMapShuttleLaunchFix] Preserving PocketMapParent through ShipJob_Arrive for explicit underground landing.");
base.Arrived(transporters, destinationTile);
}
finally
{
ExplicitPocketArrivalScope.Exit();
}
}
public override void ExposeData()
{
base.ExposeData();
Scribe_References.Look(ref pocketParent, "pocketMapParent");
Scribe_Values.Look(ref surfaceTile, "surfaceTile", PlanetTile.Invalid);
}
}
}
