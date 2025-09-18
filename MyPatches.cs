using HarmonyLib;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using SpaceEngineers.Game.SessionComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Utils;

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
        public static IEnumerable<CodeInstruction> MyGlobalEncountersGenerator_RegisterEncounter_Transpiler(IEnumerable<CodeInstruction> instructions)
            => MyPatchUtilities.ExecuteTranspilerPatch(instructions,
                // #1 - max timer clamp removal
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte) 91), // loads lower bound for maximum timer
                        new CodeInstruction(OpCodes.Ldc_I4, 180), // loads upper bound
                        new CodeInstruction(OpCodes.Call, typeof(MyUtils).GetMethod("GetClampInt")) // pops 3 (max, min, original), pushes clamped result
                    },
                    replacementSequence:
                    new List<CodeInstruction>()), // just don't do anything here, this will leave us with the original int32 on the stack and the following code has no clue it wasn't clamped
                // #2 - min timer clamp adjustment
                new MyTranspilerReplacementPattern(
                    targetPattern:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldfld, typeof(MyObjectBuilder_SessionSettings).GetField("GlobalEncounterMinRemovalTimer")), // loads setting for minimum timer
                        new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte) 90) // loads lower bound
                        // .. next instruction is a local variable load to recall the maximum timer value, we want to keep that
                    },
                    replacementSequence:
                    new List<CodeInstruction>()
                    {
                        new CodeInstruction(OpCodes.Ldfld, typeof(MyObjectBuilder_SessionSettings).GetField("GlobalEncounterMinRemovalTimer")), // still load the setting - only for ensuring correct IL is changed
                        new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte) 0) // load the lower bound as 0 instead
                        // .. still recall the max value
                        // .. still execute that clamp
                    })
                );
        #endregion
    }
}
