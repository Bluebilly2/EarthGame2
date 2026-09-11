using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>A thing let fall is drawn between the positions the server stated (M1.5a promise 8), as a remote body is.</summary>
    public sealed class EntityMirrorTests
    {
        private static EntityStateMessage Fell(ulong id, long tick, double up)
        {
            EntityStateMessage m = default;
            m.Id = id;
            m.ServerTick = tick;
            m.Fields = EntityFields.Position;
            m.East = 1;
            m.Up = up;
            m.North = 2;
            return m;
        }

        [Test]
        public void AFallingThingIsDrawnBetweenItsStatedPositions()
        {
            EntityMirror mirror = new EntityMirror();
            EntitySpawnMessage spawn = default;
            spawn.Id = 5;
            spawn.DefinitionId = DefinitionCatalogue.Stick.Id.Value;
            spawn.ServerTick = 10;
            spawn.East = 1;
            spawn.Up = 1.3;
            spawn.North = 2;
            spawn.HasItem = true;
            spawn.Item = new ItemComponent { Resting = false, FallSpeed = 0f };
            mirror.Apply(spawn);
            EntityView view = mirror.Views[5];
            Assert.That(view.PositionAt(20).Y, Is.EqualTo(1.3), "one position held: it is there");

            mirror.Apply(Fell(5, 11, 1.2));
            mirror.Apply(Fell(5, 13, 1.0));
            Assert.That(view.PositionAt(9).Y, Is.EqualTo(1.3), "before the first, where it first was");
            Assert.That(view.PositionAt(10.5).Y, Is.EqualTo(1.25).Within(1e-12));
            Assert.That(view.PositionAt(12).Y, Is.EqualTo(1.1).Within(1e-12), "halfway across a tick no state came for");
            Assert.That(view.PositionAt(12).X, Is.EqualTo(1.0));
            Assert.That(view.PositionAt(20).Y, Is.EqualTo(1.0), "past the newest, where it is now");

            EntityStateMessage turn = default;
            turn.Id = 5;
            turn.ServerTick = 14;
            turn.Fields = EntityFields.Yaw;
            turn.YawDeg = 30f;
            mirror.Apply(turn);
            Assert.That(view.PositionAt(14).Y, Is.EqualTo(1.0), "a turn states no position");
            mirror.Apply(Fell(5, 12, 5.0));
            Assert.That(view.PositionAt(12).Y, Is.EqualTo(1.1).Within(1e-12), "a state older than the newest is dropped");

            for (int tick = 15; tick < 40; tick++) mirror.Apply(Fell(5, tick, 1.0 - 0.01 * (tick - 14)));
            Assert.That(view.PositionAt(0).Y, Is.EqualTo(1.0 - 0.01 * (32 - 14)).Within(1e-12), "only the last few are kept");
            Assert.That(view.PositionAt(38.5).Y, Is.EqualTo(1.0 - 0.01 * (38.5 - 14)).Within(1e-12));
        }
    }
}
