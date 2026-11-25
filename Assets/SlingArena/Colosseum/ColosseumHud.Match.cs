using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        const int MatchTeamSize = ColosseumGame.UnitsPerSide, MatchReelSteps = 24;
        // The hero cards were drawn four to a row; a longer line-up is drawn smaller to fit the same width.
        static readonly float MatchCardFit = Mathf.Min(1f, 4.3f / MatchTeamSize);
        const float MatchImpactAt = .36f, MatchReelWidth = 486f;
        const float MatchLastSlotLength = 1.02f, MatchSnapLength = .34f;
        const float MatchRevealLead = .05f, MatchRevealStep = .12f, MatchRevealLength = .24f, MatchRevealHold = .78f;
        static readonly Vector2 MatchCenter = new Vector2(.5f, .5f);
        static readonly Color MatchBlue = new Color32(47, 159, 255, 255);
        static readonly Color MatchRed = new Color32(255, 91, 96, 255);
        static readonly Color MatchRivalTrim = new Color32(255, 184, 160, 255);
        static readonly Color MatchCardFace = new Color32(255, 235, 190, 255);
        static readonly Vector2 MatchPortraitSize = new Vector2(112f, 130f);

        sealed class MatchIdentity
        {
            public RectTransform Root;
            public Image Avatar;
            public TextMeshProUGUI Name, Trophies;
        }

        sealed class MatchCardView
        {
            public RectTransform Root;
            public CanvasGroup Face;
            public GameObject Placeholder;
            public RawImage Portrait;
            public TextMeshProUGUI Level;
            public bool Revealed;
        }

        readonly struct ReelIdentity
        {
            public readonly string Name;
            public readonly int Avatar;

            public ReelIdentity(string name, int avatar)
            {
                Name = name;
                Avatar = avatar;
            }
        }

        public event Action<string> MatchSound;
        public bool MatchReadyToStart { get; private set; }

        int rivalCardCount;
        readonly MatchCardView[] matchCards = new MatchCardView[MatchTeamSize * 2];
        readonly Texture2D[] matchPortraits = new Texture2D[ColosseumRoster.Heroes.Length];
        readonly ReelIdentity[] matchCandidates = new ReelIdentity[MatchReelSteps + 1];
        readonly System.Random matchRandom = new System.Random();
        GameObject matchRoot;
        RectTransform matchBluePanel, matchRedPanel, matchVs, matchBoltCore, matchRivalFrame;
        TextMeshProUGUI matchStatus;
        Image matchFlash, matchRivalEdge;
        CanvasGroup matchCenterGraphics;
        MatchIdentity matchPlayer, matchReelCurrent, matchReelIncoming;
        float matchAge, matchSearchTime;
        int matchReelIndex, matchRivalTrophies;
        bool matchImpacted, matchSettled;

        void BuildMatch(Transform root)
        {
            Image backdrop = Plain(root, MatchCenter, Vector2.zero, Vector2.zero, new Color32(28, 19, 49, 255));
            backdrop.rectTransform.anchorMin = Vector2.zero;
            backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.raycastTarget = true;

            matchRedPanel = BuildMatchPanel(root, true);
            matchBluePanel = BuildMatchPanel(root, false);
            matchStatus = Label(matchBluePanel, string.Empty, 26f, Cream, MatchCenter,
                new Vector2(0f, -549f), new Vector2(660f, 42f), bold);

            GameObject center = Group("Match impact", root);
            matchCenterGraphics = center.AddComponent<CanvasGroup>();
            RectTransform bolt = Group("Impact seam", center.transform).GetComponent<RectTransform>();
            bolt.localRotation = Quaternion.Euler(0f, 0f, 12f);
            // Where the two halves meet: one clean black line (it thickens for a moment on impact).
            matchBoltCore = SmoothQuad(Plain(bolt, MatchCenter, Vector2.zero, new Vector2(4000f, 12f), Ink)).rectTransform;

            matchVs = Group("Versus", center.transform).GetComponent<RectTransform>();
            matchVs.localRotation = Quaternion.Euler(0f, 0f, -7f);
            Material versusInk = OutlineMaterial(.12f, .02f);
            versusInk.name = "Colosseum versus text";
            Label(matchVs, "VS", 199f, Ink, MatchCenter, new Vector2(0f, -9f), new Vector2(420f, 230f), versusInk);
            TextMeshProUGUI versus = Label(matchVs, "VS", 199f, Color.white, MatchCenter,
                Vector2.zero, new Vector2(420f, 230f), versusInk);
            versus.enableVertexGradient = true;
            versus.colorGradient = new VertexGradient(new Color32(255, 240, 140, 255), new Color32(255, 240, 140, 255),
                new Color32(255, 166, 38, 255), new Color32(255, 166, 38, 255));

            matchFlash = Plain(root, MatchCenter, Vector2.zero, Vector2.zero, Color.clear);
            matchFlash.rectTransform.anchorMin = Vector2.zero;
            matchFlash.rectTransform.anchorMax = Vector2.one;
        }

        RectTransform BuildMatchPanel(Transform root, bool rival)
        {
            RectTransform panel = Group(rival ? "Red team panel" : "Blue team panel", root).GetComponent<RectTransform>();
            float side = rival ? 1f : -1f;
            Color tint = rival ? MatchRed : MatchBlue;
            Image background = SmoothQuad(Plain(panel, MatchCenter, Vector2.zero, new Vector2(4000f, 2400f), tint));
            background.rectTransform.pivot = new Vector2(.5f, rival ? 0f : 1f);
            background.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            Shade(background, .19f, .11f);
            ColosseumBackdropTiles.Add(background.rectTransform, ColosseumBackdropTiles.Motif.Stars, new Color32(255, 255, 255, 28));

            BuildMatchPlate(panel, rival, side * 215f);
            for (int i = 0; i < MatchTeamSize; i++)
            {
                int index = rival ? MatchTeamSize + i : i;
                matchCards[index] = BuildMatchCard(panel, new Vector2((i - (MatchTeamSize - 1) * .5f) * 146f * MatchCardFit, side * 405f), rival);
                matchCards[index].Root.localScale = Vector3.one * MatchCardFit;
            }
            return panel;
        }

        void BuildMatchPlate(Transform parent, bool rival, float y)
        {
            Color trim = rival ? MatchRivalTrim : new Color32(158, 234, 255, 255);
            Color fill = rival ? new Color32(167, 53, 78, 255) : new Color32(37, 91, 170, 255);
            Box(parent, MatchCenter, new Vector2(0f, y - 7f), new Vector2(530f, 117f), new Color32(32, 19, 51, 163), 1.1f);
            Image frame = Box(parent, MatchCenter, new Vector2(0f, y), new Vector2(522f, 115f), Ink, 1.15f);
            Image edge = Box(frame.transform, MatchCenter, Vector2.zero, new Vector2(508f, 101f), trim, 1.25f);
            Image interior = Box(edge.transform, MatchCenter, Vector2.zero, new Vector2(495f, 88f), fill, 1.45f);
            Shade(interior, .18f, .17f);

            RectTransform view = Group(rival ? "Rival reel viewport" : "Player plate", interior.transform).GetComponent<RectTransform>();
            Place(view, interior.transform, MatchCenter, Vector2.zero, new Vector2(MatchReelWidth, 84f));
            if (rival)
            {
                matchRivalFrame = frame.rectTransform;
                matchRivalEdge = edge;
                view.gameObject.AddComponent<RectMask2D>();
                matchReelCurrent = BuildMatchIdentity(view, "Rival reel current", trim);
                matchReelIncoming = BuildMatchIdentity(view, "Rival reel incoming", trim);
            }
            else
            {
                matchPlayer = BuildMatchIdentity(view, "Player identity", trim);
                matchProfileAvatar = matchPlayer.Avatar;
            }
        }

        MatchIdentity BuildMatchIdentity(Transform parent, string label, Color trim)
        {
            var identity = new MatchIdentity { Root = Group(label, parent).GetComponent<RectTransform>() };
            Vector2 left = new Vector2(0f, .5f);
            Image rim = Icon(identity.Root, circle, MatchCenter, new Vector2(-181f, 0f), 81f, Ink);
            Image face = Icon(rim.transform, circle, MatchCenter, Vector2.zero, 71f, trim);
            identity.Avatar = Art(face.transform, null, MatchCenter, Vector2.zero, 78f);
            ColosseumUiContour.Artwork(identity.Avatar, face.rectTransform, face.rectTransform.rect.width * .5f);
            identity.Name = Label(identity.Root, string.Empty, 32f, Color.white, MatchCenter, new Vector2(38f, 14f), new Vector2(332f, 43f), bold);
            identity.Name.alignment = TextAlignmentOptions.Left;
            identity.Name.richText = false;
            identity.Name.enableAutoSizing = true;
            identity.Name.fontSizeMin = 18f;
            identity.Name.fontSizeMax = 32f;
            identity.Name.overflowMode = TextOverflowModes.Ellipsis;
            Art(identity.Root, "cup", MatchCenter, new Vector2(-113f, -22f), 36f);
            identity.Trophies = Label(identity.Root, string.Empty, 24f, Cream, left, new Vector2(28f, -22f), new Vector2(228f, 31f), bold);
            identity.Trophies.alignment = TextAlignmentOptions.Left;
            return identity;
        }

        MatchCardView BuildMatchCard(Transform parent, Vector2 position, bool rival)
        {
            var card = new MatchCardView { Root = Group(rival ? "Rival unit card" : "Player unit card", parent).GetComponent<RectTransform>() };
            card.Root.anchoredPosition = position;
            Box(card.Root, MatchCenter, new Vector2(0f, -6f), new Vector2(134f, 152f), new Color32(31, 23, 51, 170), 1.1f);
            Image rim = Box(card.Root, MatchCenter, Vector2.zero, new Vector2(134f, 152f), Ink, 1.1f);
            Image unknown = Box(rim.transform, MatchCenter, Vector2.zero, new Vector2(120f, 138f), new Color32(199, 80, 107, 255), 1.3f);
            Shade(unknown, .21f, .16f);
            Label(unknown.transform, "?", 70f, new Color32(255, 203, 200, 255), MatchCenter, Vector2.zero, new Vector2(96f, 100f), bold);
            card.Placeholder = unknown.gameObject;

            Image face = Box(rim.transform, MatchCenter, Vector2.zero, new Vector2(120f, 138f), MatchCardFace, 1.3f);
            card.Face = face.gameObject.AddComponent<CanvasGroup>();
            Image portraitWindow = Box(face.transform, MatchCenter, Vector2.zero, new Vector2(MatchPortraitSize.x, MatchPortraitSize.y), new Color32(116, 87, 132, 255), 1.5f);
            var shot = new GameObject("Illustrated portrait", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            Place(shot.rectTransform, portraitWindow.transform, MatchCenter, Vector2.zero, MatchPortraitSize);
            shot.raycastTarget = false;
            ColosseumUiContour.Artwork(shot, portraitWindow.rectTransform, 22f / portraitWindow.pixelsPerUnitMultiplier);
            card.Portrait = shot;

            Image badge = Box(face.transform, MatchCenter, new Vector2(35f, 55f), new Vector2(56f, 27f), Ink, 2.4f);
            Box(badge.transform, MatchCenter, Vector2.zero, new Vector2(49f, 21f), new Color32(255, 207, 84, 255), 2.7f);
            card.Level = Label(badge.transform, "LV 1", 16f, Cream, MatchCenter, Vector2.zero, new Vector2(51f, 23f), bold);
            card.Level.enableAutoSizing = true;
            card.Level.fontSizeMin = 10f;
            card.Level.fontSizeMax = 16f;
            return card;
        }

        public void ShowMatch(string player, int trophies, Texture[] portraits, int[] heroIndices, int[] levels,
            string rivalName, int rivalTrophies, int rivalAvatar, float searchDuration)
        {
            CloseProfile();
            lobbyInput.interactable = false;
            lobbyInput.blocksRaycasts = false;
            matchRoot.SetActive(true);
            matchRoot.transform.SetAsLastSibling();
            matchAge = 0f;
            matchSearchTime = Mathf.Max(.5f, searchDuration);
            matchImpacted = matchSettled = MatchReadyToStart = false;
            matchReelIndex = 0;
            matchRivalTrophies = rivalTrophies;
            matchPlayer.Name.text = player;
            matchPlayer.Trophies.text = trophies.ToString();
            RefreshPlayerPortraits();
            matchReelIncoming.Root.gameObject.SetActive(true);
            matchRivalFrame.localScale = Vector3.one;
            matchRivalEdge.color = MatchRivalTrim;
            matchStatus.text = "SEARCHING FOR OPPONENT...";
            matchStatus.color = Cream;

            BuildMatchCandidates(rivalName, rivalAvatar);
            SetMatchReel(0);
            PositionMatchReel(0f);
            int playerCardCount = 0;
            rivalCardCount = 0;
            for (int slot = 0; slot < MatchTeamSize; slot++)
            {
                if (heroIndices[slot] >= 0) playerCardCount++;
                if (heroIndices[MatchTeamSize + slot] >= 0) rivalCardCount++;
            }
            for (int i = 0; i < matchCards.Length; i++)
            {
                bool rival = i >= MatchTeamSize;
                int hero = heroIndices[i];
                int level = levels[i];
                var card = matchCards[i];
                card.Root.gameObject.SetActive(hero >= 0);
                if (hero < 0) continue;
                int rowCount = rival ? rivalCardCount : playerCardCount;
                card.Root.anchoredPosition = new Vector2((i % MatchTeamSize - (rowCount - 1) * .5f) * 146f * MatchCardFit,
                    card.Root.anchoredPosition.y);
                if (matchPortraits[hero] == null)
                    matchPortraits[hero] = Resources.Load<Texture2D>("ColosseumUnitPortraits/" + ColosseumRoster.Heroes[hero].Key);
                Texture portrait = matchPortraits[hero];
                int photo = (rival ? ColosseumRoster.Heroes.Length : 0) + hero;
                if (portrait == null && portraits != null && photo < portraits.Length) portrait = portraits[photo];
                card.Portrait.texture = portrait;
                card.Portrait.enabled = portrait != null;
                if (portrait != null)
                {
                    // Cover the whole card window; the sides of the square art are trimmed by the mask.
                    float ratio = (float)portrait.width / portrait.height;
                    float height = Mathf.Max(MatchPortraitSize.y, MatchPortraitSize.x / ratio);
                    card.Portrait.rectTransform.sizeDelta = new Vector2(height * ratio, height);
                }
                card.Level.text = "LV " + level;
                card.Face.alpha = rival ? 0f : 1f;
                card.Placeholder.SetActive(rival);
                card.Root.localScale = Vector3.one * MatchCardFit;
                card.Revealed = !rival;
            }
            AnimateMatchImpact();
        }

        void BuildMatchCandidates(string rivalName, int rivalAvatar)
        {
            string[] names = ColosseumGame.RivalNames;
            int nameOffset = matchRandom.Next(names.Length);
            int avatarOffset = matchRandom.Next(ColosseumSave.AvatarIds.Length);
            for (int i = 0; i < MatchReelSteps; i++)
            {
                int next = (nameOffset + i * 5) % names.Length;
                if (names[next] == rivalName) next = (next + 1) % names.Length;
                int avatar = ColosseumSave.AvatarIds[(avatarOffset + i) % ColosseumSave.AvatarIds.Length];
                matchCandidates[i] = new ReelIdentity(names[next], avatar);
            }
            matchCandidates[MatchReelSteps] = new ReelIdentity(rivalName, rivalAvatar);
        }

        void SetMatchReel(int index)
        {
            SetRivalIdentity(matchReelCurrent, matchCandidates[index], "-");
            SetRivalIdentity(matchReelIncoming, matchCandidates[index + 1], "-");
        }

        void SetRivalIdentity(MatchIdentity row, ReelIdentity entry, string trophies)
        {
            row.Name.text = entry.Name;
            row.Trophies.text = trophies;
            int index = ColosseumSave.AvatarIndex(entry.Avatar);
            Sprite avatar = index >= 0 ? profileAvatars[index] : null;
            artSprites.TryGetValue("rival", out Sprite fallback);
            SetPortrait(row.Avatar, avatar != null ? avatar : fallback, avatar != null ? 74f : 79f, avatar != null ? 0f : -5f);
        }

        void PositionMatchReel(float offset)
        {
            matchReelCurrent.Root.anchoredPosition = new Vector2(-MatchReelWidth * offset, 0f);
            matchReelIncoming.Root.anchoredPosition = new Vector2(MatchReelWidth * (1f - offset), 0f);
        }

        float MatchReelTravel(float age)
        {
            // One long spin that slows the whole way, the last names crawling past before it settles on the rival.
            float left = 1f - Mathf.Clamp01(age / matchSearchTime);
            return MatchReelSteps * (1f - left * left * left);
        }

        void AnimateMatchSnap(float sinceContact)
        {
            float progress = Mathf.Clamp01(sinceContact / MatchSnapLength);
            float decay = 1f - progress;
            float bounce = -Mathf.Sin(progress * Mathf.PI * 2f) * decay * 24f;
            float pop = Mathf.Sin(progress * Mathf.PI) * decay;
            matchReelCurrent.Root.anchoredPosition = new Vector2(bounce, 0f);
            matchRivalFrame.localScale = Vector3.one * (1f + pop * .085f);
            matchRivalEdge.color = Color.Lerp(MatchRivalTrim, Cream, pop * .75f);
        }

        public void HideMatch()
        {
            matchRoot.SetActive(false);
            if (lobbyInput != null)
            {
                lobbyInput.interactable = true;
                lobbyInput.blocksRaycasts = true;
            }
            MatchReadyToStart = false;
        }

        void AnimateMatch(float delta)
        {
            matchAge += delta;
            AnimateMatchImpact();
            float searchAge = matchAge - MatchImpactAt;
            if (searchAge < 0f) return;
            if (!matchImpacted)
            {
                matchImpacted = true;
                MatchSound?.Invoke("match-found");
            }
            if (searchAge < matchSearchTime)
            {
                float slots = MatchReelTravel(searchAge);
                int index = Mathf.Min(MatchReelSteps - 1, Mathf.FloorToInt(slots));
                if (index != matchReelIndex)
                {
                    matchReelIndex = index;
                    SetMatchReel(index);
                    MatchSound?.Invoke("match-tick");
                }
                PositionMatchReel(Mathf.Clamp01(slots - index));
                return;
            }

            if (!matchSettled)
            {
                matchSettled = true;
                SetRivalIdentity(matchReelCurrent, matchCandidates[MatchReelSteps], matchRivalTrophies.ToString());
                PositionMatchReel(0f);
                matchReelIncoming.Root.gameObject.SetActive(false);
                matchStatus.text = "OPPONENT FOUND!";
                matchStatus.color = RoadYellow;
                MatchSound?.Invoke("match-ding");
            }

            float sinceContact = searchAge - matchSearchTime;
            AnimateMatchSnap(sinceContact);
            float revealAge = sinceContact - MatchSnapLength - MatchRevealLead;
            for (int i = 0; i < rivalCardCount; i++)
            {
                float since = revealAge - i * MatchRevealStep;
                if (since < 0f) continue;
                MatchCardView card = matchCards[MatchTeamSize + i];
                if (!card.Revealed)
                {
                    card.Revealed = true;
                    MatchSound?.Invoke("match-reveal");
                }
                float t = Mathf.Clamp01(since / MatchRevealLength);
                card.Face.alpha = t;
                card.Root.localScale = Vector3.one * (MatchCardFit * (1f + Mathf.Sin(t * Mathf.PI) * .15f));
                if (t >= 1f && card.Placeholder.activeSelf) card.Placeholder.SetActive(false);
            }
            MatchReadyToStart = revealAge >= (rivalCardCount - 1) * MatchRevealStep + MatchRevealLength + MatchRevealHold;
        }

        void AnimateMatchImpact()
        {
            float travel = Mathf.Clamp01(matchAge / MatchImpactAt);
            float offscreen = Mathf.Max(1280f, canvas.rect.height) * .68f * (1f - travel * travel * travel);
            float since = Mathf.Max(0f, matchAge - MatchImpactAt);
            float recoil = Mathf.Sin(since * 46f) * Mathf.Exp(-since * 19f) * 17f;
            matchRedPanel.anchoredPosition = new Vector2(0f, offscreen + recoil);
            matchBluePanel.anchoredPosition = new Vector2(0f, -offscreen - recoil);
            matchCenterGraphics.alpha = travel >= 1f ? 1f : 0f;
            matchVs.localScale = Vector3.one * (1f + Mathf.Cos(since * 27f) * Mathf.Exp(-since * 10f) * .37f);
            matchBoltCore.localScale = new Vector3(1f, 1f + Mathf.Exp(-since * 13f) * 1.6f, 1f);
            matchFlash.color = new Color(1f, .87f, .56f, travel >= 1f ? .37f * Mathf.Exp(-since * 23f) : 0f);
        }
    }
}
