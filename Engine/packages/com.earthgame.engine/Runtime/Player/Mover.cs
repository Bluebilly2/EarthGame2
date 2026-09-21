using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// How a body moves over the ground: one step of it, as a pure function of the state, the input, the step
    /// length and the world's collision (ARCHITECTURE §9; Rust's mover shape). The client runs it at 50 Hz on its
    /// own input and shows the result at once; the server runs the same function against its heightfield to say
    /// whether what the client reports was possible. Nothing here reads a clock, a random number or a frame.
    ///
    /// <para>The rules, in the order they are applied: what is under the feet decides grounded, sliding or
    /// airborne; water over the ground decides wading, and water too deep to stand in swimming (M1.5e); on walkable
    /// ground the wish becomes a velocity at the speed <see cref="Locomotion"/> gives for the slope in that direction,
    /// reached by the gait's acceleration and left by the brake (M1.5h), a jump adds its take-off speed; in the water the wish is swum at the stroke's pace and the water turns the body
    /// towards its float line; in the air gravity acts and the wish only eases the horizontal velocity; the motion is
    /// then swept through the world, stepping onto low obstacles and sliding along everything else; finally grounded
    /// feet snap to the ground beneath them or discover there is none, and a swimmer is held clear of taking the eye
    /// under.</para>
    /// </summary>
    public static class Mover
    {
        /// <summary>How many times a step slides along what stopped it before it gives up; the flight sweeps by the same rule (M1.D).</summary>
        internal const int MaxSlideIterations = 4;
        internal const double Epsilon = 1e-9;
        /// <summary>How close to the ground airborne feet must be to count as resting on it, metres.</summary>
        private const double LandingTolerance = 0.02;

        public static MoverState Step(MoverState s, MoverInput input, double dt, IWorldCollision world, MoverConfig cfg = null, double workCapacity01 = 1.0)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            cfg = cfg ?? MoverConfig.Default;
            if (!(dt > 0.0) || !s.IsFinite) return s;
            input.Normalise();

            double radius = cfg.CapsuleRadius;
            s.Stance = input.Crouch ? Stance.Crouching : Stance.Standing;
            double height = s.Stance == Stance.Crouching ? cfg.CrouchHeight : cfg.StandingHeight;
            Double3 feet = s.Feet;
            Double3 vel = s.Velocity;

            // 1. What is under the feet. Grounded feet look a step up and a snap down, and so do a swimmer's, so that a
            //    bottom rising to meet them is stood on; airborne feet only count ground they are resting on, and only
            //    while not rising (a jump must not be re-grounded on its first centimetre).
            bool groundFound;
            double groundUp;
            Double3 groundNormal;
            if (s.Grounded || s.Swimming)
                groundFound = world.ProbeGround(feet, radius, cfg.StepHeight, cfg.GroundSnapDistance, out groundUp, out groundNormal);
            else if (vel.Y <= 0.0)
                groundFound = world.ProbeGround(feet, radius, LandingTolerance, LandingTolerance, out groundUp, out groundNormal);
            else
            {
                groundFound = false;
                groundUp = 0.0;
                groundNormal = Double3.Up;
            }
            bool walkable = groundFound && SlopeDeg(groundNormal) <= cfg.WalkableSlopeDeg;

            // 2. Water over the ground: waded where the bottom is within a standing body's reach, swum where it is not
            //    (M1.5e, CANON ruling 24). A body off the ground and under the surface swims unless there is ground between
            //    its feet and the float line (the swimming depth under the surface) to come down on and stand in. It looks
            //    down from the feet rather than from the surface, so that a bank beside a swimmer is not taken for ground
            //    under them.
            double water = world.WaterSurfaceAt(feet.X, feet.Z);
            bool wet = !double.IsNaN(water);
            double floatLine = water - cfg.SwimDepth;
            double depth = wet ? water - (groundFound ? groundUp : feet.Y) : 0.0;
            bool swimming = wet && (groundFound
                ? depth > cfg.SwimDepth
                : feet.Y < water && !(feet.Y > floatLine && world.ProbeGround(feet, radius, 0.0, feet.Y - floatLine, out _, out _)));
            s.Swimming = swimming;
            s.Wading = !swimming && depth > cfg.WadeDepth;
            bool deep = depth > cfg.DeepDepth;
            // Nor does a founder crouch where the crouched eye would be under the water: nothing is drawn from under it.
            if (s.Stance == Stance.Crouching && (swimming || depth > cfg.CrouchEyeHeight - cfg.SwimEyeAboveWaterM))
            {
                s.Stance = Stance.Standing;
                height = cfg.StandingHeight;
            }

            // 3. The velocity this step.
            bool jumped = false;
            if (swimming)
            {
                // Swum at the breaststroke's pace, or the crawl's when pushed, with the water turning the body's own rise
                // or fall towards the float line; there is no jumping out of the water.
                s.Grounded = false;
                double swimSpeed = Locomotion.SwimmingSpeedMs(input.Sprint, workCapacity01);
                double toward = (floatLine - feet.Y) * cfg.BuoyancyPerSecond;
                double velUp = vel.Y + (toward - vel.Y) * Math.Min(1.0, cfg.WaterDragPerSecond * dt);
                vel = new Double3(input.WishEast * swimSpeed, velUp, input.WishNorth * swimSpeed);
            }
            else
            {
                Gait gait = input.Sprint && !s.Wading ? Gait.Running : Gait.Walking;
                double wishLen = input.WishLength;
                if (walkable)
                {
                    feet = new Double3(feet.X, groundUp, feet.Z);
                    double slopeAlong = 0.0;
                    if (wishLen > Epsilon)
                        slopeAlong = -(groundNormal.X * input.WishEast + groundNormal.Z * input.WishNorth) / (groundNormal.Y * wishLen);
                    double speed = Locomotion.SpeedMs(slopeAlong, gait, workCapacity01);
                    if (s.Stance == Stance.Crouching) speed *= cfg.CrouchSpeedFactor;
                    if (s.Wading) speed *= deep ? cfg.DeepWadeSpeedFactor : cfg.WadeSpeedFactor;
                    // The wish becomes a velocity by acceleration, not at once (M1.5h, CANON ruling 34): a body is at nine
                    // tenths of its pace by the third step (Gait & Posture 83, 2021), quicker at a run, and brakes harder than
                    // it starts (v1's 4 m/s²: a quarter of a metre from a walk, a metre and a half from a run). The velocity
                    // is turned toward the wished one as a vector, so a reversed wish passes through a stop.
                    Double3 wished = new Double3(input.WishEast * speed, 0.0, input.WishNorth * speed);
                    vel = Approach(new Double3(vel.X, 0.0, vel.Z), wished, gait == Gait.Running ? cfg.RunAccelMs2 : cfg.WalkAccelMs2, cfg.BrakeMs2, dt);
                    s.Grounded = true;
                    if (input.Jump && !deep)
                    {
                        vel = new Double3(vel.X, Math.Sqrt(2.0 * cfg.Gravity * cfg.JumpHeight), vel.Z);
                        s.Grounded = false;
                        jumped = true;
                    }
                }
                else
                {
                    s.Grounded = false;
                    double velUp = vel.Y - cfg.Gravity * dt;
                    if (velUp < -cfg.MaxFallSpeed) velUp = -cfg.MaxFallSpeed;
                    vel = new Double3(vel.X, velUp, vel.Z);
                    if (!groundFound)
                    {
                        // In the air the wish only eases the horizontal velocity; on ground too steep to stand on there
                        // is no control at all: that is a slide, and gravity decides it.
                        double airSpeed = Locomotion.SpeedMs(0.0, gait, workCapacity01);
                        double k = Math.Min(1.0, cfg.AirControl * dt);
                        vel = new Double3(vel.X + (input.WishEast * airSpeed - vel.X) * k, vel.Y, vel.Z + (input.WishNorth * airSpeed - vel.Z) * k);
                    }
                }
            }

            // 4. Move, stepping onto what is low and sliding along what is not.
            Double3 delta = vel * dt;
            bool steppedUp = false;
            for (int i = 0; i < MaxSlideIterations && delta.SqrLength > Epsilon * Epsilon; i++)
            {
                if (!world.SweepCapsule(feet, radius, height, delta, out double fraction, out Double3 normal))
                {
                    feet = feet + delta;
                    break;
                }
                feet = feet + delta * fraction;
                Double3 remaining = delta * (1.0 - fraction);

                if (s.Grounded && !steppedUp && !jumped)
                {
                    Double3 lifted = new Double3(feet.X, feet.Y + cfg.StepHeight, feet.Z);
                    Double3 horizontal = new Double3(remaining.X, 0.0, remaining.Z);
                    if (horizontal.SqrLength > Epsilon * Epsilon
                        && !world.SweepCapsule(lifted, radius, height, horizontal, out _, out _)
                        && world.ProbeGround(lifted + horizontal, radius, 0.0, cfg.StepHeight + cfg.GroundSnapDistance, out double stepGround, out Double3 stepNormal)
                        && SlopeDeg(stepNormal) <= cfg.WalkableSlopeDeg)
                    {
                        feet = new Double3(feet.X + horizontal.X, stepGround, feet.Z + horizontal.Z);
                        steppedUp = true;
                        break;
                    }
                }

                double into = Double3.Dot(remaining, normal);
                if (into < 0.0) remaining = remaining - normal * into;
                double velInto = Double3.Dot(vel, normal);
                if (velInto < 0.0) vel = vel - normal * velInto;
                delta = remaining;
            }

            // 5. Grounded feet follow the ground down a slope or over a small drop; otherwise they are falling.
            //    Falling feet that reached the ground during the sweep land. A swimmer lands on nothing, and however hard
            //    they came down into the water it takes them before the eye goes under.
            if (s.Grounded)
            {
                if (world.ProbeGround(feet, radius, cfg.StepHeight, cfg.GroundSnapDistance, out double below, out Double3 belowNormal)
                    && SlopeDeg(belowNormal) <= cfg.WalkableSlopeDeg)
                    feet = new Double3(feet.X, below, feet.Z);
                else
                    s.Grounded = false;
            }
            else if (vel.Y <= 0.0 && !jumped && !swimming)
            {
                if (world.ProbeGround(feet, radius, LandingTolerance, LandingTolerance, out double landing, out Double3 landingNormal)
                    && SlopeDeg(landingNormal) <= cfg.WalkableSlopeDeg)
                {
                    feet = new Double3(feet.X, landing, feet.Z);
                    vel = new Double3(vel.X, 0.0, vel.Z);
                    s.Grounded = true;
                }
                // A falling body whose feet the sweep has let under the ground is put back on the surface and slides along it
                // (M1.5i, 2026-09-21): the client's sweep is a capsule cast that starts with its foot inside a steep face and reports
                // nothing, and William fell through the world on a face too steep to stand on. The mover keeps the feet on the
                // surface itself, whatever the sweep fails to see, and takes the velocity's part into the surface away so the rest
                // is the slide down it.
                else if (world.ProbeGround(feet, radius, cfg.StepHeight, 0.0, out double under, out Double3 underNormal) && under > feet.Y)
                {
                    feet = new Double3(feet.X, under, feet.Z);
                    double velInto = Double3.Dot(vel, underNormal);
                    if (velInto < 0.0) vel = vel - underNormal * velInto;
                }
            }
            if (swimming)
            {
                // Nor do a swimmer's feet end a step under a bottom rising beneath them: they are left on it, and the next
                // step stands them there.
                double lowest = floatLine - cfg.SwimSinkM;
                if (world.ProbeGround(feet, radius, cfg.StepHeight, 0.0, out double bottom, out _) && bottom > lowest) lowest = bottom;
                if (feet.Y < lowest)
                {
                    feet = new Double3(feet.X, lowest, feet.Z);
                    if (vel.Y < 0.0) vel = new Double3(vel.X, 0.0, vel.Z);
                }
            }

            s.East = feet.X;
            s.Up = feet.Y;
            s.North = feet.Z;
            s.VelEast = vel.X;
            s.VelUp = vel.Y;
            s.VelNorth = vel.Z;
            return s;
        }

        /// <summary>Angle of a surface from the horizontal, degrees, from its unit normal.</summary>
        /// <summary>
        /// The horizontal velocity moved toward the wished one: at the gait's acceleration while the change goes the way the
        /// body already moves (speeding up), at the brake otherwise (slowing, stopping, turning back); never past the wish.
        /// </summary>
        private static Double3 Approach(Double3 vel, Double3 wished, double accel, double brake, double dt)
        {
            double dx = wished.X - vel.X, dz = wished.Z - vel.Z;
            double gap = Math.Sqrt(dx * dx + dz * dz);
            if (gap < Epsilon) return wished;
            double rate = vel.X * dx + vel.Z * dz >= 0.0 ? accel : brake;
            double most = rate * dt;
            if (gap <= most) return wished;
            return new Double3(vel.X + dx / gap * most, 0.0, vel.Z + dz / gap * most);
        }

        public static double SlopeDeg(Double3 normal)
            => Math.Acos(SimMath.Clamp(normal.Y, -1.0, 1.0)) * GeoMath.RadToDeg;
    }
}
