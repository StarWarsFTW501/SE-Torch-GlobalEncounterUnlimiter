using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GlobalEncounterUnlimiter
{
    /// <summary>
    /// Defines a sequence of IL instructions to replace with a new sequence.
    /// </summary>
    public class MyTranspilerReplacementPattern
    {
        List<CodeInstruction> _targetPattern;
        /// <summary>
        /// The sequence of instructions this pattern aims to replace its target with.
        /// </summary>
        public List<CodeInstruction> ReplacementSequence { get; private set; }
        /// <summary>
        /// Total number of instructions in the target pattern.
        /// </summary>
        public int TotalCount { get; private set; }
        /// <summary>
        /// Number of instructions to copy from the buffer before this pattern's target pattern starts.
        /// </summary>
        public int InstructionsToCopyBefore { get; private set; } = -1;

        int _matchedCount = 0;
        bool _hasEnded = false;

        /// <summary>
        /// Creates a definition of a sequence of IL <see cref="CodeInstruction"/>s to replace with a new sequence.
        /// </summary>
        /// <param name="targetPattern">Pattern of instructions to replace.</param>
        /// <param name="replacementSequence">New sequence of instructions to insert in place of <paramref name="targetPattern"/>.</param>
        /// <exception cref="InvalidOperationException">Exception thrown when <paramref name="targetPattern"/> is empty.</exception>
        public MyTranspilerReplacementPattern(List<CodeInstruction> targetPattern, List<CodeInstruction> replacementSequence)
        {
            _targetPattern = targetPattern;

            if (replacementSequence == null)
                replacementSequence = new List<CodeInstruction>();

            if (replacementSequence.Count == 0)
                replacementSequence.Add(new CodeInstruction(OpCodes.Nop));

            ReplacementSequence = replacementSequence;
            TotalCount = targetPattern.Count;
            if (TotalCount == 0)
                throw new InvalidOperationException("Cannot generate transpiler replacement pattern for empty target sequence!");
        }
        /// <summary>
        /// Checks a given instruction for a match with this pattern's target pattern.
        /// </summary>
        /// <param name="instruction">The instruction to check.</param>
        /// <param name="bufferSize">The current number of instructions within the instruction buffer you are keeping for later flushing.</param>
        /// <returns>The match result of this instruction check.</returns>
        public MyTranspilerReplacementResult CheckMatch(CodeInstruction instruction, int bufferSize)
        {
            if (_hasEnded)
            {
                return MyTranspilerReplacementResult.NOMATCH;
            }
            if (instruction.opcode == _targetPattern[_matchedCount].opcode && (_targetPattern[_matchedCount].operand == null || AreOperandsEqual(_targetPattern[_matchedCount].operand, instruction.operand)))
            {
                _matchedCount++;
                if (_matchedCount == 1)
                    InstructionsToCopyBefore = bufferSize - 1;
                if (_matchedCount == TotalCount)
                {
                    _hasEnded = true;
                    _matchedCount = 0;
                    return MyTranspilerReplacementResult.END;
                }
                return MyTranspilerReplacementResult.MATCH;
            }
            else
            {
                _matchedCount = 0;
                InstructionsToCopyBefore = -1;
                return MyTranspilerReplacementResult.NOMATCH;
            }
        }
        /// <summary>
        /// Changes the length this pattern references. If the pattern was matching and no longer starts within the buffer, it is reset.
        /// </summary>
        public void ChangeBufferLength(int newLength)
        {
            if (_matchedCount > newLength)
            {
                _matchedCount = 0;
                InstructionsToCopyBefore = -1;
            }
            else InstructionsToCopyBefore = newLength - _matchedCount;
        }


        bool AreOperandsEqual(object a, object b)
        {
            if (a == b)
                return true;

            if (a == null || b == null)
                return false;

            switch (a)
            {
                case MethodInfo am when b is MethodInfo bm:
                    return am.MetadataToken == bm.MetadataToken && am.Module == bm.Module;

                case FieldInfo af when b is FieldInfo bf:
                    return af.MetadataToken == bf.MetadataToken && af.Module == bf.Module;

                case Type at when b is Type bt:
                    return at.MetadataToken == bt.MetadataToken && at.Module == bt.Module;

                case int ai when b is int bi:
                    return ai == bi;

                case string sa when b is string sb:
                    return sa == sb;

                case LocalBuilder la when b is LocalBuilder lb:
                    return la.LocalIndex == lb.LocalIndex && la.LocalType == lb.LocalType;

                case Label la when b is Label lb:
                    return la.Equals(lb);

                default:
                    return a.Equals(b);
            }
        }
    }
    /// <summary>
    /// Possible results for an IL instruction match check
    /// </summary>
    public enum MyTranspilerReplacementResult
    {
        /// <summary>
        /// The sequence is matching so far.
        /// </summary>
        MATCH,
        /// <summary>
        /// The sequence does not match.
        /// </summary>
        NOMATCH,
        /// <summary>
        /// The sequence has just finished matching and is ready for replacement.
        /// </summary>
        END
    }
}
