using FirstChair.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FirstChair.Riding
{
    /// <summary>
    /// A small test HUD in the top-right corner, in GMA2's style (white, bold, a soft shadow): speed in km/h, the slope's
    /// steepness under the rider in degrees, the snow type and the run score. It helps judge the riding feel while tuning.
    /// F1 hides it, and it hides itself while the sim is paused (on the chairlift, where it would show the last speed).
    /// Read only.
    /// </summary>
    public sealed class RiderHud : MonoBehaviour
    {
        [SerializeField] private SimDriver driver;
        [SerializeField] private bool visible = true;

        private GUIStyle _style;
        private GUIStyle _shadow;
        private int _speed = int.MinValue;
        private int _slope = int.MinValue;
        private long _score = long.MinValue;
        private SnowType _snow = (SnowType)255;
        private string _text = string.Empty;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible || driver == null || driver.Session == null || driver.Paused)
            {
                return;
            }

            RefreshText();
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
                _shadow = new GUIStyle(_style);
                _style.normal.textColor = Color.white;
                _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.45f);
            }

            int size = Mathf.Max(14, Screen.height / 36);
            _style.fontSize = size;
            _shadow.fontSize = size;
            var area = new Rect(Screen.width - 420, 12, 400, size * 6);
            GUI.Label(new Rect(area.x + 2, area.y + 2, area.width, area.height), _text, _shadow);
            GUI.Label(area, _text, _style);
        }

        private void RefreshText()
        {
            RideSession session = driver.Session;
            RiderPose pose = driver.Pose;
            float metresPerSecond = new Vector3(pose.VelocityX, pose.VelocityY, pose.VelocityZ).magnitude;
            int speed = Mathf.RoundToInt(metresPerSecond * 3.6f);

            RiderState current = session.Current;
            session.Slope.Heightmap.Sample(current.Position.X, current.Position.Z, out _, out Vec3 normal);
            int slope = Mathf.RoundToInt(Mathf.Acos(Mathf.Clamp01(SimCoordinates.ToFloat(normal.Y))) * Mathf.Rad2Deg);
            long score = session.Score.Total;
            SnowType snow = current.Snow;
            if (speed == _speed && slope == _slope && score == _score && snow == _snow)
            {
                return;
            }

            _speed = speed;
            _slope = slope;
            _score = score;
            _snow = snow;
            _text = speed + " KM/H\n" + slope + "° SLOPE\n" + snow.ToString().ToUpperInvariant() + "\n" + score + " PTS";
        }
    }
}
