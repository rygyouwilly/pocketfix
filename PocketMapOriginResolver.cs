using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PocketMapShuttleLaunchFix
{
    internal sealed class LaunchOriginRecord
    {
        internal readonly PlanetTile Tile;

        internal LaunchOriginRecord(PlanetTile tile)
        {
            Tile = tile;
        }
    }

    internal static class PocketMapOriginResolver
    {
        private static readonly ConditionalWeakTable<FlyShipLeaving, LaunchOriginRecord> LeavingOrigins =
            new ConditionalWeakTable<FlyShipLeaving, LaunchOriginRecord>();

        [ThreadStatic]
        private static bool pendingLaunchOriginSet;

        [ThreadStatic]
        private static PlanetTile pendingLaunchOrigin;

        private static int lastFailureMessageTick = -1;
        private static int lastFallbackMessageTick = -1;

        internal static bool TryResolvePocketOrigin(Map map, out PlanetTile tile, out string failure)
        {
            tile = PlanetTile.Invalid;
            failure = null;

            if (map == null)
            {
                failure = "The shuttle has no map.";
                return false;
            }

            if (!map.IsPocketMap)
            {
                tile = map.Tile;
                return true;
            }

            HashSet<Map> visited = new HashSet<Map>();
            List<string> path = ModBootstrap.DebugMode ? new List<string>() : null;
            Map current = map;

            while (current != null && current.IsPocketMap)
            {
                if (!visited.Add(current))
                {
                    failure = "A cycle was detected in the pocket-map source chain.";
                    DebugPath(path, current, tile, failure);
                    return false;
                }

                if (path != null)
                {
                    path.Add(DescribeMap(current));
                }

                PocketMapParent pocketParent = current.PocketMapParent;
                if (pocketParent == null)
                {
                    failure = "The pocket map has no PocketMapParent.";
                    DebugPath(path, current, tile, failure);
                    return false;
                }

                if (pocketParent.sourceMap == null)
                {
                    failure = "The PocketMapParent has no source map.";
                    DebugPath(path, current, tile, failure);
                    return false;
                }

                current = pocketParent.sourceMap;
            }

            if (current == null)
            {
                failure = "The outer source map is missing.";
                DebugPath(path, current, tile, failure);
                return false;
            }

            if (Current.Game == null || Current.Game.Maps == null || !Current.Game.Maps.Contains(current))
            {
                failure = "The outer source map is no longer present in Current.Game.Maps.";
                DebugPath(path, current, tile, failure);
                return false;
            }

            if (current.Parent == null)
            {
                failure = "The outer source map has no MapParent.";
                DebugPath(path, current, tile, failure);
                return false;
            }

            tile = current.Tile;
            if (!tile.Valid)
            {
                failure = "The outer source map has no valid world tile.";
                DebugPath(path, current, tile, failure);
                return false;
            }

            if (path != null)
            {
                path.Add(DescribeMap(current));
            }

            DebugPath(path, current, tile, null);
            return true;
        }

        internal static PlanetTile OriginForThingOrVanilla(Thing thing)
        {
            if (thing == null || thing.Map == null || !thing.Map.IsPocketMap)
            {
                return thing == null ? PlanetTile.Invalid : thing.Tile;
            }

            PlanetTile tile;
            string failure;
            if (TryResolvePocketOrigin(thing.Map, out tile, out failure))
            {
                return tile;
            }

            throw new InvalidOperationException("Pocket-map launch origin was not validated before use: " + failure);
        }

        internal static PlanetTile OriginForMapOrVanilla(Map map)
        {
            if (map == null || !map.IsPocketMap)
            {
                return map == null ? PlanetTile.Invalid : map.Tile;
            }

            PlanetTile tile;
            string failure;
            if (TryResolvePocketOrigin(map, out tile, out failure))
            {
                return tile;
            }

            throw new InvalidOperationException("Pocket-map launch origin was not validated before use: " + failure);
        }

        internal static bool ValidateLaunchable(CompLaunchable launchable, bool showMessage, out PlanetTile tile)
        {
            tile = PlanetTile.Invalid;
            Map map = launchable == null || launchable.parent == null ? null : launchable.parent.Map;
            if (map == null || !map.IsPocketMap)
            {
                return true;
            }

            string failure;
            if (TryResolvePocketOrigin(map, out tile, out failure))
            {
                return true;
            }

            if (showMessage)
            {
                ShowFailure(failure);
            }

            ModBootstrap.DebugLog("Launch rejected: " + failure);
            return false;
        }

        internal static void BeginTryLaunch(CompLaunchable launchable, PlanetTile destinationTile)
        {
            pendingLaunchOriginSet = false;
            pendingLaunchOrigin = PlanetTile.Invalid;

            Map map = launchable == null || launchable.parent == null ? null : launchable.parent.Map;
            if (map != null && map.IsPocketMap)
            {
                PlanetTile origin;
                string failure;
                if (TryResolvePocketOrigin(map, out origin, out failure))
                {
                    pendingLaunchOrigin = origin;
                    pendingLaunchOriginSet = true;
                    ModBootstrap.DebugLog("Launch destination: " + destinationTile + "; resolved origin: " + origin + ".");
                }
            }
        }

        internal static void EndTryLaunch()
        {
            pendingLaunchOriginSet = false;
            pendingLaunchOrigin = PlanetTile.Invalid;
        }

        internal static Thing SpawnAndRememberOrigin(Thing thing, IntVec3 position, Map map, WipeMode wipeMode)
        {
            Thing spawned = GenSpawn.Spawn(thing, position, map, wipeMode);
            FlyShipLeaving leaving = spawned as FlyShipLeaving;
            if (leaving != null && map != null && map.IsPocketMap && pendingLaunchOriginSet && pendingLaunchOrigin.Valid)
            {
                LeavingOrigins.Remove(leaving);
                LeavingOrigins.Add(leaving, new LaunchOriginRecord(pendingLaunchOrigin));
                ModBootstrap.DebugLog("Cached takeoff origin " + pendingLaunchOrigin + " for " + leaving + ".");
            }

            return spawned;
        }

        internal static PlanetTile OriginForLeavingShip(FlyShipLeaving leaving)
        {
            Map map = leaving == null ? null : leaving.Map;
            if (map == null || !map.IsPocketMap)
            {
                return map == null ? PlanetTile.Invalid : map.Tile;
            }

            PlanetTile liveTile;
            string failure;
            if (TryResolvePocketOrigin(map, out liveTile, out failure))
            {
                ModBootstrap.DebugLog("FlyShipLeaving origin: " + liveTile + "; destination: " + leaving.destinationTile + ".");
                return liveTile;
            }

            LaunchOriginRecord cached;
            if (LeavingOrigins.TryGetValue(leaving, out cached) && cached.Tile.Valid)
            {
                ShowFallbackOnce();
                Log.Warning("[PocketMapShuttleLaunchFix] Pocket-map source became unavailable during takeoff; using cached origin " + cached.Tile + ". Reason: " + failure);
                return cached.Tile;
            }

            // FlyShipLeaving checks destinationTile.Valid before reaching the patched origin read.
            // Using it as a zero-distance fallback is preferable to repeating LeaveMap forever or destroying contents.
            ShowFallbackOnce();
            Log.Error("[PocketMapShuttleLaunchFix] No live or cached pocket-map launch origin remained during takeoff. Using valid destination tile " + leaving.destinationTile + " as a cargo-safe last resort. Reason: " + failure);
            return leaving.destinationTile;
        }

        private static void ShowFailure(string failure)
        {
            int tick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
            if (tick == lastFailureMessageTick)
            {
                return;
            }

            lastFailureMessageTick = tick;
            Messages.Message("PocketMapShuttleLaunchFix_CannotResolveOrigin".Translate(failure), MessageTypeDefOf.RejectInput, false);
        }

        private static void ShowFallbackOnce()
        {
            int tick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
            if (tick == lastFallbackMessageTick)
            {
                return;
            }

            lastFallbackMessageTick = tick;
            Messages.Message("PocketMapShuttleLaunchFix_LeavingFallback".Translate(), MessageTypeDefOf.CautionInput, false);
        }

        private static string DescribeMap(Map map)
        {
            if (map == null)
            {
                return "<null map>";
            }

            string generator = map.generatorDef == null ? "no generator" : map.generatorDef.defName;
            return map + " [" + (map.IsPocketMap ? "PocketMap" : "surface") + "/" + generator + "]";
        }

        private static void DebugPath(List<string> path, Map finalMap, PlanetTile tile, string failure)
        {
            if (!ModBootstrap.DebugMode || path == null)
            {
                return;
            }

            string text = string.Join(" -> ", path.ToArray());
            if (failure == null)
            {
                text += " -> surface tile " + tile;
            }
            else
            {
                text += " -> FAILED: " + failure;
            }

            ModBootstrap.DebugLog("Origin path: " + text);
        }
    }
}
