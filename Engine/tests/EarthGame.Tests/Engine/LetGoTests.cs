using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The hands let go of everything where the founder fell (FP.2, the Standard death): the things return to the world with their ids, falling, and the hands are empty.</summary>
    public sealed class LetGoTests
    {
        [Test]
        public void EverythingCarriedReturnsToTheWorldWhereTheFounderFellAndTheHandsAreEmpty()
        {
            WorldState world = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            Entity stick = world.SpawnItem(DefinitionCatalogue.Stick, 10.0, 20.0);
            Entity cobble = world.SpawnItem(DefinitionCatalogue.Cobble, 10.5, 20.0);
            world.Step(0.05);
            Hands hands = new Hands();
            Double3 eye = new Double3(10.0, world.GroundAt(10.0, 20.0) + 1.65, 20.0);
            Assert.That(hands.PickUp(world, stick.Id.Value, eye), Is.EqualTo(VerbOutcome.Done));
            Assert.That(hands.PickUp(world, cobble.Id.Value, eye), Is.EqualTo(VerbOutcome.Done));
            world.Step(0.05);
            Assert.That(hands.Things.Count, Is.EqualTo(2));
            Assert.That(world.Entities.TryGet(stick.Id.Value, out _), Is.False, "carried things are out of the world");

            Double3 fell = new Double3(40.0, world.GroundAt(40.0, -5.0), -5.0);
            hands.LetGoOfEverything(world, fell, 90f);
            Assert.That(hands.Things.Count, Is.EqualTo(0));
            Assert.That(hands.Hand, Is.EqualTo((byte)0));
            Assert.That(world.Entities.TryGet(stick.Id.Value, out Entity backStick), Is.True, "the stick is in the world again with the id it always had");
            Assert.That(world.Entities.TryGet(cobble.Id.Value, out Entity backCobble), Is.True);
            Assert.That(backStick.Position.X, Is.EqualTo(40.0));
            Assert.That(backStick.Position.Z, Is.EqualTo(-5.0));
            Assert.That(backStick.Position.Y, Is.EqualTo(fell.Y + Hands.ReleaseM).Within(1e-9), "let go a little above the ground, to fall");
            Assert.That(backStick.Item.Resting, Is.False);
            Assert.That(backCobble.YawDeg, Is.EqualTo(90f));
            hands.LetGoOfEverything(world, fell, 0f);
            Assert.That(hands.Things.Count, Is.EqualTo(0), "empty hands let go of nothing, and nothing breaks");
        }
    }
}
