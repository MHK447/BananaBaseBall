using UnityEngine;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumGame
    {
        bool slingLessonActive, lessonShotTaken, lessonHit, archerLessonMatch;
        ColosseumDoll lessonArcher;
        ColosseumDoll RequiredLessonHero => slingLessonActive && !lessonShotTaken && lessonArcher != null && lessonArcher.Alive
            ? lessonArcher : null;
        float hitCelebrationAge;
        int trainingRivalShots;
        ColosseumDoll trainingPlayer, trainingLastTarget;
        bool trainingMiss, trainingHitLanded;
        // The lesson battles, and a guided battle's rival turn when it is to throw its shot away, are played this way.
        bool TrainingRivalTurn => (trainingMatch || guidedMissTurn) && againstAi && turn == 1;

        void BeginSlingLesson(bool vsAi)
        {
            trainingRivalShots = 0;
            trainingPlayer = trainingLastTarget = null;
            trainingMiss = false;
            slingLessonActive = vsAi && trainingMatch;
            lessonArcher = slingLessonActive && archerLessonMatch
                ? dolls.Find(doll => doll.Team == 0 && doll.HeroIndex == ColosseumRoster.Archer) : null;
            lessonShotTaken = lessonHit = false;
            hitCelebrationAge = 0f;
            hud.HideSlingCoach();
        }

        bool SafeTrainingHit(ColosseumDoll attacker, ColosseumDoll victim, Vector3 push)
        {
            float ratio = attacker.Kind == WeaponKind.Slam ? SlamInnerDamageMultiplier : 1f;
            return victim.Health > AttackDamage(attacker, ratio) && !quake.LeadsOut(victim.Body.position, push, out _);
        }

        /// <summary>How much of a team's health is still standing, 0 to 1.</summary>
        float TeamShare(int team)
        {
            float left = 0f, whole = 0f;
            foreach (var doll in dolls)
            {
                if (doll.Team != team) continue;
                whole += doll.MaxHealth;
                if (doll.Alive) left += doll.Health;
            }
            return whole > 0f ? left / whole : 0f;
        }

        // While the player is level or ahead the rival lands its shots, on whoever can take one and stay in the
        // fight. Once the player has fallen behind, it sends them wide down an open lane.
        void AimTrainingShot()
        {
            Vector3 from = Kit.Flat(aiDoll.Body.position);
            bool playerAhead = !guidedMissTurn && TeamShare(0) >= TeamShare(1);
            if (playerAhead)
            {
                ColosseumDoll pick = null;
                float nearest = float.PositiveInfinity;
                foreach (var defender in dolls)
                {
                    if (defender.Team != 0 || !defender.Alive) continue;
                    Vector3 gap = Kit.Flat(defender.Body.position) - from;
                    float far = gap.magnitude;
                    if (!SafeTrainingHit(aiDoll, defender, far > .001f ? gap / far : Vector3.forward)) continue;
                    // Whoever took the last one is passed over while there is anyone else to aim at.
                    if (defender == trainingLastTarget) far += 1000f;
                    if (far >= nearest) continue;
                    nearest = far;
                    pick = defender;
                }
                if (pick != null) trainingPlayer = pick;
            }
            Vector3 offset = Kit.Flat(trainingPlayer.Body.position) - from;
            float distance = offset.magnitude;
            Vector3 toward = distance > .001f ? offset / distance : Vector3.forward;
            trainingMiss = !playerAhead || !SafeTrainingHit(aiDoll, trainingPlayer, toward);
            if (!trainingMiss)
            {
                aiDirection = toward;
                aiPower = aiDoll.Kind == WeaponKind.Slam ? Mathf.InverseLerp(LeapNear, LeapFar, distance)
                    : Mathf.Clamp01(.22f + distance * .035f);
                return;
            }

            aiPower = .035f;
            const float travel = 11f; // approximate runout for this light pull
            float best = float.NegativeInfinity;
            float side = trainingRivalShots % 2 == 0 ? 1f : -1f;
            aiDirection = -toward;
            for (int i = 1; i < 16; i++)
            {
                Vector3 way = Quaternion.Euler(0f, side * i * 22.5f, 0f) * toward;
                float clearance = float.PositiveInfinity;
                foreach (var defender in dolls)
                {
                    if (defender.Team != 0 || !defender.Alive) continue;
                    Vector3 gap = Kit.Flat(defender.Body.position) - from;
                    float along = Mathf.Clamp(Vector3.Dot(gap, way), 0f, travel);
                    clearance = Mathf.Min(clearance, (gap - way * along).magnitude);
                }
                Vector3 end = from + way * travel;
                float wallMargin = Mathf.Min(HalfWidth - Mathf.Abs(end.x), HalfDepth - Mathf.Abs(end.z));
                // Prefer a clear lane with room to stop, then the one closest to a believable near miss.
                float score = Mathf.Min(2f, clearance) + Mathf.Min(.8f, wallMargin)
                    + .2f * Vector3.Dot(way, toward);
                if (score <= best) continue;
                best = score;
                aiDirection = way;
            }
        }

        void TickSlingLesson(float real)
        {
            if (lessonHit)
            {
                hud.ShowSlingCoach();
                hud.UpdateSlingCoach(SlingCoachStep.Hit, real, arenaCamera, null, null);
                hitCelebrationAge += real;
                if (hitCelebrationAge >= .85f)
                {
                    if (archerLessonMatch)
                    {
                        // The second battle is coached all the way through: the prompt returns with each turn.
                        lessonHit = false;
                        hitCelebrationAge = 0f;
                        hud.HideSlingCoach();
                    }
                    else CloseSlingLesson();
                }
                return;
            }
            if (turn != 0 || phase != Phase.Aim || (!pulling && hud.TurnCallShowing))
            {
                hud.HideSlingCoach();
                return;
            }

            var hero = selected;
            if (hero == null && lessonArcher != null && lessonArcher.Alive) hero = lessonArcher;
            if (hero == null)
            {
                foreach (var ally in dolls)
                {
                    if (!ally.Alive || ally.Team != 0) continue;
                    if (hero == null || ally.HeroIndex == ColosseumRoster.Knight) hero = ally;
                }
            }
            if (hero == null) return;
            ColosseumDoll target = null;
            bool aligned = false;
            float nearest = float.PositiveInfinity;
            foreach (var enemy in dolls)
            {
                if (!enemy.Alive || enemy.Team == 0) continue;
                bool inPath = pulling && InAimPath(hero, enemy);
                if (aligned && !inPath) continue;
                float distance = Kit.Flat(enemy.transform.position - hero.transform.position).sqrMagnitude;
                if (inPath == aligned && distance >= nearest) continue;
                target = enemy;
                aligned = inPath;
                nearest = distance;
            }
            if (target == null) return;
            var step = pulling ? (aligned ? SlingCoachStep.Release : SlingCoachStep.Aim)
                : (lessonShotTaken ? SlingCoachStep.Retry : SlingCoachStep.Touch);
            hud.ShowSlingCoach();
            hud.UpdateSlingCoach(step, real, arenaCamera, hero, target);
        }

        void CloseSlingLesson()
        {
            slingLessonActive = false;
            hud.HideSlingCoach();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && pulling) CancelPull();
        }
    }
}
