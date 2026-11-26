using System.Collections.Generic;
using UnityEngine;

namespace BananaBall
{
    public enum BallEvent { None, Bounce, Fence, Wall, Net, HomeRun, OverFence, Stands, Stopped }

    public struct BallSample
    {
        public float t;
        public Vector3 p;
        public bool air;   // still on its first flight (a catch here is a fly out)
    }

    public struct BallState
    {
        public Vector3 p, v;
        public bool rolling, touched;
        public int bounces;
    }

    public struct Prediction
    {
        public bool homeRun, stands, overFence, landed;
        public BallEvent firstEvent;
        public Vector3 land;
        public float landTime;
        public Vector3 end;
        public float endTime;
        public float apex;
    }

    /// <summary>
    /// Deterministic fixed-step physics for batted and thrown balls: gravity, quadratic drag, grass bounces and rolls,
    /// outfield fence, foul-ground walls and the backstop net. Predictions run the same Step, so a predicted catch point
    /// is exactly where the live ball will be.
    /// </summary>
    public static class BallSim
    {
        public const float Dt = 1f / 120f;
        const float Rest = 0.44f, RollMin = 1.3f, RollDec = 3.5f;

        public static BallEvent Step(ref BallState s, float dt)
        {
            BallEvent ev = BallEvent.None;
            float r = Cfg.BallRadius;
            if (!s.rolling)
            {
                float sp = s.v.magnitude;
                s.v += (Vector3.down * Cfg.Gravity - s.v * (Cfg.Drag * sp)) * dt;
                s.p += s.v * dt;
                float gy = Geo.GroundY(s.p) + r;
                if (s.p.y <= gy)
                {
                    s.p.y = gy;
                    float vy = -s.v.y;
                    float hs = Mathf.Sqrt(s.v.x * s.v.x + s.v.z * s.v.z);
                    if (vy < RollMin) { s.rolling = true; s.v.y = 0f; }
                    else
                    {
                        float steep = vy / Mathf.Max(0.01f, Mathf.Sqrt(vy * vy + hs * hs));
                        float fr = Mathf.Lerp(0.84f, 0.58f, steep);
                        s.v.y = vy * Rest;
                        s.v.x *= fr;
                        s.v.z *= fr;
                    }
                    s.bounces++;
                    s.touched = true;
                    ev = BallEvent.Bounce;
                }
            }
            else
            {
                float sp = Mathf.Sqrt(s.v.x * s.v.x + s.v.z * s.v.z);
                float dec = RollDec * dt;
                if (sp <= dec) { s.v = Vector3.zero; ev = BallEvent.Stopped; }
                else { float k = (sp - dec) / sp; s.v.x *= k; s.v.z *= k; s.v.y = 0f; }
                s.p += s.v * dt;
                s.p.y = Geo.GroundY(s.p) + r;
            }

            BallEvent wall = Walls(ref s);
            return wall != BallEvent.None ? wall : ev;
        }

        static BallEvent Walls(ref BallState s)
        {
            float r = Cfg.BallRadius;
            float rr = Mathf.Sqrt(s.p.x * s.p.x + s.p.z * s.p.z);
            Geo.LineCoords(s.p, out int side, out float ls, out float ld);

            // outfield fence (fair territory between the poles)
            if (ld <= 0f && rr > 40f)
            {
                float ang = Geo.AngleDeg(s.p);
                float R = Geo.FenceR(ang);
                if (rr >= R - r)
                {
                    float fh = Geo.FenceH(ang);
                    if (s.p.y > fh)
                    {
                        if (!s.touched) return BallEvent.HomeRun;
                        return BallEvent.OverFence;
                    }
                    Vector3 n = new Vector3(s.p.x / rr, 0f, s.p.z / rr);
                    float vn = Vector3.Dot(s.v, n);
                    if (vn > 0f) { s.v -= n * (vn * 1.32f); s.v.x *= 0.7f; s.v.z *= 0.7f; }
                    float back = R - r - 0.03f;
                    s.p.x = n.x * back;
                    s.p.z = n.z * back;
                    s.touched = true;
                    return BallEvent.Fence;
                }
                return BallEvent.None;
            }

            if (ld <= 0f) return BallEvent.None;

            if (ls < 0f)
            {
                // behind home plate: the backstop wall and its tall net
                if (rr >= Geo.WallD0 - r)
                {
                    if (s.p.y < Geo.NetH)
                    {
                        Vector3 n = new Vector3(s.p.x / rr, 0f, s.p.z / rr);
                        float vn = Vector3.Dot(s.v, n);
                        float rest = s.p.y < Geo.WallH ? 1.35f : 1.12f;
                        if (vn > 0f) { s.v -= n * (vn * rest); s.v.x *= 0.5f; s.v.z *= 0.5f; }
                        float back = Geo.WallD0 - r - 0.03f;
                        s.p.x = n.x * back;
                        s.p.z = n.z * back;
                        s.touched = true;
                        return BallEvent.Net;
                    }
                    return BallEvent.Stands;
                }
                return BallEvent.None;
            }

            if (ls >= Geo.FenceLine - r)
            {
                // corner wall between the foul pole and the foul-ground wall
                if (ld < Geo.WallD(ls))
                {
                    if (s.p.y < Geo.FenceH(45f))
                    {
                        Vector3 along = Geo.FromLine(side, 1f, 0f) - Geo.FromLine(side, 0f, 0f);
                        along.Normalize();
                        float vn = Vector3.Dot(s.v, along);
                        if (vn > 0f) { s.v -= along * (vn * 1.3f); s.v.x *= 0.7f; s.v.z *= 0.7f; }
                        s.p -= along * (ls - (Geo.FenceLine - r - 0.03f));
                        s.touched = true;
                        return BallEvent.Wall;
                    }
                    return BallEvent.Stands;
                }
                return BallEvent.Stands;
            }

            float wd = Geo.WallD(ls);
            if (ld >= wd - r)
            {
                if (s.p.y < Geo.WallH)
                {
                    Vector3 n = Geo.FromLine(side, 0f, 1f);
                    n.Normalize();
                    float vn = Vector3.Dot(s.v, n);
                    if (vn > 0f) { s.v -= n * (vn * 1.35f); s.v.x *= 0.75f; s.v.z *= 0.75f; }
                    s.p -= n * (ld - (wd - r - 0.03f));
                    s.touched = true;
                    return BallEvent.Wall;
                }
                return BallEvent.Stands;
            }
            return BallEvent.None;
        }

        public static Prediction Predict(BallState s, float maxT, List<BallSample> samples)
        {
            if (samples != null) samples.Clear();
            var pr = new Prediction { apex = s.p.y };
            float t = 0f;
            int k = 0;
            if (samples != null) samples.Add(new BallSample { t = 0f, p = s.p, air = !s.touched });
            while (t < maxT)
            {
                BallEvent ev = Step(ref s, Dt);
                t += Dt;
                k++;
                if (s.p.y > pr.apex) pr.apex = s.p.y;
                if (!pr.landed && ev != BallEvent.None && ev != BallEvent.Stopped)
                {
                    pr.landed = true;
                    pr.land = s.p;
                    pr.landTime = t;
                    pr.firstEvent = ev;
                }
                if (samples != null && ((k & 1) == 0 || ev != BallEvent.None))
                    samples.Add(new BallSample { t = t, p = s.p, air = !s.touched });
                if (ev == BallEvent.HomeRun) { pr.homeRun = true; break; }
                if (ev == BallEvent.OverFence) { pr.overFence = true; break; }
                if (ev == BallEvent.Stands) { pr.stands = true; break; }
                if (ev == BallEvent.Stopped) break;
            }
            pr.end = s.p;
            pr.endTime = t;
            return pr;
        }

        public static Vector3 SampleAt(List<BallSample> samples, float t, out bool air)
        {
            air = false;
            if (samples == null || samples.Count == 0) return Vector3.zero;
            if (t <= samples[0].t) { air = samples[0].air; return samples[0].p; }
            int lo = 0, hi = samples.Count - 1;
            if (t >= samples[hi].t) { air = samples[hi].air; return samples[hi].p; }
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (samples[mid].t < t) lo = mid; else hi = mid;
            }
            var a = samples[lo];
            var b = samples[hi];
            float u = (t - a.t) / Mathf.Max(1e-5f, b.t - a.t);
            air = a.air && b.air;
            return Vector3.Lerp(a.p, b.p, u);
        }

        /// <summary>Velocity that carries a thrown ball from a to b in time T under gravity and drag (approximate; drag is small over a throw).</summary>
        public static Vector3 ThrowVelocity(Vector3 a, Vector3 b, float T)
        {
            Vector3 d = b - a;
            Vector3 v = d / T;
            v.y += 0.5f * Cfg.Gravity * T;
            float sp = v.magnitude;
            v *= 1f + Cfg.Drag * sp * T * 0.5f;
            return v;
        }
    }

    /// <summary>
    /// Pitch flight. Constant acceleration from release to the plate (so curves bend late, as real pitches do), plus the
    /// magic-pitch extras: zigzag, floating balloon, phantom clones, rocket missile and the mid-air stop.
    /// </summary>
    public struct PitchPath
    {
        public Vector3 S, T, V0, A;
        public float tf;          // flight time from release to the plate (without the stop pause)
        public PitchType type;
        public float pause, pauseAt, seed;
        public bool rightArm;

        public float PlateTime => tf + pause;

        public static PitchPath Make(PitchType type, Vector3 release, Vector3 target, float tf, bool rightArm, float seed)
        {
            var p = new PitchPath { S = release, T = target, tf = tf, type = type, rightArm = rightArm, seed = seed };
            var info = PitchInfo.Of(type);
            float arm = rightArm ? -1f : 1f;           // a right-hander's arm side is toward third base (-x)
            Vector3 brk = new Vector3(arm * info.brk.x, info.brk.y, 0f);
            p.A = 2f * brk / (tf * tf);
            p.V0 = (target - release - 0.5f * p.A * tf * tf) / tf;
            if (type == PitchType.Stop) { p.pauseAt = tf * 0.47f; p.pause = 0.52f; }
            return p;
        }

        float Eff(float t)
        {
            if (pause <= 0f || t < pauseAt) return t;
            if (t < pauseAt + pause) return pauseAt;
            return t - pause;
        }

        public bool Paused(float t) => pause > 0f && t >= pauseAt && t < pauseAt + pause;

        Vector3 Base(float t)
        {
            if (t <= tf) return S + V0 * t + 0.5f * A * t * t;
            Vector3 v = V0 + A * tf;
            float d = t - tf;
            return T + v * d + 0.5f * Vector3.down * Cfg.Gravity * d * d * 0.6f;
        }

        public Vector3 Eval(float t)
        {
            float te = Eff(t);
            Vector3 p = Base(te);
            float s = Mathf.Clamp01(te / tf);
            switch (type)
            {
                case PitchType.Zigzag:
                    p.x += Mathf.Sin(Mathf.PI * 4f * s) * Mathf.Sin(Mathf.PI * s) * 0.48f;
                    break;
                case PitchType.Balloon:
                    p.y += Mathf.Sin(Mathf.PI * 5f * s + seed) * Mathf.Sin(Mathf.PI * s) * 0.32f;
                    p.x += Mathf.Sin(Mathf.PI * 3f * s + seed * 2f) * Mathf.Sin(Mathf.PI * s) * 0.18f;
                    break;
                case PitchType.Missile:
                    float w = Mathf.Sin(Mathf.PI * s);
                    p.x += (Mathf.PerlinNoise(seed, te * 22f) - 0.5f) * 0.08f * w;
                    p.y += (Mathf.PerlinNoise(te * 22f, seed) - 0.5f) * 0.08f * w;
                    break;
                case PitchType.Stop:
                    if (Paused(t)) p.y += Mathf.Sin((t - pauseAt) * 40f) * 0.012f;
                    break;
            }
            return p;
        }

        /// <summary>Phantom clones: they split from the real ball and fade before the plate.</summary>
        public Vector3 Ghost(float t, int i)
        {
            Vector3 p = Eval(t);
            float s = Mathf.Clamp01(t / tf);
            float side = i == 0 ? -1f : 1f;
            p.x += side * 0.62f * s * s;
            p.y += (i == 0 ? 0.18f : -0.12f) * s;
            return p;
        }

        public Vector3 Velocity(float t)
        {
            float h = 0.004f;
            return (Eval(t + h) - Eval(t - h)) / (2f * h);
        }

        /// <summary>Flight time for this pitcher and pitch.</summary>
        public static float FlightTime(PitchType type, PlayerData pitcher, float fatigue)
        {
            float fast = Mathf.Lerp(Cfg.FastTimeSlow, Cfg.FastTimeFast, pitcher.Vel) * (1f + 0.06f * fatigue);
            return fast * PitchInfo.Of(type).time;
        }

        public static Vector3 Release(PlayerData pitcher)
        {
            bool right = pitcher.throws == Hand.Right;
            float h = pitcher.body.height;
            return new Vector3((right ? -1f : 1f) * Cfg.ReleaseX, Cfg.ReleaseY * h / 1.88f, Cfg.ReleaseZ);
        }
    }

    public struct HitResult
    {
        public bool contact, foulBack, perfect, bunt;
        public Vector3 pos, vel;
        public float quality, exit, launch, spray;
    }

    /// <summary>Bat-on-ball model: timing error and how far the ball is from the contact area decide speed, launch and spray.</summary>
    public static class HitModel
    {
        public static float Radius(PlayerData b, bool auto, bool mega)
        {
            float r = Cfg.ContactR + Cfg.ContactRCon * b.Con;
            if (auto) r *= 0.9f;
            if (mega) r *= Cfg.MegaBatR;
            return r;
        }

        public static float Window(PlayerData b, bool mega) => (Cfg.Window + Cfg.WindowCon * b.Con) * (mega ? 1.25f : 1f);

        /// <summary>
        /// e: contact time minus plate time (negative = early). plate: ball crossing point at the plate (x, y).
        /// centre: contact area centre (x, y). at: ball position at contact.
        /// </summary>
        public static HitResult Swing(PlayerData b, float e, Vector2 plate, Vector2 centre, Vector3 at, bool auto, bool mega, System.Random rng)
        {
            var h = new HitResult();
            float R = Radius(b, auto, mega);
            float W = Window(b, mega);
            Vector2 d = (plate - centre) / R;
            float dist = d.magnitude;
            float te = Mathf.Abs(e) / W;
            if (te > 1f || dist > 1f + Cfg.BallRadius / R) return h;
            h.contact = true;
            h.pos = at;
            float qT = 1f - te * te;
            float qS = 1f - Mathf.Min(1f, dist * dist);
            h.quality = Mathf.Clamp01(qT * (0.35f + 0.65f * qS));
            h.perfect = te < Cfg.PerfectT && dist < Cfg.PerfectS;
            float sign = b.bats == Hand.Right ? 1f : -1f;

            if (qS < 0.2f || (te > 0.88f && rng.NextDouble() < 0.5))
            {
                // barely got a piece: foul tip back into the net or straight up
                h.foulBack = true;
                h.exit = MathX.Range(rng, 14f, 24f);
                h.launch = MathX.Range(rng, 8f, 55f);
                h.spray = (rng.NextDouble() < 0.5 ? -1f : 1f) * MathX.Range(rng, 110f, 172f);
                h.vel = Velocity(h.exit, h.launch, h.spray);
                return h;
            }

            float vmax = Cfg.ExitMax + Cfg.ExitPow * b.Pow;
            if (auto) vmax *= Cfg.AutoPower;
            if (mega) vmax *= Cfg.MegaBatPower;
            if (h.perfect) vmax *= 1.04f;
            h.exit = Mathf.Lerp(Cfg.ExitMin, vmax, Mathf.Pow(h.quality, 0.75f)) * MathX.Range(rng, 0.96f, 1.03f);

            float loft = 9f + 16f * b.Pow;
            h.launch = loft + 34f * d.y + MathX.Gauss(rng) * (h.perfect ? 2.5f : 8f);
            if (te > 0.7f) h.launch -= 8f * (te - 0.7f) / 0.3f;
            h.launch = Mathf.Clamp(h.launch, -28f, 72f);

            float u = e / W;
            h.spray = sign * u * 48f + 7f * d.x * sign + MathX.Gauss(rng) * (h.perfect ? 3f : 7f);
            if (te > 0.62f) h.spray += Mathf.Sign(h.spray == 0f ? sign * u : h.spray) * 30f * (te - 0.62f) / 0.38f;
            h.spray = Mathf.Clamp(h.spray, -88f, 88f);
            h.vel = Velocity(h.exit, h.launch, h.spray);
            return h;
        }

        /// <summary>Bunt: no timing needed, only the bat over the ball. Slow ball toward a chosen side.</summary>
        public static HitResult Bunt(PlayerData b, Vector2 plate, Vector2 centre, Vector3 at, System.Random rng)
        {
            var h = new HitResult { bunt = true };
            float R = (Cfg.ContactR + Cfg.ContactRCon * b.Con) * 1.45f;
            Vector2 d = (plate - centre) / R;
            float dist = d.magnitude;
            if (dist > 1f) return h;
            h.contact = true;
            h.pos = at;
            h.quality = 1f - dist;
            if (dist > 0.8f && rng.NextDouble() < 0.6)
            {
                h.foulBack = true;
                h.exit = MathX.Range(rng, 8f, 15f);
                h.launch = MathX.Range(rng, 5f, 40f);
                h.spray = (rng.NextDouble() < 0.5 ? -1f : 1f) * MathX.Range(rng, 95f, 160f);
                h.vel = Velocity(h.exit, h.launch, h.spray);
                return h;
            }
            h.exit = MathX.Range(rng, 6.5f, 11f) * (0.8f + 0.2f * h.quality);
            h.launch = Mathf.Clamp(-9f + 26f * d.y + MathX.Gauss(rng) * 4f, -25f, 38f);
            h.spray = (rng.NextDouble() < 0.5 ? -1f : 1f) * MathX.Range(rng, 6f, 34f) + d.x * 10f;
            if (rng.NextDouble() < 0.12) h.spray = Mathf.Sign(h.spray) * MathX.Range(rng, 46f, 70f);
            h.vel = Velocity(h.exit, h.launch, h.spray);
            return h;
        }

        public static Vector3 Velocity(float speed, float launchDeg, float sprayDeg)
        {
            float l = launchDeg * Mathf.Deg2Rad, s = sprayDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(s) * Mathf.Cos(l), Mathf.Sin(l), Mathf.Cos(s) * Mathf.Cos(l)) * speed;
        }
    }
}
