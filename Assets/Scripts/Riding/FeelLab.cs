using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using FirstChair.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FirstChair.Riding
{
    /// <summary>
    /// The Feel Lab (Story 009): a tuning panel over the game, opened with the backtick key or F1, as the user's Bluebird
    /// prototype had. It edits every riding number (applied by restarting the run from where the rider is), every follow
    /// camera and view setting (live), switches camera presets, graphs the speed and turn rate of the last six seconds, and
    /// copies or saves the changed numbers as text to paste back. A tuning tool: it never writes rider state, and a run
    /// restarted with new numbers records them, so its replay still checks out.
    /// </summary>
    /// <remarks>
    /// It installs itself after the scene loads wherever a <see cref="SimDriver"/> rides, and finds the camera's and
    /// view's settings assets on their components. Drawn with IMGUI, like the HUD; it allocates only while open.
    /// </remarks>
    public sealed class FeelLab : MonoBehaviour
    {
        private const int GraphSamples = 360;
        private const int GraphWidth = 360;
        private const int GraphHeight = 110;
        private const float SpeedScaleKmh = 150f;
        private const float TurnScaleDegrees = 180f;
        private const string SaveFileName = "feel-lab.txt";

        private static readonly string[] Tabs = { "Riding", "Camera", "Effects", "Light", "Controls" };

        // How far above the lift's boarding point "Go to the lift" starts the rider, metres: just outside the boarding
        // area (LiftRide's 9 m), on the nearly flat snow round the station, so a short push takes the rider in.
        private const float LiftApproachMetres = 12f;

        // How far above the first kicker's foot "Go to the park" starts the rider, metres.
        private const double ParkApproachMetres = 40.0;

        // The scenes in the build to ride (Story 010), by scene name.
        private static readonly string[] Places = { "TestSlope", "TestMountain" };

        // The old game's chase camera (Bluebird src/render/scene.ts): close behind and low, a 62 degree view widening with
        // speed, quick to follow the travel, banking and shaking more.
        private static readonly Dictionary<string, float> ChasePreset = new Dictionary<string, float>
        {
            { "pitchAboveSlopeDegrees", 12f }, { "minPitchDegrees", 8f }, { "maxPitchDegrees", 60f }, { "pitchSmoothTime", 0.5f },
            { "distance", 12f }, { "aimHeight", 0.6f }, { "travelWeight", 1f }, { "minTravelSpeed", 1.5f },
            { "yawSmoothTime", 0.25f }, { "lookAheadBase", 2f }, { "lookAheadPerSpeed", 0.27f }, { "lookAheadMax", 9f },
            { "lowestRiderOnScreen", 0.9f }, { "focalLengthMm", 20f }, { "driftMetres", 0f }, { "positionSmoothTime", 0.14f },
            { "fovPerSpeed", 0.35f }, { "fovSpeedMax", 16f }, { "rollPerG", 2f }, { "rollMax", 4f },
            { "shakeFromSpeed", 18f }, { "shakeFullSpeed", 40f }, { "shakeDegrees", 0.3f },
        };

        private static readonly string[] ControlsText =
        {
            "A / D or arrows: turn (carve, keeps speed)",
            "Shift + turn: drift turn (tighter; a boost when you let go)",
            "Ctrl or Alt + turn: quick turn (sharpest, costs speed)",
            "S: brake.  S + turn: hockey stop",
            "W: tuck (turns less sharply); poles on flats",
            "Left, right, left on the flat: skate",
            "Space: hold to load, release to pop",
            "In the air: A / D spin, W / S flip, Shift + direction grab",
            "R: back to the last safe spot.  F5: replay check.  F6: save the ride",
            "Test mountain: ski into the lift's bottom station to ride up; W hurries the lift",
            "` or F1: this panel",
        };

        private readonly List<SettingField> _cameraFields = new List<SettingField>();
        private readonly List<SettingField> _viewFields = new List<SettingField>();
        private readonly List<SettingField> _lightFields = new List<SettingField>();
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>();
        private readonly float[] _speeds = new float[GraphSamples];
        private readonly float[] _turns = new float[GraphSamples];
        private SimDriver _driver;
        private Tuning _pending = Tuning.Default;
        private bool _open;
        private int _tab;
        private Vector2 _scroll;
        private string _filter = string.Empty;
        private string _status = string.Empty;
        private int _sample;
        private float _lastPath;
        private bool _hasPath;
        private Texture2D _graph;
        private Color32[] _pixels;
        private GUIStyle _hint;

        /// <summary>True while one of the lab's text fields has the keyboard, so typing doesn't steer the rider.</summary>
        public static bool KeyboardCaptured { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // Every scene gets its own lab: the test slope and the test mountain (Story 010) each have a rider.
            SceneManager.sceneLoaded -= InstallIn;
            SceneManager.sceneLoaded += InstallIn;
            InstallHere();
        }

        private static void InstallIn(Scene scene, LoadSceneMode mode) => InstallHere();

        private static void InstallHere()
        {
            if (FindAnyObjectByType<FeelLab>() == null && FindAnyObjectByType<SimDriver>() != null)
            {
                new GameObject("FeelLab").AddComponent<FeelLab>();
            }
        }

        private void Start()
        {
            _driver = FindAnyObjectByType<SimDriver>();
            _pending = _driver != null ? _driver.RunTuning : Tuning.Default;
            Collect(SettingsOf(FindAnyObjectByType<FollowCamera>()), _cameraFields);
            Collect(SettingsOf(FindAnyObjectByType<SkierView>()), _viewFields);
            Collect(SettingsOf(FindAnyObjectByType<LightingRig>()), _lightFields);
            _pixels = new Color32[GraphWidth * GraphHeight];
            _graph = new Texture2D(GraphWidth, GraphHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        }

        private void OnDestroy()
        {
            KeyboardCaptured = false;
            if (_graph != null)
            {
                Destroy(_graph);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.backquoteKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame))
            {
                _open = !_open;
                if (!_open)
                {
                    KeyboardCaptured = false;
                }
            }

            Sample();
        }

        // One graph sample per frame: the speed and the path's turn rate.
        private void Sample()
        {
            if (_driver == null || _driver.Session == null)
            {
                return;
            }

            RiderState state = _driver.Session.Current;
            float vx = SimCoordinates.ToFloat(state.Velocity.X);
            float vz = SimCoordinates.ToFloat(state.Velocity.Z);
            float speed = Mathf.Sqrt(vx * vx + vz * vz + Sq(SimCoordinates.ToFloat(state.Velocity.Y)));
            float turn = 0f;
            if (speed > 0.5f && Time.deltaTime > 0f)
            {
                float path = Mathf.Atan2(vx, vz) * Mathf.Rad2Deg;
                if (_hasPath)
                {
                    turn = Mathf.DeltaAngle(_lastPath, path) / Time.deltaTime;
                }

                _lastPath = path;
                _hasPath = true;
            }
            else
            {
                _hasPath = false;
            }

            _speeds[_sample] = speed * 3.6f;
            _turns[_sample] = turn;
            _sample = (_sample + 1) % GraphSamples;
        }

        private void OnGUI()
        {
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (!_open)
            {
                KeyboardCaptured = false;
                if (_hint == null)
                {
                    _hint = new GUIStyle(GUI.skin.label) { fontSize = 15 };
                    _hint.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
                }

                GUI.Label(new Rect(14f, 1080f - 30f, 400f, 24f), "` or F1: Feel Lab and controls", _hint);
                return;
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 520f, 1060f), GUI.skin.box);
            GUILayout.Label("Feel Lab  (` or F1 closes)");
            _tab = GUILayout.Toolbar(_tab, Tabs);
            if (_tab == 0 || _tab == 1 || _tab == 2)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Filter", GUILayout.Width(40f));
                _filter = GUILayout.TextField(_filter, GUILayout.Width(200f));
                GUILayout.EndHorizontal();
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(640f));
            switch (_tab)
            {
                case 0:
                    RidingTab();
                    break;
                case 1:
                    SettingsTab(_cameraFields, "camera");
                    break;
                case 2:
                    SettingsTab(_viewFields, "view");
                    break;
                case 3:
                    SettingsTab(_lightFields, "light");
                    break;
                default:
                    foreach (string line in ControlsText)
                    {
                        GUILayout.Label(line);
                    }

                    // Story 010: where to ride.
                    GUILayout.Space(8f);
                    GUILayout.BeginHorizontal();
                    foreach (string place in Places)
                    {
                        if (GUILayout.Button("Ride the " + FeelLabValues.Label(place).ToLowerInvariant()))
                        {
                            SceneManager.LoadScene(place);
                        }
                    }

                    GUILayout.EndHorizontal();
                    ChairLift lift = FindAnyObjectByType<ChairLift>();
                    if (lift != null && _driver != null && GUILayout.Button("Go to the lift (start just above its bottom station)"))
                    {
                        // Uphill of the boarding point (the fall line runs toward +Z), facing it.
                        Vector3 start = lift.BoardingPoint - Vector3.forward * LiftApproachMetres;
                        _status = _driver.RestartRunAt(_driver.RunTuning, SimCoordinates.FromMetres(start.x), SimCoordinates.FromMetres(start.z))
                            ?? "Started by the lift: push (W) into the station to ride up.";
                    }

                    SlopeLayout layout = _driver != null && _driver.Session != null ? _driver.Session.Slope.Layout : null;
                    if (layout != null && SlopeSpots.AboveTheKickers(layout, ParkApproachMetres, out double parkX, out double parkZ)
                        && GUILayout.Button("Go to the park (start above its first kicker)"))
                    {
                        _status = _driver.RestartRunAt(_driver.RunTuning, SimCoordinates.FromMetres(parkX), SimCoordinates.FromMetres(parkZ))
                            ?? "Started above the park: let go to roll down the kicker line.";
                    }

                    break;
            }

            GUILayout.EndScrollView();
            Footer();
            GUILayout.EndArea();
            KeyboardCaptured = GUIUtility.keyboardControl != 0;
        }

        private void RidingTab()
        {
            GUILayout.Label("Riding numbers. Apply restarts the run here with them (at rest).");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply: restart here"))
            {
                Apply(true);
            }

            if (GUILayout.Button("Apply: restart at top"))
            {
                Apply(false);
            }

            if (GUILayout.Button("Defaults"))
            {
                _pending = Tuning.Default;
                _texts.Clear();
            }

            GUILayout.EndHorizontal();
            Tuning defaults = Tuning.Default;
            foreach (string name in FeelLabValues.TuningNames())
            {
                if (!Matches(name))
                {
                    continue;
                }

                double value = FeelLabValues.Get(_pending, name);
                bool changed = Math.Abs(value - FeelLabValues.Get(defaults, name)) > 1e-9;
                double edited = Row("Tuning." + name, FeelLabValues.Label(name), null, value, changed);
                if (edited != value)
                {
                    _pending = FeelLabValues.With(_pending, name, edited);
                }
            }
        }

        private void SettingsTab(List<SettingField> fields, string what)
        {
            if (fields.Count == 0)
            {
                GUILayout.Label("No " + what + " settings found in this scene.");
                return;
            }

            if (fields == _cameraFields)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Preset: GMA far (default)"))
                {
                    foreach (SettingField field in _cameraFields)
                    {
                        field.Set(field.Initial);
                    }

                    _texts.Clear();
                }

                if (GUILayout.Button("Preset: chase (old game)"))
                {
                    foreach (SettingField field in _cameraFields)
                    {
                        if (ChasePreset.TryGetValue(field.Field.Name, out float preset))
                        {
                            field.Set(preset);
                        }
                    }

                    _texts.Clear();
                }

                GUILayout.EndHorizontal();
                GUILayout.Label("Live. Speed effects: field of view, bank and shake.");
            }
            else if (fields == _lightFields)
            {
                GUILayout.Label("Live: the sun, the sky light and the camera's exposure and contrast.");
            }
            else
            {
                GUILayout.Label("Live, except spray size, lifetime, gravity and count: those take the next launch.");
            }

            string prefix = fields == _cameraFields ? "Camera." : fields == _lightFields ? "Light." : "View.";
            foreach (SettingField field in fields)
            {
                if (!Matches(field.Field.Name))
                {
                    continue;
                }

                float value = field.Get();
                double edited = Row(prefix + field.Field.Name, field.Label, field.Tooltip, value, Mathf.Abs(value - field.Initial) > 1e-6f);
                if (edited != value)
                {
                    field.Set((float)edited);
                }
            }
        }

        // One editable number: label, text box and nudge buttons; returns the value, edited or not.
        private double Row(string key, string label, string tooltip, double value, bool changed)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent((changed ? "* " : "  ") + label, tooltip), GUILayout.Width(290f));
            if (!_texts.TryGetValue(key, out string text) || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double shown)
                || Math.Abs(shown - value) > 1e-9 * Math.Max(1.0, Math.Abs(value)))
            {
                text = value.ToString("0.#####", CultureInfo.InvariantCulture);
            }

            string typed = GUILayout.TextField(text, GUILayout.Width(90f));
            _texts[key] = typed;
            double result = value;
            if (typed != text && double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                result = parsed;
            }

            if (GUILayout.Button("-", GUILayout.Width(28f)))
            {
                result = value == 0.0 ? -0.1 : value * 0.9;
                _texts.Remove(key);
            }

            if (GUILayout.Button("+", GUILayout.Width(28f)))
            {
                result = value == 0.0 ? 0.1 : value * 1.1;
                _texts.Remove(key);
            }

            GUILayout.EndHorizontal();
            return result;
        }

        private void Footer()
        {
            DrawGraph();
            GUILayout.Label("Last 6 s: speed (white, 0 to 150 km/h), turn rate (orange, -180 to 180 deg/s)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy values"))
            {
                GUIUtility.systemCopyBuffer = ChangedText();
                _status = "Copied the changed numbers: paste them to Claude.";
            }

            if (GUILayout.Button("Save"))
            {
                Save();
            }

            if (GUILayout.Button("Load saved"))
            {
                Load();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(_status);
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                GUILayout.Label(GUI.tooltip);
            }
        }

        private void DrawGraph()
        {
            if (Event.current.type == EventType.Repaint && _graph != null)
            {
                var background = new Color32(20, 24, 32, 200);
                for (int i = 0; i < _pixels.Length; i++)
                {
                    _pixels[i] = background;
                }

                var grid = new Color32(70, 80, 100, 255);
                Plot(GraphHeight / 2, grid);
                for (int x = 0; x < GraphWidth; x++)
                {
                    int index = (_sample + x) % GraphSamples;
                    Plot(x, Mathf.Clamp(Mathf.RoundToInt(_speeds[index] / SpeedScaleKmh * (GraphHeight - 1)), 0, GraphHeight - 1),
                        new Color32(255, 255, 255, 255));
                    float turn = Mathf.Clamp(_turns[index] / TurnScaleDegrees, -1f, 1f);
                    Plot(x, Mathf.RoundToInt((turn * 0.5f + 0.5f) * (GraphHeight - 1)), new Color32(255, 160, 40, 255));
                }

                _graph.SetPixels32(_pixels);
                _graph.Apply(false);
            }

            GUILayout.Box(_graph, GUILayout.Width(GraphWidth), GUILayout.Height(GraphHeight));
        }

        private void Plot(int row, Color32 colour)
        {
            for (int x = 0; x < GraphWidth; x++)
            {
                Plot(x, row, colour);
            }
        }

        private void Plot(int x, int y, Color32 colour) => _pixels[y * GraphWidth + x] = colour;

        private void Apply(bool here)
        {
            if (_driver == null)
            {
                _status = "No ride to restart.";
                return;
            }

            string error = _driver.RestartRun(_pending, here);
            _status = error == null ? (here ? "Restarted here with these numbers." : "Restarted at the top with these numbers.") : error;
        }

        // The numbers that differ from the defaults, as "name = value" lines.
        private string ChangedText()
        {
            var values = new List<KeyValuePair<string, double>>();
            Tuning running = _driver != null ? _driver.RunTuning : _pending;
            Tuning defaults = Tuning.Default;
            foreach (string name in FeelLabValues.TuningNames())
            {
                double value = FeelLabValues.Get(running, name);
                if (Math.Abs(value - FeelLabValues.Get(defaults, name)) > 1e-9)
                {
                    values.Add(new KeyValuePair<string, double>("Tuning." + name, value));
                }
            }

            AddChanged(values, _cameraFields, "Camera.");
            AddChanged(values, _viewFields, "View.");
            AddChanged(values, _lightFields, "Light.");
            return "# First Chair Feel Lab, sim version " + Simulation.Version + ", "
                + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n"
                + "# Numbers changed from the defaults; riding numbers are those of the running ride.\n"
                + FeelLabValues.Format(values);
        }

        private static void AddChanged(List<KeyValuePair<string, double>> values, List<SettingField> fields, string prefix)
        {
            foreach (SettingField field in fields)
            {
                float value = field.Get();
                if (Mathf.Abs(value - field.Initial) > 1e-6f)
                {
                    values.Add(new KeyValuePair<string, double>(prefix + field.Field.Name, value));
                }
            }
        }

        private void Save()
        {
            string text = ChangedText();
            string path = Path.Combine(Application.persistentDataPath, SaveFileName);
            try
            {
                File.WriteAllText(path, text);
                Debug.Log("FeelLab: saved to " + path + "\n" + text);
                _status = "Saved to " + path;
            }
            catch (Exception e)
            {
                _status = "Could not save: " + e.Message;
            }
        }

        private void Load()
        {
            string path = Path.Combine(Application.persistentDataPath, SaveFileName);
            if (!File.Exists(path))
            {
                _status = "Nothing saved yet at " + path;
                return;
            }

            Dictionary<string, double> values = FeelLabValues.Parse(File.ReadAllText(path));
            Tuning tuning = Tuning.Default;
            foreach (KeyValuePair<string, double> value in values)
            {
                if (value.Key.StartsWith("Tuning.", StringComparison.Ordinal))
                {
                    try
                    {
                        tuning = FeelLabValues.With(tuning, value.Key.Substring("Tuning.".Length), value.Value);
                    }
                    catch (ArgumentException)
                    {
                        // A number from another sim version: skip it.
                    }
                }
            }

            SetByName(values, _cameraFields, "Camera.");
            SetByName(values, _viewFields, "View.");
            SetByName(values, _lightFields, "Light.");
            _pending = tuning;
            _texts.Clear();
            Apply(true);
            _status = "Loaded " + values.Count + " numbers from " + path + ". " + _status;
        }

        private static void SetByName(Dictionary<string, double> values, List<SettingField> fields, string prefix)
        {
            foreach (SettingField field in fields)
            {
                if (values.TryGetValue(prefix + field.Field.Name, out double value))
                {
                    field.Set((float)value);
                }
            }
        }

        private bool Matches(string name) =>
            _filter.Length == 0 || name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;

        private static float Sq(float value) => value * value;

        // A component's settings asset, from its private "settings" field.
        private static ScriptableObject SettingsOf(Component component)
        {
            if (component == null)
            {
                return null;
            }

            FieldInfo field = component.GetType().GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(component) as ScriptableObject;
        }

        // Every serialized float or int on a settings asset, in declaration order.
        private static void Collect(ScriptableObject settings, List<SettingField> fields)
        {
            if (settings == null)
            {
                return;
            }

            foreach (FieldInfo field in settings.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if ((field.FieldType != typeof(float) && field.FieldType != typeof(int)) || field.GetCustomAttribute<SerializeField>() == null)
                {
                    continue;
                }

                var setting = new SettingField(settings, field);
                fields.Add(setting);
            }
        }

        /// <summary>One serialized float or int on a settings asset, with its tooltip and limits.</summary>
        private sealed class SettingField
        {
            private readonly ScriptableObject _owner;
            private readonly float _min = float.NegativeInfinity;
            private readonly float _max = float.PositiveInfinity;

            public SettingField(ScriptableObject owner, FieldInfo field)
            {
                _owner = owner;
                Field = field;
                Label = FeelLabValues.Label(field.Name);
                Tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
                RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();
                MinAttribute min = field.GetCustomAttribute<MinAttribute>();
                if (range != null)
                {
                    _min = range.min;
                    _max = range.max;
                }
                else if (min != null)
                {
                    _min = min.min;
                }

                Initial = Get();
            }

            public FieldInfo Field { get; }

            public string Label { get; }

            public string Tooltip { get; }

            public float Initial { get; }

            public float Get() => Field.FieldType == typeof(int) ? (int)Field.GetValue(_owner) : (float)Field.GetValue(_owner);

            public void Set(float value)
            {
                float clamped = Mathf.Clamp(value, _min, _max);
                if (Field.FieldType == typeof(int))
                {
                    Field.SetValue(_owner, Mathf.RoundToInt(clamped));
                }
                else
                {
                    Field.SetValue(_owner, clamped);
                }
            }
        }
    }
}
