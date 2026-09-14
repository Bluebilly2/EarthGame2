using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    public sealed class JsonIntegerTests
    {
        [TestCase("0", 0UL)]
        [TestCase("1347", 1347UL)]
        [TestCase("9007199254740991", 9007199254740991UL)]
        [TestCase("9007199254740992", 9007199254740992UL)]
        [TestCase("9007199254740993", 9007199254740993UL)]
        [TestCase("18446744073709551615", ulong.MaxValue)]
        public void UnsignedIntegersKeepEveryDigit(string token, ulong expected)
        {
            JsonObject doc = Json.ParseObject("{\"seed\":" + token + "}");
            Assert.That(doc.UInt64("seed"), Is.EqualTo(expected));
            Assert.That(Json.Write(doc), Is.EqualTo("{\"seed\":" + token + "}"));
        }

        [TestCase("-9223372036854775808", long.MinValue)]
        [TestCase("9223372036854775807", long.MaxValue)]
        [TestCase("9007199254740993", 9007199254740993L)]
        [TestCase("-9007199254740993", -9007199254740993L)]
        public void SignedIntegersKeepEveryDigit(string token, long expected)
        {
            JsonObject doc = Json.ParseObject("{\"tick\":" + token + "}");
            Assert.That(doc.Int64("tick"), Is.EqualTo(expected));
            Assert.That(Json.Write(doc), Is.EqualTo("{\"tick\":" + token + "}"));
        }

        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("1.00000000000000000001")]
        [TestCase("1.0")]
        [TestCase("1e0")]
        [TestCase("18446744073709551616")]
        [TestCase("9007199254740993e0")]
        [TestCase("1e100")]
        [TestCase("\"1347\"")]
        [TestCase("null")]
        public void InvalidOrInexactUnsignedValuesAreRefused(string token)
        {
            var doc = Json.ParseObject("{\"seed\":" + token + "}");
            Assert.That(() => doc.UInt64("seed"), Throws.TypeOf<JsonException>().With.Message.Contains("seed"));
        }

        [Test]
        public void SignedOverflowIsRefusedAndMissingValuesAloneGetDefaults()
        {
            var doc = Json.ParseObject("{\"tick\":9223372036854775808,\"seed\":-1}");
            Assert.That(() => doc.Int64("tick"), Throws.TypeOf<JsonException>());
            Assert.That(() => doc.UInt64Or("seed", 1), Throws.TypeOf<JsonException>());
            Assert.That(doc.UInt64Or("absent", 17), Is.EqualTo(17));
            Assert.That(doc.Int64Or("absent", -3), Is.EqualTo(-3));
        }
    }
}
