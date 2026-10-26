using FirstChair.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstChair.Riding
{
    /// <summary>
    /// Snow thrown by the skis (Story 008): quick turns, drifts and skids throw it out of the turn, a brake throws it ahead,
    /// and a hard, fast carve throws a little (Story 009), so the kinds of turn read apart on screen. How many flakes comes from
    /// <see cref="SnowSprayMath"/>; where they go, from the rider's velocity across the skis. Presentation only: it reads
    /// sim state and never writes it.
    /// </summary>
    /// <remarks>
    /// Runs after the skier view (execution order 60 against its 50), so the rider's transform is already placed on the
    /// snow for this frame: its forward is the skis' heading on the slope.
    /// </remarks>
    [DefaultExecutionOrder(60)]
    public sealed class SnowSpray : MonoBehaviour
    {
        [SerializeField] private SimDriver driver;
        [SerializeField] private RiderViewSettings settings;

        [Tooltip("Material for the flakes: URP Particles/Unlit with a round, alpha-clipped flake.")]
        [SerializeField] private Material material;

        private ParticleSystem _particles;
        private float _owed;
        private bool _reported;

        private void Awake()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RiderViewSettings>();
            }

            var host = new GameObject("SnowSpray");
            host.transform.SetParent(transform, false);
            _particles = host.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = settings.SprayMaxParticles;
            main.gravityModifier = settings.SprayGravity;
            // Each flake a colour between snow in shade and in sun: unlit white alone tonemaps to the sunlit snow's own
            // brightness and vanishes against it.
            main.startColor = new ParticleSystem.MinMaxGradient(settings.SprayShadeColor, settings.SprayLightColor);
            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;

            // Flakes shrink away as they fade (the material clips alpha, so the fade reads as a shrink) and grow a little
            // as they spread.
            ParticleSystem.ColorOverLifetimeModule color = _particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));

            var particleRenderer = host.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            if (material != null)
            {
                particleRenderer.sharedMaterial = material;
            }
            else
            {
                Debug.LogWarning("SnowSpray: no flake material; the spray uses Unity's default particle material.", this);
            }

            _particles.Play();
        }

        private void LateUpdate()
        {
            if (driver == null || driver.Session == null || _particles == null)
            {
                return;
            }

            RiderState state = driver.Session.Current;
            Vector3 velocity = SimCoordinatesUnity.ToVector3(state.Velocity);
            float speed = velocity.magnitude;
            float factor = SnowSprayMath.SnowFactor(state.Snow, settings.SprayPowderFactor, settings.SprayIceFactor);
            bool onSnow = !state.Airborne && !state.Crashed && !driver.Paused;
            double maxEdge = SimCoordinates.ToDouble(driver.Session.Tuning.MaxEdgeAngle);
            double edge = maxEdge > 0.0 ? SimCoordinates.ToDouble(state.EdgeAngle) / maxEdge : 0.0;
            float rate = Mathf.Min(settings.SprayMaxRate,
                SnowSprayMath.Rate(SimCoordinates.ToDouble(state.SlipAngle), speed, state.Stance == Stance.Brake, onSnow, factor,
                    settings.SprayPerSlideMetre, settings.SprayPerBrakeMetre, settings.SprayMaxRate)
                + SnowSprayMath.CarveRate(edge, speed, onSnow, factor, settings.SprayPerCarveMetre, settings.SprayCarveEdge,
                    settings.SprayCarveMinSpeed));
            if (rate <= 0f)
            {
                _owed = 0f;
                return;
            }

            _owed += rate * Time.deltaTime;
            int count = Mathf.FloorToInt(_owed);
            if (count <= 0)
            {
                return;
            }

            _owed -= count;
            Transform rider = transform;
            Vector3 forward = rider.forward;
            Vector3 up = rider.up;

            // The skis push the snow the way they slide: across them in a skid, ahead in a straight brake.
            Vector3 slide = velocity - forward * Vector3.Dot(velocity, forward);
            Vector3 thrown = slide.sqrMagnitude > 0.01f ? slide.normalized : (speed > 0.1f ? velocity / speed : forward);
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                emit.position = rider.position - forward * (settings.SprayBehind * Random.value)
                    + rider.right * ((Random.value - 0.5f) * settings.SprayWidth) + up * settings.SprayRise;
                emit.velocity = velocity * settings.SprayCarry
                    + thrown * (settings.SprayThrow * (0.5f + Random.value))
                    + up * (settings.SprayLift * (0.5f + Random.value));
                emit.startSize = settings.SpraySize * (0.6f + 0.8f * Random.value);
                emit.startLifetime = settings.SprayLifetime * (0.7f + 0.6f * Random.value);
                _particles.Emit(emit, 1);
            }

            if (!_reported)
            {
                // One line per session, so a run's log shows the spray working (a player built for CI can't be watched).
                _reported = true;
                Debug.Log("SnowSpray: throwing snow, " + _particles.particleCount + " flakes alive after the first " + count + ".");
            }
        }
    }
}
