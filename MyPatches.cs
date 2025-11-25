using HarmonyLib;
using Sandbox.Definitions;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using SpaceEngineers.Game.SessionComponents;
using SteamKit2.GC.Dota.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace GlobalEncounterUnlimiter
{
    [HarmonyPatch]
    public class MyPatches
    {
        #region Login GPS synchronization
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MyPlayerCollection), "OnNewPlayerRequest")]
        public static IEnumerable<CodeInstruction> MyPlayerCollection_OnNewPlayerRequest_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            var branchTarget = ilGenerator.DefineLabel();
            var variable = ilGenerator.DeclareLocal(typeof(MyIdentity));
            return MyPatchUtilities.ExecuteTranspilerPatch(instructions,
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyPlayerCollection).GetMethod("TryGetPlayerIdentity", new Type[] { typeof(MyPlayer.PlayerId) })) // gets player identity, or null if none found. following original instructions store this into a variable and do further stuff with it.
                    },
                    replacementSequence:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyPlayerCollection).GetMethod("TryGetPlayerIdentity", new Type[] { typeof(MyPlayer.PlayerId) })), // still do the same retrieval
                        new CodeInstruction(OpCodes.Dup), // duplicate the result - we will pop one for the null check
                        new CodeInstruction(OpCodes.Ldnull), // load null for comparison
                        new CodeInstruction(OpCodes.Ceq), // pop 2, compare identity with null, we want our if statement to execute if it is not null (= returned false)
                        new CodeInstruction(OpCodes.Brtrue_S, branchTarget), // branch if true to the label, otherwise continue with our registration
                        new CodeInstruction(OpCodes.Dup), // duplicate the identity again - we will pop one for the registration
                        new CodeInstruction(OpCodes.Stloc, variable), // store the identity for ordering
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Plugin).GetField("Instance")), // reference our plugin's instance
                        new CodeInstruction(OpCodes.Ldfld, typeof(Plugin).GetField("EncounterGpsSynchronizer")), // reference our synchronizer instance
                        new CodeInstruction(OpCodes.Ldloc, variable), // load the identity - it is popped first, before the object reference
                        new CodeInstruction(OpCodes.Call, typeof(MyEncounterGpsSynchronizer).GetMethod("RegisterNewPlayerWithExistingIdentity")), // pop identity, register in our synchronizer instance
                        new CodeInstruction(OpCodes.Nop) { labels = new List<Label> { branchTarget } } // nop to branch to since the actual instruction that should be here is a local variable store which i can't stably touch
                    }));
        }
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MyGlobalEncountersGenerator), "OnSpawnFinished")]
        public static IEnumerable<CodeInstruction> MyGlobalEncountersGenerator_OnSpawnFinished_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            var variable = ilGenerator.DeclareLocal(typeof(MyGps));
            return MyPatchUtilities.ExecuteTranspilerPatch(instructions,
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyGps).GetMethod("set_EntityId")) // last thing done in the construction of the GPS
                        // .. next instruction is a local variable store of the GPS
                    },
                    replacementSequence:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyGps).GetMethod("set_EntityId")), // finish the construction correctly
                        new CodeInstruction(OpCodes.Dup), // duplicate the GPS so we can use it and original code can later store it
                        new CodeInstruction(OpCodes.Stloc, variable), // store the GPS into a variable so we can recall it in proper order
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Plugin).GetField("Instance")), // reference our plugin's instance
                        new CodeInstruction(OpCodes.Ldfld, typeof(Plugin).GetField("EncounterGpsSynchronizer")), // reference our synchronizer instance
                        new CodeInstruction(OpCodes.Ldarg_1), // load the encounter ID given as a method argument
                        new CodeInstruction(OpCodes.Ldloc, variable), // load the GPS back
                        new CodeInstruction(OpCodes.Call, typeof(MyEncounterGpsSynchronizer).GetMethod("OnGlobalEncounterSpawned")) // pop encounter id and variable, then register spawn
                    }
                    ));
        }
        public static void MySession_Save_DynamicPostfix(ref MySessionSnapshot snapshot, string customSaveName, Action<SaveProgress> progress, bool __result)
        {
            if (__result)
            {
                Plugin.Instance.EncounterGpsSynchronizer.SaveToFile();
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyGlobalEncountersGenerator), "RemoveGlobalEncounter")]
        public static void MyGlobalEncountersGenerator_RemoveGlobalEncounter_Postfix(long encounterId)
            => Plugin.Instance.EncounterGpsSynchronizer.OnGlobalEncounterDespawned(encounterId);
        #endregion

        #region Hardcoded timer limit removal
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MyGlobalEncountersGenerator), "RegisterEncounter")]
        public static IEnumerable<CodeInstruction> MyGlobalEncountersGenerator_RegisterEncounter_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            var maxTimer = ilGenerator.DeclareLocal(typeof(int));
            return MyPatchUtilities.ExecuteTranspilerPatch(instructions,
                // #1 - max timer clamp removal
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte) 21), // loads lower bound for maximum timer
                        new CodeInstruction(OpCodes.Ldc_I4, 1440), // loads upper bound
                        new CodeInstruction(OpCodes.Call, typeof(MyUtils).GetMethod("GetClampInt")) // pops 3 (max, min, original), pushes clamped result
                    },
                    replacementSequence:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Dup), // duplicate the resulting max timer
                        new CodeInstruction(OpCodes.Stloc, maxTimer), // store one copy into our local for later use
                        // ... stloc of clamped max timer consumes topmost stack value (dup'ed max timer)
                    }),
                // #2 - min timer clamp adjustment
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldfld, typeof(MyObjectBuilder_SessionSettings).GetField("GlobalEncounterMinRemovalTimer")), // loads setting for minimum timer
                        new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte) 20), // loads lower bound
                        new CodeInstruction(OpCodes.Ldc_I4, 1440) // loads upper bound
                        // ... next instruction is the clamp call
                    },
                    replacementSequence:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldfld, typeof(MyObjectBuilder_SessionSettings).GetField("GlobalEncounterMinRemovalTimer")), // still load the setting - only for ensuring correct IL is changed
                        new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte) 0), // load the lower bound as 0 instead
                        new CodeInstruction(OpCodes.Ldloc, maxTimer) // load our stored max timer as upper bound
                        // ... still execute that clamp
                    })
                );
        }
        #endregion

        #region Spawn limiting
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MyGlobalEncountersGenerator), "UpdateBeforeSimulation")]
        public static IEnumerable<CodeInstruction> MyGlobalEncountersGenerator_UpdateBeforeSimulation_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            var dontExecuteLabel = ilGenerator.DefineLabel();
            var executeLabel = ilGenerator.DefineLabel();
            return MyPatchUtilities.ExecuteTranspilerPatch(instructions,
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldc_R4, 0f), // lower bound for random selection
                        new CodeInstruction(OpCodes.Ldc_R4, 1f), // upper bound for random selection
                        new CodeInstruction(OpCodes.Call, typeof(MyUtils).GetMethod("GetRandomFloat", new Type[] { typeof(float), typeof(float) })) // get the random value
                    },
                    replacementSequence:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Plugin).GetField("Instance")), // reference our plugin's instance
                        new CodeInstruction(OpCodes.Callvirt, typeof(Plugin).GetMethod("get_Config")), // reference our config
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyPluginConfig).GetMethod("get_LocationRestriction")), // get if we do restriction
                        new CodeInstruction(OpCodes.Brfalse, executeLabel), // jump and execute original IL
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Plugin).GetField("Instance")), // reference our plugin's instance
                        new CodeInstruction(OpCodes.Callvirt, typeof(Plugin).GetMethod("get_Config")), // reference our config
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyPluginConfig).GetMethod("get_LocationRestrictionAllowPlanets")), // get if we allow planet orbit spawns
                        new CodeInstruction(OpCodes.Brtrue, executeLabel), // jump to original IL and allow planet spawn
                        new CodeInstruction(OpCodes.Ldc_R4, 1.1f), // load above 1 to avoid planet spawn
                        new CodeInstruction(OpCodes.Br, dontExecuteLabel), // jump over original code to not trigger random selection
                        new CodeInstruction(OpCodes.Ldc_R4, 0f) { labels = { executeLabel } }, // lower bound for random selection, with target to jump to
                        new CodeInstruction(OpCodes.Ldc_R4, 1f), // upper bound for random selection
                        new CodeInstruction(OpCodes.Call, typeof(MyUtils).GetMethod("GetRandomFloat", new Type[] { typeof(float), typeof(float) })), // get the random value
                        new CodeInstruction(OpCodes.Nop) { labels = { dontExecuteLabel } }, // nop jump target
                    }));
        }
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MyGlobalEncountersGenerator), "GetSpawnPositionInSpace")]
        public static IEnumerable<CodeInstruction> MyGlobalEncountersGenerator_GetSpawnPositionInSpace_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            var min_endLabel = ilGenerator.DefineLabel();
            var max_endLabel = ilGenerator.DefineLabel();
            var use_dontExecuteLabel = ilGenerator.DefineLabel();
            var use_executeLabel = ilGenerator.DefineLabel();
            var radius = ilGenerator.DeclareLocal(typeof(double));
            return MyPatchUtilities.ExecuteTranspilerPatch(instructions,
                // #1 - min distance adjustment
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldfld, typeof(Sandbox.Definitions.GlobalEncounterSettings).GetField("MinDistanceFromCenter"))
                    },
                    replacementSequence:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldfld, typeof(Sandbox.Definitions.GlobalEncounterSettings).GetField("MinDistanceFromCenter")),
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Plugin).GetField("Instance")), // reference our plugin's instance
                        new CodeInstruction(OpCodes.Callvirt, typeof(Plugin).GetMethod("get_Config")), // reference our config
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyPluginConfig).GetMethod("get_LocationRestriction")), // get if we do restriction
                        new CodeInstruction(OpCodes.Brfalse, min_endLabel), // jump to after this alteration
                        new CodeInstruction(OpCodes.Pop), // remove the original min distance
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Plugin).GetField("Instance")),
                        new CodeInstruction(OpCodes.Callvirt, typeof(Plugin).GetMethod("get_Config")), // reference our config
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyPluginConfig).GetMethod("get_LocationRestrictionMinRadius")), // get the minimumm distance
                        new CodeInstruction(OpCodes.Nop) { labels = { min_endLabel } } // nop jump target if we aren't doing this
                    }),
                // #2 - max distance adjustment
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldfld, typeof(Sandbox.Definitions.GlobalEncounterSettings).GetField("MaxDistanceFromCenter"))
                    },
                    replacementSequence:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldfld, typeof(Sandbox.Definitions.GlobalEncounterSettings).GetField("MaxDistanceFromCenter")),
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Plugin).GetField("Instance")), // reference our plugin's instance
                        new CodeInstruction(OpCodes.Callvirt, typeof(Plugin).GetMethod("get_Config")), // reference our config
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyPluginConfig).GetMethod("get_LocationRestriction")), // get if we do restriction
                        new CodeInstruction(OpCodes.Brfalse, max_endLabel), // jump to after this alteration
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Plugin).GetField("Instance")),
                        new CodeInstruction(OpCodes.Callvirt, typeof(Plugin).GetMethod("get_Config")), // reference our config
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyPluginConfig).GetMethod("get_LocationRestrictionMaxRadius")), // get the maximum distance
                        new CodeInstruction(OpCodes.Call, typeof(Math).GetMethod("Min", new Type[] { typeof(int), typeof(int) })), // force the maximum
                        new CodeInstruction(OpCodes.Nop) { labels = { max_endLabel } } // nop jump target if we aren't doing this
                    }),
                // #3 - redirect radius store into our own variable
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Add), // adds the group's spawn radius to the random value
                        new CodeInstruction(OpCodes.Stloc_S) // stores radius in unknown local
                    },
                    replacementSequence:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Add), // perform the add
                        new CodeInstruction(OpCodes.Conv_R8), // directly convert to double
                        new CodeInstruction(OpCodes.Stloc, radius) // store in our local
                    }),
                // #4 - alter sphere creation to be centered on our defined center
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Vector3D).GetField("Zero")), // loads zero vector
                        new CodeInstruction(OpCodes.Ldloc_S), // loads radius
                        new CodeInstruction(OpCodes.Conv_R8) // converts radius to double
                    },
                    replacementSequence:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Plugin).GetField("Instance")), // reference our plugin's instance
                        new CodeInstruction(OpCodes.Callvirt, typeof(Plugin).GetMethod("get_Config")), // reference our config
                        new CodeInstruction(OpCodes.Callvirt, typeof(MyPluginConfig).GetMethod("get_LocationRestriction")), // get if we do restriction
                        new CodeInstruction(OpCodes.Brfalse, use_dontExecuteLabel), // jump to after this alteration
                        new CodeInstruction(OpCodes.Call, typeof(MyPatchUtilities).GetMethod("GetSpawnRestrictionCenter")), // load center vector
                        new CodeInstruction(OpCodes.Br, use_executeLabel), // jump to last instruction - don't execute zero vector retrieval
                        new CodeInstruction(OpCodes.Ldsfld, typeof(Vector3D).GetField("Zero")) { labels = { use_dontExecuteLabel } }, // jump target for no alteration code path
                        new CodeInstruction(OpCodes.Ldloc, radius) { labels = { use_executeLabel } } // load radius (no need to convert, we already did)
                    })
                );
        }
        #endregion
    }
}
