using CargoSquad.Combat;
using NUnit.Framework;

namespace CargoSquad.Tests
{
    public sealed class WeaponSpreadTests
    {
        [TestCase(60, 7)] [TestCase(20, 1)] [TestCase(90, 7)]
        public void EverySampleStaysInsideConeAndOwnStratum(float angle, int count)
        {
            var random = new System.Random(402);
            for (int shot = 0; shot < 100; shot++)
                for (int pellet = 0; pellet < count; pellet++)
                {
                    float yaw = WeaponDefinition.SampleYaw(pellet, count, angle, SpreadPattern.Stratified, (float)random.NextDouble());
                    Assert.That(yaw, Is.InRange(-angle / 2, angle / 2));
                    if (count > 1) Assert.That(yaw, Is.InRange(-angle / 2 + angle * pellet / count, -angle / 2 + angle * (pellet + 1) / count));
                }
        }
    }
}
