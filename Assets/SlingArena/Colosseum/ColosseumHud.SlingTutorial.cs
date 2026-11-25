using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    internal enum SlingCoachStep { Touch, Aim, Release, Hit, Retry }

    public sealed partial class ColosseumHud
    {
        GameObject slingCoach;
        Image practiceRim, practiceFace, practiceHand, practiceTarget, practiceTargetRim;
        TextMeshProUGUI practiceTitle, practiceHint;
        SlingCoachStep practiceStep;
        bool practiceArcher;
        float gestureTime;

        internal void ShowSlingCoach()
        {
            if (slingCoach && slingCoach.activeSelf) return;
            if (!slingCoach)
            {
                slingCoach = Group("First match shot guide", canvas, true);
                practiceRim = Box(slingCoach.transform, new Vector2(.5f, .83f), Vector2.zero,
                    new Vector2(584f, 105f), Color.white, 1.5f);
                practiceFace = Box(practiceRim.transform, HeroCenter, Vector2.zero,
                    new Vector2(579f, 100f), Color.white, 1.7f);
                Box(practiceFace.transform, new Vector2(0f, 1f), new Vector2(42f, -9f),
                    new Vector2(46f, 4f), new Color32(255, 255, 255, 211), 7f);
                Material lettering = OutlineMaterial(0f, 0f);
                lettering.DisableKeyword("OUTLINE_ON");
                lettering.SetColor("_FaceColor", Color.white);
                practiceTitle = Label(practiceFace.transform, "", 38f, new Color32(26, 42, 61, 255), HeroCenter,
                    new Vector2(0f, 18f), new Vector2(550f, 47f), lettering);
                practiceHint = Label(practiceFace.transform, "", 22f, new Color32(44, 62, 78, 255), HeroCenter,
                    new Vector2(0f, -22f), new Vector2(550f, 33f), lettering);
                Sprite target = Paint("Practice target brackets", 128, (x, y) =>
                {
                    float radius = x * x + y * y;
                    return radius < .94f && radius > .75f && Mathf.Abs(x) > .3f && Mathf.Abs(y) > .3f;
                });
                practiceTargetRim = Icon(slingCoach.transform, target, HeroCenter, Vector2.zero, 106f, Ink);
                practiceTarget = Icon(practiceTargetRim.transform, target, HeroCenter, Vector2.zero, 100f, Color.white);
                practiceHand = Icon(slingCoach.transform, pickerFinger ? pickerFinger.GetComponent<Image>().sprite : MakeFinger(128),
                    HeroCenter, Vector2.zero, 88f, Color.white);
                practiceHand.rectTransform.pivot = Fingertip;
                practiceHand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            }
            slingCoach.SetActive(true);
            slingCoach.transform.SetAsLastSibling();
            SetPracticeStep(SlingCoachStep.Touch);
        }

        void SetPracticeStep(SlingCoachStep step)
        {
            practiceStep = step;
            gestureTime = 0f;
            var gold = new Color32(255, 210, 86, 255);
            var mint = new Color32(123, 255, 169, 255);
            bool ready = step == SlingCoachStep.Release || step == SlingCoachStep.Hit;
            practiceHand.gameObject.SetActive(step == SlingCoachStep.Touch || step == SlingCoachStep.Retry);
            practiceTargetRim.gameObject.SetActive(step != SlingCoachStep.Hit);
            practiceTarget.color = ready ? mint : gold;
            switch (step)
            {
                case SlingCoachStep.Touch:
                    practiceFace.color = new Color32(255, 242, 204, 255);
                    practiceRim.color = new Color32(167, 119, 52, 255);
                    practiceTitle.text = practiceArcher ? "PULL THE ARCHER" : "PULL BACK";
                    practiceHint.text = practiceArcher ? "Hold the Archer and drag back." : "Hold a blue hero and drag backwards.";
                    break;
                case SlingCoachStep.Retry:
                    practiceFace.color = new Color32(227, 241, 255, 255);
                    practiceRim.color = new Color32(92, 137, 182, 255);
                    practiceTitle.text = "AIM FOR AN ENEMY";
                    practiceHint.text = practiceArcher ? "Drag the Archer back to try another volley." : "Drag away from the enemy you want to hit.";
                    break;
                case SlingCoachStep.Aim:
                    practiceFace.color = new Color32(211, 237, 255, 255);
                    practiceRim.color = new Color32(79, 129, 172, 255);
                    practiceTitle.text = practiceArcher ? "AIM THE VOLLEY" : "LINE UP THE SHOT";
                    practiceHint.text = practiceArcher ? "Arrows also fire left and right as you move." : "Point the guide at an enemy.";
                    break;
                case SlingCoachStep.Release:
                    practiceFace.color = new Color32(187, 248, 223, 255);
                    practiceRim.color = new Color32(54, 137, 111, 255);
                    practiceTitle.text = "LET GO!";
                    practiceHint.text = practiceArcher ? "Lift your finger to fire the volley." : "Lift your finger to launch.";
                    break;
                case SlingCoachStep.Hit:
                    practiceFace.color = new Color32(255, 234, 173, 255);
                    practiceRim.color = new Color32(180, 128, 42, 255);
                    practiceTitle.text = "DIRECT HIT!";
                    practiceHint.text = "Keep battling with your team.";
                    break;
            }
        }

        internal void UpdateSlingCoach(SlingCoachStep step, float delta, Camera camera, ColosseumDoll hero, ColosseumDoll target)
        {
            bool archer = hero != null && hero.Kind == WeaponKind.Bow;
            if (step != practiceStep || archer != practiceArcher)
            {
                practiceArcher = archer;
                SetPracticeStep(step);
            }
            gestureTime += delta;
            if (practiceTargetRim.gameObject.activeSelf && ToCanvas(camera, target.transform.position + Vector3.up * .8f, out Vector2 aim))
            {
                practiceTargetRim.rectTransform.anchoredPosition = aim;
                practiceTargetRim.rectTransform.localScale = Vector3.one * (1f + .055f * Mathf.Sin(gestureTime * 5f));
            }
            if (!practiceHand.gameObject.activeSelf) return;
            var start = hero.transform.position + Vector3.up * (ColosseumDoll.Radius * 1.8f);
            Vector3 backwards = Kit.Flat(hero.transform.position - target.transform.position).normalized;
            if (!ToCanvas(camera, start, out Vector2 touch) || !ToCanvas(camera, start + backwards * 2.35f, out Vector2 drag)) return;
            float beat = Mathf.Repeat(gestureTime, 1.85f);
            practiceHand.rectTransform.anchoredPosition = Vector2.Lerp(touch, drag, Mathf.SmoothStep(0f, 1f, (beat - .28f) / .78f));
            practiceHand.color = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, (beat - 1.23f) / .35f));
        }

        internal void HideSlingCoach()
        {
            if (slingCoach) slingCoach.SetActive(false);
        }
    }
}
