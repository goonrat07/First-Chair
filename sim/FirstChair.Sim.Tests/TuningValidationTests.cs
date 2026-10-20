using System;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// A ride refuses tuning it cannot run on (Simulation.Validate), so a bad Unity-side Tuning fails at the start of
    /// a run instead of dividing by zero or hanging in the air partway through it.
    /// </summary>
    public class TuningValidationTests
    {
        private static readonly Fix64 TooLong = Fix64.FromInt(int.MaxValue / Simulation.StepsPerSecond);

        [Test]
        public void NewRun_DefaultTuning_IsAccepted()
        {
            // Arrange and Act and Assert
            Assert.That(() => Start(Tuning.Default), Throws.Nothing);
        }

        [TestCase("Gravity zero")]
        [TestCase("Gravity negative")]
        [TestCase("MaxEdgeAngle zero")]
        [TestCase("PopLoadTime zero")]
        [TestCase("PopLoadTime too long")]
        [TestCase("CrashResetDelay negative")]
        [TestCase("CrashResetDelay too long")]
        [TestCase("TakeoffDropSpeed negative")]
        [TestCase("CleanLandingAngle negative")]
        [TestCase("Solid below Clean")]
        [TestCase("Sketchy below Solid")]
        [TestCase("GrabSteeringThreshold zero")]
        [TestCase("GrabSteeringThreshold above one")]
        [TestCase("FlowTrickBonus above 1000")]
        public void NewRun_TuningARideCannotRunOn_ThrowsArgumentException(string badValue)
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            switch (badValue)
            {
                case "Gravity zero": tuning.Gravity = Fix64.Zero; break;
                case "Gravity negative": tuning.Gravity = -Tuning.Default.Gravity; break;
                case "MaxEdgeAngle zero": tuning.MaxEdgeAngle = Fix64.Zero; break;
                case "PopLoadTime zero": tuning.PopLoadTime = Fix64.Zero; break;
                case "PopLoadTime too long": tuning.PopLoadTime = TooLong; break;
                case "CrashResetDelay negative": tuning.CrashResetDelay = -Fix64.One; break;
                case "CrashResetDelay too long": tuning.CrashResetDelay = TooLong; break;
                case "TakeoffDropSpeed negative": tuning.TakeoffDropSpeed = -Fix64.One; break;
                case "CleanLandingAngle negative": tuning.CleanLandingAngle = -Tuning.Default.CleanLandingAngle; break;
                case "Solid below Clean": tuning.SolidLandingAngle = tuning.CleanLandingAngle - Fix64.FromRaw(1); break;
                case "Sketchy below Solid": tuning.SketchyLandingAngle = tuning.SolidLandingAngle - Fix64.FromRaw(1); break;
                case "GrabSteeringThreshold zero": tuning.GrabSteeringThreshold = Fix64.Zero; break;
                case "GrabSteeringThreshold above one": tuning.GrabSteeringThreshold = Fix64.One + Fix64.FromRaw(1); break;
                case "FlowTrickBonus above 1000": tuning.FlowTrickBonus = Fix64.FromInt(1000) + Fix64.FromRaw(1); break;
                default: throw new ArgumentOutOfRangeException(nameof(badValue), badValue, "no such case");
            }

            // Act and Assert
            Assert.That(() => Start(tuning), Throws.ArgumentException);
        }

        [Test]
        public void NewRun_StartInsideATrunk_ThrowsArgumentException()
        {
            // Arrange: a tree whose trunk covers the start; the reset spot would be the start, so it would crash forever.
            var forest = new Forest(new[] { new SlopeTree(StartMm + 100, StartMm, 400, 2_000) });

            // Act and Assert
            Assert.That(() => Start(Tuning.Default, forest), Throws.ArgumentException);
        }

        [Test]
        public void NewRun_StartJustOutsideTheTrunkAndRider_IsAccepted()
        {
            // Arrange: the gap between the start and the trunk centre is exactly trunk radius plus rider radius.
            Tuning tuning = Tuning.Default;
            int reachMm = 400 + (int)(tuning.RiderRadius.Raw * 1000 >> Fix64.FractionalBits);
            var forest = new Forest(new[] { new SlopeTree(StartMm + reachMm, StartMm, 400, 2_000) });

            // Act and Assert: inside the branches is fine; only the trunk matters.
            Assert.That(() => Start(tuning, forest), Throws.Nothing);
        }

        [Test]
        public void ToString_MinimumValue_PrintsWithoutOverflow()
        {
            // Arrange
            Fix64 minimum = Fix64.FromRaw(long.MinValue);

            // Act
            string text = minimum.ToString();

            // Assert: -2^31 exactly.
            Assert.That(text, Is.EqualTo("-2147483648.000000000"));
        }

        private const int StartMm = 64_000;

        private static Simulation Start(Tuning tuning) => Start(tuning, Forest.Empty);

        private static Simulation Start(Tuning tuning, Forest forest)
        {
            Heightmap heightmap = TestTerrain.Flat();
            return new Simulation(
                new RunSetup(Sport.Skis, 0, 1UL, Fix64.FromRatio(StartMm, 1000), Fix64.FromRatio(StartMm, 1000)),
                heightmap,
                TestTerrain.Uniform(heightmap, SnowType.Groomed),
                forest,
                tuning);
        }
    }
}
