using System.Collections.Generic;
using UnityEngine;

namespace BananaBall
{
    public enum FState { Idle, Ready, Move, Chase, Cover, Hold, Dive, Down, Dizzy, Back }
    public enum RState { Base, Run, Out, Scored }
    public enum OutKind { Fly, Force, Tag, Strike }

    public class Fielder
    {
        public PlayerData p;
        public Ballplayer g;
        public Pos pos;
        public Vector3 at, goal;
        public float v, react;
        public FState st;
        public float stT;
        public int cover;            // base this fielder covers on the play (1..4), 0 = none
        public float holdT;
        public float throwAt = -1f;  // play time the ball leaves the hand
        public int throwTo;
        public bool runToBase;
        public float decidedAt;
        public Vector3 diveDir;
        public bool jumping;
        public float Speed => p.RunSpeed * 0.95f;
        public bool Busy => st == FState.Down || st == FState.Dizzy || st == FState.Dive;
    }

    public class Runner
    {
        public PlayerData p;
        public Ballplayer g;
        public float pos;           // along the base path: 0 home, 1 first, 2 second, 3 third, 4 home again
        public int start;           // base at contact
        public int target;          // base he is going to (or staying on)
        public float v;
        public bool forced, isBatter, manual, mustTag, tagged, safeAtFirst;
        public float holdAt = -1f;
        public float delay;
        public RState st;
        public Vector3 box;         // batter's start point
        public float goneT;
        public bool Moving => st == RState.Run;
    }

    /// <summary>
    /// One live ball, from bat contact until every runner is stopped and a fielder holds the ball. Real rules: fair/foul,
    /// fly outs with tag-ups and double-offs, force and tag outs, runs that do not count on a third-out force, home runs and
    /// ground-rule doubles. The catcher covers home (the pitcher backs him up). Both teams' fielders and runners are AI-driven;
    /// the human picks throws (or lets the recommended one go) and can send or hold runners.
    /// </summary>
    public class Play
    {
        public enum Mode { Flight, Held, Throw, Dead }
        public enum Kind { InPlay, Foul, HomeRun, GroundRule }

        readonly Match M;
        public readonly List<Fielder> F;
        public readonly List<Runner> R = new List<Runner>();
        public readonly Runner batter;
        public readonly bool humanField, humanRun;
        readonly System.Random rng;
        public Mode mode;
        public Kind kind = Kind.InPlay;
        public BallState ball;
        public Vector3 ballPos;
        public Fielder holder, chaser;
        readonly List<BallSample> pred = new List<BallSample>();
        public Prediction prd;
        public Vector3 catchPoint;
        public float catchTime;
        public bool catchAir;
        public float t;
        float acc, replanT, runT;
        public bool decided, fair, caught, landed, fielded, finished, deadBall;
        public float deadT;
        public bool error, divingCatch, doublePlay, bigHit;
        public int outsOnPlay, runsOnPlay, hitBases;
        readonly List<Runner> scoredList = new List<Runner>();
        float endWait = -1f;
        bool threeOuts;

        // throw in flight
        Fielder thrower, receiver;
        public int throwBase;
        Vector3 tA, tB;
        float tT, tDur, tH;

        // human throw prompt
        public bool prompt;
        public float promptT;
        public int recommend;
        public float dangerArrow;

        public Play(Match m, List<Fielder> fielders, Ballplayer batterGuy, PlayerData batterData, Vector3 box, HitResult hit, bool humanField, bool humanRun, System.Random rng)
        {
            M = m;
            F = fielders;
            this.humanField = humanField;
            this.humanRun = humanRun;
            this.rng = rng;
            ball = new BallState { p = hit.pos, v = hit.vel };
            ballPos = hit.pos;
            mode = Mode.Flight;
            bigHit = hit.perfect;
            batter = new Runner { p = batterData, g = batterGuy, pos = 0f, start = 0, target = 1, isBatter = true, st = RState.Run, box = box, delay = Cfg.BatterStart };
            R.Add(batter);
            for (int b = 3; b >= 1; b--)
                if (m.bases[b] != null)
                {
                    var g = m.GuyOf(m.bases[b]);
                    R.Add(new Runner { p = m.bases[b], g = g, pos = b, start = b, target = b, st = RState.Base });
                }
            foreach (var f in F)
            {
                f.react = Cfg.FielderReact * MathX.Range(rng, 0.8f, 1.25f) + (f.pos == Pos.P ? 0.18f : f.pos == Pos.C ? 0.12f : 0f);
                f.st = FState.Ready;
                f.cover = 0;
                f.throwAt = -1f;
                f.runToBase = false;
                f.jumping = false;
                f.g.hasBall = false;
                f.v = 0f;
            }
            Forces();
            Plan();
            AssignCovers();
            DecideRunners();
        }

        // ------------------------------------------------------------------ helpers

        public static Vector3 PathPos(float pos)
        {
            pos = Mathf.Clamp(pos, 0f, 4f);
            int k = Mathf.Min(3, Mathf.FloorToInt(pos));
            float f = pos - k;
            return Vector3.Lerp(Geo.Base(k), Geo.Base(k + 1 == 4 ? 0 : k + 1), f);
        }

        Vector3 RunnerWorld(Runner r)
        {
            if (r.isBatter && r.pos < 1f) return Vector3.Lerp(r.box, Geo.First, r.pos);
            return PathPos(r.pos);
        }

        Fielder At(Pos p) { foreach (var f in F) if (f.pos == p) return f; return null; }

        bool IsOutfielder(Fielder f) => f.pos == Pos.LF || f.pos == Pos.CF || f.pos == Pos.RF;

        float ThrowTime(Fielder h, Vector3 from, int b)
        {
            float d = MathX.Horiz(from, Geo.Cover(b));
            return Ballplayer.ThrowRelease + d / h.p.ArmSpeed + 0.06f + d * 0.002f;
        }

        float RunnerTime(Runner r, float to)
        {
            float d = Mathf.Abs(to - r.pos) * Geo.BaseDist;
            float vmax = r.p.RunSpeed;
            float v0 = (to > r.pos) == (r.v > 0f) ? Mathf.Min(Mathf.Abs(r.v), vmax) : 0f;
            float tAcc = (vmax - v0) / 7.5f;
            float dAcc = (v0 + vmax) * 0.5f * tAcc;
            float tt = d <= dAcc ? d / Mathf.Max(0.5f, (v0 + vmax) * 0.5f) : tAcc + (d - dAcc) / vmax;
            return tt + Mathf.Max(0f, r.delay);
        }

        /// <summary>Earliest time from now the ball can be at base b.</summary>
        float BallTime(int b)
        {
            switch (mode)
            {
                case Mode.Held:
                {
                    float gather = Mathf.Max(0f, Cfg.GatherTime - holder.holdT);
                    if (holder.throwAt >= 0f) gather = 0f;
                    float d = MathX.Horiz(holder.at, Geo.Base(b == 4 ? 0 : b));
                    if (d < 3f) return gather + d / holder.Speed;
                    return gather + ThrowTime(holder, holder.at, b);
                }
                case Mode.Throw:
                {
                    float rem = Mathf.Max(0f, tDur - tT);
                    if (throwBase == b) return rem;
                    return rem + 0.25f + (receiver != null ? ThrowTime(receiver, tB, b) : 2f);
                }
                case Mode.Flight:
                {
                    if (chaser == null) return 9f;
                    float tc = Mathf.Max(0f, catchTime - t);
                    return tc + Cfg.GatherTime + ThrowTime(chaser, catchPoint, b);
                }
                default:
                    return 99f;
            }
        }

        // ------------------------------------------------------------------ planning

        /// <summary>Who gets to the ball first, where and when.</summary>
        void Plan()
        {
            prd = BallSim.Predict(ball, 9f, pred);
            Fielder best = null;
            float bestT = 1e9f;
            Vector3 bestP = Vector3.zero;
            bool bestAir = false;
            foreach (var f in F)
            {
                if (f.Busy || f == holder) continue;
                if (Intercept(f, out float it, out Vector3 ip, out bool air) && it < bestT)
                {
                    bestT = it; bestP = ip; best = f; bestAir = air;
                }
            }
            if (best == null)
            {
                // nobody can get it in time: closest fielder to where it stops
                float bd = 1e9f;
                foreach (var f in F)
                {
                    if (f.Busy) continue;
                    float d = MathX.Horiz(f.at, prd.end);
                    if (d < bd) { bd = d; best = f; }
                }
                bestP = prd.end;
                bestT = t + prd.endTime + bd / Mathf.Max(1f, best != null ? best.Speed : 6f);
                bestAir = false;
            }
            if (chaser != null && chaser != best && chaser.st == FState.Chase) chaser.st = FState.Ready;
            chaser = best;
            catchPoint = bestP;
            catchTime = bestT;
            catchAir = bestAir;
            if (chaser != null)
            {
                chaser.st = FState.Chase;
                chaser.goal = bestP;
                if (chaser.cover > 0) { chaser.cover = 0; AssignCovers(); }
            }
        }

        bool Intercept(Fielder f, out float it, out Vector3 ip, out bool air)
        {
            it = 0f; ip = Vector3.zero; air = false;
            float react = Mathf.Max(0f, f.react - t);
            float sp = f.Speed;
            float reach = Cfg.ReachH;
            for (int i = 1; i < pred.Count; i++)
            {
                var s = pred[i];
                if (s.p.y > Cfg.CatchTop) continue;
                if (s.p.y > 0.9f && !s.air) continue;
                float run = Mathf.Max(0f, MathX.Horiz(f.at, s.p) - reach);
                float need = react + run / sp;
                if (need <= s.t)
                {
                    it = t + s.t;
                    ip = new Vector3(s.p.x, 0f, s.p.z);
                    air = s.air;
                    // fly balls: wait under it rather than meeting it high
                    if (s.air && s.p.y > 1.2f)
                    {
                        for (int j = i + 1; j < pred.Count; j++)
                        {
                            if (!pred[j].air || pred[j].p.y < 0.7f) break;
                            ip = new Vector3(pred[j].p.x, 0f, pred[j].p.z);
                            it = t + pred[j].t;
                            if (pred[j].p.y < 1.6f) break;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        void AssignCovers()
        {
            foreach (var f in F) if (f != chaser && f != holder && f.cover > 0) f.cover = 0;
            bool right = ball.p.x > 0f || (chaser != null && chaser.at.x > 0f);
            Pos[][] pref =
            {
                null,
                new[] { Pos.B1, Pos.P, Pos.B2 },
                right ? new[] { Pos.SS, Pos.B2, Pos.P } : new[] { Pos.B2, Pos.SS, Pos.P },
                new[] { Pos.B3, Pos.SS, Pos.P },
                new[] { Pos.C, Pos.P, Pos.B1 },
            };
            for (int b = 1; b <= 4; b++)
            {
                foreach (var p in pref[b])
                {
                    var f = At(p);
                    if (f == null || f == chaser || f == holder || f.cover > 0 || f.Busy) continue;
                    f.cover = b;
                    if (f.st != FState.Hold) f.st = FState.Cover;
                    f.goal = Geo.Cover(b);
                    break;
                }
            }
            foreach (var f in F)
            {
                if (f == chaser || f == holder || f.cover > 0 || f.Busy) continue;
                f.st = IsOutfielder(f) ? FState.Back : FState.Ready;
            }
        }

        Fielder CoverOf(int b)
        {
            foreach (var f in F) if (f.cover == b && f != holder) return f;
            Fielder best = null;
            float bd = 1e9f;
            Vector3 bp = Geo.Cover(b);
            foreach (var f in F)
            {
                if (f == holder || f.Busy) continue;
                float d = MathX.Horiz(f.at, bp);
                if (d < bd) { bd = d; best = f; }
            }
            if (best != null) { best.cover = b; best.st = FState.Cover; best.goal = bp; }
            return best;
        }

        // ------------------------------------------------------------------ forces and outs

        void Forces()
        {
            foreach (var r in R) r.forced = false;
            if (caught) return;
            batter.forced = batter.st != RState.Out && !batter.safeAtFirst && batter.pos < 1f;
            bool pushing = batter.st != RState.Out;
            for (int b = 1; b <= 3; b++)
            {
                Runner r = null;
                foreach (var x in R) if (!x.isBatter && x.start == b) r = x;
                if (r == null || !pushing) break;
                if (r.st == RState.Out) break;
                r.forced = r.pos < b + 1f;
            }
        }

        void Out(Runner r, OutKind k)
        {
            if (r.st == RState.Out || r.st == RState.Scored) return;
            r.st = RState.Out;
            r.v = 0f;
            r.goneT = 0f;
            outsOnPlay++;
            if (outsOnPlay >= 2) doublePlay = true;
            int total = M.AddOut();
            if (!r.g.Floored)
            {
                double o = rng.NextDouble();
                if (o < 0.3) r.g.Ragdoll(r.g.transform.forward * -1.5f + Vector3.up * 1.2f, r.g.transform.right * -4f, 1.2f);   // collapses in a heap
                else r.g.Play(o < 0.55 ? Act.Flop : o < 0.8 ? Act.Tantrum : Act.Sad);
            }
            M.OnRunnerOut(r, k, this);
            if (total >= Cfg.Outs)
            {
                threeOuts = true;
                bool cancel = k == OutKind.Force || k == OutKind.Fly || (r.isBatter && !r.safeAtFirst);
                if (cancel)
                    for (int i = scoredList.Count - 1; i >= 0; i--) { M.RemoveRun(); runsOnPlay--; scoredList.RemoveAt(i); }
                endWait = 0.9f;
                foreach (var x in R) if (x.st == RState.Run) { x.st = RState.Base; x.v = 0f; }
                return;
            }
            Forces();
        }

        void Score(Runner r)
        {
            if (r.st == RState.Scored || threeOuts) return;
            r.st = RState.Scored;
            r.v = 0f;
            r.pos = 4f;
            r.goneT = 0f;
            runsOnPlay++;
            scoredList.Add(r);
            M.AddRun(r, this);
        }

        /// <summary>Fielder f holds the ball at base b: force-outs and double-offs happen now; runners arriving later are tagged.</summary>
        void BaseTouched(int b, Fielder f)
        {
            if (threeOuts) return;
            foreach (var r in R.ToArray())
            {
                if (r.st == RState.Out || r.st == RState.Scored) continue;
                int fb = r.isBatter ? 1 : r.start + 1;
                if (r.forced && fb == b && r.pos < b - 0.001f) { Out(r, OutKind.Force); M.Popup("OUT!", Pal.Red); continue; }
                if (caught && r.mustTag && !r.tagged && r.start == b && (b != 0)) { Out(r, OutKind.Tag); M.Popup("DOUBLED OFF!", Pal.Red); continue; }
            }
        }

        // ------------------------------------------------------------------ events

        void Decide(bool isFair)
        {
            if (decided) return;
            decided = true;
            fair = isFair;
            if (!fair) Foul();
        }

        void Foul()
        {
            kind = Kind.Foul;
            deadBall = true;
            deadT = 0f;
            mode = Mode.Dead;
            foreach (var r in R)
            {
                if (r.isBatter) { r.st = RState.Base; r.v = 0f; continue; }
                r.target = r.start;
                r.holdAt = -1f;
            }
            foreach (var f in F) if (f.st == FState.Chase) f.st = FState.Ready;
        }

        void HomeRun()
        {
            kind = Kind.HomeRun;
            deadBall = true;
            deadT = 0f;
            mode = Mode.Dead;
            decided = fair = true;
            foreach (var r in R)
            {
                if (r.st == RState.Out) continue;
                r.target = 4;
                r.manual = true;
                r.holdAt = -1f;
                r.mustTag = false;
                // the batter stops to celebrate (the camera comes to him once the ball lands), then trots
                if (r.isBatter) { r.v = 0f; r.delay = 3.9f; }   // play time (Cfg.PlaySpeed): about 3.2 s
                else r.delay = 0.2f;
            }
            foreach (var f in F) { f.st = FState.Ready; f.g.Set(Act.Sad); }
            // a fielder standing where the batter stops to celebrate steps out of his way
            Vector3 bp = batter.g.pos;
            foreach (var f in F)
            {
                Vector3 d = f.at - bp;
                d.y = 0f;
                if (d.magnitude > 1.9f || f.g.Floored) continue;
                Vector3 away = d.sqrMagnitude > 1e-3f ? d.normalized : Vector3.right;
                f.st = FState.Cover;
                f.goal = bp + away * 2.8f;
                f.goal.y = 0f;
            }
            hitBases = 4;
        }

        void GroundRule()
        {
            kind = Kind.GroundRule;
            deadBall = true;
            deadT = 0f;
            mode = Mode.Dead;
            decided = fair = true;
            foreach (var r in R)
            {
                if (r.st == RState.Out) continue;
                r.target = Mathf.Min(4, (r.isBatter ? 0 : r.start) + 2);
                r.manual = true;
                r.holdAt = -1f;
            }
            foreach (var f in F) if (f.st == FState.Chase) f.st = FState.Ready;
        }

        void OnBallEvent(BallEvent ev)
        {
            switch (ev)
            {
                case BallEvent.Bounce:
                    if (!landed)
                    {
                        landed = true;
                        M.OnBallLanded(this);
                        if (!decided)
                        {
                            Geo.LineCoords(ball.p, out _, out float s, out float d);
                            if (s >= Geo.BaseDist || MathX.Horiz(ball.p, Vector3.zero) > 32f) Decide(d <= 0f);
                        }
                        if (mode == Mode.Flight) { foreach (var r in R) r.holdAt = -1f; DecideRunners(); }
                    }
                    break;
                case BallEvent.Fence:
                    if (!decided) Decide(true);
                    landed = true;
                    M.PlaySfx(Sfx.Id.Thud, 0.8f);
                    break;
                case BallEvent.Wall:
                    if (!decided) Decide(false);
                    landed = true;
                    M.PlaySfx(Sfx.Id.Thud, 0.6f);
                    break;
                case BallEvent.Net:
                    if (!decided || !fair) Decide(false);
                    landed = true;
                    break;
                case BallEvent.HomeRun:
                    HomeRun();
                    M.OnHomeRun(this);
                    break;
                case BallEvent.OverFence:
                    if (!decided) Decide(Geo.FairAngle(ball.p));
                    if (fair) { GroundRule(); M.Popup("GROUND RULE\nDOUBLE!", Pal.Gold); }
                    break;
                case BallEvent.Stands:
                    if (decided && fair) { GroundRule(); M.Popup("GROUND RULE\nDOUBLE!", Pal.Gold); }
                    else { Decide(false); if (kind == Kind.Foul) M.OnFoulIntoStands(this); }
                    break;
                case BallEvent.Stopped:
                    if (!decided) Decide(Geo.FairAngle(ball.p));
                    break;
            }
        }

        void FairCheck()
        {
            if (decided || !landed) return;
            Geo.LineCoords(ball.p, out _, out float s, out float d);
            if (s >= Geo.BaseDist) Decide(d <= 0f);
        }

        void TryCatch()
        {
            if (mode != Mode.Flight || threeOuts) return;
            // nobody gloves a ball in the first instant off the bat: a late swing meets the ball right over the catcher's mitt,
            // and without this he 'caught' rockets (no-doubt homers included) before they had left the plate
            if (t < 0.12f) return;
            foreach (var f in F)
            {
                if (f.st == FState.Down || f.st == FState.Dizzy || f == holder) continue;
                float reach = f.st == FState.Dive ? Cfg.DiveReach * 0.6f : Cfg.ReachH;
                float top = f.jumping ? Cfg.JumpTop : Cfg.CatchTop;
                if (ballPos.y > top) continue;
                if (MathX.Horiz(f.at, ballPos) > reach) continue;
                if (ballPos.y > 1.0f && ball.touched && ball.v.y > 0f) continue;
                // tough chances can be muffed (wacky bonk off the head)
                float chance = Cfg.ErrorChance;
                if (f.st == FState.Dive) chance = 0.4f;
                if (!ball.touched && ball.v.magnitude > 30f) chance += 0.06f;
                if (rng.NextDouble() < chance)
                {
                    Muff(f);
                    return;
                }
                Catch(f);
                return;
            }
        }

        void Muff(Fielder f)
        {
            error = true;
            M.errors[M.Fld]++;
            Vector3 v = ball.v;
            ball.v = new Vector3(-v.x * 0.25f + MathX.Range(rng, -2f, 2f), Mathf.Abs(v.y) * 0.2f + 3.5f, -v.z * 0.25f + MathX.Range(rng, -2f, 2f));
            ball.touched = true;
            ball.rolling = false;
            landed = true;
            if (!decided)
            {
                Geo.LineCoords(ball.p, out _, out float s, out float d);
                if (s >= Geo.BaseDist || MathX.Horiz(ball.p, Vector3.zero) > 32f) Decide(d <= 0f);
            }
            if (rng.NextDouble() < 0.65)
            {
                // knocked flat by the ball
                Vector3 bd = v.sqrMagnitude > 1e-3f ? v.normalized : Vector3.back;
                f.st = FState.Down;
                f.stT = 0f;
                f.g.Ragdoll(new Vector3(bd.x, 0f, bd.z) * 2.4f + Vector3.up * 1.6f, Vector3.Cross(Vector3.up, bd) * 5f, 0.9f, RagdollRig.SegHead, bd * 7f);
            }
            else
            {
                f.st = FState.Dizzy;
                f.stT = 0f;
                f.g.Play(Act.Dizzy);
            }
            M.OnMuff(f, this);
            replanT = 0f;
            foreach (var r in R) r.holdAt = -1f;
            DecideRunners();
        }

        void Catch(Fielder f)
        {
            bool fly = !ball.touched;
            if (!fly && !decided)
            {
                Decide(Geo.FairAngle(ball.p));
                if (!fair) return;
            }
            if (f.st == FState.Dive || f.g.act == Act.Dive) divingCatch = true;
            holder = f;
            mode = Mode.Held;
            f.st = FState.Hold;
            f.holdT = 0f;
            f.throwAt = -1f;
            f.runToBase = false;
            f.g.hasBall = true;
            f.cover = 0;
            fielded = true;
            ball.v = Vector3.zero;
            f.jumping = false;
            M.OnCatch(f, fly, this);
            if (fly)
            {
                caught = true;
                decided = true;
                fair = true;
                Out(batter, OutKind.Fly);
                foreach (var r in R)
                {
                    if (r.isBatter || r.st == RState.Out) continue;
                    r.holdAt = -1f;
                    r.mustTag = true;
                    r.tagged = Mathf.Abs(r.pos - r.start) < 0.02f;
                    if (!r.tagged) { r.target = r.start; }
                }
                Forces();
            }
            AssignCovers();
            DecideRunners();
        }

        // ------------------------------------------------------------------ throws

        /// <summary>The AI's choice for the ball holder: the base where an out is likeliest (lead runners first), or 0 to hold.</summary>
        int BestBase(Fielder h, out bool run, out float bestMargin)
        {
            run = false;
            int best = 0;
            float bestScore = -1e9f;
            bestMargin = -9f;
            float gather = Mathf.Max(0f, Cfg.GatherTime - h.holdT);
            for (int b = 1; b <= 4; b++)
            {
                Runner r = null;
                float tr = 1e9f;
                foreach (var x in R)
                {
                    if (x.st == RState.Out || x.st == RState.Scored) continue;
                    bool heading = x.target >= b && x.pos < b - 0.001f && x.pos > b - 1.001f;
                    bool doubleOff = caught && x.mustTag && !x.tagged && x.start == b;
                    if (!heading && !doubleOff) continue;
                    float tt = doubleOff ? RunnerTime(x, b) : RunnerTime(x, b);
                    if (tt < tr) { tr = tt; r = x; }
                }
                if (r == null) continue;
                float d = MathX.Horiz(h.at, Geo.Base(b == 4 ? 0 : b));
                bool canRun = d < 3.2f;
                float tb = canRun ? gather + d / h.Speed : gather + ThrowTime(h, h.at, b);
                bool forcedHere = r.forced && (r.isBatter ? 1 : r.start + 1) == b;
                bool tagPlay = !forcedHere;
                float margin = tr - tb - (tagPlay ? 0.18f : 0f);
                float score = margin > 0.04f ? 10f + b * 2f + (forcedHere ? 3f : 0f) + Mathf.Min(margin, 1f) : -10f + margin;
                if (score > bestScore) { bestScore = score; best = b; run = canRun; bestMargin = margin; }
            }
            if (bestScore < 0f)
            {
                // no out: get the ball back to the infield, toward the base the lead runner wants
                int lead = 0;
                foreach (var x in R) if (x.st == RState.Run && x.target > lead) lead = x.target;
                if (IsOutfielder(h) || MathX.Horiz(h.at, Geo.Rubber) > 30f)
                {
                    run = false;
                    return lead >= 3 ? Mathf.Min(4, lead) : 2;
                }
                if (lead > 0 && lead <= 4) { run = false; return lead == 1 ? 0 : lead; }
                return 0;
            }
            return best;
        }

        public void HumanThrow(int b)
        {
            if (holder == null || mode != Mode.Held || holder.throwAt >= 0f || !humanField) return;
            Throw(holder, b, false);
        }

        void Throw(Fielder h, int b, bool allowRun)
        {
            prompt = false;
            h.decidedAt = t;
            if (b <= 0) { h.throwTo = 0; h.throwAt = -2f; return; }
            float d = MathX.Horiz(h.at, Geo.Base(b == 4 ? 0 : b));
            if (allowRun && d < 3.2f)
            {
                h.runToBase = true;
                h.throwTo = b;
                h.goal = Geo.Base(b == 4 ? 0 : b);
                h.throwAt = -2f;
                return;
            }
            h.throwTo = b;
            h.throwAt = t + Mathf.Max(0f, Cfg.GatherTime - h.holdT) + Ballplayer.ThrowRelease;
            h.g.Play(Act.Throw);
            receiver = CoverOf(b);
            M.OnThrowDecided(h, b, this);
        }

        void Release(Fielder h)
        {
            int b = h.throwTo;
            thrower = h;
            throwBase = b;
            receiver = CoverOf(b);
            tA = h.g.HandWorld;
            Vector3 cp = Geo.Cover(b);
            tB = cp + Vector3.up * 1.15f;
            float d = MathX.Horiz(tA, tB);
            tDur = d / h.p.ArmSpeed + 0.06f;
            tH = Mathf.Min(3.5f, 0.15f + d * 0.018f);
            tT = 0f;
            mode = Mode.Throw;
            holder = null;
            h.g.hasBall = false;
            h.st = FState.Ready;
            h.throwAt = -1f;
            M.PlaySfx(Sfx.Id.Whoosh, 0.5f);
            DecideRunners();
        }

        void StepThrow(float dt)
        {
            tT += dt;
            float s = Mathf.Clamp01(tT / tDur);
            ballPos = Vector3.Lerp(tA, tB, s) + Vector3.up * tH * 4f * s * (1f - s);
            if (s < 1f) return;
            if (receiver != null && !receiver.Busy && MathX.Horiz(receiver.at, tB) < 1.4f)
            {
                if (rng.NextDouble() < 0.012)
                {
                    // wacky: the throw bonks the receiver
                    LooseAt(tB, (tB - tA).normalized * 4f);
                    receiver.st = FState.Dizzy;
                    receiver.stT = 0f;
                    receiver.g.Play(Act.Dizzy);
                    M.OnMuff(receiver, this);
                    error = true;
                    M.errors[M.Fld]++;
                    return;
                }
                holder = receiver;
                mode = Mode.Held;
                receiver.st = FState.Hold;
                receiver.holdT = Cfg.GatherTime - 0.08f;
                receiver.throwAt = -1f;
                receiver.runToBase = false;
                receiver.g.hasBall = true;
                M.PlaySfx(Sfx.Id.Pop, 0.7f);
                int b = throwBase;
                receiver.cover = b;
                BaseTouched(b, receiver);
                DecideRunners();
                return;
            }
            LooseAt(tB, (tB - tA).normalized * 6f);
        }

        void LooseAt(Vector3 p, Vector3 v)
        {
            mode = Mode.Flight;
            ball = new BallState { p = new Vector3(p.x, Mathf.Max(p.y, 0.3f), p.z), v = new Vector3(v.x, 1.5f, v.z), touched = true };
            landed = true;
            holder = null;
            replanT = 0f;
            Plan();
            AssignCovers();
        }

        // ------------------------------------------------------------------ runners

        public void HumanAdvance(int b)
        {
            if (!humanRun || finished || deadBall) return;
            Runner best = null;
            float bp = -1f;
            foreach (var r in R)
            {
                if (r.st == RState.Out || r.st == RState.Scored) continue;
                if (r.pos < b - 0.001f && r.pos > bp && r.target < b) { bp = r.pos; best = r; }
            }
            if (best == null) return;
            // only if no other runner sits between him and b
            foreach (var r in R)
                if (r != best && r.st != RState.Out && r.st != RState.Scored && r.pos > best.pos && r.pos < b - 0.001f && r.target < b) return;
            if (caught && best.mustTag && !best.tagged) return;
            best.target = b;
            best.manual = true;
            best.holdAt = -1f;
            if (best.st == RState.Base) best.st = RState.Run;
            M.PlaySfx(Sfx.Id.Tap, 0.6f);
        }

        public void HumanBack(int b)
        {
            if (!humanRun || finished || deadBall) return;
            foreach (var r in R)
            {
                if (r.st != RState.Run || r.forced || r.isBatter && r.pos < 1f) continue;
                if (r.target == b && r.pos < b)
                {
                    r.target = Mathf.FloorToInt(r.pos + 1e-4f);
                    if (r.target < r.pos - 0.001f) { } // heading back
                    r.manual = true;
                    M.PlaySfx(Sfx.Id.Tap, 0.6f);
                    return;
                }
            }
        }

        /// <summary>Is any runner heading to base b (for the HUD)?</summary>
        public Runner HeadingTo(int b)
        {
            foreach (var r in R)
                if (r.st == RState.Run && r.target == b && r.pos < b) return r;
            return null;
        }

        public Runner StandingOn(int b)
        {
            foreach (var r in R)
                if (r.st != RState.Out && r.st != RState.Scored && Mathf.Abs(r.pos - b) < 0.01f && r.target == b) return r;
            return null;
        }

        float CatchLikely()
        {
            if (!catchAir || chaser == null) return 0f;
            return 0.85f;
        }

        /// <summary>AI base-running for both teams (the human's runners too, unless the player took over).</summary>
        void DecideRunners()
        {
            if (finished || deadBall || threeOuts) return;
            float margin = humanRun ? 0.5f : 0.32f;
            int outsNow = M.outs;
            if (outsNow >= 2) margin -= 0.12f;
            var order = new List<Runner>(R);
            order.Sort((a, b) => b.pos.CompareTo(a.pos));
            float blockAt = 5f;
            foreach (var r in order)
            {
                if (r.st == RState.Out || r.st == RState.Scored) continue;
                int cap = Mathf.FloorToInt(blockAt - 0.001f);
                int last = Mathf.Clamp(Mathf.FloorToInt(r.pos + 1e-4f), 0, 3);
                int forceBase = r.isBatter ? 1 : r.start + 1;
                if (caught && r.mustTag && !r.tagged)
                {
                    r.target = r.start;
                    blockAt = Mathf.Min(blockAt, r.pos);
                    continue;
                }
                if (!landed && !caught && mode == Mode.Flight && !r.isBatter)
                {
                    if (outsNow >= 2) r.holdAt = -1f;
                    else r.holdAt = r.start + (CatchLikely() > 0.5f ? 0.18f : 0.5f);
                    r.target = Mathf.Min(Mathf.Max(r.target, r.start + 1), Mathf.Max(cap, r.start));
                    blockAt = Mathf.Min(blockAt, r.target);
                    if (Mathf.Abs(r.target - r.pos) > 0.001f) r.st = RState.Run;
                    continue;
                }
                if (!r.manual)
                {
                    int must = r.forced ? forceBase : (r.isBatter && r.pos < 1f ? 1 : last);
                    int go = must;
                    for (int b = last + 1; b <= 4; b++)
                    {
                        if (b <= must) continue;
                        float m = BallTime(b) - RunnerTime(r, b);
                        if (b == 4) m -= 0.12f;
                        if (m > margin) go = b; else break;
                    }
                    // caught between bases without a force: pick the safer bag
                    if (!r.forced && go == last && r.pos > last + 0.001f && !(r.isBatter && r.pos < 1f))
                    {
                        float mf = BallTime(last + 1) - RunnerTime(r, last + 1);
                        float mb = BallTime(last) - RunnerTime(r, last);
                        if (mf > mb) go = last + 1;
                    }
                    // keep a committed target while it is still reasonably safe
                    if (r.target > go && r.target <= 4)
                    {
                        float mk = BallTime(r.target) - RunnerTime(r, r.target);
                        if (mk > margin * 0.35f) go = r.target;
                    }
                    r.target = go;
                }
                if (r.target > cap) r.target = Mathf.Max(cap, r.forced ? forceBase : last);
                if (r.target < 0) r.target = 0;
                blockAt = Mathf.Min(blockAt, r.target);
                if (Mathf.Abs(r.target - r.pos) > 0.001f) r.st = RState.Run;
            }
        }

        void StepRunner(Runner r, float dt)
        {
            if (r.st == RState.Out || r.st == RState.Scored)
            {
                r.goneT += dt;
                Vector3 off = r.st == RState.Scored ? Geo.Dugout(M.Bat) : Geo.Dugout(M.Bat);
                Vector3 p = r.g.pos;
                if (r.goneT > 0.9f)
                {
                    Vector3 d = off - p; d.y = 0f;
                    if (d.magnitude > 0.5f) { r.g.pos = p + d.normalized * 4.5f * dt; r.g.yaw = MathX.YawTo(p, off); r.g.Set(Act.Jog); }
                    if (r.goneT > 3.2f) r.g.Show(false);
                }
                return;
            }
            if (r.delay > 0f) { r.delay -= dt; Pose(r); return; }
            float goal = r.target;
            if (r.holdAt >= 0f && !caught && !landed) goal = Mathf.Min(goal, r.holdAt);
            if (caught && r.mustTag && !r.tagged) goal = r.start;
            float diff = goal - r.pos;
            if (Mathf.Abs(diff) < 1e-4f)
            {
                r.pos = goal;
                r.v = 0f;
                if (Mathf.Abs(goal - Mathf.Round(goal)) < 1e-3f) r.st = RState.Base;
                Pose(r);
                return;
            }
            r.st = RState.Run;
            float dir = Mathf.Sign(diff);
            float vmax = r.p.RunSpeed * (kind == Kind.HomeRun || kind == Kind.GroundRule ? 0.5f : 1f);
            if (r.v > 0f && dir != Mathf.Sign(r.v)) r.v = 0f;
            float sp = Mathf.Min(vmax, Mathf.Abs(r.v) + 7.5f * dt);
            r.v = sp * dir;
            float step = sp * dt / Geo.BaseDist;
            float np = r.pos + dir * step;
            if ((dir > 0f && np >= goal) || (dir < 0f && np <= goal)) np = goal;
            // bases crossed on the way
            if (dir > 0f)
            {
                int b0 = Mathf.FloorToInt(r.pos + 1e-4f) + 1, b1 = Mathf.FloorToInt(np + 1e-4f);
                for (int b = b0; b <= b1; b++)
                {
                    r.pos = b;
                    if (Arrive(r, b)) { Pose(r); return; }
                }
            }
            else
            {
                int b0 = Mathf.CeilToInt(r.pos - 1e-4f) - 1, b1 = Mathf.CeilToInt(np - 1e-4f);
                for (int b = b0; b >= b1; b--)
                {
                    r.pos = b;
                    if (Arrive(r, b)) { Pose(r); return; }
                }
            }
            r.pos = np;
            Pose(r);
        }

        /// <summary>Runner touches base b. Returns true if he is done moving for this step (out, scored).</summary>
        bool Arrive(Runner r, int b)
        {
            // a fielder already holding the ball on this base: tagged (or forced) out
            if (holder != null && mode == Mode.Held && kind == Kind.InPlay && !threeOuts && MathX.Horiz(holder.at, Geo.Base(b == 4 ? 0 : b)) < 1.4f
                && !(caught && r.mustTag && !r.tagged && b == r.start))
            {
                bool bangBang = holder.holdT < 0.12f;
                // a play at the plate is a train wreck more often than not
                bool wreck = rng.NextDouble() < (b == 4 ? 0.75 : 0.3);
                if (!bangBang || rng.NextDouble() < 0.5)
                {
                    bool forcedHere = r.forced && b == (r.isBatter ? 1 : r.start + 1);
                    if (wreck) Collide(r, holder, true);
                    Out(r, forcedHere ? OutKind.Force : OutKind.Tag);
                    M.Popup("OUT!", Pal.Red);
                    return true;
                }
                if (wreck) Collide(r, holder, false);
                M.Popup("SAFE!", Pal.Green);
            }
            if (b == 4) { Score(r); return true; }
            if (r.isBatter && b == 1) { r.safeAtFirst = true; Forces(); }
            if (caught && r.mustTag && !r.tagged && b == r.start) { r.tagged = true; M.PlaySfx(Sfx.Id.Tap, 0.4f); }
            if (r.forced && b == (r.isBatter ? 1 : r.start + 1)) r.forced = false;
            return false;
        }

        /// <summary>Runner and fielder meet at full speed: both go flying (the runner over the top when he is out).</summary>
        void Collide(Runner r, Fielder f, bool runnerOut)
        {
            Vector3 dir = r.g.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = (f.at - r.g.pos).normalized;
            dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            float sp = Mathf.Max(4f, Mathf.Abs(r.v));
            r.g.Ragdoll(dir * sp * (runnerOut ? 0.55f : 0.35f) + Vector3.up * (runnerOut ? 4.5f : 2.5f), side * (runnerOut ? 9f : 5f), 1.2f);
            f.st = FState.Down;
            f.stT = 0f;
            f.g.Ragdoll(dir * sp * 0.5f + Vector3.up * 1.8f, side * -6f, 0.8f);
            M.OnCollision(r, f, this);
        }

        void Pose(Runner r)
        {
            Vector3 w = RunnerWorld(r);
            Vector3 prev = r.g.pos;
            r.g.pos = w;
            if (Mathf.Abs(r.v) > 0.3f)
            {
                float yaw = MathX.YawTo(prev, w);
                if ((w - prev).sqrMagnitude > 1e-6f) r.g.yaw = Mathf.LerpAngle(r.g.yaw, yaw, 0.5f);
                bool trot = kind == Kind.HomeRun || kind == Kind.GroundRule;
                // slide into a contested base
                float toBase = Mathf.Abs(r.target - r.pos) * Geo.BaseDist;
                bool contested = !trot && toBase < 2.6f && r.v > 0f && (mode == Mode.Throw && throwBase == r.target || mode == Mode.Held && holder != null && MathX.Horiz(holder.at, Geo.Base(r.target == 4 ? 0 : r.target)) < 6f);
                if (contested && r.g.act != Act.Slide) { r.g.Play(Act.Slide); M.OnSlide(r); }
                else if (!contested && r.g.act != Act.Slide) r.g.Set(trot ? Act.Jog : Act.Run);
                r.g.speedHint = Mathf.Abs(r.v);
            }
            else
            {
                if (r.g.act == Act.Run || r.g.act == Act.Jog || (r.g.act == Act.Slide && r.g.actT > 0.8f)) r.g.Set(Act.Idle);
                r.g.speedHint = -1f;
                if (r.st == RState.Base) r.g.yaw = Mathf.LerpAngle(r.g.yaw, MathX.YawTo(r.g.pos, ballPos), 0.1f);
            }
            r.g.lookAt = ballPos;
            r.g.looking = true;
        }

        // ------------------------------------------------------------------ fielders

        void StepFielder(Fielder f, float dt)
        {
            f.stT += dt;
            var g = f.g;
            if (g.Floored)
            {
                // limp or getting up: the logic point follows the body
                f.at = g.Ragdolled ? g.RagdollGround : g.pos;
                f.v = 0f;
                g.lookAt = ballPos;
                return;
            }
            g.looking = true;
            g.lookAt = ballPos;
            Vector3 goal = f.at;
            float sp = f.Speed;
            bool moving = false;
            switch (f.st)
            {
                case FState.Down:
                    if (f.stT > 1.0f) { f.st = FState.Ready; f.stT = 0f; }
                    g.Set(Act.Down);
                    Place(f);
                    return;
                case FState.Dizzy:
                    if (f.stT > 0.9f) { f.st = FState.Ready; f.stT = 0f; replanT = 0f; }
                    g.Set(Act.Dizzy);
                    Place(f);
                    return;
                case FState.Dive:
                {
                    float k = Mathf.Clamp01(1f - f.stT / 0.36f);
                    f.at += f.diveDir * (5.5f * k + 1.5f) * dt;
                    if (f.stT > 0.36f)
                    {
                        // belly-flop and tumble head over heels along the grass
                        f.st = FState.Down;
                        f.stT = 0f;
                        Place(f);
                        g.Ragdoll(f.diveDir * MathX.Range(rng, 3.5f, 5f) + Vector3.up * 0.8f, Vector3.Cross(Vector3.up, f.diveDir) * MathX.Range(rng, 6f, 9f), 0.5f);
                        M.OnTumble(f, this);
                        return;
                    }
                    Place(f);
                    return;
                }
                case FState.Chase:
                    goal = catchPoint;
                    break;
                case FState.Cover:
                    goal = f.goal;
                    break;
                case FState.Hold:
                    goal = f.runToBase ? f.goal : f.at;
                    break;
                case FState.Back:
                    goal = Vector3.Lerp(f.at, prd.landed ? prd.land : catchPoint, 0.35f * dt);
                    sp *= 0.5f;
                    break;
                default:
                    goal = f.at;
                    break;
            }
            if (t < f.react && f.st != FState.Hold) goal = f.at;
            Vector3 d = goal - f.at;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > 0.05f)
            {
                f.v = Mathf.Min(sp, f.v + 10f * dt);
                float step = Mathf.Min(dist, f.v * dt);
                f.at += d / dist * step;
                moving = step > 0.002f;
                g.yaw = Mathf.LerpAngle(g.yaw, MathX.YawTo(Vector3.zero, d), 1f - Mathf.Exp(-12f * dt));
            }
            else f.v = 0f;

            // chaser: dive or jump when it is the only way
            if (f.st == FState.Chase && mode == Mode.Flight && !ball.rolling)
            {
                float closeT = TimeToClosest(f, out Vector3 cp, out float cd, out float ch);
                if (closeT < 0.34f && closeT > 0f && cd > Cfg.ReachH && cd < Cfg.DiveReach && ch < 1.6f)
                {
                    f.st = FState.Dive;
                    f.stT = 0f;
                    Vector3 dd = cp - f.at; dd.y = 0f;
                    f.diveDir = dd.sqrMagnitude > 1e-4f ? dd.normalized : Vector3.forward;
                    g.yaw = MathX.YawTo(Vector3.zero, f.diveDir);
                    g.Play(Act.Dive);
                    M.OnDive(f, this);
                    Place(f);
                    return;
                }
                if (closeT < 0.32f && closeT > 0f && cd <= Cfg.ReachH * 1.4f && ch > Cfg.CatchTop && ch < Cfg.JumpTop && !f.jumping)
                {
                    f.jumping = true;
                    g.Play(Act.Jump);
                }
            }
            if (f.jumping && g.act == Act.Jump && g.actT > 0.62f) f.jumping = false;

            // poses (let the pitcher finish his follow-through first)
            if (f.pos == Pos.P && g.act == Act.PitchThrow && g.actT < 0.75f && !moving) { Place(f); return; }
            if (f.st == FState.Hold)
            {
                if (g.act != Act.Throw || g.actT > 0.45f)
                {
                    if (moving) g.Set(Act.Run);
                    else if (f.cover > 0 || MathX.Horiz(f.at, Geo.Base(1)) < 1.5f) g.Set(Act.Tag);
                    else g.Set(Act.Idle);
                }
                g.gloveW = 0f;
            }
            else if (f.jumping) { }
            else if (moving && f.v > 1.2f) g.Set(Act.Run);
            else if (f.st == FState.Chase && mode == Mode.Flight && ball.rolling) g.Set(Act.Scoop);
            else g.Set(Act.Ready);
            g.speedHint = moving ? f.v : -1f;

            // glove toward the ball when it is close
            bool reachFor = (mode == Mode.Flight && MathX.Horiz(f.at, ballPos) < 5f && ballPos.y < Cfg.JumpTop + 0.5f) || (mode == Mode.Throw && receiver == f && tT > tDur - 0.4f);
            g.gloveW = reachFor ? 1f : 0f;
            g.gloveAt = ballPos;
            if (!moving && f.st != FState.Hold && (mode == Mode.Flight || mode == Mode.Throw)) g.yaw = Mathf.LerpAngle(g.yaw, MathX.YawTo(f.at, ballPos), 0.12f);
            Place(f);
        }

        void Place(Fielder f)
        {
            f.at.y = Geo.GroundY(f.at);
            f.g.pos = f.at;
        }

        float TimeToClosest(Fielder f, out Vector3 cp, out float cd, out float ch)
        {
            cp = Vector3.zero; cd = 1e9f; ch = 0f;
            float best = -1f;
            float tt = 0f;
            var s = ball;
            for (int i = 0; i < 60; i++)
            {
                BallSim.Step(ref s, BallSim.Dt);
                tt += BallSim.Dt;
                float d = MathX.Horiz(f.at, s.p);
                if (d < cd && s.p.y < Cfg.JumpTop) { cd = d; cp = s.p; ch = s.p.y; best = tt; }
                if (s.touched && !ball.touched) break;
            }
            return best;
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dtReal)
        {
            if (finished) return;
            float dt = dtReal * Cfg.PlaySpeed;
            t += dt;

            if (mode == Mode.Flight || deadBall)
            {
                acc += dt;
                int guard = 0;
                while (acc >= BallSim.Dt && guard++ < 40)
                {
                    acc -= BallSim.Dt;
                    if (deadBall && (kind == Kind.HomeRun ? ballPos.y < -5f : deadT > 2f)) break;
                    if (!deadBall && mode != Mode.Flight) break;
                    BallEvent ev = BallSim.Step(ref ball, BallSim.Dt);
                    ballPos = ball.p;
                    if (deadBall) continue;
                    if (ev != BallEvent.None) OnBallEvent(ev);
                    if (mode != Mode.Flight) break;
                    FairCheck();
                    TryCatch();
                }
            }
            else if (mode == Mode.Throw) StepThrow(dt);
            else if (mode == Mode.Held && holder != null) ballPos = holder.g.hasBall ? holder.g.HandWorld : holder.g.GloveWorld;

            if (deadBall) deadT += dt;

            // re-plan the chase now and then
            replanT -= dt;
            if (mode == Mode.Flight && !deadBall && replanT <= 0f) { replanT = 0.25f; Plan(); }

            foreach (var f in F) StepFielder(f, dt);

            // the ball holder
            if (holder != null && mode == Mode.Held && !deadBall && !threeOuts && !holder.g.Floored)
            {
                var h = holder;
                h.holdT += dt;
                if (h.throwAt == -2f && !h.runToBase && Runners() > 0 && t - h.decidedAt > 0.3f) { h.throwAt = -1f; prompt = false; }
                if (h.runToBase && MathX.Horiz(h.at, h.goal) < 0.5f)
                {
                    h.runToBase = false;
                    h.cover = h.throwTo;
                    BaseTouched(h.throwTo, h);
                    DecideRunners();
                }
                if (h.throwAt >= 0f && t >= h.throwAt) Release(h);
                else if (h.throwAt == -1f && h.holdT >= Cfg.GatherTime * 0.7f)
                {
                    int ai = BestBase(h, out bool run, out float margin);
                    recommend = ai;
                    if (humanField)
                    {
                        if (!prompt) { prompt = true; promptT = 0f; }
                        promptT += dt;
                        bool onlyOne = ai > 0 && margin > 0.25f && Runners() <= 1;
                        if (promptT >= Cfg.HumanThrowWait || (onlyOne && promptT >= 0.55f) || (ai == 0 && promptT > 0.6f)) Throw(h, ai, run);
                    }
                    else if (h.holdT >= Cfg.GatherTime + MathX.Range(rng, 0.02f, 0.12f)) Throw(h, ai, run);
                }
            }

            runT -= dt;
            if (runT <= 0f) { runT = 0.3f; DecideRunners(); }
            foreach (var r in R) StepRunner(r, dt);

            CheckEnd(dt);
        }

        int Runners()
        {
            int n = 0;
            foreach (var r in R) if (r.st == RState.Run) n++;
            return n;
        }

        void CheckEnd(float dt)
        {
            if (endWait >= 0f)
            {
                endWait -= dt;
                if (endWait <= 0f) Finish();
                return;
            }
            bool trotting = kind == Kind.HomeRun || kind == Kind.GroundRule;
            if (t > (trotting ? Cfg.TrotCap : Cfg.PlayCap))
            {
                // a home-run trot (or a ground-rule double) is long but already decided: finish it as if skipped,
                // so the runs count instead of the runners being frozen on the bases
                if (trotting) { SkipTrot(); Finish(); return; }
                foreach (var r in R)
                    if (r.st == RState.Run)
                    {
                        r.pos = Mathf.Clamp(Mathf.Round(r.pos), 1f, 3f);
                        r.st = RState.Base; r.v = 0f; r.target = (int)r.pos;
                        Pose(r);
                    }
                Finish();
                return;
            }
            bool anyRun = false;
            foreach (var r in R) if (r.st == RState.Run || r.delay > 0f) anyRun = true;
            switch (kind)
            {
                case Kind.Foul:
                    if (deadT > 1.1f) { foreach (var r in R) { if (!r.isBatter) { r.pos = r.start; r.target = r.start; r.st = RState.Base; r.v = 0f; Pose(r); } } Finish(); }
                    return;
                case Kind.HomeRun:
                case Kind.GroundRule:
                    // a runner who was standing on a base when the ball left the park (the batter after his celebration)
                    // is not "running" yet on the tick his delay runs out: the trot ends only once everyone has arrived
                    foreach (var r in R) if (r.st != RState.Out && r.st != RState.Scored && r.pos < r.target - 1e-3f) anyRun = true;
                    if (!anyRun && deadT > 1.2f) Finish();
                    return;
            }
            if (anyRun) return;
            if (mode == Mode.Held && holder != null && holder.throwAt < 0f && !holder.runToBase && (!IsOutfielder(holder) || holder.throwAt == -2f) && holder.holdT > 0.35f)
            {
                if (endWait < 0f) endWait = 0.45f;
            }
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            prompt = false;
            foreach (var f in F) { f.g.gloveW = 0f; f.g.hasBall = false; }
            if (kind == Kind.InPlay && !caught && batter.st != RState.Out)
                hitBases = batter.st == RState.Scored ? 4 : Mathf.Clamp(Mathf.RoundToInt(batter.pos), 1, 3);
            else if (kind == Kind.GroundRule) hitBases = 2;
            M.OnPlayFinished(this);
        }

        /// <summary>Skip the home-run trot.</summary>
        public void SkipTrot()
        {
            if (kind != Kind.HomeRun && kind != Kind.GroundRule) return;
            foreach (var r in R)
            {
                if (r.st == RState.Out || r.st == RState.Scored) continue;
                r.delay = 0f;
                if (r.target >= 4) { r.pos = 4f; Score(r); }
                else { r.pos = r.target; r.st = RState.Base; r.v = 0f; }
                Pose(r);
            }
        }
    }
}
