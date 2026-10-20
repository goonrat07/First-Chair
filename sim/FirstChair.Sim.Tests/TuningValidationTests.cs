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
                default: throw new ArgumentOutOfRangeException(nameof(badValue), badValue, "no such case");
            }

            // Act and Assert
            Assert.That(() => Start(tuning), Throws.ArgumentException);
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

        private static Simulation Start(Tuning tuning)
        {
            Heightmap heightmap = TestTerrain.Flat();
            return new Simulation(
                new RunSetup(Sport.Skis, 0, 1UL, Fix64.FromInt(64), Fix64.FromInt(64)),
                heightmap,
                TestTerrain.Uniform(heightmap, SnowType.Groomed),
                tuning);
        }
    }
}
