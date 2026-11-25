using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The league page opened from the lobby's crown tab (the LeaguePage prefab): the player's league, rank and
    /// trophies, the climb to the next league, and the table of rivals. The player's own line is pinned below the
    /// table only while it is out of sight in it: once it can be seen there, the pinned copy and its shelf go and
    /// the table takes their room.
    /// </summary>
    public sealed class ColosseumLeaguePage : MonoBehaviour
    {
        [SerializeField] Image emblem;
        [Tooltip("League emblems in order: bronze, silver, gold, royal.")]
        [SerializeField] Sprite[] emblems;
        [SerializeField] TextMeshProUGUI leagueLabel, rankLabel, trophiesLabel, promotionLabel, headingLabel;
        [SerializeField] RectTransform promotionFill;
        [SerializeField] float promotionWidth = 600f;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform list;
        [SerializeField] ColosseumLeagueRowView rowPrefab;
        [SerializeField] List<ColosseumLeagueRowView> rows = new List<ColosseumLeagueRowView>();
        [SerializeField] ColosseumLeagueRowView ownRow;
        [SerializeField] float rowHeight = 86f;
        /// <summary>The player's place in the table as last worked out; 0 before the page has been filled.</summary>
        public int Place { get; private set; }
        // How much of the player's line has to show in the table before the pinned copy is not needed.
        const float InSight = .6f;
        RectTransform ownInTable, shelf, tableArea;
        float pinnedInset;
        bool pinned = true;
        VerticalLayoutGroup column;
        ContentSizeFitter listFitter;
        // The layout's final row centres, indexed from the achieved place to the previous place.
        Vector2[] climbSlots;
        int shownPlace, playerSibling;
        float climbTime, travelTime, nextTickTime;
        bool landed, climbInterrupted;
        Action<string> climbSound;
        ColosseumLeagueSky sky;
        ColosseumMedalLight medalLight;
        Image ribbonFace, rankFace, trophyFace, fillFace, shelfFace;

        static readonly (int Top, int Bottom, int Ribbon, int Row, int Rank, int Trophies)[] Palettes =
        {
            (0xeeb289, 0xbf7c7f, 0xac5940, 0x654451, 0x5c5180, 0x925635), // Bronze
            (0xb8e2ee, 0x769cbe, 0x497b99, 0x365978, 0x405d92, 0x417879), // Silver
            (0xffdf91, 0xe9a15f, 0xad7024, 0x73503c, 0x65517a, 0x99631e), // Gold
            (0xcaaff0, 0x8a70b6, 0x8051a7, 0x55416e, 0x63478c, 0x94546a), // Royal
        };

        void Awake()
        {
            sky = GetComponentInChildren<ColosseumLeagueSky>();
            ColosseumBackdropTiles.Add(sky.rectTransform, ColosseumBackdropTiles.Motif.Stars, new Color32(255, 250, 241, 38));
            medalLight = ColosseumMedalLight.Attach(transform.Find("Emblem glow").GetComponent<Image>(),
                transform.Find("Emblem glow core").GetComponent<Image>());
            ribbonFace = transform.Find("League ribbon/Ribbon face").GetComponent<Image>();
            rankFace = transform.Find("Rank tile/Plate/Face").GetComponent<Image>();
            trophyFace = transform.Find("Trophy tile/Plate/Face").GetComponent<Image>();
            fillFace = promotionFill.GetComponent<Image>();
            shelf = transform.Find("Own row shelf") as RectTransform;
            shelfFace = shelf.GetComponent<Image>();
            if (list != null)
            {
                column = list.GetComponent<VerticalLayoutGroup>();
                listFitter = list.GetComponent<ContentSizeFitter>();
            }
            if (scroll != null)
            {
                tableArea = (RectTransform)scroll.transform;
                pinnedInset = tableArea.offsetMin.y;
            }
        }

        void LateUpdate()
        {
            if (climbSlots != null)
            {
                if (climbInterrupted) EndClimb();
                else AnimateClimb(Time.unscaledDeltaTime);
                return;
            }
            if (ownRow == null || scroll == null || scroll.viewport == null) return;
            bool seen = ownInTable != null && ownInTable.gameObject.activeInHierarchy && Showing(ownInTable, scroll.viewport) >= InSight;
            if (seen == pinned) Pin(!seen);
        }

        /// <summary>Shows or takes away the pinned line with its shelf; without them the table runs to the foot of the page.</summary>
        void Pin(bool on)
        {
            pinned = on;
            if (ownRow != null) ownRow.gameObject.SetActive(on);
            if (shelf != null) shelf.gameObject.SetActive(on);
            if (tableArea != null) tableArea.offsetMin = new Vector2(tableArea.offsetMin.x, on ? pinnedInset : Mathf.Min(pinnedInset, 6f));
        }

        /// <summary>The share of a line's height that is inside the window of the table.</summary>
        static float Showing(RectTransform line, RectTransform window)
        {
            float low = window.InverseTransformPoint(line.TransformPoint(line.rect.min)).y;
            float high = window.InverseTransformPoint(line.TransformPoint(line.rect.max)).y;
            float inside = Mathf.Min(high, window.rect.yMax) - Mathf.Max(low, window.rect.yMin);
            return Mathf.Clamp01(inside / Mathf.Max(1f, high - low));
        }

        public void Open(int previousPlace = 0, Action<string> playSound = null, bool promoted = false)
        {
            if (climbSlots != null && !climbInterrupted && gameObject.activeInHierarchy) return;
            gameObject.SetActive(true);
            Refresh();
            int start = promoted ? rows.FindLastIndex(row => row.gameObject.activeSelf) + 1 : Mathf.Min(previousPlace, rows.Count);
            if ((start > Place || promoted) && ownInTable != null && scroll != null && column != null && listFitter != null)
                BeginClimb(Mathf.Max(Place, start), playSound);
        }

        public void Close()
        {
            EndClimb();
            gameObject.SetActive(false);
        }

        // Restoring sibling order must wait until Unity has finished deactivating the parent canvas.
        void OnDisable() { climbInterrupted = climbSlots != null; }

        public void Refresh()
        {
            EndClimb();
            int league = ColosseumSave.League, trophies = ColosseumSave.Trophies;
            var theme = Palettes[league];
            Color rowColor = MeshKit.Rgb(theme.Row, 1f);
            sky.SetGradient(MeshKit.Rgb(theme.Top, 1f), MeshKit.Rgb(theme.Bottom, 1f));
            ribbonFace.color = MeshKit.Rgb(theme.Ribbon, 1f);
            rankFace.color = MeshKit.Rgb(theme.Rank, 1f);
            trophyFace.color = MeshKit.Rgb(theme.Trophies, 1f);
            shelfFace.color = Color.Lerp(rowColor, Color.black, .28f);
            Color metal = MeshKit.Rgb(ColosseumSave.LeagueTone[league], 1f);
            fillFace.color = Color.Lerp(metal, Color.white, .18f);
            medalLight.SetTint(Color.Lerp(metal, Color.white, .37f), Color.Lerp(metal, Color.white, .78f));

            List<ColosseumRoster.Standing> table = ColosseumRoster.Standings();
            while (rows.Count < table.Count && rowPrefab != null && list != null)
                rows.Add(Instantiate(rowPrefab, list));
            int place = 1;
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(i < table.Count);
                if (i >= table.Count) continue;
                rows[i].Bind(i + 1, table[i], rowColor);
                if (table[i].You)
                {
                    place = i + 1;
                    ownInTable = (RectTransform)rows[i].transform;
                    if (ownRow != null) ownRow.Bind(place, table[i]);
                }
            }

            int floor = ColosseumSave.LeagueFloor, goal = ColosseumSave.LeagueGoal;
            if (emblem != null && emblems != null && emblems.Length > 0) emblem.sprite = emblems[Mathf.Clamp(league, 0, emblems.Length - 1)];
            if (leagueLabel != null) leagueLabel.text = ColosseumSave.LeagueName;
            Place = place;
            if (rankLabel != null) rankLabel.text = "#" + place;
            if (trophiesLabel != null) trophiesLabel.text = trophies.ToString();
            if (promotionLabel != null) promotionLabel.text = goal > 0 ? (goal - trophies) + " TO NEXT LEAGUE" : "TOP LEAGUE";
            if (headingLabel != null) headingLabel.text = "LEAGUE STANDINGS  -  " + table.Count + " PLAYERS";
            if (promotionFill != null)
            {
                float climbed = goal > floor ? Mathf.Clamp01((float)(trophies - floor) / (goal - floor)) : 1f;
                promotionFill.sizeDelta = new Vector2(climbed > 0f ? Mathf.Max(18f, promotionWidth * climbed) : 0f, promotionFill.sizeDelta.y);
            }
            // Open with the player's own line in view.
            if (scroll != null && table.Count > 1)
            {
                Canvas.ForceUpdateCanvases();
                scroll.StopMovement();
                FocusRank(place, .4f);
            }
        }

        void FocusRank(float place, float viewportFraction)
        {
            float height = scroll.viewport.rect.height;
            float span = Mathf.Max(1f, list.rect.height - height);
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(((place - .5f) * rowHeight - height * viewportFraction) / span);
        }

        void BeginClimb(int start, Action<string> playSound)
        {
            Pin(false);
            Canvas.ForceUpdateCanvases();
            climbSlots = new Vector2[start - Place + 1];
            for (int i = 0; i < climbSlots.Length; i++)
                climbSlots[i] = ((RectTransform)rows[Place - 1 + i].transform).anchoredPosition;

            // Freeze the measured slots so the highlighted row can slide in front of each rival it passes.
            listFitter.enabled = false;
            column.enabled = false;
            scroll.vertical = false;
            playerSibling = ownInTable.GetSiblingIndex();
            ownInTable.SetAsLastSibling();
            shownPlace = start;
            if (rankLabel != null) rankLabel.SetText("#{0}", start);
            climbSound = playSound;
            climbTime = nextTickTime = 0f;
            travelTime = .46f + .21f * Mathf.Sqrt(start - Place);
            landed = false;
            AnimateClimb(0f);
        }

        void AnimateClimb(float delta)
        {
            climbTime += delta;
            float t = Mathf.Clamp01((climbTime - .19f) / travelTime);
            float progress = Mathf.SmoothStep(0f, 1f, t);
            int steps = climbSlots.Length - 1;
            float remaining = steps * (1f - progress);
            int lower = Mathf.FloorToInt(remaining);
            Vector2 position = Vector2.Lerp(climbSlots[lower], climbSlots[Mathf.Min(lower + 1, steps)], remaining - lower);
            int rank = Place + Mathf.RoundToInt(remaining);
            rows[Place - 1].SetPlace(rank);
            if (rank != shownPlace)
            {
                shownPlace = rank;
                if (rankLabel != null) rankLabel.SetText("#{0}", rank);
                if (t < 1f && climbTime >= nextTickTime)
                {
                    climbSound?.Invoke("match-tick");
                    nextTickTime = climbTime + .065f;
                }
            }
            for (int i = 1; i <= steps; i++)
            {
                var rival = rows[Place - 1 + i];
                float offset = Mathf.Clamp01(remaining - i + 1f);
                ((RectTransform)rival.transform).anchoredPosition = Vector2.Lerp(climbSlots[i], climbSlots[i - 1], offset);
                rival.SetPlace(Place + i - (rank >= Place + i ? 1 : 0));
            }

            float settle = Mathf.Clamp01((climbTime - .19f - travelTime) / .32f);
            float bounce = Mathf.Sin(settle * Mathf.PI) * (1f - settle);
            ownInTable.anchoredPosition = position + Vector2.up * (bounce * 12f);
            ownInTable.localScale = Vector3.one * (1f + .022f * Mathf.Sin(progress * Mathf.PI) + .06f * bounce);
            FocusRank(Place + remaining, Mathf.Lerp(.60f, .40f, progress));
            if (t == 1f && !landed)
            {
                landed = true;
                climbSound?.Invoke("rank-up");
            }
            if (settle >= 1f) EndClimb();
        }

        void EndClimb()
        {
            if (climbSlots == null) return;
            for (int i = 0; i < climbSlots.Length; i++)
            {
                var row = rows[Place - 1 + i];
                ((RectTransform)row.transform).anchoredPosition = climbSlots[i];
                row.SetPlace(Place + i);
            }
            climbSlots = null;
            climbSound = null;
            climbInterrupted = false;
            ownInTable.localScale = Vector3.one;
            ownInTable.SetSiblingIndex(playerSibling);
            column.enabled = true;
            listFitter.enabled = true;
            scroll.vertical = true;
            if (rankLabel != null) rankLabel.SetText("#{0}", Place);
        }
    }
}
