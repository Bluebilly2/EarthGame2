using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The engine's own JSON: strict on the way in, stable on the way out.</summary>
    public sealed class JsonTests
    {
        [Test]
        public void ParsesADocumentOfEveryKind()
        {
            JsonObject doc = Json.ParseObject("{\"name\":\"heights\",\"width\":2001,\"cell_m\":4.0,\"neg\":-1.5e-3,\"coast\":true,\"none\":null,\"tags\":[1,\"two\",[3]],\"source\":{\"zoom\":14}}");
            Assert.That(doc.String("name"), Is.EqualTo("heights"));
            Assert.That(doc.Int("width"), Is.EqualTo(2001));
            Assert.That(doc.Number("cell_m"), Is.EqualTo(4.0));
            Assert.That(doc.Number("neg"), Is.EqualTo(-0.0015).Within(1e-15));
            Assert.That(doc.Bool("coast"), Is.True);
            Assert.That(doc["none"], Is.Null);
            List<object> tags = doc.Array("tags");
            Assert.That(tags.Count, Is.EqualTo(3));
            Assert.That(tags[1], Is.EqualTo("two"));
            Assert.That(((List<object>)tags[2])[0], Is.EqualTo(3.0));
            Assert.That(doc.Object("source").Int("zoom"), Is.EqualTo(14));
        }

        [Test]
        public void StringsUnescape()
        {
            JsonObject doc = Json.ParseObject("{\"s\":\"a\\\"b\\\\c\\n\\t\\u00e9\\ud83d\\ude00/\"}");
            Assert.That(doc.String("s"), Is.EqualTo("a\"b\\c\n\té\U0001F600/"));
        }

        [Test]
        public void KeysKeepTheirOrderAndRoundTrip()
        {
            JsonObject doc = new JsonObject().With("zulu", 1).With("alpha", "a").With("mike", new List<object> { true, null, 2.5 });
            doc["nested"] = new JsonObject().With("k", "v");
            string text = Json.Write(doc);
            Assert.That(text, Is.EqualTo("{\"zulu\":1,\"alpha\":\"a\",\"mike\":[true,null,2.5],\"nested\":{\"k\":\"v\"}}"));
            JsonObject back = Json.ParseObject(text);
            Assert.That(back.Keys, Is.EqualTo(new[] { "zulu", "alpha", "mike", "nested" }));
            Assert.That(Json.Write(back), Is.EqualTo(text), "write, read, write is a fixed point");
        }

        [Test]
        public void IndentedOutputIsStableAndReadsBack()
        {
            JsonObject doc = new JsonObject().With("a", 1).With("b", new List<object> { 1, 2 }).With("c", new JsonObject());
            string text = Json.Write(doc, indent: true);
            Assert.That(text, Is.EqualTo("{\n  \"a\": 1,\n  \"b\": [\n    1,\n    2\n  ],\n  \"c\": {}\n}\n"));
            Assert.That(Json.Write(Json.ParseObject(text), indent: true), Is.EqualTo(text));
        }

        [Test]
        public void NumbersWriteCultureInvariantAndWholeNumbersWithoutADecimal()
        {
            Assert.That(Json.Write(new List<object> { 4.0, 0.5, -1e-7, 1234567890123.0, 3.14159 }), Is.EqualTo("[4,0.5,-1E-07,1234567890123,3.14159]"));
            Assert.That(Json.Write("tab\there"), Is.EqualTo("\"tab\\there\\u0001\""));
        }

        [Test]
        public void MalformedTextIsAnErrorWithAPlace()
        {
            Assert.That(() => Json.Parse("{\"a\":1,}"), Throws.TypeOf<JsonException>().With.Message.Contains("offset"));
            Assert.That(() => Json.Parse("{\"a\":1} x"), Throws.TypeOf<JsonException>().With.Message.Contains("trailing"));
            Assert.That(() => Json.Parse("{\"a\":1,\"a\":2}"), Throws.TypeOf<JsonException>().With.Message.Contains("duplicate"));
            Assert.That(() => Json.Parse("[1 2]"), Throws.TypeOf<JsonException>());
            Assert.That(() => Json.Parse("\"unterminated"), Throws.TypeOf<JsonException>());
            Assert.That(() => Json.Parse("01"), Throws.TypeOf<JsonException>());
            Assert.That(() => Json.Parse(""), Throws.TypeOf<JsonException>());
        }

        [Test]
        public void AMissingOrMistypedKeyNamesItself()
        {
            JsonObject doc = Json.ParseObject("{\"width\":\"wide\"}");
            Assert.That(() => doc.Number("height"), Throws.TypeOf<JsonException>().With.Message.Contains("height"));
            Assert.That(() => doc.Number("width"), Throws.TypeOf<JsonException>().With.Message.Contains("width"));
            Assert.That(doc.NumberOr("height", 7.0), Is.EqualTo(7.0));
            Assert.That(() => doc.NumberOr("width", 7.0), Throws.TypeOf<JsonException>(), "a present key of the wrong type is never silently defaulted");
        }
    }
}
