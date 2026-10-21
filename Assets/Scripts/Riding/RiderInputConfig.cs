using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>Input thresholds as data. The sim's own riding numbers stay in its Tuning; these only shape how
    /// controls become the packed sim input.</summary>
    [CreateAssetMenu(menuName = "First Chair/Rider Input Config", fileName = "RiderInputConfig")]
    public sealed class RiderInputConfig : ScriptableObject
    {
        [Tooltip("Stick deflection, 0 to under 1, that still counts as straight.")]
        [SerializeField, Range(0f, 0.9f)] private float steeringDeadzone = 0.15f;

        [Tooltip("Stick or trigger travel, 0 to 1, at which tuck or brake counts as pressed.")]
        [SerializeField, Range(0.05f, 1f)] private float axisPressThreshold = 0.5f;

        /// <summary>The settings for <see cref="RiderInputQuantizer"/>.</summary>
        public RiderInputQuantizerSettings ToSettings() => new RiderInputQuantizerSettings(steeringDeadzone, axisPressThreshold);
    }
}
