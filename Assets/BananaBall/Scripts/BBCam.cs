using UnityEngine;

namespace BananaBall
{
    /// <summary>
    /// Portrait cameras after Baseball Clash. Batting: low behind the plate on the hitter's side, hitter big on one side, pitcher and
    /// zone in the middle. Pitching: behind the pitcher on a medium lens, pitcher big at the bottom, hitter over his shoulder. Ball in play: high behind home, following the ball
    /// and the fielder chasing it. Plus a stadium fly-in, the home-run chase, an establishing shot and the menu/result orbits.
    /// </summary>
    public class CamRig
    {
        public enum Mode { Lobby, Intro, Establish, Batting, Pitching, Field, HomeRun, Result }
        public readonly Camera cam;
        readonly BananaBallGame G;
        public Mode mode = Mode.Lobby;
        public float modeT;
        public bool batterLeft;
        Vector3 pos, look;
        float fov = 45f, shake;
        bool cut;
        Vector3 hrBall;

        public CamRig(Camera c, BananaBallGame game)
        {
            cam = c;
            G = game;
            pos = new Vector3(0f, 30f, -30f);
            look = new Vector3(0f, 0f, 40f);
        }

        public void Set(Mode m)
        {
            if (mode == m) return;
            bool hard = m == Mode.Batting || m == Mode.Pitching || m == Mode.Establish || (m == Mode.Field && (mode == Mode.Batting || mode == Mode.Pitching));
            if (mode == Mode.Intro && (m == Mode.Batting || m == Mode.Pitching)) hard = false;
            if (mode == Mode.Lobby && m == Mode.Intro) hard = true;
            mode = m;
            modeT = 0f;
            cut = hard;
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        float punch, punchV, punchHold, realT;
        /// <summary>Impact zoom: the lens snaps in by 'deg' at once, holds through the freeze-frame, then springs back.</summary>
        public void Punch(float deg, float hold = 0f) { punch = Mathf.Min(punch, -deg); punchV = 0f; punchHold = Mathf.Max(punchHold, hold); }

        public void Snap() { Target(out pos, out look, out fov); }

        static float Portrait(float vfov, float aspect)
        {
            const float design = 9f / 16f;
            float tanV = Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad);
            if (aspect < design) tanV *= design / aspect;
            else if (aspect > 1f) tanV *= Mathf.Lerp(1f, 0.62f, Mathf.InverseLerp(1f, 1.8f, aspect));
            return 2f * Mathf.Atan(tanV) * Mathf.Rad2Deg;
        }

        void Target(out Vector3 tp, out Vector3 tl, out float tf)
        {
            var M = G.match;
            float side = batterLeft ? -1f : 1f;
            switch (mode)
            {
                case Mode.Batting:
                    // low behind the plate on the hitter's side: the hitter big on one side, the pitcher and the zone in the middle
                    tp = new Vector3(-0.24f * side, 1.0f, -4.2f);
                    tl = new Vector3(-0.08f * side, 0.98f, 18.4f);
                    tf = 54f;
                    break;
                case Mode.Pitching:
                    // behind and above the pitcher's glove side: the pitcher big at the bottom, the hitter over his shoulder
                    tp = new Vector3(-0.62f * side, 2.15f, 27.4f);
                    tl = new Vector3(0f, 0.86f, 0f);
                    tf = 25f;
                    break;
                case Mode.Establish:
                {
                    float u = MathX.Ease(Mathf.Clamp01(modeT / 2.2f));
                    tl = Vector3.Lerp(new Vector3(0f, 0f, 46f), new Vector3(0f, 0f, 30f), u);
                    tp = Vector3.Lerp(new Vector3(-6f, 44f, -36f), new Vector3(0f, 30f, -26f), u);
                    tf = 52f;
                    break;
                }
                case Mode.Field:
                {
                    Vector3 ball = M != null ? M.BallPos : Vector3.zero;
                    Vector3 focus = new Vector3(ball.x, 0f, ball.z);
                    var pl = M != null ? M.play : null;
                    if (pl != null && pl.chaser != null && pl.mode == Play.Mode.Flight)
                        focus = Vector3.Lerp(focus, new Vector3(pl.chaser.at.x, 0f, pl.chaser.at.z), 0.3f);
                    if (pl != null && pl.mode == Play.Mode.Flight && !pl.landed && pl.prd.landed)
                        focus = Vector3.Lerp(focus, new Vector3(pl.prd.land.x, 0f, pl.prd.land.z), 0.35f);
                    float d = new Vector2(focus.x, focus.z).magnitude;
                    float k = Mathf.Clamp01(d / 105f);
                    Vector3 dir = d > 3f ? new Vector3(focus.x, 0f, focus.z) / d : Vector3.forward;
                    dir = Vector3.Slerp(Vector3.forward, dir, 0.55f);
                    float back = Mathf.Lerp(20f, 40f, k), up = Mathf.Lerp(19f, 33f, k);
                    tl = focus + Vector3.up * Mathf.Min(ball.y * 0.35f, 9f) + dir * 3f;
                    tp = focus - dir * back + Vector3.up * up;
                    if (tp.z < -12f) tp.z = -12f;
                    tf = 50f;
                    break;
                }
                case Mode.HomeRun:
                {
                    Vector3 ball = M != null ? M.BallPos : Vector3.zero;
                    if (modeT < 0.05f) hrBall = ball;
                    if (ball.y > -1f && modeT < 1.9f) hrBall = ball;
                    if (modeT < 1.7f)
                    {
                        tl = Vector3.Lerp(new Vector3(0f, 8f, 30f), hrBall, 0.75f);
                        tp = new Vector3(hrBall.x * 0.2f, 10f + hrBall.y * 0.2f, -14f);
                        tf = 46f;
                    }
                    else
                    {
                        // the batter's celebration up close (he stops to dance or beat his chest), then follow the trot
                        Vector3 b = M != null && M.batterG != null ? M.batterG.pos : Vector3.zero;
                        if (modeT < 3.5f && M != null && M.batterG != null)
                        {
                            // in front of him by his logic heading (his body may be upside down in a backflip)
                            Vector3 fw = Quaternion.Euler(0f, M.batterG.yaw + 28f, 0f) * Vector3.forward;
                            tl = b + Vector3.up * 0.85f;
                            tp = b + fw * 6.2f + Vector3.up * 2.3f;
                            tf = 40f;
                            break;
                        }
                        tl = b + Vector3.up * 1.2f;
                        Vector3 outDir = (new Vector3(b.x, 0f, b.z - Geo.H)).normalized;
                        if (outDir.sqrMagnitude < 0.01f) outDir = Vector3.back;
                        tp = b + outDir * 13f + Vector3.up * 7f;
                        tf = 44f;
                    }
                    break;
                }
                case Mode.Intro:
                {
                    float u = MathX.Ease(Mathf.Clamp01(modeT / (Cfg.IntroTime - 0.3f)));
                    float a = Mathf.Lerp(-60f, 0f, u) * Mathf.Deg2Rad;
                    float r = Mathf.Lerp(150f, 40f, u);
                    tl = Vector3.Lerp(new Vector3(0f, 6f, 60f), new Vector3(0f, 1f, 20f), u);
                    tp = new Vector3(Mathf.Sin(a) * r, Mathf.Lerp(60f, 22f, u), 40f - Mathf.Cos(a) * r);
                    tf = Mathf.Lerp(55f, 50f, u);
                    break;
                }
                case Mode.Result:
                {
                    float a = Time.time * 7f * Mathf.Deg2Rad;
                    tl = new Vector3(0f, 1.3f, 10f);
                    tp = tl + new Vector3(Mathf.Sin(a) * 15f, 5.5f, -Mathf.Cos(a) * 15f);
                    tf = 46f;
                    break;
                }
                default:
                {
                    float a = (180f + Mathf.Sin(Time.time * 0.12f) * 18f) * Mathf.Deg2Rad;
                    tl = new Vector3(0f, 1.25f, 13.6f);
                    tp = tl + new Vector3(Mathf.Sin(a) * 11f, 2.6f, Mathf.Cos(a) * 11f);
                    tf = 48f;
                    break;
                }
            }
            tf = Portrait(tf, Mathf.Max(0.3f, cam.aspect));
        }

        public void Tick(float dt) => Tick(dt, dt);

        /// <summary>dt is game time (it stops in a freeze-frame); real keeps running so the shake and the punch play through it.</summary>
        public void Tick(float dt, float real)
        {
            modeT += dt;
            if (mode == Mode.HomeRun && modeT >= 1.7f && modeT - dt < 1.7f) cut = true;   // hard cut from the ball to the celebrating batter
            Target(out Vector3 tp, out Vector3 tl, out float tf);
            if (cut)
            {
                pos = tp; look = tl; fov = tf;
                cut = false;
            }
            else
            {
                float k = mode == Mode.Intro ? 10f : mode == Mode.Batting || mode == Mode.Pitching ? 6f : mode == Mode.Field ? 3.2f : mode == Mode.HomeRun ? 4f : 2.5f;
                pos = MathX.Damp(pos, tp, k, dt);
                look = MathX.Damp(look, tl, k * 1.5f, dt);
                fov = MathX.Damp(fov, tf, 5f, dt);
            }
            realT += real;
            shake = Mathf.Max(0f, shake - real * 1.8f);
            Vector3 sh = shake > 0f ? new Vector3(Mathf.PerlinNoise(realT * 25f, 0f) - 0.5f, Mathf.PerlinNoise(0f, realT * 25f) - 0.5f, 0f) * shake : Vector3.zero;
            if (punchHold > 0f) punchHold -= real;
            else if (punch != 0f || punchV != 0f)
            {
                punchV += (-punch * 240f - punchV * 26f) * real;
                punch += punchV * real;
                if (Mathf.Abs(punch) < 0.01f && Mathf.Abs(punchV) < 0.05f) { punch = 0f; punchV = 0f; }
            }
            if (mode == Mode.Pitching) sh *= 0.12f;
            cam.transform.position = pos + cam.transform.rotation * sh;
            Vector3 d = look - pos;
            if (d.sqrMagnitude > 1e-6f) cam.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            cam.fieldOfView = fov + punch;
            cam.nearClipPlane = mode == Mode.Pitching ? 0.5f : 0.15f;
        }
    }
}
