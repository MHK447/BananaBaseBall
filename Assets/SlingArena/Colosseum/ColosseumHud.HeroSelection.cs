using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        static readonly Vector2 HeroCenter = new Vector2(.5f, .5f);
        static readonly Color SocketBlue = new Color32(35, 112, 203, 255);
        static readonly Color ModalShade = new Color32(180, 198, 222, 202);
        static readonly Color PickerShade = new Color32(189, 210, 230, 176);
        const float FingerSize = 124f, FingerBeat = .95f, FingerGlide = .34f;
        // Where the fingertip is in the picture of the hand: the hand turns and presses about it.
        static readonly Vector2 Fingertip = new Vector2(.42f, .9375f);

        GameObject heroSelection, slotPicker;
        RectTransform heroPanel, inspectedSource, pickerHint, pickerFinger;
        Image selectionShade, heroDetailRim, detailMatte, detailBand, detailRibbon, detailPortrait, pickerRipple;
        readonly Image[] pickerShades = new Image[4];
        float pickerClock;
        ColosseumLevelStars detailStars;
        Button equipHero, upgradeHero;
        TextMeshProUGUI equipCaption, upgradePrice, pickerTitle, pickerInstruction, detailName, detailSkill, detailAbility, detailAttack, detailHealth, detailLevel, detailCopies, detailRarity;
        readonly RectTransform[] pickerSockets = new RectTransform[ColosseumRoster.LineupSize];
        int inspectedHero;

        void BuildHeroSelection()
        {
            heroSelection = Group("Hero selection", canvas, true);
            selectionShade = Plain(heroSelection.transform, HeroCenter, Vector2.zero, Vector2.zero, ModalShade);
            selectionShade.rectTransform.anchorMin = Vector2.zero;
            selectionShade.rectTransform.anchorMax = Vector2.one;
            selectionShade.raycastTarget = true;
            var outside = selectionShade.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(DismissHeroSelection);

            heroPanel = Group("Selected hero and actions", heroSelection.transform).GetComponent<RectTransform>();
            var shell = Box(heroPanel, HeroCenter, new Vector2(0f, 42f), new Vector2(494f, 708f),
                new Color32(14, 24, 66, 255), 2.4f);
            shell.raycastTarget = true;
            heroDetailRim = Box(shell.transform, HeroCenter, Vector2.zero, new Vector2(486f, 700f), Color.white, 2.5f);
            Shade(heroDetailRim, .52f, .18f);
            ColosseumCardFoil.AddTo(heroDetailRim.transform);
            Box(heroDetailRim.transform, HeroCenter, Vector2.zero, new Vector2(454f, 668f),
                new Color32(25, 43, 82, 255), 2.8f);
            detailMatte = Box(heroDetailRim.transform, HeroCenter, Vector2.zero, new Vector2(448f, 662f), Color.white, 2.9f);
            Shade(detailMatte, .32f, .08f);
            ColosseumCardFoil.AddTo(detailMatte.transform);

            var window = (RectTransform)Group("Hero illustration", detailMatte.transform).transform;
            Place(window, detailMatte.transform, HeroCenter, new Vector2(0f, 169f), new Vector2(448f, 365f));
            window.gameObject.AddComponent<RectMask2D>();
            detailPortrait = Plain(window, HeroCenter, Vector2.zero, new Vector2(412f, 369f), Color.white);
            detailPortrait.preserveAspect = true;

            Sprite hex = heroesPage.CardPrefab.LevelBadgeSprite;
            Image tag = Icon(heroDetailRim.transform, hex, HeroCenter, new Vector2(-212f, 302f), 94f,
                new Color32(18, 27, 71, 255));
            tag.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            Image metal = Icon(tag.transform, hex, HeroCenter, Vector2.zero, 84f, BattleYellow);
            Shade(metal, .25f, .11f);
            detailLevel = Label(metal.transform, "", 32f, Color.white, HeroCenter, Vector2.zero, new Vector2(74f, 70f), bold);
            detailLevel.lineSpacing = -18f;

            detailBand = Box(detailMatte.transform, HeroCenter, new Vector2(0f, -162f),
                new Vector2(449f, 328f), Color.white, 3.3f);
            Shade(detailBand, .13f, .18f);
            Plain(detailBand.transform, new Vector2(.5f, 1f), new Vector2(0f, -1f),
                new Vector2(441f, 3f), new Color32(255, 255, 255, 145));
            detailName = Label(detailBand.transform, "", 39f, Color.white, HeroCenter, new Vector2(-127f, 132f),
                new Vector2(156f, 47f), bold);
            detailName.alignment = TextAlignmentOptions.Left;
            detailName.enableAutoSizing = true;
            detailName.fontSizeMin = 27f;
            detailName.fontSizeMax = 39f;
            Image copies = Box(detailBand.transform, HeroCenter, new Vector2(11f, 131f),
                new Vector2(104f, 48f), new Color32(20, 40, 78, 235), 3.5f);
            copies.name = "Card count";
            Label(copies.transform, "CARDS", 15f, new Color32(197, 233, 255, 255), HeroCenter,
                new Vector2(0f, 13f), new Vector2(94f, 17f), slim);
            detailCopies = Label(copies.transform, "", 27f, Color.white, HeroCenter,
                new Vector2(0f, -8f), new Vector2(96f, 29f), bold);
            detailCopies.enableAutoSizing = true;
            detailCopies.fontSizeMin = 13f;
            detailCopies.fontSizeMax = 27f;
            detailStars = ColosseumLevelStars.Create(detailBand.transform, new Vector2(141f, 130f), new Vector2(135f, 30f));
            detailRibbon = Box(detailBand.transform, HeroCenter, new Vector2(-137f, 88f), new Vector2(135f, 31f), Color.white, 5f);
            detailRarity = Label(detailRibbon.transform, "", 17f, Color.white, HeroCenter, Vector2.zero, new Vector2(131f, 29f), slim);
            detailAttack = StatTile(detailBand.transform, new Vector2(-104f, 28f), "ATTACK", new Vector2(194f, 62f));
            detailHealth = StatTile(detailBand.transform, new Vector2(104f, 28f), "HEALTH", new Vector2(194f, 62f));
            Plain(detailBand.transform, HeroCenter, new Vector2(0f, -18f), new Vector2(405f, 2f), new Color32(240, 254, 255, 95));
            detailSkill = Label(detailBand.transform, "", 23f, Cream, HeroCenter, new Vector2(0f, -49f), new Vector2(405f, 36f), bold);
            detailSkill.alignment = TextAlignmentOptions.Left;
            detailAbility = Label(detailBand.transform, "", 22f, Color.white, HeroCenter, new Vector2(0f, -103f),
                new Vector2(405f, 69f), slim);
            detailAbility.alignment = TextAlignmentOptions.TopLeft;
            detailAbility.textWrappingMode = TextWrappingModes.Normal;

            Image closeFace = Frame(shell.transform, Vector2.one, new Vector2(-28f, -24f),
                new Vector2(70f, 68f), new Color32(233, 63, 76, 255), 5f);
            Image closeButton = closeFace.transform.parent.GetComponent<Image>();
            closeButton.name = "Close hero details";
            Label(closeFace.transform, "X", 38f, Color.white, HeroCenter, Vector2.zero, new Vector2(52f, 54f), bold);
            ProfileButton(closeButton, closeFace, DismissHeroSelection);

            var upgrade = Press(heroPanel, "LEVEL UP", HeroCenter, new Vector2(-122f, -367f),
                new Vector2(220f, 65f), BattleYellow, BattleLip, 30f, UpgradeSelectedHero);
            upgrade.name = "Upgrade hero";
            upgradeHero = upgrade.GetComponent<Button>();
            var upgradeCaption = upgrade.GetComponentInChildren<TextMeshProUGUI>();
            upgradeCaption.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            upgradePrice = Label(upgradeCaption.transform.parent, "", 17f, Cream, HeroCenter,
                new Vector2(0f, -14f), new Vector2(182f, 24f), slim);
            var equip = Press(heroPanel, "EQUIP", HeroCenter, new Vector2(122f, -367f), new Vector2(220f, 65f),
                new Color32(41, 203, 167, 255), new Color32(18, 122, 108, 255), 30f, ShowEquipTargets);
            equip.name = "Equip hero";
            equipHero = equip.GetComponent<Button>();
            equipCaption = equip.GetComponentInChildren<TextMeshProUGUI>();

            slotPicker = Group("Choose a hero to replace", heroSelection.transform, true);
            // While a slot is chosen the deck is left clear and everything round it is shaded: four sheets, laid out
            // against the deck's tray each time the picker opens.
            for (int i = 0; i < pickerShades.Length; i++)
            {
                pickerShades[i] = Plain(slotPicker.transform, HeroCenter, Vector2.zero, Vector2.zero, PickerShade);
                pickerShades[i].name = "Shade beside the deck";
            }
            // Narrow enough to sit over the deck's title without touching the coin counter beside it.
            var hint = Frame(slotPicker.transform, HeroCenter, Vector2.zero, new Vector2(360f, 80f), SocketBlue, 4f);
            pickerHint = (RectTransform)hint.transform.parent;
            pickerTitle = Label(hint.transform, "", 26f, Color.white, HeroCenter, new Vector2(0f, 15f), new Vector2(344f, 34f), bold);
            pickerInstruction = Label(hint.transform, "", 20f, Cream, HeroCenter, new Vector2(0f, -19f), new Vector2(344f, 27f), slim);
            for (int i = 0; i < pickerSockets.Length; i++)
            {
                int target = i;
                var hit = Plain(slotPicker.transform, HeroCenter, Vector2.zero, Vector2.one, Color.clear);
                hit.name = "Replace slot " + (i + 1);
                // Draw only the edges so the actual four portraits underneath stay visible.
                for (int side = 0; side < 4; side++)
                {
                    bool vertical = side < 2;
                    float edge = side % 2;
                    Vector2 anchor = vertical ? new Vector2(edge, .5f) : new Vector2(.5f, edge);
                    var line = Box(hit.transform, anchor, Vector2.zero, vertical ? new Vector2(4f, 0f) : new Vector2(0f, 4f), new Color32(98, 255, 215, 255));
                    line.rectTransform.anchorMin = vertical ? new Vector2(edge, 0f) : new Vector2(0f, edge);
                    line.rectTransform.anchorMax = vertical ? new Vector2(edge, 1f) : new Vector2(1f, edge);
                }
                ProfileButton(hit, hit, () => ReplaceHero(target));
                pickerSockets[i] = hit.rectTransform;
            }
            Press(slotPicker.transform, "BACK", new Vector2(.5f, 0f), new Vector2(0f, 198f), new Vector2(150f, 50f),
                SocketBlue, new Color32(26, 61, 124, 255), 23f, ShowHeroActions);
            // A hand goes along the four slots tapping each in turn: any of them can be taken.
            pickerRipple = Icon(slotPicker.transform, circle, HeroCenter, Vector2.zero, 34f, Color.clear);
            pickerRipple.name = "Tap ripple";
            Image hand = Icon(slotPicker.transform, TutorialHand, HeroCenter, Vector2.zero, FingerSize, Color.white);
            hand.name = "Pointing hand";
            pickerFinger = hand.rectTransform;
            pickerFinger.pivot = Fingertip;
            pickerFinger.localRotation = Quaternion.Euler(0f, 0f, 22f);
            BuildCardActionView();
        }

        TextMeshProUGUI StatTile(Transform parent, Vector2 position, string title, Vector2 size)
        {
            var tile = Box(parent, HeroCenter, position, size, new Color32(18, 49, 92, 194), 3.1f);
            Vector2 iconPos = new Vector2(-size.x * .30f, 0f);
            float iconSize = Mathf.Min(44f, size.y * .65f);
            if (title == "ATTACK")
                Art(tile.transform, "swords", HeroCenter, iconPos, iconSize);
            else
            {
                Icon(tile.transform, heart, HeroCenter, iconPos, iconSize, new Color32(86, 26, 64, 255));
                Icon(tile.transform, heart, HeroCenter, iconPos + Vector2.up, iconSize * .85f, new Color32(255, 85, 113, 255));
            }
            Vector2 textArea = new Vector2(size.x * .64f, 24f);
            Label(tile.transform, title, 15f, new Color32(191, 225, 249, 255), HeroCenter,
                new Vector2(size.x * .11f, size.y * .23f), textArea, slim);
            return Label(tile.transform, "", 26f, Color.white, HeroCenter, new Vector2(size.x * .11f, -size.y * .14f),
                new Vector2(size.x * .74f, 33f), bold);
        }

        void OpenHeroSelection(int hero, RectTransform source)
        {
            if (cardAction != CardAction.None) return;
            if (heroSelection == null) BuildHeroSelection();
            inspectedHero = hero;
            inspectedSource = source;
            heroSelection.SetActive(true);
            MatchSound?.Invoke("card-pick");
            heroSelection.transform.SetAsLastSibling();
            lobbyInput.interactable = false;
            lobbyInput.blocksRaycasts = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            ShowHeroActions();
            CoachSelectedHero(hero);
        }

        void ShowHeroActions()
        {
            heroPanel.gameObject.SetActive(true);
            slotPicker.SetActive(false);
            selectionShade.color = ModalShade;
            var hero = ColosseumRoster.Heroes[inspectedHero];
            int level = ColosseumRoster.Level(inspectedHero);
            detailPortrait.sprite = ColosseumHeroCardView.Fighter(inspectedHero);
            detailPortrait.enabled = detailPortrait.sprite != null;
            detailPortrait.color = hero.Locked ? new Color32(64, 76, 101, 255) : Color.white;
            Color stock = ColosseumHeroCardView.RarityColor(hero.Rarity);
            heroDetailRim.color = Color.Lerp(stock, Color.white, .30f);
            detailMatte.color = stock;
            detailBand.color = ColosseumHeroCardView.BandColor(stock);
            detailRibbon.color = Color.Lerp(stock, new Color32(20, 35, 73, 255), .19f);
            detailRarity.text = ColosseumHeroCardView.RarityName(hero.Rarity);
            detailStars.SetLevel(hero.Locked ? 0 : level);
            detailName.text = hero.Name;
            detailSkill.text = hero.Skill;
            detailAbility.text = hero.SkillDescription;
            var stats = ColosseumUnitInfo.For(hero.Weapon, level);
            detailAttack.SetText("{0}", stats.Attack);
            detailHealth.SetText("{0}", stats.Health);
            detailLevel.text = hero.Locked ? "<size=56%>LOCKED</size>" : "<size=47%>LV</size>\n" + level;
            int required = ColosseumRoster.CardsNeeded(inspectedHero);
            detailCopies.text = hero.Locked ? "–" : required == 0 ? "MAX" : ColosseumRoster.Cards(inspectedHero) + "/" + required;

            bool equipped = ColosseumRoster.IsEquipped(inspectedHero);
            equipHero.gameObject.SetActive(!equipped);
            SetHeroAction(equipHero, !equipped && !hero.Locked);
            var upgradePosition = (RectTransform)upgradeHero.transform;
            var equipPosition = ((RectTransform)equipHero.transform).anchoredPosition;
            upgradePosition.anchoredPosition = new Vector2(equipped ? 0f : -equipPosition.x, equipPosition.y);
            equipCaption.text = hero.Locked ? "LOCKED" : "EQUIP";
            bool canUpgrade = ColosseumRoster.CanUpgrade(inspectedHero);
            SetHeroAction(upgradeHero, canUpgrade);
            int missing = required - ColosseumRoster.Cards(inspectedHero);
            int price = ColosseumRoster.UpgradeCost(inspectedHero);
            if (hero.Locked) upgradePrice.text = ColosseumRoster.UnlockHint(inspectedHero);
            else if (required == 0) upgradePrice.text = "MAX LEVEL";
            else if (missing > 0) upgradePrice.text = "NEED " + missing + (missing == 1 ? " CARD" : " CARDS");
            else upgradePrice.text = (canUpgrade ? "" : "NEED ") + price + " COINS";
        }

        static void SetHeroAction(Button button, bool available)
        {
            button.interactable = available;
            foreach (Image image in button.GetComponentsInChildren<Image>())
            {
                if (image.TryGetComponent<ColosseumUiContour>(out var contour)) contour.SetGrayscale(!available);
                else image.material = available ? null : GrayLook();
            }
        }

        void UpgradeSelectedHero()
        {
            if (cardAction != CardAction.None || !heroPanel.gameObject.activeSelf) return;
            int oldLevel = ColosseumRoster.Level(inspectedHero);
            int oldCards = ColosseumRoster.Cards(inspectedHero), costInCards = ColosseumRoster.CardsNeeded(inspectedHero);
            bool lesson = ColosseumRoster.PendingUpgradeLesson >= 0 || (coachingButton && coachedHero == inspectedHero);
            if (!ColosseumRoster.Upgrade(inspectedHero)) return;
            // With the lesson's upgrade made, the guide leads back to the BATTLE button.
            if (lesson) battleNudge = true;
            HideUpgradeCoach();
            heroesPage.Refresh();
            RefreshLobby(0);
            BeginLevelUp(oldLevel, oldCards, costInCards);
        }

        void ShowEquipTargets()
        {
            if (cardAction != CardAction.None || !equipHero.interactable) return;
            heroPanel.gameObject.SetActive(false);
            slotPicker.SetActive(true);
            // The full-screen sheet only catches taps here; the shading is done by the four sheets round the deck.
            selectionShade.color = Color.clear;
            pickerTitle.text = "EQUIP " + ColosseumRoster.Heroes[inspectedHero].Name;
            pickerInstruction.text = "Choose a deck slot";
            for (int i = 0; i < pickerSockets.Length; i++)
            {
                RectTransform slot = heroesPage.LineupSlot(i);
                Vector2 lo = canvas.InverseTransformPoint(slot.TransformPoint(slot.rect.min));
                Vector2 hi = canvas.InverseTransformPoint(slot.TransformPoint(slot.rect.max));
                pickerSockets[i].anchoredPosition = (lo + hi) * .5f;
                pickerSockets[i].sizeDelta = hi - lo + Vector2.one * 4f;
            }

            RectTransform deck = heroesPage.DeckArea;
            Vector2 low = canvas.InverseTransformPoint(deck.TransformPoint(deck.rect.min));
            Vector2 high = canvas.InverseTransformPoint(deck.TransformPoint(deck.rect.max));
            // What to do is said above the deck, over its shaded title; under the deck is left to the hand.
            pickerHint.anchoredPosition = new Vector2((low.x + high.x) * .5f, high.y + 33f);
            low -= Vector2.one * 5f;
            high += Vector2.one * 5f;
            Vector2 farLow = canvas.rect.min - Vector2.one * 40f, farHigh = canvas.rect.max + Vector2.one * 40f;
            ShadeSheet(0, new Vector2(farLow.x, high.y), farHigh);                      // above the deck
            ShadeSheet(1, farLow, new Vector2(farHigh.x, low.y));                       // below it
            ShadeSheet(2, new Vector2(farLow.x, low.y), new Vector2(low.x, high.y));    // to its left
            ShadeSheet(3, new Vector2(high.x, low.y), new Vector2(farHigh.x, high.y));  // to its right
            pickerClock = FingerGlide * FingerBeat;
            TickSlotPicker(0f);
        }

        void ShadeSheet(int index, Vector2 from, Vector2 to)
        {
            RectTransform sheet = pickerShades[index].rectTransform;
            sheet.anchoredPosition = (from + to) * .5f;
            sheet.sizeDelta = new Vector2(Mathf.Max(0f, to.x - from.x), Mathf.Max(0f, to.y - from.y));
        }

        /// <summary>The hand hops to each slot in turn and taps it; the slot it taps answers, and a ripple goes out.</summary>
        void TickSlotPicker(float delta)
        {
            pickerClock += delta;
            float beat = pickerClock / FingerBeat, t = beat - Mathf.Floor(beat);
            int at = Mathf.FloorToInt(beat) % pickerSockets.Length, from = (at + pickerSockets.Length - 1) % pickerSockets.Length;
            float glide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / FingerGlide));
            float press = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - .44f) / .28f));
            Vector2 spot = Vector2.Lerp(FingerSpot(from), FingerSpot(at), glide) + Vector2.up * (16f * Mathf.Sin(glide * Mathf.PI));
            pickerFinger.anchoredPosition = spot + new Vector2(13f, -20f) * (1f - press);
            pickerFinger.localScale = Vector3.one * (1f - .09f * press);
            float wave = Mathf.Clamp01((t - .58f) / .36f);
            pickerRipple.rectTransform.anchoredPosition = FingerSpot(at);
            pickerRipple.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(34f, 128f, wave);
            pickerRipple.color = new Color(1f, 1f, 1f, t < .58f ? 0f : .5f * (1f - wave));
            for (int i = 0; i < pickerSockets.Length; i++)
                pickerSockets[i].localScale = Vector3.one * (i == at ? 1f + .035f * press : 1f);
        }

        /// <summary>Where the fingertip comes down on a slot: on the foot of the card, the hand itself below the deck.</summary>
        Vector2 FingerSpot(int slot)
        {
            return pickerSockets[slot].anchoredPosition + Vector2.down * (pickerSockets[slot].sizeDelta.y * .5f - 32f);
        }

        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float radius) { return Mathf.Sqrt(Nearest(p, a, b)) - radius; }

        static float RoundBox(Vector2 p, Vector2 centre, Vector2 half, float radius)
        {
            float x = Mathf.Abs(p.x - centre.x) - half.x + radius, y = Mathf.Abs(p.y - centre.y) - half.y + radius;
            return new Vector2(Mathf.Max(x, 0f), Mathf.Max(y, 0f)).magnitude + Mathf.Min(Mathf.Max(x, y), 0f) - radius;
        }

        /// <summary>How far a point is from the glove (under zero inside it): one finger out, three curled, and a thumb.</summary>
        static float PointingHand(Vector2 p)
        {
            float d = Capsule(p, new Vector2(-.16f, 0f), new Vector2(-.16f, .66f), .145f);
            d = Mathf.Min(d, RoundBox(p, new Vector2(.04f, -.24f), new Vector2(.36f, .26f), .15f));
            d = Mathf.Min(d, Capsule(p, new Vector2(.12f, -.10f), new Vector2(.12f, .06f), .125f));
            d = Mathf.Min(d, Capsule(p, new Vector2(.34f, -.12f), new Vector2(.34f, .04f), .115f));
            d = Mathf.Min(d, Capsule(p, new Vector2(.50f, -.20f), new Vector2(.50f, -.06f), .095f));
            return Mathf.Min(d, Capsule(p, new Vector2(-.30f, -.30f), new Vector2(-.50f, -.02f), .115f));
        }

        /// <summary>
        /// The picture of a gloved hand pointing up, painted here so that it is the game's own: a white glove with
        /// an ink outline, a cool shade down the side away from the light, and a gold cuff.
        /// </summary>
        static Sprite MakeFinger(int size)
        {
            var texture = Kit.Own(new Texture2D(size, size, TextureFormat.RGBA32, true)
                { name = "Colosseum UI pointing hand", filterMode = FilterMode.Trilinear });
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            const float line = .07f;
            Color glove = Color.white, gloveShade = new Color32(196, 213, 243, 255);
            Vector2 cuffAt = new Vector2(.04f, -.68f), cuffHalf = new Vector2(.33f, .13f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector4 sum = Vector4.zero;
                    for (int j = 0; j < 3; j++)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            var p = new Vector2((x + (i + .5f) / 3f) / size * 2f - 1f, (y + (j + .5f) / 3f) / size * 2f - 1f);
                            float sleeve = RoundBox(p, cuffAt, cuffHalf, .05f), hand = PointingHand(p);
                            Color tone;
                            if (sleeve <= 0f) tone = p.y < cuffAt.y - .03f ? BattleLip : BattleYellow;
                            else if (sleeve <= line) tone = Ink;
                            else if (hand <= 0f)
                            {
                                bool crease = Capsule(p, new Vector2(-.01f, -.14f), new Vector2(-.01f, .03f), .016f) <= 0f
                                    || Capsule(p, new Vector2(.23f, -.13f), new Vector2(.23f, .09f), .016f) <= 0f
                                    || Capsule(p, new Vector2(.43f, -.17f), new Vector2(.43f, .02f), .016f) <= 0f;
                                tone = crease ? Ink : PointingHand(p + new Vector2(.075f, -.075f)) > 0f ? gloveShade : glove;
                            }
                            else if (hand <= line) tone = Ink;
                            else continue;
                            sum += new Vector4(tone.r, tone.g, tone.b, 1f);
                        }
                    }
                    // The colour is the average of what was hit, so that the edge does not darken; how much was hit is the alpha.
                    pixels[y * size + x] = sum.w > 0f
                        ? (Color32)new Color(sum.x / sum.w, sum.y / sum.w, sum.z / sum.w, sum.w / 9f)
                        : new Color32(22, 16, 34, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return Kit.Own(Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f));
        }

        void ReplaceHero(int slot)
        {
            if (cardAction != CardAction.None || !slotPicker.activeInHierarchy) return;
            if (ColosseumRoster.Equip(inspectedHero, slot)) BeginEquip(slot);
            else if (ColosseumRoster.LineupHero(slot) == inspectedHero) CloseHeroSelection();
        }

        void DismissHeroSelection()
        {
            if (cardAction != CardAction.None) return;
            MatchSound?.Invoke("ui-close");
            CloseHeroSelection();
        }

        void CloseHeroSelection()
        {
            if (heroSelection == null || !heroSelection.activeSelf) return;
            ResetCardAction();
            heroSelection.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            lobbyInput.interactable = true;
            lobbyInput.blocksRaycasts = true;
        }
    }
}
