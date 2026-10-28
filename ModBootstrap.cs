using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace PocketMapShuttleLaunchFix
{
    [StaticConstructorOnStartup]
    internal static class ModBootstrap
    {
        internal const string HarmonyId = "wicha.pocketmapshuttlelaunchfix";
        internal const string TestedGameVersion = "1.6.9676.17735";
        internal const bool DebugMode = false;

        static ModBootstrap()
        {
            Harmony harmony = new Harmony(HarmonyId);
            try
            {
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                VerifyOwnedPatches();
                Log.Message("[PocketMapShuttleLaunchFix] Harmony patches applied. Tested against RimWorld " + TestedGameVersion + ".");
            }
            catch (Exception exception)
            {
                harmony.UnpatchAll(HarmonyId);
                Log.Error("[PocketMapShuttleLaunchFix] Failed to apply compatibility patches; all patches owned by this mod were removed to avoid an incomplete patch set.\n" + exception);
            }
        }

        private static void VerifyOwnedPatches()
        {
            MethodBase[] expected = PatchTargets.All;
            for (int i = 0; i < expected.Length; i++)
            {
                MethodBase target = expected[i];
                Patches info = Harmony.GetPatchInfo(target);
                bool found = info != null && info.Owners.Contains(HarmonyId);
                if (!found)
                {
                    throw new InvalidOperationException("Harmony did not report an owned patch for " + PatchTargets.Describe(target) + ".");
                }

                Log.Message("[PocketMapShuttleLaunchFix] Applied: " + PatchTargets.Describe(target));
            }
        }

        internal static void DebugLog(string message)
        {
            if (DebugMode)
            {
                Log.Message("[PocketMapShuttleLaunchFix] " + message);
            }
        }
    }
}
