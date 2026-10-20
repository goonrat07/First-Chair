using System.Collections.Generic;

namespace FirstChair.Sim
{
    /// <summary>
    /// A ride as data: a header (the <see cref="RunSetup"/> plus tuning, heightmap, snow map, forest and rail hashes)
    /// followed by one packed input per step. Replaying it with the same heightmap, snow map, forest, rails and tuning
    /// rebuilds the ride bit for bit.
    /// </summary>
    /// <remarks>
    /// <para><b>Byte layout</b> (all little-endian, <see cref="HeaderSize"/> = 78 bytes of header):</para>
    /// <list type="table">
    /// <item><term>0, 4 bytes</term><description>magic "FCRC"</description></item>
    /// <item><term>4, 2</term><description>format version (<see cref="FormatVersion"/>)</description></item>
    /// <item><term>6, 2</term><description>sim version; stays at this offset in every format version</description></item>
    /// <item><term>8, 1</term><description>sport</description></item>
    /// <item><term>9, 1</term><description>assist level</description></item>
    /// <item><term>10, 8</term><description>seed</description></item>
    /// <item><term>18, 8</term><description>start X, raw Q32.32</description></item>
    /// <item><term>26, 8</term><description>start Z, raw Q32.32</description></item>
    /// <item><term>34, 8</term><description>tuning hash</description></item>
    /// <item><term>42, 8</term><description>heightmap hash</description></item>
    /// <item><term>50, 8</term><description>snow map hash (added in format version 2)</description></item>
    /// <item><term>58, 8</term><description>forest hash (added in format version 3)</description></item>
    /// <item><term>66, 8</term><description>rail set hash (added in format version 4)</description></item>
    /// <item><term>74, 4</term><description>input count</description></item>
    /// <item><term>78, 4 each</term><description>packed inputs, one per step</description></item>
    /// </list>
    /// <para>The format version describes this container; the sim version describes riding behaviour.
    /// A recording with another sim version still reads, and the <see cref="Replayer"/> refuses it. A recording
    /// in another format version does not read.</para>
    /// <para>Not thread-safe.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var recording = new Recording(setup, heightmap, snowMap, forest, tuning);
    /// recording.Append(input);              // before each simulation.Step(input)
    /// byte[] bytes = recording.ToBytes();   // the game layer saves them
    /// if (Recording.TryRead(bytes, out Recording loaded))
    /// {
    ///     ReplayResult result = Replayer.Replay(loaded, heightmap, snowMap, forest, tuning);
    /// }
    /// </code>
    /// </example>
    public sealed class Recording
    {
        /// <summary>Version of this byte layout.</summary>
        public const ushort FormatVersion = 4;

        /// <summary>Header size in bytes; the inputs start here.</summary>
        public const int HeaderSize = 78;

        private const uint MagicValue = 0x43524346u; // "FCRC" read as a little-endian uint32
        private const int InputSize = 4;

        private readonly List<SimInput> _inputs;

        /// <summary>Starts an empty recording of a run with no trees (<see cref="Forest.Empty"/>).</summary>
        /// <param name="setup">The run setup, copied into the header.</param>
        /// <param name="heightmap">The terrain; its hash goes in the header.</param>
        /// <param name="snowMap">The snow types; their hash goes in the header.</param>
        /// <param name="tuning">The tuning; its hash goes in the header.</param>
        public Recording(RunSetup setup, Heightmap heightmap, SnowMap snowMap, Tuning tuning)
            : this(setup, heightmap, snowMap, Forest.Empty, tuning)
        {
        }

        /// <summary>Starts an empty recording of a run among trees.</summary>
        /// <param name="setup">The run setup, copied into the header.</param>
        /// <param name="heightmap">The terrain; its hash goes in the header.</param>
        /// <param name="snowMap">The snow types; their hash goes in the header.</param>
        /// <param name="forest">The trees; their hash goes in the header.</param>
        /// <param name="tuning">The tuning; its hash goes in the header.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="forest"/> is null.</exception>
        public Recording(RunSetup setup, Heightmap heightmap, SnowMap snowMap, Forest forest, Tuning tuning)
            : this(setup, heightmap, snowMap, forest, RailSet.Empty, tuning)
        {
        }

        /// <summary>Starts an empty recording of a run among trees, rails and boxes.</summary>
        /// <param name="setup">The run setup, copied into the header.</param>
        /// <param name="heightmap">The terrain; its hash goes in the header.</param>
        /// <param name="snowMap">The snow types; their hash goes in the header.</param>
        /// <param name="forest">The trees; their hash goes in the header.</param>
        /// <param name="rails">The rails and boxes; their hash goes in the header.</param>
        /// <param name="tuning">The tuning; its hash goes in the header.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="forest"/> or <paramref name="rails"/> is null.</exception>
        public Recording(RunSetup setup, Heightmap heightmap, SnowMap snowMap, Forest forest, RailSet rails, Tuning tuning)
            : this(
                setup,
                tuning.ComputeHash(),
                heightmap.Hash,
                snowMap.Hash,
                (forest ?? throw new System.ArgumentNullException(nameof(forest))).Hash,
                (rails ?? throw new System.ArgumentNullException(nameof(rails))).Hash,
                new List<SimInput>())
        {
        }

        private Recording(
            RunSetup setup, ulong tuningHash, ulong heightmapHash, ulong snowMapHash, ulong forestHash, ulong railsHash, List<SimInput> inputs)
        {
            RailsHash = railsHash;
            Setup = setup;
            TuningHash = tuningHash;
            HeightmapHash = heightmapHash;
            SnowMapHash = snowMapHash;
            ForestHash = forestHash;
            _inputs = inputs;
        }

        /// <summary>The run setup from the header.</summary>
        public RunSetup Setup { get; }

        /// <summary>Hash of the tuning the ride was made with.</summary>
        public ulong TuningHash { get; }

        /// <summary>Hash of the heightmap the ride was made with.</summary>
        public ulong HeightmapHash { get; }

        /// <summary>Hash of the snow map the ride was made with.</summary>
        public ulong SnowMapHash { get; }

        /// <summary>Hash of the trees the ride was made among (<see cref="Forest.Hash"/>).</summary>
        public ulong ForestHash { get; }

        /// <summary>Hash of the rails and boxes the ride was made on (<see cref="RailSet.Hash"/>).</summary>
        public ulong RailsHash { get; }

        /// <summary>The recorded inputs, one per step.</summary>
        public IReadOnlyList<SimInput> Inputs => _inputs;

        /// <summary>Appends one step's input. Call it with the same input passed to <see cref="Simulation.Step"/>.</summary>
        /// <param name="input">The step's input.</param>
        public void Append(SimInput input) => _inputs.Add(input);

        /// <summary>Serializes the header and inputs to bytes.</summary>
        public byte[] ToBytes()
        {
            var bytes = new byte[HeaderSize + InputSize * _inputs.Count];
            int offset = 0;
            Write(bytes, ref offset, MagicValue, 4);
            Write(bytes, ref offset, FormatVersion, 2);
            Write(bytes, ref offset, Setup.SimVersion, 2);
            Write(bytes, ref offset, (byte)Setup.Sport, 1);
            Write(bytes, ref offset, Setup.AssistLevel, 1);
            Write(bytes, ref offset, Setup.Seed, 8);
            Write(bytes, ref offset, unchecked((ulong)Setup.StartX.Raw), 8);
            Write(bytes, ref offset, unchecked((ulong)Setup.StartZ.Raw), 8);
            Write(bytes, ref offset, TuningHash, 8);
            Write(bytes, ref offset, HeightmapHash, 8);
            Write(bytes, ref offset, SnowMapHash, 8);
            Write(bytes, ref offset, ForestHash, 8);
            Write(bytes, ref offset, RailsHash, 8);
            Write(bytes, ref offset, (uint)_inputs.Count, 4);
            for (int i = 0; i < _inputs.Count; i++)
            {
                Write(bytes, ref offset, _inputs[i].Packed, InputSize);
            }

            return bytes;
        }

        /// <summary>
        /// Reads a recording. Returns false for null data, a wrong magic, an unknown format version, or a
        /// length that does not match the input count. A different sim version still reads.
        /// </summary>
        /// <param name="data">Bytes written by <see cref="ToBytes"/>.</param>
        /// <param name="recording">The recording, or null when the method returns false.</param>
        public static bool TryRead(byte[] data, out Recording recording)
        {
            recording = null;
            if (data == null || data.Length < HeaderSize)
            {
                return false;
            }

            int offset = 0;
            if (Read(data, ref offset, 4) != MagicValue || Read(data, ref offset, 2) != FormatVersion)
            {
                return false;
            }

            var simVersion = (ushort)Read(data, ref offset, 2);
            var sport = (Sport)Read(data, ref offset, 1);
            var assistLevel = (byte)Read(data, ref offset, 1);
            ulong seed = Read(data, ref offset, 8);
            long startX = unchecked((long)Read(data, ref offset, 8));
            long startZ = unchecked((long)Read(data, ref offset, 8));
            ulong tuningHash = Read(data, ref offset, 8);
            ulong heightmapHash = Read(data, ref offset, 8);
            ulong snowMapHash = Read(data, ref offset, 8);
            ulong forestHash = Read(data, ref offset, 8);
            ulong railsHash = Read(data, ref offset, 8);
            ulong count = Read(data, ref offset, 4);
            if ((ulong)data.Length != HeaderSize + InputSize * count)
            {
                return false;
            }

            var inputs = new List<SimInput>((int)count);
            for (ulong i = 0; i < count; i++)
            {
                inputs.Add(SimInput.FromPacked((uint)Read(data, ref offset, InputSize)));
            }

            var setup = new RunSetup(simVersion, sport, assistLevel, seed, Fix64.FromRaw(startX), Fix64.FromRaw(startZ));
            recording = new Recording(setup, tuningHash, heightmapHash, snowMapHash, forestHash, railsHash, inputs);
            return true;
        }

        private static void Write(byte[] buffer, ref int offset, ulong value, int byteCount)
        {
            for (int i = 0; i < byteCount; i++)
            {
                buffer[offset++] = unchecked((byte)(value >> (8 * i)));
            }
        }

        private static ulong Read(byte[] data, ref int offset, int byteCount)
        {
            ulong value = 0UL;
            for (int i = 0; i < byteCount; i++)
            {
                value |= (ulong)data[offset++] << (8 * i);
            }

            return value;
        }
    }
}
