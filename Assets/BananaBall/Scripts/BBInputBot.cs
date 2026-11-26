using UnityEngine;

namespace BananaBall
{
    /// <summary>
    /// Test helper (BananaBallGame.debugBot): plays the human side through the real input paths — taps the field to swing
    /// (simulated pointer), aims with the joystick and holds/releases pitch buttons, picks throws and sends runners.
    /// </summary>
    public class InputBot
    {
        readonly BananaBallGame G;
        readonly System.Random r = new System.Random(9);
        float wait;
        bool tapDown;
        float swingAt = -1f;
        float releaseAt = -1f;
        int slot;
        Vector2 aimGoal;
        bool planned;
        public int swings, pitches, throwsPicked, sends;

        public InputBot(BananaBallGame g) { G = g; }

        public void Tick(float dt)
        {
            var M = G.match;
            Ptr.SimActive = true;
            if (tapDown) { Ptr.SimHeld = false; tapDown = false; }
            wait -= dt;
            switch (M.phase)
            {
                case Phase.Lobby:
                    if (wait <= 0f) { wait = 1f; M.StartMatch(G.hud.nextRival); }
                    break;
                case Phase.Result:
                    if (wait <= 0f) { wait = 1.5f; M.StartMatch(M.T[1]); }
                    break;
                case Phase.PitchReady:
                    if (M.HumanPitches)
                    {
                        if (!planned)
                        {
                            planned = true;
                            float cy = (M.zoneLow + M.zoneHigh) * 0.5f;
                            aimGoal = new Vector2(MathX.Range(r, -0.2f, 0.2f), cy + MathX.Range(r, -0.25f, 0.25f));
                            wait = MathX.Range(r, 0.4f, 0.9f);
                            slot = r.Next(3);
                        }
                        Vector2 d = aimGoal - M.aim;
                        d.x *= M.AimSign;   // the stick is screen-relative
                        G.hud.DebugJoy(d.magnitude > 0.02f ? Vector2.ClampMagnitude(d * 6f, 1f) : Vector2.zero);
                        if (wait <= 0f)
                        {
                            G.hud.DebugPitch(slot, true);
                            releaseAt = Cfg.RingTime + MathX.Gauss(r) * 0.08f;
                            pitches++;
                        }
                    }
                    else planned = false;
                    swingAt = -1f;
                    break;
                case Phase.Windup:
                    if (M.HumanPitches && M.holding && M.holdT >= releaseAt) { G.hud.DebugPitch(slot, false); planned = false; }
                    break;
                case Phase.PitchFlight:
                    planned = false;
                    if (M.HumanBats && !M.swung)
                    {
                        if (swingAt < 0f)
                        {
                            Vector3 plate = M.path.Eval(M.path.PlateTime);
                            bool good = Geo.InZone(plate, M.zoneLow, M.zoneHigh) || r.NextDouble() < 0.2;
                            swingAt = good ? M.path.PlateTime - Cfg.SwingToContact + MathX.Gauss(r) * 0.035f : 99f;
                        }
                        if (M.pt >= swingAt)
                        {
                            Ptr.SimPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
                            Ptr.SimHeld = true;
                            tapDown = true;
                            swings++;
                            swingAt = 99f;
                        }
                    }
                    break;
                case Phase.Live:
                    if (M.play != null)
                    {
                        if (M.ShowsThrowPrompt && M.play.promptT > 0.3f && M.play.recommend > 0) { M.play.HumanThrow(M.play.recommend); throwsPicked++; }
                        if (M.ShowsRunControls && wait <= 0f)
                        {
                            wait = 0.8f;
                            if (r.NextDouble() < 0.15) { M.play.HumanAdvance(Mathf.Min(4, 2 + r.Next(3))); sends++; }
                        }
                    }
                    break;
            }
        }

        public void Stop()
        {
            Ptr.SimActive = false;
            Ptr.SimHeld = false;
        }
    }
}
