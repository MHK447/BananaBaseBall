using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum.EditorTools
{
    /// <summary>
    /// Creates the heroes page and league page prefabs (and the card, slot and row prefabs nested in them) in the
    /// lobby's art style. The prefabs are the source of truth afterwards: the menu item only creates the ones that
    /// are missing, so anything edited by hand is left alone.
    /// </summary>
    public static class ColosseumUiPrefabs
    {
        const string Art = "Assets/SlingArena/Colosseum/UIArt/";
        const string Icons = "Assets/SlingArena/Colosseum/Resources/ColosseumUI/";
        const string Out = "Assets/SlingArena/Colosseum/Resources/ColosseumPrefabs/";

        static readonly Color Ink = new Color32(22, 16, 34, 255);
        static readonly Color Plate = new Color32(34, 32, 88, 255);
        static readonly Color PlateDeep = new Color32(26, 24, 70, 255);
        static readonly Color Periwinkle = new Color32(124, 130, 246, 255);
        static readonly Color Sky = new Color32(92, 186, 255, 255);
        static readonly Color Violet = new Color32(134, 90, 238, 255);
        static readonly Color Yellow = new Color32(255, 204, 36, 255);
        static readonly Color YellowLip = new Color32(236, 130, 20, 255);
        static readonly Color Cream = new Color32(255, 246, 222, 255);
        static readonly Color Green = new Color32(98, 228, 60, 255);
        static readonly Color GreenLip = new Color32(40, 150, 44, 255);
        static readonly Color Track = new Color32(48, 38, 64, 255);
        static readonly Vector2 C = new Vector2(.5f, .5f), T = new Vector2(.5f, 1f), B = new Vector2(.5f, 0f);

        static Sprite round, circle, coin, lockIcon, cup;
        static Material gray;
        static TMP_FontAsset font;
        static Material bold, slim;

        [MenuItem("Tools/Colosseum/Create UI Prefabs (missing only)")]
        static void CreateMissing() { Build(false); }

        public static string Build(bool overwrite)
        {
            round = Load<Sprite>(Art + "ui-round.png");
            circle = Load<Sprite>(Art + "ui-circle.png");
            coin = Load<Sprite>(Art + "coin.png");
            lockIcon = Load<Sprite>(Icons + "lock.png");
            cup = Load<Sprite>(Icons + "lobby-cup.png");
            // The project's own grayscale UI material: greys out whatever cannot be used yet.
            gray = Load<Material>("Assets/Arts/Base/Materials/Material_UI_GrayScale.mat");
            font = Load<TMP_FontAsset>("Assets/Arts/Fonts/LilitaOne/LilitaOne SDF.asset");
            if (round == null || font == null) return "missing art or font";
            Directory.CreateDirectory(Out);
            bold = TextLook(Art + "LilitaBold.mat", .30f, .16f);
            slim = TextLook(Art + "LilitaSlim.mat", .24f, .10f);

            var log = new List<string>();
            Make("PageTitle", overwrite, log, BuildPageTitle);
            Make("LeagueStatTile", overwrite, log, BuildStatTile);
            Make("HeroCard", overwrite, log, BuildHeroCard);
            Make("LineupSlot", overwrite, log, BuildLineupSlot);
            Make("LeagueRow", overwrite, log, BuildLeagueRow);
            Make("HeroesPage", overwrite, log, BuildHeroesPage);
            Make("LeaguePage", overwrite, log, BuildLeaguePage);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return string.Join(", ", log);
        }

        delegate GameObject Builder();

        static void Make(string name, bool overwrite, List<string> log, Builder builder)
        {
            string path = Out + name + ".prefab";
            if (!overwrite && File.Exists(path)) { log.Add(name + " kept"); return; }
            GameObject root = builder();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            log.Add(name + " created");
        }

        // ------------------------------------------------------------------ helpers

        static T Load<T>(string path) where T : Object { return AssetDatabase.LoadAssetAtPath<T>(path); }

        static Material TextLook(string path, float outline, float dilate)
        {
            var look = Load<Material>(path);
            if (look != null) return look;
            look = new Material(font.material);
            look.EnableKeyword("OUTLINE_ON");
            look.DisableKeyword("UNDERLAY_ON");
            look.DisableKeyword("UNDERLAY_INNER");
            look.SetFloat("_Sharpness", -.22f);
            look.SetFloat("_FaceDilate", dilate);
            look.SetFloat("_OutlineWidth", outline);
            look.SetColor("_OutlineColor", Ink);
            AssetDatabase.CreateAsset(look, path);
            return look;
        }

        static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = C;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Plain or rounded picture; tight > 0 slices the rounded sprite, larger values give smaller corners.</summary>
        static Image Pic(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite, Color color, float tight = 0f)
        {
            var image = Node(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (tight > 0f)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = tight;
            }
            return image;
        }

        static Image Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, float tight = 2f)
        {
            return Pic(name, parent, anchor, position, size, round, color, tight);
        }

        static Image FullWidthStrip(string name, Transform parent, float anchorY, float y, float height, Color color)
        {
            var strip = Pic(name, parent, new Vector2(0f, anchorY), Vector2.zero, Vector2.zero, null, color);
            strip.rectTransform.anchorMax = new Vector2(1f, anchorY);
            strip.rectTransform.sizeDelta = new Vector2(0f, height);
            strip.rectTransform.anchoredPosition = new Vector2(0f, y);
            return strip;
        }

        /// <summary>Ink-outlined plate with a soft gloss on its upper half; returns the coloured face.</summary>
        static Image Frame(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color fill, float border = 5f, float tight = 2f)
        {
            Image outer = Box(name, parent, anchor, position, size, Ink, tight);
            Image face = Box("Face", outer.transform, C, Vector2.zero, size - Vector2.one * border * 2f, fill, tight * 1.2f);
            Box("Gloss", face.transform, T, new Vector2(0f, -(size.y - border * 2f) * .22f),
                new Vector2(size.x - border * 2f - 8f, (size.y - border * 2f) * .36f), new Color(1f, 1f, 1f, .14f), tight * 1.7f);
            return face;
        }

        static TextMeshProUGUI Text(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 area, string text, float size, Color color,
            Material look, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var label = Node(name, parent, anchor, position, area).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = look;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        static void Effect(Component target, string type, params object[] fields)
        {
            System.Type kind = System.Type.GetType("SlingArena.Colosseum." + type + ", Assembly-CSharp");
            if (kind == null) return;
            var so = new SerializedObject(target.gameObject.AddComponent(kind));
            for (int i = 0; i + 1 < fields.Length; i += 2)
            {
                SerializedProperty field = so.FindProperty((string)fields[i]);
                if (field != null) field.floatValue = (float)fields[i + 1];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Slant(Graphic graphic, float lean = .16f) { Effect(graphic, "ColosseumSlant", "lean", lean); }
        static void Shade(Graphic graphic, float highlight, float shadow) { Effect(graphic, "ColosseumHudShade", "highlight", highlight, "shadow", shadow); }

        static void IconShadow(Image icon, float depth)
        {
            var shadow = icon.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(36, 24, 49, 96);
            shadow.effectDistance = new Vector2(1f, -depth);
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogWarning("ColosseumUiPrefabs: no field " + field + " on " + target.GetType().Name); return; }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetMany(Object target, string field, IList<Object> values)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogWarning("ColosseumUiPrefabs: no field " + field + " on " + target.GetType().Name); return; }
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property != null) property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Square picture with rounded corners and an ink frame; returns the picture itself.</summary>
        static Image Portrait(string name, Transform parent, Vector2 anchor, Vector2 position, float size, Sprite picture, float tight)
        {
            Box(name + " frame", parent, anchor, position, new Vector2(size + 10f, size + 10f), Ink, tight);
            Image clip = Box(name + " clip", parent, anchor, position, new Vector2(size, size), Color.white, tight * 1.15f);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Image image = Pic(name, clip.transform, C, Vector2.zero, new Vector2(size, size), picture, Color.white);
            image.preserveAspect = true;
            return image;
        }

        static GameObject Nested(string prefab, Transform parent)
        {
            var asset = Load<GameObject>(Out + prefab + ".prefab");
            return (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        }

        /// <summary>Sky backdrop with room at the bottom for the tab bar.</summary>
        static RectTransform PageRoot(string name)
        {
            var root = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            Stretch(root, 0f, 142f, 0f, 0f);
            var back = Node("Sky", root, C, Vector2.zero, Vector2.zero);
            Stretch(back, 0f, 0f, 0f, 0f);
            back.gameObject.AddComponent<ColosseumLeagueSky>().raycastTarget = true;
            return root;
        }

        // ------------------------------------------------------------------ small shared parts

        /// <summary>A slanted dark title plate.</summary>
        static GameObject BuildPageTitle()
        {
            RectTransform root = Node("PageTitle", null, C, Vector2.zero, new Vector2(264f, 66f));
            Image plate = Box("Plate", root, C, Vector2.zero, new Vector2(264f, 66f), new Color32(30, 26, 78, 224), 2.6f);
            Slant(plate);
            Shade(plate, .095f, .22f);
            Text("Title", root, C, new Vector2(0f, 1f), new Vector2(250f, 56f), "TITLE", 40f, Color.white, bold);
            return root.gameObject;
        }

        /// <summary>A dark tile with a picture, a caption and a big number (rank, trophies).</summary>
        static GameObject BuildStatTile()
        {
            RectTransform root = Node("LeagueStatTile", null, C, Vector2.zero, new Vector2(332f, 94f));
            Image face = Frame("Plate", root, C, Vector2.zero, new Vector2(332f, 94f), Plate);
            var icon = Pic("Icon", root, C, new Vector2(-117f, 1f), new Vector2(82f, 82f), cup, Color.white);
            icon.preserveAspect = true;
            IconShadow(icon, 3f);
            Text("Caption", root, C, new Vector2(-28f, 0f), new Vector2(110f, 60f), "TROPHIES", 20f, Cream, slim);
            Text("Value", root, C, new Vector2(94f, 1f), new Vector2(130f, 60f), "50", 46f, Color.white, bold);
            return root.gameObject;
        }

        // ------------------------------------------------------------------ hero card

        static GameObject BuildHeroCard()
        {
            Vector2 size = new Vector2(156f, 220f);
            RectTransform root = Node("HeroCard", null, C, Vector2.zero, size);
            var view = root.gameObject.AddComponent<ColosseumHeroCardView>();
            Box("Cast shadow", root, C, new Vector2(0f, -4f), size, new Color32(9, 22, 60, 240), 4f);
            Box("Ink frame", root, C, Vector2.zero, size, new Color32(16, 29, 69, 255), 4f);
            Image rim = Box("Rarity rim", root, C, Vector2.zero, size - Vector2.one * 5f, Color.white, 4.4f);
            Shade(rim, .45f, .28f);
            Image face = Box("Card stock", root, C, new Vector2(0f, 1f), new Vector2(144f, 207f), Color.white, 4.6f);
            Shade(face, .24f, .13f);
            ColosseumCardFoil.AddTo(face.transform);
            var banner = Box("Printed footer", root, C, new Vector2(0f, -71f), new Vector2(145f, 64f), Color.white, 4.4f);
            Shade(banner, .16f, .15f);
            Pic("Foil rule", banner.transform, T, Vector2.zero, new Vector2(138f, 2f), null, new Color32(255, 255, 255, 150));
            RectTransform clip = Node("Print area", root, C, new Vector2(0f, 35f), new Vector2(149f, 164f));
            clip.gameObject.AddComponent<RectMask2D>();
            var portrait = Pic("Fighter", clip, C, Vector2.zero, new Vector2(162f, 167f),
                Load<Sprite>("Assets/SlingArena/Colosseum/Resources/ColosseumCardFighters/axe.png"), Color.white);
            portrait.preserveAspect = true;

            var stars = ColosseumLevelStars.Create(root, new Vector2(0f, -41f), new Vector2(105f, 23f));

            var name = Text("Hero name", root, C, new Vector2(0f, -62f), new Vector2(143f, 29f), "VIKING", 22f, Color.white, slim);
            var rail = Box("Copies", root, C, new Vector2(0f, -86f), new Vector2(134f, 27f), new Color32(31, 59, 92, 255), 4.5f);
            var clippedFill = Box("Fill mask", rail.transform, C, Vector2.zero, new Vector2(128f, 21f), Color.white, 4.5f);
            clippedFill.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var fill = Pic("Copies filled", clippedFill.transform, C, Vector2.zero, Vector2.zero, null, Sky);
            Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
            Shade(fill, .12f, .14f);
            var count = Text("Copy count", rail.transform, C, Vector2.zero, new Vector2(133f, 27f), "2/2", 18f, Color.white, slim);

            Sprite hex = Load<Sprite>("Assets/Arts/Base/Components/Button/Button_Hexagon199_White_Bg.png");
            var chip = Pic("Level badge", root, C, new Vector2(-56f, 89f), new Vector2(42f, 48f), hex, new Color32(21, 26, 64, 255));
            var metal = Pic("Gold", chip.transform, C, new Vector2(0f, 1f), new Vector2(36f, 42f), hex, Yellow);
            Text("Level caption", metal.transform, C, new Vector2(0f, 9f), new Vector2(30f, 13f), "LV", 11f, Cream, slim);
            var level = Text("Level", metal.transform, C, new Vector2(0f, -4f), new Vector2(36f, 26f), "1", 22f, Color.white, bold);

            var team = Pic("In deck", root, C, Vector2.zero, new Vector2(148f, 30f), null, new Color32(0, 0, 0, 215));
            Text("Deck tag", team.transform, C, Vector2.zero, new Vector2(142f, 30f), "EQUIPPED", 22f, Color.white, slim);

            var locked = Box("Locked footer", root, C, new Vector2(0f, -86f), new Vector2(139f, 27f), new Color32(52, 68, 91, 255), 4.5f);
            Pic("Lock", locked.transform, C, new Vector2(-55f, 0f), new Vector2(19f, 19f), lockIcon, Color.white);
            Text("Source", locked.transform, C, new Vector2(9f, 0f), new Vector2(113f, 25f), "MATCH REWARD", 14f, Cream, slim);
            Set(view, "face", face);
            Set(view, "footer", banner);
            Set(view, "rim", rim);
            Set(view, "portrait", portrait);
            Set(view, "nameLabel", name);
            Set(view, "levelLabel", level);
            Set(view, "levelChip", chip.gameObject);
            Set(view, "progressLabel", count);
            Set(view, "progressFill", fill.rectTransform);
            Set(view, "progressTone", fill);
            Set(view, "progressRoot", rail.gameObject);
            Set(view, "lineupTag", team.gameObject);
            Set(view, "lockRoot", locked.gameObject);
            Set(view, "grayLook", gray);
            Set(view, "levelStars", stars);
            return root.gameObject;
        }

        static GameObject BuildLineupSlot()
        {
            Vector2 size = new Vector2(156f, 220f);
            RectTransform root = Node("LineupSlot", null, C, Vector2.zero, size);
            var view = root.gameObject.AddComponent<ColosseumLineupSlotView>();
            Box("Socket bevel", root, C, new Vector2(0f, -2f), size + new Vector2(8f, 12f), new Color32(131, 147, 180, 255), 2.7f);
            Box("Recess", root, C, new Vector2(0f, 0f), size + new Vector2(6f, 10f), new Color32(32, 41, 69, 255), 2.8f);
            GameObject card = Nested("HeroCard", root);
            Set(view, "card", card.GetComponent<ColosseumHeroCardView>());
            // Route taps over both the art and the empty edges to this slot.
            var hit = Pic("Select slot", root, C, Vector2.zero, size, null, Color.clear);
            hit.raycastTarget = true;
            return root.gameObject;
        }

        // ------------------------------------------------------------------ league row

        static GameObject BuildLeagueRow()
        {
            RectTransform root = Node("LeagueRow", null, C, Vector2.zero, new Vector2(680f, 78f));
            var view = root.gameObject.AddComponent<ColosseumLeagueRowView>();
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 78f;
            Box("Outline", root, C, Vector2.zero, new Vector2(680f, 78f), Ink, 2.4f);
            Image face = Box("Face", root, C, Vector2.zero, new Vector2(670f, 68f), new Color32(46, 44, 112, 255), 2.8f);
            Shade(face, .1f, .18f);
            Image tokenRim = Pic("Rank token rim", root, C, new Vector2(-298f, -1f), new Vector2(54f, 54f), circle, Ink);
            Shade(tokenRim, .38f, .16f);
            Image token = Pic("Rank token", root, C, new Vector2(-298f, 1f), new Vector2(46f, 46f), circle, new Color32(86, 92, 190, 255));
            Shade(token, .34f, .25f);
            TextMeshProUGUI rank = Text("Rank", root, C, new Vector2(-298f, 1f), new Vector2(60f, 40f), "10", 25f, Color.white, bold);
            rank.enableAutoSizing = true;
            rank.fontSizeMin = 14f;
            rank.fontSizeMax = 25f;
            Sprite sample = Load<Sprite>("Assets/SlingArena/Colosseum/Resources/ColosseumAvatars/warrior-01.png");
            Image avatar = Portrait("Avatar", root, C, new Vector2(-226f, 0f), 54f, sample, 3.6f);
            TextMeshProUGUI name = Text("Name", root, C, new Vector2(-36f, 1f), new Vector2(300f, 40f), "BoneKing", 27f, Color.white, bold, TextAlignmentOptions.Left);
            Image you = Box("You tag", root, C, new Vector2(134f, 0f), new Vector2(62f, 30f), Ink, 3.6f);
            Slant(you);
            Text("You text", you.transform, C, new Vector2(0f, 1f), new Vector2(60f, 26f), "YOU", 18f, Yellow, slim);
            you.gameObject.SetActive(false);
            var trophy = Pic("Trophy", root, C, new Vector2(211f, 1f), new Vector2(53f, 53f), cup, Color.white);
            trophy.preserveAspect = true;
            IconShadow(trophy, 2f);
            TextMeshProUGUI score = Text("Score", root, C, new Vector2(280f, 1f), new Vector2(84f, 40f), "120", 28f, Color.white, bold, TextAlignmentOptions.Left);
            Set(view, "face", face);
            Set(view, "rankToken", token);
            Set(view, "avatar", avatar);
            Set(view, "rankLabel", rank);
            Set(view, "nameLabel", name);
            Set(view, "scoreLabel", score);
            Set(view, "youTag", you.gameObject);
            return root.gameObject;
        }

        // ------------------------------------------------------------------ heroes page

        static GameObject BuildHeroesPage()
        {
            RectTransform root = (RectTransform)new GameObject("HeroesPage", typeof(RectTransform)).transform;
            Stretch(root, 0f, 142f, 0f, 0f);
            var page = root.gameObject.AddComponent<ColosseumHeroesPage>();
            var backdrop = Node("Deck backdrop", root, C, Vector2.zero, Vector2.zero);
            Stretch(backdrop, 0f, 0f, 0f, 0f);
            backdrop.gameObject.AddComponent<ColosseumDeckBackground>().raycastTarget = true;
            var edge = new Color32(42, 91, 134, 255);
            var inset = new Color32(69, 120, 162, 255);
            var bevel = new Color32(205, 238, 255, 255);
            var enamel = new Color32(116, 186, 232, 255);
            var shade = new Color32(65, 105, 139, 102);

            Box("Rack shadow", root, T, new Vector2(0f, -230f), new Vector2(680f, 389f), shade, 1.7f);
            var rack = Box("Deck rack", root, T, new Vector2(0f, -223f), new Vector2(680f, 388f), edge, 1.7f);
            Box("Lower lip", rack.transform, C, new Vector2(0f, -1f), new Vector2(673f, 377f), new Color32(65, 126, 181, 255), 1.8f);
            Box("Bevel", rack.transform, C, new Vector2(0f, 3f), new Vector2(673f, 373f), bevel, 1.8f);
            var frame = Box("Rack face", rack.transform, C, new Vector2(0f, 2f), new Vector2(669f, 367f), enamel, 1.9f);
            Shade(frame, .09f, .18f);

            var header = Node("Deck header", rack.transform, T, new Vector2(0f, -43f), new Vector2(650f, 62f));
            Text("Deck title", header, C, Vector2.zero, new Vector2(330f, 61f), "DECK", 44f, Cream, bold);
            var coinWell = Box("Wallet bevel", header, new Vector2(1f, .5f), new Vector2(-70f, -1f), new Vector2(132f, 47f), bevel, 2.3f);
            var purse = Box("Coin balance", coinWell.transform, C, new Vector2(0f, 1f), new Vector2(127f, 43f), inset, 2.5f);
            Shade(purse, 0f, .11f);
            Image coinIcon = Pic("Coin", purse.transform, C, new Vector2(-42f, 0f), new Vector2(34f, 34f), coin, Color.white);
            TextMeshProUGUI coins = Text("Coins", purse.transform, C, new Vector2(16f, 0f), new Vector2(76f, 36f), "90", 23f, Cream, slim);
            coins.enableAutoSizing = true;
            coins.fontSizeMin = 12f;
            coins.fontSizeMax = 23f;

            var trayBevel = Box("Tray bevel", rack.transform, T, new Vector2(0f, -222f), new Vector2(663f, 293f), new Color32(167, 215, 242, 255), 2.2f);
            var tray = Box("Equipped deck", trayBevel.transform, C, new Vector2(0f, 2f), new Vector2(659f, 289f), inset, 2.3f);
            Box("Tray lining", tray.transform, C, new Vector2(0f, -2f), new Vector2(653f, 281f), new Color32(98, 156, 200, 255), 2.5f);
            TextMeshProUGUI attack = Text("Deck attack", tray.transform, T, new Vector2(0f, -24f),
                new Vector2(440f, 35f), "TEAM ATTACK", 23f, new Color32(245, 225, 187, 255), slim);
            var slots = new List<Object>();
            for (int i = 0; i < ColosseumRoster.LineupSize; i++)
            {
                GameObject slot = Nested("LineupSlot", tray.transform);
                ((RectTransform)slot.transform).anchoredPosition = new Vector2((i - 1.5f) * 164f, -17f);
                slots.Add(slot.GetComponent<ColosseumLineupSlotView>());
            }

            const float divider = ColosseumDeckBackground.CollectionHeaderDepth;
            FullWidthStrip("Collection shadow", root, 1f, -divider - 5f, 58f, new Color32(145, 100, 71, 92));
            var band = FullWidthStrip("Collection header", root, 1f, -divider, 60f, new Color32(136, 83, 57, 255));
            FullWidthStrip("Bevel", band.transform, .5f, 1f, 54f, new Color32(255, 224, 171, 255));
            var bandFace = FullWidthStrip("Face", band.transform, .5f, 0f, 49f, new Color32(211, 147, 95, 255));
            Shade(bandFace, .09f, .10f);
            const float sidePadding = 24f;
            const float counterWidth = 95f;
            var title = Text("Collection", band.transform, C, Vector2.zero, Vector2.zero, "COLLECTION", 33f, Cream, bold, TextAlignmentOptions.Left);
            Stretch(title.rectTransform, sidePadding, 7f, sidePadding + counterWidth + 16f, 5f);
            var counter = Box("Owned count", band.transform, new Vector2(1f, .5f), new Vector2(-sidePadding - counterWidth * .5f, 1f),
                new Vector2(counterWidth, 35f), new Color32(124, 76, 54, 255), 2.8f);
            var owned = Text("Collected", counter.transform, C, Vector2.zero, new Vector2(84f, 35f), "4 / 9", 25f, Cream, slim);

            RectTransform scrollRoot = Node("Collection scroll", root, C, Vector2.zero, Vector2.zero);
            // Keep the scrolling cards clear of the raised HEROES tab, even as it bounces.
            Stretch(scrollRoot, 0f, 36f, 0f, 501f);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            RectTransform viewport = Node("Viewport", scrollRoot, C, Vector2.zero, Vector2.zero);
            Stretch(viewport, 0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            RectTransform grid = Node("Card grid", viewport, T, Vector2.zero, new Vector2(0f, 720f));
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(.5f, 1f);
            grid.anchoredPosition = Vector2.zero;
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(156f, 220f);
            layout.spacing = new Vector2(12f, 17f);
            layout.padding = new RectOffset(0, 0, 12, 52);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = grid;
            var cards = new List<Object>();
            for (int i = 0; i < ColosseumRoster.Heroes.Length; i++)
                cards.Add(Nested("HeroCard", grid).GetComponent<ColosseumHeroCardView>());

            SetMany(page, "lineup", slots);
            SetMany(page, "cards", cards);
            Set(page, "grid", grid);
            Set(page, "cardPrefab", Load<GameObject>(Out + "HeroCard.prefab").GetComponent<ColosseumHeroCardView>());
            Set(page, "coinsLabel", coins);
            Set(page, "countLabel", owned);
            Set(page, "teamAttack", attack);
            Set(page, "coinIcon", coinIcon);
            return root.gameObject;
        }

        // ------------------------------------------------------------------ league page

        static GameObject BuildLeaguePage()
        {
            RectTransform root = PageRoot("LeaguePage");
            var page = root.gameObject.AddComponent<ColosseumLeaguePage>();

            Pic("Emblem glow", root, T, new Vector2(0f, -146f), new Vector2(236f, 236f), circle, new Color(1f, 1f, 1f, .12f));
            Pic("Emblem glow core", root, T, new Vector2(0f, -146f), new Vector2(188f, 188f), circle, new Color(1f, 1f, 1f, .12f));
            Image emblem = Pic("Emblem", root, T, new Vector2(0f, -146f), new Vector2(164f, 164f), Load<Sprite>(Icons + "medal0.png"), Color.white);
            emblem.preserveAspect = true;

            Image ribbon = Box("League ribbon", root, T, new Vector2(0f, -256f), new Vector2(440f, 58f), Ink, 2.4f);
            Slant(ribbon, .12f);
            Image ribbonFace = Box("Ribbon face", ribbon.transform, C, Vector2.zero, new Vector2(430f, 48f), Violet, 2.8f);
            Slant(ribbonFace, .12f);
            Shade(ribbonFace, .2f, .2f);
            TextMeshProUGUI league = Text("League name", ribbon.transform, C, new Vector2(0f, 1f), new Vector2(420f, 46f), "BRONZE LEAGUE", 33f, Color.white, bold);

            GameObject rankTile = Nested("LeagueStatTile", root);
            rankTile.name = "Rank tile";
            var rankRect = (RectTransform)rankTile.transform;
            rankRect.anchorMin = rankRect.anchorMax = T;
            rankRect.anchoredPosition = new Vector2(-172f, -344f);
            rankTile.transform.Find("Icon").GetComponent<Image>().sprite = Load<Sprite>(Icons + "crown.png");
            rankTile.transform.Find("Caption").GetComponent<TextMeshProUGUI>().text = "RANK";
            TextMeshProUGUI rank = rankTile.transform.Find("Value").GetComponent<TextMeshProUGUI>();
            rank.text = "#12";
            rank.color = Yellow;
            GameObject trophyTile = Nested("LeagueStatTile", root);
            trophyTile.name = "Trophy tile";
            var trophyRect = (RectTransform)trophyTile.transform;
            trophyRect.anchorMin = trophyRect.anchorMax = T;
            trophyRect.anchoredPosition = new Vector2(172f, -344f);
            TextMeshProUGUI trophies = trophyTile.transform.Find("Value").GetComponent<TextMeshProUGUI>();

            Box("Promotion track", root, T, new Vector2(0f, -420f), new Vector2(676f, 34f), Ink, 3f);
            Box("Promotion track face", root, T, new Vector2(0f, -420f), new Vector2(666f, 24f), Track, 3.8f);
            Image fill = Box("Promotion fill", root, new Vector2(.5f, 1f), new Vector2(-333f, -420f), new Vector2(220f, 24f), Sky, 3.8f);
            fill.rectTransform.pivot = new Vector2(0f, .5f);
            TextMeshProUGUI promotion = Text("Promotion text", root, T, new Vector2(0f, -419f), new Vector2(660f, 30f), "100 TO NEXT LEAGUE", 19f, Color.white, slim);
            TextMeshProUGUI heading = Text("Standings heading", root, T, new Vector2(0f, -462f), new Vector2(680f, 30f), "LEAGUE STANDINGS  -  30 PLAYERS", 20f, Cream, slim);

            RectTransform scrollRoot = Node("Standings scroll", root, C, Vector2.zero, Vector2.zero);
            Stretch(scrollRoot, 0f, 104f, 0f, 484f);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            RectTransform viewport = Node("Viewport", scrollRoot, C, Vector2.zero, Vector2.zero);
            Stretch(viewport, 0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            RectTransform list = Node("Rows", viewport, T, Vector2.zero, new Vector2(0f, 700f));
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(.5f, 1f);
            list.anchoredPosition = Vector2.zero;
            var column = list.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 8f;
            column.padding = new RectOffset(0, 0, 4, 12);
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = false;
            column.childControlHeight = false;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = list;
            var rows = new List<Object>();
            for (int i = 0; i < 8; i++)
                rows.Add(Nested("LeagueRow", list).GetComponent<ColosseumLeagueRowView>());

            Image shelf = Pic("Own row shelf", root, B, new Vector2(0f, 50f), new Vector2(4000f, 100f), null, new Color32(26, 24, 70, 236));
            Pic("Shelf edge", shelf.transform, T, new Vector2(0f, 0f), new Vector2(4000f, 5f), null, Ink);
            GameObject own = Nested("LeagueRow", root);
            var ownRect = (RectTransform)own.transform;
            ownRect.anchorMin = ownRect.anchorMax = B;
            ownRect.anchoredPosition = new Vector2(0f, 49f);
            own.name = "Own row";

            Set(page, "emblem", emblem);
            SetMany(page, "emblems", new List<Object> { Load<Sprite>(Icons + "medal0.png"), Load<Sprite>(Icons + "medal1.png"), Load<Sprite>(Icons + "medal2.png"), Load<Sprite>(Icons + "medal3.png") });
            Set(page, "leagueLabel", league);
            Set(page, "rankLabel", rank);
            Set(page, "trophiesLabel", trophies);
            Set(page, "promotionLabel", promotion);
            Set(page, "headingLabel", heading);
            Set(page, "promotionFill", fill.rectTransform);
            SetFloat(page, "promotionWidth", 666f);
            SetFloat(page, "rowHeight", 86f);
            Set(page, "scroll", scroll);
            Set(page, "list", list);
            Set(page, "rowPrefab", Load<GameObject>(Out + "LeagueRow.prefab").GetComponent<ColosseumLeagueRowView>());
            SetMany(page, "rows", rows);
            Set(page, "ownRow", own.GetComponent<ColosseumLeagueRowView>());
            return root.gameObject;
        }
    }
}
