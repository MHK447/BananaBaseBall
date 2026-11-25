using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>One line of the league table (the LeagueRow prefab): place, portrait, name and trophies.</summary>
    public sealed class ColosseumLeagueRowView : MonoBehaviour
    {
        [SerializeField] Image face, rankToken, avatar;
        [SerializeField] TextMeshProUGUI rankLabel, nameLabel, scoreLabel;
        [SerializeField] GameObject youTag;

        [Header("Look")]
        [SerializeField] Color normalFace = new Color32(46, 44, 112, 255), youFace = new Color32(255, 196, 40, 255);
        [Tooltip("Rank token colour for 1st, 2nd and 3rd place, then everyone else.")]
        [SerializeField] Color[] rankTones = { new Color32(255, 204, 36, 255), new Color32(196, 206, 222, 255), new Color32(214, 140, 84, 255), new Color32(86, 92, 190, 255) };

        int shownPlace;

        void Awake()
        {
            ColosseumUiContour.PrefabShapes(gameObject);
            // The prefab's stencil also provides its editor preview. Sample the same aperture
            // with fractional pixel coverage for the scrolling portrait at runtime.
            if (avatar == null || !avatar.transform.parent.TryGetComponent<Mask>(out var mask)) return;
            var window = (Image)mask.graphic;
            ColosseumUiContour.Artwork(avatar, window.rectTransform, 30f / window.pixelsPerUnitMultiplier);
            mask.enabled = false;
            window.enabled = false;
        }

        public void SetPlace(int place)
        {
            if (shownPlace == place) return;
            shownPlace = place;
            if (rankLabel != null) rankLabel.SetText("{0}", place);
            if (rankToken != null && rankTones.Length > 0) rankToken.color = rankTones[Mathf.Clamp(place - 1, 0, rankTones.Length - 1)];
        }

        public void Bind(int place, ColosseumRoster.Standing standing, Color? tableColor = null)
        {
            SetPlace(place);
            if (nameLabel != null) nameLabel.text = standing.Name;
            if (scoreLabel != null) scoreLabel.text = standing.Trophies.ToString();
            if (face != null) face.color = standing.You ? youFace : (tableColor ?? normalFace);
            if (youTag != null) youTag.SetActive(standing.You);
            Sprite picture = Resources.Load<Sprite>("ColosseumAvatars/warrior-" + standing.Avatar.ToString("D2"));
            if (avatar != null && picture != null) avatar.sprite = picture;
        }
    }
}
