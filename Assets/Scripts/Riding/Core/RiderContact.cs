using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// Whether the rider's skis are on the snow, for everything the view draws where skis touch snow: the track ribbons,
    /// the skis' footprints and the trenches they cut (Story 018), and the spray (Story 008). Not in the air, not
    /// crashed, not on the chairlift, and not grinding a rail or box, where the skis touch the feature and no snow.
    /// Engine-free and pure, so any thread may call it.
    /// </summary>
    /// <example>
    /// <code>
    /// bool onSnow = RiderContact.OnSnow(session.Current, driver.Paused);
    /// track.Paint(skiBase, tip, velocity, snowPoint, snowNormal, onSnow, jumped);
    /// </code>
    /// </example>
    public static class RiderContact
    {
        /// <summary>True while the skis ride on the snow.</summary>
        /// <param name="state">The rider's state this step.</param>
        /// <param name="paused">True while the sim is paused (riding the chairlift).</param>
        /// <returns>False in the air, crashed, grinding a rail or box, or paused.</returns>
        public static bool OnSnow(in RiderState state, bool paused) =>
            !state.Airborne && !state.Crashed && !state.Grinding && !paused;
    }
}
