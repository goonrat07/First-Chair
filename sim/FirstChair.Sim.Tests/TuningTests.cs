using System.Reflection;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    public class TuningTests
    {
        [Test]
        public void ComputeHash_AnyFieldChanged_ChangesTheHash()
        {
            // Arrange: every riding number must be a public Fix64 field that the hash covers.
            FieldInfo[] fields = typeof(Tuning).GetFields(BindingFlags.Public | BindingFlags.Instance);
            FieldInfo[] hidden = typeof(Tuning).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            ulong baseline = Tuning.Default.ComputeHash();

            // Act and Assert
            Assert.That(hidden, Is.Empty, "Tuning must hold no private data the hash could miss.");
            Assert.That(fields, Is.Not.Empty);
            foreach (FieldInfo field in fields)
            {
                Assert.That(field.FieldType, Is.EqualTo(typeof(Fix64)), field.Name + " must be a Fix64.");
                object boxed = Tuning.Default;
                var value = (Fix64)field.GetValue(boxed);
                field.SetValue(boxed, Fix64.FromRaw(value.Raw + 1));
                ulong changed = ((Tuning)boxed).ComputeHash();
                Assert.That(changed, Is.Not.EqualTo(baseline), field.Name + " is not covered by ComputeHash.");
            }
        }
    }
}
