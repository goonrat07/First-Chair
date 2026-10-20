using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Checks the compiled FirstChair.Sim assembly, not its source text, against .claude/rules/sim-code.md:
    /// no Unity, no clocks, no System.Random or System.Math, no threads, no unordered collections, and no
    /// floating point anywhere. The compiled form sees through aliases, <c>var</c> and generated code.
    /// </summary>
    public class SimAssemblyRulesTests
    {
        private static readonly Assembly SimAssembly = typeof(Simulation).Assembly;

        private static readonly string[] BannedTypes =
        {
            "System.Math", "System.MathF", "System.Random", "System.Guid", "System.Environment",
            "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.Diagnostics.Stopwatch",
            "System.Single", "System.Double", "System.Decimal",
            "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.HashSet`1", "System.Collections.Hashtable",
        };

        private static readonly string[] BannedNamespacePrefixes = { "UnityEngine", "UnityEditor", "System.Threading" };

        private static readonly OpCode[] FloatingPointOpCodes =
        {
            OpCodes.Ldc_R4, OpCodes.Ldc_R8, OpCodes.Conv_R4, OpCodes.Conv_R8, OpCodes.Conv_R_Un, OpCodes.Ckfinite,
            OpCodes.Ldind_R4, OpCodes.Ldind_R8, OpCodes.Stind_R4, OpCodes.Stind_R8,
            OpCodes.Ldelem_R4, OpCodes.Ldelem_R8, OpCodes.Stelem_R4, OpCodes.Stelem_R8,
        };

        [Test]
        public void CompiledAssembly_ReferencesOnlyNetstandard()
        {
            // Arrange and Act
            string[] references = SimAssembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();

            // Assert: no UnityEngine, UnityEditor or any other library.
            Assert.That(references, Is.EquivalentTo(new[] { "netstandard" }));
        }

        [Test]
        public void CompiledAssembly_ReferencesNoBannedTypes()
        {
            // Arrange
            var found = new List<string>();
            using FileStream stream = File.OpenRead(SimAssembly.Location);
            using var peReader = new PEReader(stream);
            MetadataReader metadata = peReader.GetMetadataReader();

            // Act
            foreach (TypeReferenceHandle handle in metadata.TypeReferences)
            {
                TypeReference reference = metadata.GetTypeReference(handle);
                string space = metadata.GetString(reference.Namespace);
                string name = metadata.GetString(reference.Name);
                string fullName = space.Length == 0 ? name : space + "." + name;
                if (BannedTypes.Contains(fullName) || BannedNamespacePrefixes.Any(prefix => space.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    found.Add(fullName);
                }
            }

            // Assert
            Assert.That(found, Is.Empty);
        }

        [Test]
        public void CompiledAssembly_HasNoFloatingPointTypesOrOpcodes()
        {
            // Arrange
            const BindingFlags everything = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var found = new List<string>();

            // Act: field, parameter, return and local types, plus every IL instruction.
            foreach (Type type in SimAssembly.GetTypes())
            {
                foreach (FieldInfo field in type.GetFields(everything))
                {
                    AddIfFloatingPoint(found, field.FieldType, type.Name + "." + field.Name);
                }

                IEnumerable<MethodBase> methods = type.GetMethods(everything).Cast<MethodBase>().Concat(type.GetConstructors(everything));
                foreach (MethodBase method in methods)
                {
                    string where = type.Name + "." + method.Name;
                    if (method is MethodInfo info)
                    {
                        AddIfFloatingPoint(found, info.ReturnType, where + " return");
                    }

                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        AddIfFloatingPoint(found, parameter.ParameterType, where + " parameter " + parameter.Name);
                    }

                    MethodBody body = method.GetMethodBody();
                    if (body == null)
                    {
                        continue;
                    }

                    foreach (LocalVariableInfo local in body.LocalVariables)
                    {
                        AddIfFloatingPoint(found, local.LocalType, where + " local " + local.LocalIndex);
                    }

                    foreach (string opcode in FloatingPointInstructions(body.GetILAsByteArray()))
                    {
                        found.Add(where + " uses " + opcode);
                    }
                }
            }

            // Assert
            Assert.That(found, Is.Empty);
        }

        private static void AddIfFloatingPoint(List<string> found, Type type, string where)
        {
            if (IsFloatingPoint(type))
            {
                found.Add(where + " is " + type.Name);
            }
        }

        private static bool IsFloatingPoint(Type type)
        {
            while (type.HasElementType)
            {
                type = type.GetElementType();
            }

            return type == typeof(float) || type == typeof(double) || type == typeof(decimal)
                || (type.IsGenericType && type.GetGenericArguments().Any(IsFloatingPoint));
        }

        /// <summary>Decodes IL instruction by instruction and names every floating-point opcode.</summary>
        private static IEnumerable<string> FloatingPointInstructions(byte[] il)
        {
            Dictionary<short, OpCode> byValue = typeof(OpCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => (OpCode)field.GetValue(null))
                .GroupBy(opcode => opcode.Value)
                .ToDictionary(group => group.Key, group => group.First());
            var floating = new HashSet<short>(FloatingPointOpCodes.Select(opcode => opcode.Value));

            int offset = 0;
            while (offset < il.Length)
            {
                short value = il[offset] == 0xFE ? unchecked((short)(0xFE00 | il[offset + 1])) : il[offset];
                offset += il[offset] == 0xFE ? 2 : 1;
                OpCode opcode = byValue[value];
                if (floating.Contains(opcode.Value))
                {
                    yield return opcode.Name;
                }

                offset += OperandSize(opcode, il, offset);
            }
        }

        private static int OperandSize(OpCode opcode, byte[] il, int offset)
        {
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return 4 + 4 * BitConverter.ToInt32(il, offset);
                default:
                    return 4;
            }
        }
    }
}
