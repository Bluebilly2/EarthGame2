using System;
using System.Collections.Generic;
using System.IO;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// One record of change beside the layers that never change (BF.3 promise 1): every kind of change a cell can hold,
    /// merged as a taking (bits set, never cleared) or as a measure (the larger kept); written to the region file by layer
    /// with its length so a reader skips what it does not know; carried by one message; named by the digest; restored by
    /// the save into the world it was taken from.
    /// </summary>
    public sealed class WorldChangesTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);

        private static WorldState World()
        {
            RegionRaster loose = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "changes_loose", "loose",
                (row, col) => row == 110 && col == 110 ? LooseCodes.Pack(3, 2) : 0u, null);
            RegionRaster cover = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "changes_cover", "cover",
                (row, col) => GroundCovers.Pack(GroundCover.Heath, 1), null);
            return new WorldState(1347UL, Fixture, Fixture.WakeClock(), new Heightfield(TestRasters.MadeCoast()), 0, null, null, cover, null, loose, null);
        }

        [Test]
        public void ATakingSetsBitsForGoodAndAMeasureKeepsTheLarger()
        {
            WorldChanges changes = new WorldChanges(new LooseTaken());
            Assert.That(changes.TakeTuft(2, 3, 1), Is.True);
            Assert.That(changes.TakeTuft(2, 3, 1), Is.False, "once");
            Assert.That(changes.IsTuftTaken(2, 3, 1), Is.True);
            Assert.That(changes.IsTuftTaken(2, 3, 0), Is.False, "the tuft beside it keeps its place");
            Assert.That(changes.TakeTuft(2, 3, 16), Is.False, "sixteen tufts a cell is the most the bits hold");
            changes.SetTrunkCut(4, 4, 40);
            changes.SetTrunkCut(4, 4, 30);
            Assert.That(changes.TrunkOf(4, 4).Cut, Is.EqualTo((byte)40), "a cut is never undone");
            changes.MarkTrunk(4, 4, TrunkChange.BarkTaken);
            Assert.That((changes.TrunkOf(4, 4).Flags & TrunkChange.BarkTaken) != 0, Is.True);
            Assert.That((changes.TrunkOf(4, 4).Flags & TrunkChange.Felled) != 0, Is.False);
            changes.Dig(5, 6, 20);
            changes.Dig(5, 6, 35);
            changes.Dig(5, 6, 10);
            Assert.That(changes.GroundOf(5, 6).DugCm, Is.EqualTo((byte)35));
            changes.Clear(5, 6);
            Assert.That((changes.GroundOf(5, 6).Flags & GroundChange.Cleared) != 0, Is.True);
            Assert.That(changes.GroundOf(9, 9).DugCm, Is.EqualTo((byte)0), "a cell nothing happened to");

            changes.Merge(new CellChange { Row = 2, Col = 3, Tufts = 4, TrunkCut = 10 });
            Assert.That(changes.TryGet(2, 3, out CellChange merged), Is.True);
            Assert.That(merged.Tufts, Is.EqualTo((ushort)6), "bits added to bits");
            Assert.That(merged.TrunkCut, Is.EqualTo((byte)10));
            Assert.Throws<ArgumentException>(() => changes.Merge(new CellChange { Row = -1, Col = 0, Tufts = 1 }), "no such cell");
            Assert.Throws<ArgumentException>(() => changes.Merge(new CellChange { Row = 1, Col = 1, TrunkFlags = 0x80 }), "a trunk flag this build does not know");
            List<CellChange> cells = changes.Cells();
            Assert.That(cells.Count, Is.EqualTo(3));
            Assert.That((cells[0].Row, cells[0].Col), Is.EqualTo((2, 3)), "by row and then column");
            Assert.That((cells[2].Row, cells[2].Col), Is.EqualTo((5, 6)));
            Assert.That(cells[0].Layers, Is.EqualTo(CellChange.TuftLayer | CellChange.TrunkLayer));
            Assert.That(cells[2].Layers, Is.EqualTo(CellChange.GroundLayer));
        }

        private static CellChange Full(int row, int col) => new CellChange { Row = row, Col = col, Tufts = 0x0105, TrunkFlags = TrunkChange.BarkTaken | TrunkChange.Felled, TrunkCut = 255, GroundFlags = GroundChange.Cleared, DugCm = 42 };

        [Test]
        public void TheRegionFileWritesEveryLayerWithItsLengthReadsOlderFilesAndSkipsWhatItDoesNotKnow()
        {
            Assert.That(RegionFile.Version, Is.EqualTo((ushort)5));
            List<LooseTaken.Cell> taken = new List<LooseTaken.Cell> { new LooseTaken.Cell { Row = 7, Col = 8, Sticks = 5, Cobbles = 1 } };
            List<CellChange> changes = new List<CellChange> { Full(7, 8), new CellChange { Row = 9, Col = 1, DugCm = 3 } };
            byte[] file = RegionFile.Encode(1, 1, new SavedEntity[0], taken, changes);
            List<SavedEntity> entities = RegionFile.Decode(file, out _, out _, out List<LooseTaken.Cell> takenBack, out List<CellChange> changesBack);
            Assert.That(entities.Count, Is.EqualTo(0));
            Assert.That(takenBack.Count, Is.EqualTo(1));
            Assert.That(takenBack[0].Sticks, Is.EqualTo((ushort)5));
            Assert.That(changesBack.Count, Is.EqualTo(2));
            Assert.That(changesBack[0].Tufts, Is.EqualTo((ushort)0x0105));
            Assert.That(changesBack[0].TrunkFlags, Is.EqualTo((byte)(TrunkChange.BarkTaken | TrunkChange.Felled)));
            Assert.That(changesBack[0].TrunkCut, Is.EqualTo((byte)255));
            Assert.That(changesBack[0].GroundFlags, Is.EqualTo(GroundChange.Cleared));
            Assert.That(changesBack[0].DugCm, Is.EqualTo((byte)42));
            Assert.That(changesBack[1].DugCm, Is.EqualTo((byte)3));
            Assert.That(changesBack[1].Layers, Is.EqualTo(CellChange.GroundLayer), "a cell with one layer writes one diff");
            int diffs = BitConverter.ToInt32(file, 4 + 2 + 4 + 4 + 8 + 4);
            Assert.That(diffs, Is.EqualTo(1 + 3 + 1), "the loose diff, three layers of the full cell, one of the other");

            // A version-5 file with a diff of a layer this build does not know is read past it by its length.
            byte[] strange = WithDiffs(5, new byte[] { 200, 7, 0, 8, 0, 3, 1, 2, 3, (byte)ChangeLayer.Ground, 9, 0, 1, 0, 2, GroundChange.Cleared, 0 });
            RegionFile.Decode(strange, out _, out _, out List<LooseTaken.Cell> none, out List<CellChange> known);
            Assert.That(none.Count, Is.EqualTo(0));
            Assert.That(known.Count, Is.EqualTo(1), "the diff it knows, after the one it skipped");
            Assert.That(known[0].GroundFlags, Is.EqualTo(GroundChange.Cleared));

            // A version-4 file: a loose diff of nine bytes with no length, as BF.1 wrote it.
            byte[] older = WithDiffs(4, new byte[] { (byte)TileLayer.Loose, 7, 0, 8, 0, 5, 0, 1, 0 });
            RegionFile.Decode(older, out _, out _, out List<LooseTaken.Cell> oldTaken, out List<CellChange> oldChanges);
            Assert.That(oldTaken.Count, Is.EqualTo(1));
            Assert.That(oldTaken[0].Sticks, Is.EqualTo((ushort)5));
            Assert.That(oldChanges.Count, Is.EqualTo(0));
        }

        /// <summary>A region file of the given version with no entities and these diff bytes, its counts and CRC right.</summary>
        private static byte[] WithDiffs(ushort version, byte[] diffBytes)
        {
            int diffs = 0;
            for (int at = 0; at < diffBytes.Length;)
            {
                diffs++;
                if (version >= 5) at += 5 + 1 + diffBytes[at + 5];
                else at += 9;
            }
            PacketWriter head = new PacketWriter(RegionFile.HeaderBytes);
            foreach (char c in "EG2R") head.WriteByte((byte)c);
            head.WriteUInt16(version);
            head.WriteInt32(1);
            head.WriteInt32(1);
            head.WriteDouble(RegionCells.CellM);
            head.WriteUInt32(0);
            head.WriteUInt32((uint)diffs);
            head.WriteUInt32(Crc32.Compute(diffBytes));
            byte[] file = new byte[RegionFile.HeaderBytes + diffBytes.Length];
            head.Written.CopyTo(file);
            Buffer.BlockCopy(diffBytes, 0, file, RegionFile.HeaderBytes, diffBytes.Length);
            return file;
        }

        [Test]
        public void TheChangesMessageCarriesEveryLayerAndRefusesWhatItDoesNotKnow()
        {
            Assert.That(ProtocolInfo.Version, Is.EqualTo((ushort)21), "BF.3's changes are protocol 19, its standing targets 20, WG.2c's two-byte stand 21");
            Assert.That((byte)MessageKind.Changes, Is.EqualTo((byte)26));
            ChangesMessage m = new ChangesMessage { Cells = new[] { Full(7, 8), new CellChange { Row = 2, Col = 2, Sticks = 3 } } };
            PacketWriter w = new PacketWriter(64);
            m.Write(w);
            Assert.That(w.Written.Length, Is.EqualTo(1 + 2 + (2 + 2 + 1 + 2 + 2 + 2) + (2 + 2 + 1 + 4)), "a full cell without loose, and a cell of loose alone");
            ChangesMessage back = ChangesMessage.Read(new PacketReader(w.Written.ToArray(), 1, w.Written.Length - 1));
            Assert.That(back.Cells.Length, Is.EqualTo(2));
            Assert.That(back.Cells[0].DugCm, Is.EqualTo((byte)42));
            Assert.That(back.Cells[0].Tufts, Is.EqualTo((ushort)0x0105));
            Assert.That(back.Cells[1].Sticks, Is.EqualTo((ushort)3));
            Assert.That(back.Cells[1].Layers, Is.EqualTo(CellChange.LooseLayer));

            w.Reset();
            w.WriteByte((byte)MessageKind.Changes);
            w.WriteUInt16(1);
            w.WriteUInt16(1);
            w.WriteUInt16(1);
            w.WriteByte(0x10);
            Assert.Throws<ProtocolException>(() => ChangesMessage.Read(new PacketReader(w.Written.ToArray(), 1, w.Written.Length - 1)), "a layer this build does not know");
            CellChange[] many = new CellChange[ChangesMessage.MostCells + 1];
            for (int i = 0; i < many.Length; i++) many[i] = new CellChange { Row = i / 100, Col = i % 100, Tufts = 1 };
            w.Reset();
            Assert.Throws<ProtocolException>(() => new ChangesMessage { Cells = many }.Write(w), "more cells than one message carries");
        }

        [Test]
        public void TheDigestNamesEveryChangeAndTheSaveRestoresIt()
        {
            WorldState w = World();
            string before = WorldDigest.World(w, new Dictionary<string, MoverState>());
            w.Changes.TakeTuft(110, 110, 2);
            string tuft = WorldDigest.World(w, new Dictionary<string, MoverState>());
            Assert.That(tuft, Is.Not.EqualTo(before), "a tuft taken is a different world");
            w.Changes.Dig(110, 111, 15);
            w.Changes.MarkTrunk(110, 109, TrunkChange.BarkTaken);
            w.Taken.Take(new LyingThing(110, 110, StandLayout.Kind.Stick, 0));
            string all = WorldDigest.World(w, new Dictionary<string, MoverState>());
            Assert.That(all, Is.Not.EqualTo(tuft));
            Assert.That(WorldDigest.Lines(w, new Dictionary<string, MoverState>()), Does.Contain("tuft 110 110 4\n").And.Contain("trunk 110 109 1 0\n").And.Contain("ground 110 111 0 15\n").And.Contain("taken 110 110 1 0\n"));

            string dir = Path.Combine(Path.GetTempPath(), "EarthGame2.Tests", "changes", Guid.NewGuid().ToString("N"));
            try
            {
                WorldSave.Write(dir, w, new SavedPlayer[0], "2026-09-22T10:00:00Z");
                WorldSaveInfo info = WorldSave.Read(dir);
                Assert.That(info.Changes.Count, Is.EqualTo(3), "three cells changed besides the taking");
                WorldState restored = WorldSave.Restore(info, new Heightfield(TestRasters.MadeCoast()), Fixture, null, w.Cover, null, w.Loose);
                Assert.That(restored.Changes.IsTuftTaken(110, 110, 2), Is.True);
                Assert.That(restored.Changes.GroundOf(110, 111).DugCm, Is.EqualTo((byte)15));
                Assert.That(restored.Changes.TrunkOf(110, 109).Flags, Is.EqualTo(TrunkChange.BarkTaken));
                Assert.That(restored.Taken.IsTaken(new LyingThing(110, 110, StandLayout.Kind.Stick, 0)), Is.True);
                Assert.That(WorldDigest.World(restored, new Dictionary<string, MoverState>()), Is.EqualTo(all), "the same name after the round trip");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
