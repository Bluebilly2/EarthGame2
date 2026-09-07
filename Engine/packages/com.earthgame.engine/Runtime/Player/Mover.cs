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
    /// airborne; water over the ground decides wading; on walkable ground the wish becomes a velocity at the
    /// speed <see cref="Locomotion"/> gives for the slope in that direction, a jump adds its take-off speed;
    /// in the air gravity acts and the wish only eases the horizontal velocity; the motion is then swept through
    /// the world, stepping onto low obstacles and sliding along everything else; finally grounded feet snap to
    /// the ground beneath them or discover there is none.</para>
    /// </summary>
    public static class Mover
    {
        private const int MaxSlideIterations = 4;
        private const double Epsilon = 1e-9;
        /// <summary>How close to the ground airborne feet must be to count as resting on it, metres.</summary>
        private const double LandingTolerance = 0.02;

        public static MoverState Step(MoverState s, MoverInput input, double dt, IWorldCollision world, MoverConfig cfg = null)
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

            // 1. What is under the feet. Grounded feet look a step up and a snap down; airborne feet only count
            //    ground they are resting on, and only while not rising (a jump must not be re-grounded on its
            //    first centimetre).
            bool groundFound;
            double groundUp;
            Double3 groundNormal;
            if (s.Grounded)
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

            // 2. Water over the ground.
            double water = world.WaterSurfaceAt(feet.X, feet.Z);
            double depth = double.IsNaN(water) ? 0.0 : water - (groundFound ? groundUp : feet.Y);
            s.Wading = depth > cfg.WadeDepth;
            bool deep = depth > cfg.DeepDepth;

            // 3. The velocity this step.
            Gait gait = input.Sprint && !s.Wading ? Gait.Running : Gait.Walking;
            double wishLen = input.WishLength;
            bool jumped = false;
            if (walkable)
            {
                feet = new Double3(feet.X, groundUp, feet.Z);
                double slopeAlong = 0.0;
                if (wishLen > Epsilon)
                    slopeAlong = -(groundNormal.X * input.WishEast + groundNormal.Z * input.WishNorth) / (groundNormal.Y * wishLen);
                double speed = Locomotion.SpeedMs(slopeAlong, gait, cfg.WorkCapacity);
                if (s.Stance == Stance.Crouching) speed *= cfg.CrouchSpeedFactor;
                if (s.Wading) speed *= deep ? cfg.DeepWadeSpeedFactor : cfg.WadeSpeedFactor;
                vel = new Double3(input.WishEast * speed, 0.0, input.WishNorth * speed);
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
                    double airSpeed = Locomotion.SpeedMs(0.0, gait, cfg.WorkCapacity);
                    double k = Math.Min(1.0, cfg.AirControl * dt);
                    vel = new Double3(vel.X + (input.WishEast * airSpeed - vel.X) * k, vel.Y, vel.Z + (input.WishNorth * airSpeed - vel.Z) * k);
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
            //    Falling feet that reached the ground during the sweep land.
            if (s.Grounded)
            {
                if (world.ProbeGround(feet, radius, cfg.StepHeight, cfg.GroundSnapDistance, out double below, out Double3 belowNormal)
                    && SlopeDeg(belowNormal) <= cfg.WalkableSlopeDeg)
                    feet = new Double3(feet.X, below, feet.Z);
                else
                    s.Grounded = false;
            }
            else if (vel.Y <= 0.0 && !jumped)
            {
                if (world.ProbeGround(feet, radius, LandingTolerance, LandingTolerance, out double landing, out Double3 landingNormal)
                    && SlopeDeg(landingNormal) <= cfg.WalkableSlopeDeg)
                {
                    feet = new Double3(feet.X, landing, feet.Z);
                    vel = new Double3(vel.X, 0.0, vel.Z);
                    s.Grounded = true;
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
        public static double SlopeDeg(Double3 normal)
            => Math.Acos(SimMath.Clamp(normal.Y, -1.0, 1.0)) * GeoMath.RadToDeg;
    }
}
