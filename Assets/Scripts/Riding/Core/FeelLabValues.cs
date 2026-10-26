using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// The Feel Lab's values (Story 009): every riding number in <see cref="Tuning"/> by name, read and written as plain
    /// numbers, and the "name = value" text the lab copies out and loads back. Engine-free, so it is testable without
    /// Unity; the panel itself is <c>FeelLab</c>.
    /// </summary>
    /// <remarks>
    /// The lab is a tuning tool, not part of a ride: a ride started with changed numbers records them in its own setup, so
    /// its replay still checks out.
    /// </remarks>
    /// <example>
    /// <code>
    /// Tuning tuning = FeelLabValues.With(Tuning.Default, "TurnRateLow", 2.5);
    /// string copied = FeelLabValues.Format(new[] { new KeyValuePair&lt;string, double&gt;("Tuning.TurnRateLow", 2.5) });
    /// double back = FeelLabValues.Parse(copied)["Tuning.TurnRateLow"]; // 2.5
    /// </code>
    /// </example>
    public static class FeelLabValues
    {
        private const double RawPerUnit = 4294967296.0;

        private static readonly FieldInfo[] TuningFields = Array.FindAll(
            typeof(Tuning).GetFields(BindingFlags.Public | BindingFlags.Instance), field => field.FieldType == typeof(Fix64));

        private static readonly string[] Names = Array.ConvertAll(TuningFields, field => field.Name);

        /// <summary>The riding numbers' names, in declaration order.</summary>
        public static IReadOnlyList<string> TuningNames() => Names;

        /// <summary>A riding number as a plain number.</summary>
        /// <param name="tuning">The tuning to read.</param>
        /// <param name="name">A name from <see cref="TuningNames"/>.</param>
        /// <exception cref="ArgumentException">No riding number has that name.</exception>
        public static double Get(in Tuning tuning, string name) => ((Fix64)Field(name).GetValue(tuning)).Raw / RawPerUnit;

        /// <summary>A copy of <paramref name="tuning"/> with one riding number set, to the nearest step of the sim's fixed point.</summary>
        /// <param name="tuning">The tuning to copy.</param>
        /// <param name="name">A name from <see cref="TuningNames"/>.</param>
        /// <param name="value">The new value; beyond the fixed point's range it is clamped to it.</param>
        /// <exception cref="ArgumentException">No riding number has that name.</exception>
        public static Tuning With(Tuning tuning, string name, double value)
        {
            FieldInfo field = Field(name);
            double raw = Math.Round(value * RawPerUnit);
            long clamped = double.IsNaN(raw) ? 0L : raw >= long.MaxValue ? long.MaxValue : raw <= long.MinValue ? long.MinValue : (long)raw;
            object boxed = tuning;
            field.SetValue(boxed, Fix64.FromRaw(clamped));
            return (Tuning)boxed;
        }

        /// <summary>"name = value" lines, one per value, in invariant culture.</summary>
        /// <param name="values">The values, in the order to write them.</param>
        public static string Format(IEnumerable<KeyValuePair<string, double>> values)
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, double> value in values)
            {
                text.Append(value.Key).Append(" = ").Append(value.Value.ToString("0.######", CultureInfo.InvariantCulture)).Append('\n');
            }

            return text.ToString();
        }

        /// <summary>Reads "name = value" lines; blank lines, lines starting with '#' and lines that don't parse are skipped.</summary>
        /// <param name="text">The text, as <see cref="Format"/> writes it.</param>
        public static Dictionary<string, double> Parse(string text)
        {
            var values = new Dictionary<string, double>();
            if (string.IsNullOrEmpty(text))
            {
                return values;
            }

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int equals = line.IndexOf('=');
                if (line.Length == 0 || line[0] == '#' || equals <= 0)
                {
                    continue;
                }

                string name = line.Substring(0, equals).Trim();
                if (name.Length > 0 && double.TryParse(line.Substring(equals + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    values[name] = value;
                }
            }

            return values;
        }

        /// <summary>A field name as words: "lowestRiderOnScreen" becomes "Lowest rider on screen".</summary>
        /// <param name="name">A camelCase or PascalCase name.</param>
        public static string Label(string name)
        {
            var label = new StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i == 0)
                {
                    label.Append(char.ToUpperInvariant(c));
                }
                else if (char.IsUpper(c))
                {
                    label.Append(' ').Append(char.ToLowerInvariant(c));
                }
                else
                {
                    label.Append(c);
                }
            }

            return label.ToString();
        }

        private static FieldInfo Field(string name)
        {
            foreach (FieldInfo field in TuningFields)
            {
                if (field.Name == name)
                {
                    return field;
                }
            }

            throw new ArgumentException("Tuning has no riding number " + name + ".", nameof(name));
        }
    }
}
