using System;
using EarthGame.Engine;
using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// A thing's own state as one record with one owner (BF.1 promise 2): a mask says which fields the thing carries of its
    /// own, the layout writes only those, and every reader refuses a bit it does not know or a number no thing could have.
    /// </summary>
    public sealed class ThingStateTests
    {
        private static ThingState Everything()
        {
            ThingState s = default;
            s.SetMass(0.812f);
            s.SetLength(1.15f);
            s.SetDiameter(0.024f);
            s.SetMoisture(0.22f);
            s.SetEdge(0.61f);
            s.SetPlatform(74.5f);
            s.SetFlakes(3);
            s.SetLook(4);
            s.SetCondition(0.9f);
            s.SetMarks(0x0005);
            return s;
        }

        private static ThingState RoundTrip(in ThingState s, out int bytes)
        {
            PacketWriter w = new PacketWriter(64);
            ThingWire.Write(w, s);
            bytes = w.Written.Length;
            return ThingWire.Read(new PacketReader(w.Written.ToArray(), 0, bytes));
        }

        [Test]
        public void EveryFieldRoundTripsThroughTheOneLayout()
        {
            ThingState s = Everything();
            ThingState back = RoundTrip(s, out int bytes);
            Assert.That(back.Fields, Is.EqualTo(s.Fields));
            Assert.That(back.MassKg, Is.EqualTo(0.812f));
            Assert.That(back.LengthM, Is.EqualTo(1.15f));
            Assert.That(back.DiameterM, Is.EqualTo(0.024f));
            Assert.That(back.Moisture, Is.EqualTo(0.22f));
            Assert.That(back.Edge01, Is.EqualTo(0.61f));
            Assert.That(back.PlatformDeg, Is.EqualTo(74.5f));
            Assert.That(back.FlakesTaken, Is.EqualTo((ushort)3));
            Assert.That(back.Look, Is.EqualTo((byte)4));
            Assert.That(back.Condition01, Is.EqualTo(0.9f));
            Assert.That(back.Marks, Is.EqualTo((ushort)5));
            Assert.That(bytes, Is.EqualTo(2 + 4 * 6 + 2 + 1 + 4 + 2), "the mask, six floats, the flakes, the look, the condition and the marks");
        }

        [Test]
        public void AThingWithNothingOfItsOwnIsTwoBytesAndOnlyThePresentFieldsAreWritten()
        {
            ThingState none = default;
            Assert.That(none.HasAny, Is.False);
            RoundTrip(none, out int bytes);
            Assert.That(bytes, Is.EqualTo(2), "the mask alone");

            ThingState some = default;
            some.SetMass(0.5f);
            some.SetLook(2);
            ThingState back = RoundTrip(some, out bytes);
            Assert.That(bytes, Is.EqualTo(2 + 4 + 1));
            Assert.That(back.Has(ThingFields.Mass) && back.Has(ThingFields.Look), Is.True);
            Assert.That(back.Has(ThingFields.Length), Is.False, "a field not carried is not carried");
            Assert.That(back.LengthM, Is.EqualTo(0f));
        }

        [Test]
        public void SettingAFieldMarksItAsTheThingsOwn()
        {
            ThingState s = default;
            Assert.That(s.Has(ThingFields.Edge), Is.False);
            s.SetEdge(0.3f);
            Assert.That(s.Has(ThingFields.Edge), Is.True);
            Assert.That(s.Has(ThingFields.Edge | ThingFields.Mass), Is.False, "has means every bit asked for");
        }

        private static byte[] Bytes(ushort mask, params float[] floats)
        {
            PacketWriter w = new PacketWriter(64);
            w.WriteUInt16(mask);
            foreach (float f in floats) w.WriteSingle(f);
            return w.Written.ToArray();
        }

        [Test]
        public void AnUnknownBitOrANumberNoThingCouldHaveIsRefused()
        {
            Assert.Throws<ProtocolException>(() => ThingWire.Read(new PacketReader(Bytes(0x8000), 0, 2)), "a bit this build does not know");
            Assert.Throws<ProtocolException>(() => ThingWire.Read(new PacketReader(Bytes((ushort)ThingFields.Mass, float.NaN), 0, 6)), "a mass that is not a number");
            Assert.Throws<ProtocolException>(() => ThingWire.Read(new PacketReader(Bytes((ushort)ThingFields.Length, -0.5f), 0, 6)), "a negative length");
            Assert.Throws<ProtocolException>(() => ThingWire.Read(new PacketReader(Bytes((ushort)ThingFields.Edge, 1.5f), 0, 6)), "an edge over one");
            Assert.Throws<ProtocolException>(() => ThingWire.Read(new PacketReader(Bytes((ushort)ThingFields.Platform, 190f), 0, 6)), "a platform past a flat");
            Assert.Throws<ProtocolException>(() => ThingWire.Read(new PacketReader(Bytes((ushort)ThingFields.Condition, -0.1f), 0, 6)), "a condition below nothing");
            Assert.DoesNotThrow(() => ThingWire.Read(new PacketReader(Bytes((ushort)ThingFields.Moisture, 1.4f), 0, 6)), "green wood holds more water than wood");
        }

        /// <summary>The carrying message carries each thing's state with it (BF.1, protocol 17): a flake's edge and a stick's size reach the hand.</summary>
        [Test]
        public void WhatIsCarriedCrossesTheWireWithItsOwnState()
        {
            ItemComponent flake = default;
            flake.Resting = true;
            flake.State.SetMass(0.021f);
            flake.State.SetEdge(0.6f);
            ItemComponent stick = default;
            stick.Resting = true;
            stick.State.SetLength(1.1f);
            stick.State.SetDiameter(0.02f);
            stick.State.SetMoisture(0.22f);
            stick.State.SetLook(3);
            CarryingMessage m = default;
            m.Hand = 2;
            m.Things = new[]
            {
                new CarriedThing { Id = 5, Definition = DefinitionCatalogue.FlakeOf(StoneType.Silcrete), Place = 2, Item = flake },
                new CarriedThing { Id = 9, Definition = DefinitionCatalogue.StickOf(PlantSpecies.Blackbutt), Place = 3, Item = stick },
            };
            PacketWriter w = new PacketWriter(64);
            m.Write(w);
            CarryingMessage back = CarryingMessage.Read(new PacketReader(w.Written.ToArray(), 1, w.Written.Length - 1));
            Assert.That(back.Things[0].Item.State.Edge01, Is.EqualTo(0.6f), "the flake's edge crossed the wire");
            Assert.That(back.Things[0].Item.State.MassKg, Is.EqualTo(0.021f));
            Assert.That(back.Things[1].Item.State.LengthM, Is.EqualTo(1.1f), "and the stick's length");
            Assert.That(back.Things[1].Item.State.Look, Is.EqualTo((byte)3), "and the shape it lay in");
            Assert.That(back.Things[1].Item.Resting, Is.True, "at rest in a hand");
        }

        [Test]
        public void TheItemComponentCarriesTheStateAndItsRestAndFall()
        {
            ItemComponent item = default;
            item.Resting = true;
            item.FallSpeed = 0f;
            item.State.SetMass(0.02f);
            item.State.SetEdge(0.55f);
            Assert.That(item.HasOwnState, Is.True);
            PacketWriter w = new PacketWriter(64);
            EntityWire.WriteItem(w, item);
            ItemComponent back = EntityWire.ReadItem(new PacketReader(w.Written.ToArray(), 0, w.Written.Length));
            Assert.That(back.Resting, Is.True);
            Assert.That(back.State.MassKg, Is.EqualTo(0.02f));
            Assert.That(back.State.Edge01, Is.EqualTo(0.55f));
            Assert.That(back.State.Has(ThingFields.Platform), Is.False);
        }
    }
}
