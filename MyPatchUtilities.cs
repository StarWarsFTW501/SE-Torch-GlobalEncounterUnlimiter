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









        static string GetILInstructionLogEntry(OpCode code, object operand)
        {
            string value;
            if (operand is MethodInfo mi)
                value = $"Method info: {GetMethodStringInfo(mi)} (metadata token {mi.MetadataToken})";
            else if (operand is FieldInfo fi)
                value = $"Field info: {GetFieldStringInfo(fi)} in type {fi.DeclaringType.FullName} (metadata token {fi.MetadataToken})";
            else if (operand is Type t)
                value = $"Type info: {GetTypeInformationRecursive(t)} (metadata token {t.MetadataToken})";
            else if (operand == null)
                value = "<no operand>";
            else value = $"Raw operand: {operand} (Type: {GetTypeInformationRecursive(operand.GetType())})";
            return $"Instruction: {code}{new string(' ', 14 - code.ToString().Length)}{value}";
        }
        public static string GetMethodStringInfo(MethodBase method)
        {
            if (method == null)
                return "indeterminate method";

            string outputPrefix = (
                method.IsPublic ? "public" :
                method.IsPrivate ? "private" :
                method.IsFamily ? "protected" :
                method.IsAssembly ? "internal" : "") + (

                method.IsStatic ? " static" : " instance") + (

                method is ConstructorInfo ? " constructor" :
                    (method as MethodInfo).ReturnType.IsVoid() ? " void" :
                    (" " + GetTypeInformationRecursive((method as MethodInfo).ReturnType)));

            var parameters = method.GetParameters();
            string parameterDescription = "";
            if (parameters.Length != 0)
            {
                parameterDescription = GetParameterDescription(parameters[0]);
                for (int i = 1; i < parameters.Length; i++)
                    parameterDescription += $", {GetParameterDescription(parameters[i])}";
            }


            return $"{outputPrefix} {method.DeclaringType?.FullName ?? "[indeterminate owner]"}.{((method is MethodInfo methodInfo) ? methodInfo.Name : (method as ConstructorInfo).Name)}({parameterDescription})";
        }
        public static string GetFieldStringInfo(FieldInfo field)
        {
            if (field == null)
                return "indeterminate field";

            string outputPrefix = (
                field.IsPublic ? "public" :
                field.IsPrivate ? "private" :
                field.IsFamily ? "protected" :
                field.IsAssembly ? "internal" : "") + (

                field.IsStatic ? " static " : " instance ") +

                GetTypeInformationRecursive(field.FieldType);

            return $"{outputPrefix} {field.Name}";
        }
        static string GetParameterDescription(ParameterInfo parameter)
        {
            return $"{(parameter.ParameterType.IsByRef ? parameter.IsOut ? "out " : "ref " : parameter.IsIn ? "in " : "")}{GetTypeInformationRecursive(parameter.ParameterType)}";
        }
        static string GetTypeInformationRecursive(Type type)
        {
            if (type == null)
                return null;
            return $"{type.Namespace}.{type.Name}{(type.IsGenericType ? $"<{string.Join(", ", type.GetGenericArguments().Select(t => GetTypeInformationRecursive(t) ?? "indeterminate generic"))}>" : "")}";
        }
    }
}
