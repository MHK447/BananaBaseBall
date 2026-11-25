using System;
using System.Threading.Tasks;
using com.adjust.sdk;
using Facebook.Unity;
using GameAnalyticsSDK;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Voodoo.Analytics;
using Voodoo.Tiny.Sauce.Internal;
using Voodoo.Tiny.Sauce.Privacy;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        static readonly Vector2 ProfileCenter = new Vector2(.5f, .5f);
        static readonly Color ProfilePurple = new Color32(101, 64, 190, 255);
        static readonly Color ProfileShade = new Color32(60, 36, 122, 255);
        static readonly Color ProfileLight = new Color32(204, 166, 255, 255);
        readonly Sprite[] profileAvatars = new Sprite[ColosseumSave.AvatarIds.Length];
        readonly Image[] profileAvatarFrames = new Image[ColosseumSave.AvatarIds.Length];
        GameObject profileRoot;
        CanvasGroup lobbyInput;
        TMP_InputField profileName;
        TextMeshProUGUI profileNameLength;
        Button profileSave;
        Task privacySettingsTask;
        Image profilePortrait, lobbyProfileAvatar, matchProfileAvatar;
        int pendingAvatarIndex;

        void BuildProfile()
        {
            lobbyInput = menu.AddComponent<CanvasGroup>();
            profileRoot = Group("Player profile", canvas, true);
            Transform root = profileRoot.transform;
            Image backdrop = Plain(root, ProfileCenter, Vector2.zero, Vector2.zero, new Color32(13, 9, 33, 191));
            backdrop.name = "Profile backdrop";
            backdrop.rectTransform.anchorMin = Vector2.zero;
            backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.raycastTarget = true;

            const float privacyFooterHeight = 174f;
            var panelOffset = new Vector2(0f, -privacyFooterHeight * .5f);
            Box(root, ProfileCenter, panelOffset + new Vector2(0f, -12f), new Vector2(654f, 676f + privacyFooterHeight), new Color32(24, 15, 57, 211), .82f);
            Image rim = Box(root, ProfileCenter, panelOffset, new Vector2(640f, 668f + privacyFooterHeight), Ink, .82f);
            Image trim = Box(rim.transform, ProfileCenter, Vector2.zero, new Vector2(625f, 650f + privacyFooterHeight), ProfileLight, .88f);
            Image panel = Box(trim.transform, ProfileCenter, Vector2.zero, new Vector2(609f, 633f + privacyFooterHeight), ProfilePurple, .96f);
            // Extend only the footer, keeping the existing profile controls in place.
            RectTransform content = Group("Profile content", panel.transform).GetComponent<RectTransform>();
            Place(content, panel.transform, ProfileCenter, -panelOffset, new Vector2(609f, 633f));

            Image title = Frame(content, ProfileCenter, new Vector2(0f, 275f), new Vector2(583f, 76f), ProfileShade, 5f, 1.25f);
            Label(title.transform, "PLAYER PROFILE", 39f, Cream, ProfileCenter, Vector2.zero, new Vector2(420f, 52f), bold);
            RectTransform close = Press(content, "×", ProfileCenter, new Vector2(250f, 274f), new Vector2(55f, 48f),
                new Color32(247, 93, 118, 255), new Color32(157, 37, 89, 255), 34f, CloseProfile);
            close.name = "Close profile";

            Image previewRim = Box(content, ProfileCenter, new Vector2(0f, 152f), new Vector2(168f, 168f), Ink, 1f);
            Image previewBorder = Box(previewRim.transform, ProfileCenter, Vector2.zero, new Vector2(156f, 156f), Yellow, 1.12f);
            Image preview = Box(previewBorder.transform, ProfileCenter, Vector2.zero, new Vector2(143f, 143f), TileBlue, 1.3f);
            profilePortrait = Art(preview.transform, null, ProfileCenter, Vector2.zero, 146f);
            ColosseumUiContour.Artwork(profilePortrait, preview.rectTransform, 22f / preview.pixelsPerUnitMultiplier);

            var nameLabel = Label(content, "PLAYER NAME", 22f, Cream, ProfileCenter, new Vector2(-134f, 45f), new Vector2(251f, 29f), slim);
            nameLabel.alignment = TextAlignmentOptions.Left;
            profileNameLength = Label(content, string.Empty, 19f, ProfileLight, ProfileCenter, new Vector2(212f, 45f), new Vector2(97f, 29f), slim);
            profileNameLength.alignment = TextAlignmentOptions.Right;
            profileName = BuildProfileName(content);

            Label(content, "CHOOSE AVATAR", 24f, Cream, ProfileCenter, new Vector2(0f, -65f), new Vector2(400f, 34f), bold);
            for (int i = 0; i < profileAvatars.Length; i++)
            {
                int avatarIndex = i;
                int avatar = ColosseumSave.AvatarIds[avatarIndex];
                string assetPath = "ColosseumAvatars/warrior-" + avatar.ToString("D2");
                Sprite artwork = Resources.Load<Sprite>(assetPath);
                if (artwork == null)
                {
                    Texture2D texture = Resources.Load<Texture2D>(assetPath);
                    if (texture != null)
                        artwork = Kit.Own(Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), ProfileCenter, 100f));
                }
                profileAvatars[i] = artwork;

                var spot = new Vector2((avatarIndex - (profileAvatars.Length - 1) * .5f) * 109f, -141f);
                Image outer = Box(content, ProfileCenter, spot, new Vector2(98f, 100f), Ink, 1.38f);
                outer.name = "Avatar " + avatar.ToString("D2");
                Image face = Box(outer.transform, ProfileCenter, Vector2.zero, new Vector2(85f, 85f), ProfileShade, 1.58f);
                Image portrait = Art(face.transform, null, ProfileCenter, Vector2.zero, 86f);
                ColosseumUiContour.Artwork(portrait, face.rectTransform, 22f / face.pixelsPerUnitMultiplier);
                SetPortrait(portrait, artwork != null ? artwork : Drawn("hero"), 86f, 0f);
                Button choice = ProfileButton(outer, face, () => PickProfileAvatar(avatarIndex));
                choice.interactable = artwork != null;
                profileAvatarFrames[i] = outer;
            }

            RectTransform cancel = Press(content, "CANCEL", ProfileCenter, new Vector2(-128f, -255f), new Vector2(225f, 67f),
                new Color32(147, 111, 228, 255), ProfileShade, 28f, CloseProfile);
            cancel.name = "Cancel profile";
            RectTransform save = Press(content, "SAVE", ProfileCenter, new Vector2(128f, -255f), new Vector2(225f, 67f),
                BattleYellow, BattleLip, 28f, SaveProfile);
            save.name = "Save profile";
            profileSave = save.GetComponent<Button>();
            RectTransform privacy = Press(content, "PRIVACY SETTINGS", ProfileCenter, new Vector2(0f, -348f), new Vector2(481f, 56f),
                new Color32(147, 111, 228, 255), ProfileShade, 24f, OpenPrivacySettings);
            privacy.name = "Privacy settings";
            RectTransform vibration = null;
            vibration = Press(content, VibrationWord, ProfileCenter, new Vector2(0f, -426f), new Vector2(481f, 56f),
                new Color32(147, 111, 228, 255), ProfileShade, 24f, () =>
                {
                    ColosseumHaptics.On = !ColosseumHaptics.On;
                    ColosseumHaptics.Play(ColosseumHaptics.Feel.Medium);
                    vibration.GetComponentInChildren<TMP_Text>().text = VibrationWord;
                });
            vibration.name = "Vibration";
            profileName.onValueChanged.AddListener(RefreshProfileName);
            profileRoot.SetActive(false);
        }

        static string VibrationWord => ColosseumHaptics.On ? "VIBRATION: ON" : "VIBRATION: OFF";

        void OpenPrivacySettings()
        {
            var sdk = TinySauceBehaviour.Instance;
            if (sdk == null || sdk.privacyManager == null || !PrivacyManager.ConsentReady ||
                privacySettingsTask != null && !privacySettingsTask.IsCompleted) return;
            EventSystem.current?.SetSelectedGameObject(null);
            privacySettingsTask = SavePrivacySettings(sdk);
        }

        static async Task SavePrivacySettings(TinySauceBehaviour sdk)
        {
            bool hadAnalytics = PrivacyManager.AnalyticsConsent;
            bool hadAttribution = PrivacyManager.AdConsent;
            await sdk.privacyManager.OpenPrivacyScreen();
            if (Application.isEditor) return;

            if (hadAnalytics && !PrivacyManager.AnalyticsConsent)
            {
                ColosseumGame.StopMatchAnalyticsForSession();
                // TinySauce uses its persistent component to schedule performance metrics.
                sdk.StopAllCoroutines();
                StopTracking("GameAnalytics", () => GameAnalytics.SetEnabledEventSubmission(false, false));
                StopTracking("Voodoo Analytics", VoodooAnalyticsManager.Shutdown);
            }

            if (hadAttribution && !PrivacyManager.AdConsent)
            {
                StopTracking("Adjust", () => Adjust.setEnabled(false));
                // A Facebook initialization already in progress can apply the original startup choice.
                for (int attempt = 0; attempt < 50 && !FB.IsInitialized; attempt++)
                    await Task.Delay(200);
                if (!FB.IsInitialized) return;

                StopTracking("Facebook app events", () => FB.Mobile.SetAutoLogAppEventsEnabled(false));
                StopTracking("Facebook advertising ID", () => FB.Mobile.SetAdvertiserIDCollectionEnabled(false));
#if UNITY_IOS
                StopTracking("Facebook advertiser tracking", () => FB.Mobile.SetAdvertiserTrackingEnabled(false));
#endif
            }
        }

        static void StopTracking(string provider, Action stop)
        {
            try
            {
                stop();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not stop {provider} tracking ({exception.GetType().Name}).");
            }
        }

        TMP_InputField BuildProfileName(Transform parent)
        {
            Image plate = Frame(parent, ProfileCenter, Vector2.zero, new Vector2(520f, 65f), ProfileShade, 4f, 1.28f);
            plate.name = "Player name";
            plate.raycastTarget = true;
            RectTransform viewport = Group("Name viewport", plate.transform).GetComponent<RectTransform>();
            var textSize = new Vector2(464f, 50f);
            Place(viewport, plate.transform, ProfileCenter, Vector2.zero, textSize);
            viewport.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI value = Label(viewport, string.Empty, 29f, Color.white, ProfileCenter, Vector2.zero, textSize, null);
            TextMeshProUGUI hint = Label(viewport, "Enter a name", 27f, ProfileLight, ProfileCenter, Vector2.zero, textSize, null);
            value.alignment = hint.alignment = TextAlignmentOptions.Left;
            value.richText = false;

            var input = plate.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = plate;
            input.textViewport = viewport;
            input.textComponent = value;
            input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = ColosseumSave.PlayerNameLimit;
            input.richText = false;
            return input;
        }

        static Button ProfileButton(Image hitArea, Image graphic, Action action)
        {
            hitArea.raycastTarget = true;
            Button button = hitArea.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.onClick.AddListener(() => action());
            return button;
        }

        void OpenProfile()
        {
            lobbyInput.interactable = false;
            lobbyInput.blocksRaycasts = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            profileRoot.SetActive(true);
            MatchSound?.Invoke("ui-open");
            profileRoot.transform.SetAsLastSibling();
            profileName.SetTextWithoutNotify(ColosseumSave.PlayerName);
            RefreshProfileName(profileName.text);
            PickProfileAvatar(ColosseumSave.AvatarIndex(ColosseumSave.PlayerAvatar));
        }

        void CloseProfile()
        {
            if (profileRoot == null || !profileRoot.activeSelf) return;
            profileName.DeactivateInputField();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            profileRoot.SetActive(false);
            MatchSound?.Invoke("ui-close");
            lobbyInput.interactable = true;
            lobbyInput.blocksRaycasts = true;
        }

        void PickProfileAvatar(int avatarIndex)
        {
            pendingAvatarIndex = avatarIndex;
            for (int i = 0; i < profileAvatarFrames.Length; i++)
                profileAvatarFrames[i].color = i == pendingAvatarIndex ? Yellow : Ink;
            RefreshProfilePortrait();
        }

        void RefreshProfileName(string value)
        {
            profileNameLength.text = value.Length + " / " + ColosseumSave.PlayerNameLimit;
            profileSave.interactable = ColosseumSave.IsValidPlayerName(value);
        }

        void SaveProfile()
        {
            if (!ColosseumSave.SaveProfile(profileName.text, ColosseumSave.AvatarIds[pendingAvatarIndex])) return;
            lobbyName.text = ColosseumSave.PlayerName;
            RefreshPlayerPortraits();
            CloseProfile();
        }

        void RefreshProfilePortrait()
        {
            Sprite portrait = profileAvatars[pendingAvatarIndex];
            artSprites.TryGetValue("avatar", out Sprite fallback);
            SetPortrait(profilePortrait, portrait != null ? portrait : fallback, portrait != null ? 146f : 168f, portrait != null ? 0f : -17f);
        }

        void RefreshPlayerPortraits()
        {
            if (profileRoot == null) return;
            Sprite portrait = profileAvatars[ColosseumSave.AvatarIndex(ColosseumSave.PlayerAvatar)];
            artSprites.TryGetValue("avatar", out Sprite fallback);
            Sprite shown = portrait != null ? portrait : fallback;
            SetPortrait(lobbyProfileAvatar, shown, portrait != null ? 83f : 90f, portrait != null ? 0f : -10f);
            SetPortrait(matchProfileAvatar, shown, portrait != null ? 74f : 79f, portrait != null ? 0f : -5f);
            if (profileRoot.activeSelf) RefreshProfilePortrait();
        }

        static void SetPortrait(Image image, Sprite sprite, float size, float y)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.rectTransform.anchoredPosition = new Vector2(0f, y);
        }
    }
}
