using CargoSquad.Cargo;
using CargoSquad.Combat;
using NUnit.Framework;
using UnityEngine;

namespace CargoSquad.Tests
{
    public sealed class ImpactValueTests
    {
        [Test]
        public void HealthPublishesClampedChangesAndOneDeath()
        {
            var host = new GameObject("Health test");
            try
            {
                var health = host.AddComponent<Health>();
                int changes = 0, deaths = 0;
                health.Changed += (current, maximum) =>
                {
                    Assert.That(current, Is.InRange(0f, maximum));
                    changes++;
                };
                health.Died += () => deaths++;
                health.ResetMaximum(130f);
                health.ApplyDamage(15f);
                Assert.That(health.Current, Is.EqualTo(115f));
                health.ApplyDamage(float.NaN);
                health.ApplyDamage(float.PositiveInfinity);
                health.ApplyDamage(-5f);
                Assert.That(changes, Is.EqualTo(2));
                health.ApplyDamage(200f);
                health.ApplyDamage(20f);
                Assert.That(health.Current, Is.Zero);
                Assert.That(deaths, Is.EqualTo(1));
                Assert.That(changes, Is.EqualTo(3));
                health.ResetMaximum(95f);
                Assert.That(health.IsDead, Is.False);
                Assert.That(health.Current, Is.EqualTo(95f));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void GripImpactClampsAndKeepsSlipPendingUntilNextCarry()
        {
            var host = new GameObject("Grip test");
            try
            {
                var grip = host.AddComponent<CargoGrip>();
                int changes = 0;
                grip.Changed += value => changes++;
                grip.ApplyLoss(25f);
                Assert.That(grip.Percent, Is.EqualTo(75f));
                grip.ApplyLoss(float.NaN);
                grip.ApplyLoss(-5f);
                Assert.That(changes, Is.EqualTo(1));
                grip.ApplyLoss(500f);
                Assert.That(grip.Percent, Is.Zero);
                Assert.That(grip.SlipPending, Is.True);
                grip.Tick(false, false, 1f, 100f);
                grip.ApplyLoss(25f);
                Assert.That(grip.Percent, Is.Zero);
                Assert.That(changes, Is.EqualTo(2));
                grip.BeginCarry();
                Assert.That(grip.Percent, Is.EqualTo(100f));
                Assert.That(grip.SlipPending, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
