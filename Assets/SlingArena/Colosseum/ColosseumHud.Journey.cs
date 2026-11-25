using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        const float StageHold = .28f, StageSwipe = .64f;
        const float MapViewportHeight = 388f, MapMaxWidth = 608f, MapMaxHeight = 355f;
        const float MedalRestHeight = 37f;
        static readonly float[] GlintDelays = { 0f, .47f, .57f };
        static readonly Vector2 JourneyCenter = new Vector2(.5f, .5f);
        static readonly Color Mulberry = new Color32(128, 69, 96, 255);
        static readonly Color RewardIdle = new Color32(168, 93, 100, 255);
        static readonly Color RewardGold = new Color32(255, 208, 118, 255);

        sealed class MapPage
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RawImage Illustration;
            public TextMeshProUGUI Number, Title;
            public ColosseumLobbyCrowd Crowd;
        }

        sealed class JourneyReward
        {
            public RectTransform Root;
            public Image Pin, Face;
            public GameObject Check;
            public Button Button;
            public TextMeshProUGUI Caption;
            public float Pulse;
        }

        MapPage currentMap, nextMap;
        Texture2D firstArena;
        TextMeshProUGUI journeyCounter;
        ColosseumTrophyRoad road;
        RectTransform summitCrest, floatingMedal;
        readonly RectTransform[] medalGlints = new RectTransform[GlintDelays.Length];
        Image[] summitMedals;
        Image summitRibbon, medalHalo, medalRays;
        TextMeshProUGUI summitLeague;
        int displayedLeague = -1;
        JourneyReward[] journeyRewards;
        GameObject chestDock;
        TextMeshProUGUI dockCount;
        int journeyStage, journeyTarget, paintedProgress = -1, arrivingStage;
        float journeyProgress, swipeTime;
        bool swiping, climbing;

        void BuildJourney(Transform root, Texture2D illustration, string trophyArt)
        {
            firstArena = illustration;
            var top = new Vector2(.5f, 1f);
            var bottom = new Vector2(.5f, 0f);
            var scene = (RectTransform)Group("Trophy journey", root).transform;
            scene.anchorMin = bottom;
            scene.anchorMax = top;
            scene.offsetMin = new Vector2(-342f, 340f);
            scene.offsetMax = new Vector2(342f, -112f);

            // The road grows with the space between the arena entrance and the battle button.
            var track = (RectTransform)Group("Ascending trophy road", scene).transform;
            track.anchorMin = bottom;
            track.anchorMax = top;
            float halfBase = ColosseumTrophyRoad.HalfWidth(0f);
            track.offsetMin = new Vector2(-halfBase, 61f);
            track.offsetMax = new Vector2(halfBase, -409f);
            road = track.gameObject.AddComponent<ColosseumTrophyRoad>();
            road.raycastTarget = false;

            var viewport = (RectTransform)Group("Stage maps", scene).transform;
            Place(viewport, scene, top, new Vector2(0f, -250f), new Vector2(654f, MapViewportHeight));
            viewport.gameObject.AddComponent<RectMask2D>();
            currentMap = BuildMapPage(viewport);
            nextMap = BuildMapPage(viewport);
            nextMap.Root.gameObject.SetActive(false);

            var summit = (RectTransform)Group("League at the summit", scene).transform;
            Place(summit, scene, top, new Vector2(0f, -32f), Vector2.zero);
            summitCrest = summit;
            floatingMedal = (RectTransform)Group("Floating league medal", summit).transform;
            floatingMedal.anchoredPosition = new Vector2(0f, MedalRestHeight);
            BuildMedalLight();
            summitMedals = new Image[ColosseumSave.LeagueTone.Length];
            for (int i = 0; i < summitMedals.Length; i++)
            {
                summitMedals[i] = Art(floatingMedal, "medal" + i, JourneyCenter, Vector2.zero, 148f);
                summitMedals[i].gameObject.SetActive(false);
            }
            Sprite glint = Paint("League glint", 64, (x, y) => Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y)) < 1f);
            medalGlints[0] = BuildMedalGlint(glint, new Vector2(-53f, -12f), 23f);
            medalGlints[1] = BuildMedalGlint(glint, new Vector2(49f, 34f), 28f);
            medalGlints[2] = BuildMedalGlint(glint, new Vector2(62f, 21f), 14f);
            var ribbon = Box(summit, JourneyCenter, new Vector2(0f, -43f), new Vector2(192f, 35f), Ink, 2.5f);
            summitRibbon = Box(ribbon.transform, JourneyCenter, Vector2.zero, new Vector2(185f, 28f), Mulberry, 2.5f);
            summitLeague = Label(ribbon.transform, "", 22f, Cream, JourneyCenter, Vector2.zero, new Vector2(183f, 33f), bold);

            // The arena overlaps the road entrance. Draw its last milestone in front of the stonework.
            var markers = (RectTransform)Group("Roadside rewards", scene).transform;
            markers.anchorMin = track.anchorMin;
            markers.anchorMax = track.anchorMax;
            markers.offsetMin = track.offsetMin;
            markers.offsetMax = track.offsetMax;
            journeyRewards = new JourneyReward[ColosseumSave.RewardAt.Length];
            for (int i = 0; i < journeyRewards.Length; i++)
                journeyRewards[i] = BuildRoadReward(markers, i, i % 2 == 0 ? 1f : -1f);

            var tally = Frame(track, bottom, new Vector2(0f, -25f), new Vector2(269f, 53f),
                new Color32(42, 105, 124, 255), 5f, 1.8f);
            Art(tally.transform, trophyArt, JourneyCenter, new Vector2(-71f, 0f), 53f);
            journeyCounter = Label(tally.transform, "", 30f, Color.white, JourneyCenter, new Vector2(23f, 1f), new Vector2(147f, 42f), bold);
        }

        void BuildMedalLight()
        {
            var (haloSprite, raySprite) = ColosseumMedalLight.GetMasks();
            medalHalo = Icon(floatingMedal, haloSprite, JourneyCenter, Vector2.zero, 184f, Color.white);
            medalHalo.name = "League halo";
            medalRays = Icon(floatingMedal, raySprite, JourneyCenter, Vector2.zero, 200f, Color.white);
            medalRays.name = "League rays";
        }

        RectTransform BuildMedalGlint(Sprite sprite, Vector2 position, float size)
        {
            var rim = Icon(floatingMedal, sprite, JourneyCenter, position, size, new Color32(255, 201, 110, 255)).rectTransform;
            Icon(rim, sprite, JourneyCenter, Vector2.zero, size * .72f, new Color32(255, 253, 229, 255));
            rim.localScale = Vector3.zero;
            return rim;
        }

        void AnimateMedal()
        {
            float cycle = Mathf.Repeat(lobbyClock / 3.2f, 1f);
            float lift = 3.25f * (1f - Mathf.Cos(2f * Mathf.PI * cycle));
            floatingMedal.anchoredPosition = new Vector2(0f, MedalRestHeight + lift);
            medalHalo.rectTransform.localScale = Vector3.one * (1f + .04f * Mathf.Sin(cycle * 2f * Mathf.PI));
            medalRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Repeat(lobbyClock * 7f, 360f));

            // The left glint leads; the two on the right follow like a reflection catching the rim.
            for (int i = 0; i < medalGlints.Length; i++)
            {
                float flash = Mathf.Clamp01(Mathf.Repeat(cycle - GlintDelays[i], 1f) / .37f);
                float scale = .5f - .5f * Mathf.Cos(2f * Mathf.PI * flash);
                medalGlints[i].localScale = Vector3.one * scale;
            }
        }

        void BuildChestDock(Transform parent)
        {
            var face = Frame(parent, new Vector2(.5f, 0f), new Vector2(-277f, 248f), new Vector2(115f, 84f),
                new Color32(33, 137, 128, 255), 4f, 2.3f);
            var hit = face.transform.parent.GetComponent<Image>();
            chestDock = hit.gameObject;
            chestDock.name = "Journey chest dock";
            var prop = ColosseumChestProp.Create(face.transform);
            prop.transform.localScale = Vector3.one * .31f;
            ((RectTransform)prop.transform).anchoredPosition = new Vector2(0f, 23f);
            Label(face.transform, "OPEN", 23f, Cream, JourneyCenter, new Vector2(0f, -22f), new Vector2(96f, 33f), bold);
            var badge = Icon(face.transform, circle, Vector2.one, new Vector2(-2f, -2f), 35f, Ink);
            Icon(badge.transform, circle, JourneyCenter, Vector2.zero, 29f, new Color32(221, 65, 89, 255));
            dockCount = Label(badge.transform, "", 20f, Color.white, JourneyCenter, Vector2.zero, new Vector2(31f, 29f), slim);
            dockCount.enableAutoSizing = true;
            dockCount.fontSizeMin = 12f;
            ProfileButton(hit, face, () => JourneyChestPressed?.Invoke(ColosseumSave.FirstAvailableChest()));
            chestDock.SetActive(false);
        }

        void UpdateChestDock()
        {
            if (chestDock == null) return;
            int total = (journeyStage - 1) * ColosseumSave.StageGoal + Mathf.FloorToInt(journeyProgress);
            int count = ColosseumSave.AvailableChests(Mathf.Max(0, total));
            chestDock.SetActive(openTab == BattleTab && count > 0);
            dockCount.text = count > 99 ? "99+" : count.ToString();
        }

        JourneyReward BuildRoadReward(RectTransform track, int index, float side)
        {
            int at = ColosseumSave.RewardAt[index];
            float fraction = (float)at / ColosseumSave.StageGoal;
            var node = (RectTransform)Group("Reward at " + at, track).transform;
            node.anchorMin = node.anchorMax = new Vector2(.5f, fraction);
            node.anchoredPosition = new Vector2(side * ColosseumTrophyRoad.HalfWidth(fraction), 0f);

            // Each chest is attached to its own trophy graduation and can be opened independently.
            bool finalReward = at == ColosseumSave.StageGoal;
            float arm = finalReward ? 92f : 50f;
            float drop = finalReward ? 34f : 0f;
            if (finalReward)
            {
                Box(node, JourneyCenter, new Vector2(side * arm, -drop * .5f), new Vector2(11f, drop + 9f), Ink, 3f);
                Box(node, JourneyCenter, new Vector2(side * arm, -drop * .5f), new Vector2(4f, drop + 1f), RewardGold, 3f);
            }
            Box(node, JourneyCenter, new Vector2(side * arm * .5f, 0f), new Vector2(arm + 4f, 11f), Ink, 3f);
            Box(node, JourneyCenter, new Vector2(side * arm * .5f, 1f), new Vector2(arm + 1f, 4f), RewardGold, 3f);
            var ring = Icon(node, circle, JourneyCenter, Vector2.zero, 29f, Ink);
            var pin = Icon(ring.transform, circle, JourneyCenter, Vector2.zero, 19f, Cream);
            float x = side * (arm + 46f);
            var face = Frame(node, JourneyCenter, new Vector2(x, -10f - drop), new Vector2(108f, 43f),
                RewardIdle, 4f, 2.2f);
            var prop = ColosseumChestProp.Create(face.transform);
            prop.transform.localScale = Vector3.one * .16f;
            ((RectTransform)prop.transform).anchoredPosition = new Vector2(-29f, 2f);
            var caption = Label(face.transform, "CHEST", 18f, Cream, JourneyCenter,
                new Vector2(19f, 0f), new Vector2(58f, 33f), slim);
            var hit = face.transform.parent.GetComponent<Image>();
            hit.name = "Journey chest at " + at;
            var tapArea = Plain(hit.transform, JourneyCenter, new Vector2(0f, 7f), new Vector2(120f, 86f), Color.clear);
            tapArea.raycastTarget = true;
            var button = ProfileButton(hit, face, () => JourneyChestPressed?.Invoke(ColosseumSave.ChestNumber(journeyStage, index)));
            button.transition = Selectable.Transition.None;
            Label(node, at.ToString(), 23f, Color.white, JourneyCenter, new Vector2(x, 25f - drop),
                new Vector2(82f, 28f), bold);

            var check = (RectTransform)Group("Collected", face.transform).transform;
            check.anchoredPosition = new Vector2(46f, 13f);
            Icon(check, circle, JourneyCenter, Vector2.zero, 24f, Ink);
            Icon(check, circle, JourneyCenter, Vector2.zero, 18f, RewardGold);
            SmoothQuad(Plain(check, JourneyCenter, new Vector2(-3f, -1f), new Vector2(7f, 3f), Ink)).rectTransform.localRotation = Quaternion.Euler(0, 0, -48f);
            SmoothQuad(Plain(check, JourneyCenter, new Vector2(2f, 1f), new Vector2(11f, 3f), Ink)).rectTransform.localRotation = Quaternion.Euler(0, 0, 48f);
            check.gameObject.SetActive(false);
            return new JourneyReward { Root = node, Pin = pin, Face = face, Check = check.gameObject, Button = button, Caption = caption };
        }

        MapPage BuildMapPage(RectTransform parent)
        {
            var map = new MapPage();
            map.Root = (RectTransform)Group("Stage illustration", parent, true).transform;
            map.Group = map.Root.gameObject.AddComponent<CanvasGroup>();
            map.Group.blocksRaycasts = false;
            map.Illustration = new GameObject("Arena diorama", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            Place(map.Illustration.rectTransform, map.Root, JourneyCenter, new Vector2(0f, 8f), new Vector2(MapMaxWidth, MapMaxHeight));
            map.Illustration.raycastTarget = false;
            map.Crowd = Group("Grandstand spectators", map.Illustration.transform, true).AddComponent<ColosseumLobbyCrowd>();

            var badge = Box(map.Root, JourneyCenter, new Vector2(0f, -98f), new Vector2(111f, 29f), Mulberry, 2.7f);
            map.Number = Label(badge.transform, "", 18f, Cream, JourneyCenter, Vector2.zero, new Vector2(105f, 29f), slim);
            map.Title = Label(map.Root, "", 36f, Color.white, JourneyCenter, new Vector2(0f, -132f), new Vector2(456f, 48f), bold);
            return map;
        }

        void BindMap(MapPage map, int stage)
        {
            Texture2D texture = ColosseumJourneyMaps.Texture(stage, firstArena, out int seatingLayout);
            map.Illustration.enabled = texture != null;
            map.Illustration.texture = texture;
            if (texture != null)
            {
                float ratio = (float)texture.width / texture.height;
                float h = Mathf.Min(MapMaxHeight, MapMaxWidth / ratio);
                map.Illustration.rectTransform.sizeDelta = new Vector2(h * ratio, h);
            }
            map.Crowd.ShowMap(seatingLayout);
            map.Number.text = "ARENA " + stage.ToString("00");
            map.Number.color = Color.Lerp(ColosseumJourneyMaps.Accent(stage), Color.white, .32f);
            map.Title.text = ColosseumJourneyMaps.Name(stage).ToUpperInvariant();
        }

        void RefreshJourney(int total)
        {
            // Claim can refresh the lobby several times before the flying cup reaches the road.
            if (heldJourneyTrophies >= 0) total = heldJourneyTrophies;
            int league = ColosseumSave.League;
            if (displayedLeague != league && (heldJourneyTrophies < 0 || displayedLeague < 0))
            {
                if (displayedLeague >= 0 && league > displayedLeague) MatchSound?.Invoke("rank-up");
                if (displayedLeague >= 0) summitMedals[displayedLeague].gameObject.SetActive(false);
                summitMedals[league].gameObject.SetActive(true);
                summitLeague.text = ColosseumSave.LeagueName;
                Color tone = MeshKit.Rgb(ColosseumSave.LeagueTone[league], 1f);
                summitRibbon.color = Color.Lerp(Mulberry, tone, .28f);
                Color halo = Color.Lerp(tone, Color.white, .48f);
                halo.a = .85f;
                medalHalo.color = halo;
                Color rays = Color.Lerp(halo, Cream, .60f);
                rays.a = .68f;
                medalRays.color = rays;
                displayedLeague = league;
            }
            if (journeyStage == 0 || total < journeyTarget)
            {
                if (journeyStage != 0) MatchSound?.Invoke("trophy-loss");
                swiping = false;
                summitCrest.localScale = Vector3.one;
                journeyStage = ColosseumSave.StageFor(total);
                journeyProgress = ColosseumSave.StageProgressFor(total);
                currentMap.Root.anchoredPosition = Vector2.zero;
                currentMap.Group.alpha = 1f;
                BindMap(currentMap, journeyStage);
                nextMap.Root.gameObject.SetActive(false);
                PaintJourney(true);
            }
            journeyTarget = total;
        }

        void PaintJourney(bool force = false)
        {
            int amount = Mathf.FloorToInt(journeyProgress);
            float fill = journeyProgress / ColosseumSave.StageGoal;
            road.SetProgress(fill);
            if (amount == paintedProgress && !force) return;
            journeyCounter.text = amount + " / " + ColosseumSave.StageGoal;
            for (int i = 0; i < journeyRewards.Length; i++)
            {
                int threshold = ColosseumSave.RewardAt[i];
                bool taken = amount >= threshold && ColosseumSave.RewardTaken(journeyStage, i);
                bool ready = amount >= threshold && !taken;
                var reward = journeyRewards[i];
                if (!force && paintedProgress < threshold && amount >= threshold) reward.Pulse = .48f;
                reward.Pin.color = taken ? RewardGold : Cream;
                reward.Face.color = ready ? new Color32(31, 150, 112, 255) : RewardIdle;
                reward.Button.interactable = ready;
                reward.Caption.text = ready ? "OPEN" : taken ? "TAKEN" : "CHEST";
                reward.Check.SetActive(taken);
            }
            paintedProgress = amount;
            UpdateChestDock();
        }

        void TickJourney(float delta)
        {
            if (journeyStage == 0) return;
            AnimateMedal();
            currentMap.Crowd.Tick(delta);
            for (int i = 0; i < journeyRewards.Length; i++)
            {
                var r = journeyRewards[i];
                if (r.Pulse <= 0f) continue;
                r.Pulse = Mathf.Max(0f, r.Pulse - delta);
                r.Root.localScale = Vector3.one * (1f + .18f * Mathf.Sin(Mathf.PI * r.Pulse / .48f));
            }
            if (swiping)
            {
                nextMap.Crowd.Tick(delta);
                swipeTime += delta;
                float beat = Mathf.Clamp01(swipeTime / (StageHold + .2f));
                summitCrest.localScale = Vector3.one * (1f + .12f * Mathf.Sin(beat * Mathf.PI));
                if (swipeTime < StageHold) return;
                float t = Mathf.Clamp01((swipeTime - StageHold) / StageSwipe);
                float slide = Mathf.SmoothStep(0f, 1f, t);
                currentMap.Root.anchoredPosition = new Vector2(0f, -MapViewportHeight * slide);
                currentMap.Group.alpha = 1f - slide;
                nextMap.Root.anchoredPosition = new Vector2(0f, MapViewportHeight * (1f - slide));
                nextMap.Group.alpha = Mathf.Clamp01(slide * 2f);
                if (t >= .5f && journeyStage != arrivingStage)
                {
                    journeyStage = arrivingStage;
                    journeyProgress = 0f;
                    PaintJourney(true);
                }
                if (t < 1f) return;
                currentMap.Root.gameObject.SetActive(false);
                var old = currentMap;
                currentMap = nextMap;
                nextMap = old;
                swiping = false;
                return;
            }

            int targetStage = ColosseumSave.StageFor(journeyTarget);
            int localTarget = targetStage > journeyStage ? ColosseumSave.StageGoal : ColosseumSave.StageProgressFor(journeyTarget);
            if (journeyProgress != localTarget && !climbing) MatchSound?.Invoke("trophy-gain");
            climbing = journeyProgress != localTarget;
            if (journeyProgress != localTarget)
            {
                float speed = Mathf.Max(50f, (localTarget - journeyProgress) * 3f);
                journeyProgress = Mathf.MoveTowards(journeyProgress, localTarget, speed * delta);
                PaintJourney();
            }
            if (journeyProgress < ColosseumSave.StageGoal || targetStage <= journeyStage) return;
            arrivingStage = targetStage;
            BindMap(nextMap, arrivingStage);
            nextMap.Root.anchoredPosition = new Vector2(0f, MapViewportHeight);
            nextMap.Group.alpha = 0f;
            nextMap.Root.gameObject.SetActive(true);
            swipeTime = 0f;
            swiping = true;
            MatchSound?.Invoke("stage-up");
        }
    }
}
