using HarmonyLib;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace GlobalEncounterUnlimiter
{
    public class MyPatchUtilities
    {
        /// <summary>
        /// Transpiles a set of instructions such that any replacement directives given by <paramref name="patterns"/> are executed.
        /// </summary>
        /// <param name="original">Original instruction sequence.</param>
        /// <param name="patterns">Replacement patterns to execute.</param>
        /// <returns>Transpiled instruction sequence.</returns>
        public static IEnumerable<CodeInstruction> ExecuteTranspilerPatch(IEnumerable<CodeInstruction> original, params MyTranspilerReplacementPattern[] patterns)
        {
            List<CodeInstruction> buffer = new List<CodeInstruction>();
            List<MyTranspilerReplacementPattern> matchingPatterns = new List<MyTranspilerReplacementPattern>(),
                endingPatterns = new List<MyTranspilerReplacementPattern>();

            foreach (var instruction in original)
            {
                buffer.Add(instruction);

                foreach (var pattern in patterns)
                {
                    var matchResult = pattern.CheckMatch(instruction, buffer.Count);
                    if (matchResult == MyTranspilerReplacementResult.MATCH)
                        matchingPatterns.Add(pattern);
                    else if (matchResult == MyTranspilerReplacementResult.END)
                        endingPatterns.Add(pattern);
                }

                if (matchingPatterns.Count == 0)
                {
                    MyTranspilerReplacementPattern patternToFinalize = null;
                    foreach (var pattern in endingPatterns)
                        if (patternToFinalize == null || pattern.TotalCount > patternToFinalize.TotalCount)
                            patternToFinalize = pattern;
                    if (patternToFinalize != null)
                    {
                        for (int i = 0; i < patternToFinalize.InstructionsToCopyBefore; i++)
                            yield return buffer[i];
                        var labels = new List<Label>();
                        int j = patternToFinalize.InstructionsToCopyBefore;
                        while (true)
                        {
                            if (j >= buffer.Count)
                            {
                                labels.AddList(instruction.labels);
                                break;
                            }
                            if (j == patternToFinalize.TotalCount + patternToFinalize.InstructionsToCopyBefore)
                                break;
                            labels.AddList(buffer[j].labels);
                            j++;
                        }
                        var blocks = buffer[patternToFinalize.InstructionsToCopyBefore].blocks;
                        foreach (var replacementInstruction in patternToFinalize.ReplacementSequence)
                        {
                            if (labels != null)
                            {
                                replacementInstruction.labels.AddList(labels);
                                labels = null;
                            }
                            replacementInstruction.blocks = blocks;
                            yield return replacementInstruction;
                        }
                        buffer.RemoveRange(0, patternToFinalize.InstructionsToCopyBefore + patternToFinalize.TotalCount);
                        foreach (var pattern in matchingPatterns)
                            pattern.ChangeBufferLength(buffer.Count);
                    }
                    else
                    {
                        foreach (var catchupInstruction in buffer)
                            yield return catchupInstruction;
                        buffer.Clear();
                    }
                    endingPatterns.Clear();
                }
                matchingPatterns.Clear();
            }
        }
    }
}
