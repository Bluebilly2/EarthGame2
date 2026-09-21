using EarthGame.ClientCore;
using NUnit.Framework;

namespace EarthGame.Tests.Client
{
    /// <summary>
    /// Looking round without turning (M1.E promise 3, CANON ruling 36: "let the alt key allow the camera to pan without
    /// moving the players body. inspired by the same mechanic in rust"), on both axes since William's word of 2026-09-21
    /// ("the x and y should both snap back, not just the x"). Engine-free, so the rule is asserted without a camera: the
    /// client feeds it each frame's mouse turn and tilt and whether Alt is held, and reads back how much the body turns and
    /// tilts and how far the view stands off the body's facing and pitch.
    /// </summary>
    public sealed class FreeLookTests
    {
        [Test]
        public void WhileHeldTheBodyKeepsItsFacingAndTheViewTurns()
        {
            FreeLook look = new FreeLook();
            FreeLook.BodyShare body = look.Step(seconds: 0.016, held: true, turnDeg: 30.0, pitchDeg: 0.0);
            Assert.That(body.TurnDeg, Is.EqualTo(0.0), "the body turned while Alt was held");
            Assert.That(look.OffsetDeg, Is.EqualTo(30.0).Within(1e-9), "the view did not turn with the mouse");
        }

        [Test]
        public void NotHeldTheBodyTakesTheWholeTurn()
        {
            FreeLook look = new FreeLook();
            FreeLook.BodyShare body = look.Step(0.016, held: false, turnDeg: 12.5, pitchDeg: -3.0);
            Assert.That(body.TurnDeg, Is.EqualTo(12.5), "the ordinary look is untouched");
            Assert.That(body.PitchDeg, Is.EqualTo(-3.0), "and so is its tilt");
            Assert.That(look.OffsetDeg, Is.EqualTo(0.0));
            Assert.That(look.PitchOffsetDeg, Is.EqualTo(0.0));
        }

        [Test]
        public void TheViewTurnsNoFurtherThanTheStatedBoundEitherWay()
        {
            Assert.That(FreeLook.MostTurnDeg, Is.EqualTo(135.0), "M1.E promises 135 degrees");
            FreeLook look = new FreeLook();
            for (int i = 0; i < 20; i++) look.Step(0.016, true, 20.0, 0.0);
            Assert.That(look.OffsetDeg, Is.EqualTo(FreeLook.MostTurnDeg).Within(1e-9), "turned past the bound to the right");
            for (int i = 0; i < 40; i++) look.Step(0.016, true, -20.0, 0.0);
            Assert.That(look.OffsetDeg, Is.EqualTo(-FreeLook.MostTurnDeg).Within(1e-9), "turned past the bound to the left");
        }

        /// <summary>Released, the view glides home to the body's facing: not a snap, and not a drift that never arrives.</summary>
        [Test]
        public void ReleasedTheViewGlidesBackToTheBodysFacing()
        {
            FreeLook look = new FreeLook();
            look.Step(0.016, true, 90.0, 0.0);
            look.Step(0.016, false, 0.0, 0.0);
            Assert.That(look.OffsetDeg, Is.GreaterThan(1.0), "it snapped back in one frame");
            Assert.That(look.OffsetDeg, Is.LessThan(90.0), "it did not start back at all");
            double t = 0.016;
            while (t < FreeLook.ReturnSeconds + 0.1)
            {
                look.Step(0.016, false, 0.0, 0.0);
                t += 0.016;
            }
            Assert.That(look.OffsetDeg, Is.EqualTo(0.0), "it had not come home within its glide");
        }

        /// <summary>A turn of the mouse during the glide home turns the body, as any look does once Alt is let go.</summary>
        [Test]
        public void DuringTheGlideTheMouseTurnsTheBody()
        {
            FreeLook look = new FreeLook();
            look.Step(0.016, true, 60.0, 0.0);
            FreeLook.BodyShare body = look.Step(0.016, false, 5.0, 2.0);
            Assert.That(body.TurnDeg, Is.EqualTo(5.0));
            Assert.That(body.PitchDeg, Is.EqualTo(2.0));
        }

        /// <summary>William, 2026-09-21: "the x and y should both snap back, not just the x." Held, the tilt is the view's too, and the body keeps its pitch.</summary>
        [Test]
        public void WhileHeldTheBodyKeepsItsPitchAndTheViewTilts()
        {
            FreeLook look = new FreeLook();
            FreeLook.BodyShare body = look.Step(0.016, held: true, turnDeg: 0.0, pitchDeg: -25.0);
            Assert.That(body.PitchDeg, Is.EqualTo(0.0), "the body tilted while Alt was held");
            Assert.That(look.PitchOffsetDeg, Is.EqualTo(-25.0).Within(1e-9), "the view did not tilt with the mouse");
        }

        [Test]
        public void ReleasedTheTiltComesHomeWithTheTurn()
        {
            FreeLook look = new FreeLook();
            look.Step(0.016, true, 60.0, -40.0);
            look.Step(0.016, false, 0.0, 0.0);
            Assert.That(look.PitchOffsetDeg, Is.LessThan(-1.0), "the tilt snapped back in one frame");
            Assert.That(look.PitchOffsetDeg, Is.GreaterThan(-40.0), "the tilt did not start back at all");
            double t = 0.016;
            while (t < FreeLook.ReturnSeconds + 0.1)
            {
                look.Step(0.016, false, 0.0, 0.0);
                t += 0.016;
            }
            Assert.That(look.PitchOffsetDeg, Is.EqualTo(0.0), "the tilt had not come home within its glide");
            Assert.That(look.OffsetDeg, Is.EqualTo(0.0), "and the turn came home beside it");
        }

        [Test]
        public void TheViewTiltsNoFurtherThanStraightUpOrDown()
        {
            Assert.That(FreeLook.MostTiltDeg, Is.EqualTo(89.0));
            FreeLook look = new FreeLook();
            for (int i = 0; i < 20; i++) look.Step(0.016, true, 0.0, -20.0);
            Assert.That(look.PitchOffsetDeg, Is.EqualTo(-FreeLook.MostTiltDeg).Within(1e-9), "tilted past straight up");
            for (int i = 0; i < 40; i++) look.Step(0.016, true, 0.0, 20.0);
            Assert.That(look.PitchOffsetDeg, Is.EqualTo(FreeLook.MostTiltDeg).Within(1e-9), "tilted past straight down");
        }
    }
}
