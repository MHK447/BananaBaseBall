using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        const float TrophyTravel = .52f, TrophyInterval = .11f, TrophyAfterglow = .42f;
        public event Action<float, float> TrophyCharge;

        RectTransform[] receiptCups;
        Image trophyHitGlow;
        TextMeshProUGUI trophyHitLabel;
        Vector2 receiptOrigin;
        int receiptStart, receiptTrophyCount, landedTrophies, profileTrophyTarget;
        int batchFirst, batchCount, batchStartProgress;
        float batchAge, lastTrophyHit;

        bool TrophyReceiptFinished => landedTrophies == receiptTrophyCount && arrivalAge - lastTrophyHit >= TrophyAfterglow;

        void BuildTrophyReceipt(int previous, int trophies)
        {
            receiptStart = previous;
            receiptTrophyCount = trophies;
            landedTrophies = batchCount = 0;
            lastTrophyHit = float.NegativeInfinity;
            if (trophies <= 0) return;

            profileTrophyTarget = ColosseumSave.Trophies;
            if (heldProfileTrophies < 0) heldProfileTrophies = Mathf.Max(0, profileTrophyTarget - trophies);
            lobbyTrophies.text = heldProfileTrophies.ToString();
            receiptOrigin = claimedCupOrigin ?? new Vector2(.65f, .59f);
            // Recycle offscreen cups. The number of launches always comes from the actual reward.
            int slots = Mathf.Min(trophies, Mathf.CeilToInt(TrophyTravel / TrophyInterval) + 1);
            receiptCups = new RectTransform[slots];
            var sprite = Drawn("lobby-cup") ?? Drawn("cup");
            for (int i = 0; i < slots; i++)
            {
                var cup = Icon(victoryFlightLayer, sprite, JourneyCenter, Vector2.zero, 66f, Color.white);
                cup.name = "Trophy to journey " + (i + 1);
                cup.preserveAspect = true;
                cup.gameObject.SetActive(false);
                receiptCups[i] = cup.rectTransform;
            }

            trophyHitGlow = Icon(victoryFlightLayer, ColosseumMedalLight.GetMasks().Halo, JourneyCenter, Vector2.zero, 88f, Color.clear);
            trophyHitLabel = Label(victoryFlightLayer, "+1", 31f, Color.white, JourneyCenter, Vector2.zero, new Vector2(70f, 42f), bold);
            trophyHitLabel.gameObject.SetActive(false);
        }

        void TickTrophyReceipt(float delta, Rect view)
        {
            if (receiptCups == null) return;
            if (batchCount == 0 && landedTrophies < receiptTrophyCount && !swiping
                && journeyStage == ColosseumSave.StageFor(receiptStart + landedTrophies))
            {
                // Launch through the top of this road; the next cup waits for its new arena to open.
                batchFirst = landedTrophies;
                batchStartProgress = ColosseumSave.StageProgressFor(receiptStart + batchFirst);
                batchCount = Mathf.Min(receiptTrophyCount - batchFirst, ColosseumSave.StageGoal - batchStartProgress);
                batchAge = 0f;
            }
            if (batchCount > 0)
            {
                // Keep individual arrivals readable even when a frame stalls.
                batchAge += Mathf.Min(delta, TrophyInterval * .9f);
                int arrived = Mathf.Clamp(Mathf.FloorToInt((batchAge - TrophyTravel) / TrophyInterval) + 1, 0, batchCount);
                if (landedTrophies < batchFirst + arrived)
                {
                    landedTrophies = batchFirst + arrived;
                    heldJourneyTrophies = receiptStart + landedTrophies;
                    journeyTarget = heldJourneyTrophies;
                    journeyProgress = batchStartProgress + arrived;
                    PaintJourney();
                    if (heldProfileTrophies < profileTrophyTarget) heldProfileTrophies++;
                    lobbyTrophies.text = heldProfileTrophies.ToString();
                    lastTrophyHit = arrivalAge;
                    MatchSound?.Invoke("trophy-tick");
                }

                int released = Mathf.Min(batchCount, Mathf.FloorToInt(batchAge / TrophyInterval) + 1);
                Vector2 origin = Rect.NormalizedToPoint(view, receiptOrigin);
                for (int slot = 0; slot < receiptCups.Length; slot++)
                {
                    // Find the most recent launch assigned to this slot in the current stage.
                    int index = released / receiptCups.Length * receiptCups.Length + slot;
                    if (index >= released) index -= receiptCups.Length;
                    var cup = receiptCups[slot];
                    bool visible = index >= arrived;
                    if (cup.gameObject.activeSelf != visible) cup.gameObject.SetActive(visible);
                    if (!visible) continue;
                    float t = Mathf.Clamp01((batchAge - index * TrophyInterval) / TrophyTravel);
                    float fill = (batchStartProgress + index + 1f) / ColosseumSave.StageGoal;
                    PlaceFlyingCup(cup, origin, RoadArrivalPoint(fill), view, t);
                }
                if (arrived == batchCount) batchCount = 0;
            }

            float sinceHit = arrivalAge - lastTrophyHit;
            float pulse = Mathf.Clamp01(1f - sinceHit / TrophyInterval);
            float strength = Mathf.Clamp01(1f - sinceHit / TrophyAfterglow);
            float sweep = (float)landedTrophies / receiptTrophyCount;
            road.SetReceiptCharge(strength * (.76f + pulse * .24f), sweep);
            TrophyCharge?.Invoke(strength, sweep);

            float currentFill = journeyProgress / ColosseumSave.StageGoal;
            Vector2 contact = RoadArrivalPoint(currentFill);
            trophyHitGlow.rectTransform.anchoredPosition = contact;
            trophyHitGlow.rectTransform.sizeDelta = new Vector2(98f + 22f * pulse, 49f + 16f * pulse);
            trophyHitGlow.color = new Color(1f, .98f, .80f, strength * (.49f + .28f * pulse));
            trophyHitLabel.gameObject.SetActive(strength > 0f);
            if (strength > 0f)
            {
                trophyHitLabel.rectTransform.anchoredPosition = contact + new Vector2(-27f, 17f + 9f * (1f - pulse));
                trophyHitLabel.rectTransform.localScale = Vector3.one * (1f + .15f * pulse);
                trophyHitLabel.alpha = strength;
            }
        }

        Vector2 RoadArrivalPoint(float fraction)
        {
            var track = road.rectTransform;
            if (track.rect.height <= 0f)
                return victoryFlightLayer.InverseTransformPoint(journeyCounter.rectTransform.TransformPoint(journeyCounter.rectTransform.rect.center));
            Vector2 point = Rect.NormalizedToPoint(track.rect, new Vector2(.5f, fraction));
            return victoryFlightLayer.InverseTransformPoint(track.TransformPoint(point));
        }

        static void PlaceFlyingCup(RectTransform cup, Vector2 source, Vector2 target, Rect view, float t)
        {
            float right = Mathf.Min(view.xMax - 34f, Mathf.Max(source.x, target.x) + 110f);
            var departure = new Vector2(right, source.y + 95f);
            var approach = new Vector2(target.x + 104f, target.y + 32f);
            float rest = 1f - t;
            cup.anchoredPosition = rest * rest * rest * source + 3f * rest * rest * t * departure
                + 3f * rest * t * t * approach + t * t * t * target;
            cup.localScale = Vector3.one * Mathf.Lerp(.95f, .60f, t);
            cup.localRotation = Quaternion.Euler(0f, 0f, -13f * Mathf.Sin(Mathf.PI * t));
        }

        void ClearTrophyReceipt()
        {
            if (receiptCups == null) return;
            road.SetReceiptCharge(0f, 0f);
            TrophyCharge?.Invoke(0f, 0f);
            receiptCups = null;
            trophyHitGlow = null;
            trophyHitLabel = null;
        }
    }
}
