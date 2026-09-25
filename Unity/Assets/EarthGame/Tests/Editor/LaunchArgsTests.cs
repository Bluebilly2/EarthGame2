using System.Collections.Generic;
using EarthGame.Shared;
using NUnit.Framework;

namespace EarthGame.Tests.Editor
{
    /// <summary>
    /// The command line's <c>-eg-*</c> arguments (<see cref="LaunchArgs"/>): a key takes the word after it as its value unless
    /// that word is the next key, and a negative number is a value, not a key (BF.4 stage three, 2026-09-25: the rocks scenario's
    /// first run lost the east of a rock in the region's west half, which is negative, and walked at nothing).
    /// </summary>
    public sealed class LaunchArgsTests
    {
        [Test]
        public void AKeyTakesItsValueAndANegativeNumberIsAValue()
        {
            Dictionary<string, string> map = LaunchArgs.Parse(new[]
            {
                "EarthGame2.exe", "-batchmode", "-eg-rock", "-1595.20,3452.04", "-eg-rock-walk", "over", "-eg-dev", "-logFile", "player.log",
                "-eg-lift", "-3", "-eg-hide", "rocks",
            });
            Assert.That(map["rock"], Is.EqualTo("-1595.20,3452.04"));
            Assert.That(map["rock-walk"], Is.EqualTo("over"));
            Assert.That(map["dev"], Is.EqualTo("true"), "a key followed by another key is a flag");
            Assert.That(map["lift"], Is.EqualTo("-3"));
            Assert.That(map["hide"], Is.EqualTo("rocks"));
            Assert.That(map.ContainsKey("logFile"), Is.False, "Unity's own arguments are not the game's");
        }
    }
}
