using UnityEngine;
using UnityEngine.UI;

public class UITileComponent : MonoBehaviour
{
    public enum Direction
    {
        LeftTop_To_RightBottom,
        RightTop_To_LeftBottom,
        LeftBottom_To_RightTop,
        RightBottom_To_LeftTop,
        Right_To_Left,
        Left_To_Right,
    }

    [SerializeField] float moveSpeed = 0.1f;
    [SerializeField] Direction direction;
    Image targetImg;
    Vector3 horizontalPeriod, verticalPeriod, origin;
    RectTransform uiField;
    Vector2 periodSize;
    float localAngle;

    // A repeated mesh can scroll just like a repeating image without shifting its backdrop or rebuilding its vertices.
    public void ScrollRepeatingGeometry(Vector3 horizontal, Vector3 vertical, float unitsPerSecond, float angle = 0f,
        Direction flow = Direction.LeftBottom_To_RightTop)
    {
        horizontalPeriod = horizontal;
        verticalPeriod = vertical;
        periodSize = new Vector2(horizontal.magnitude, vertical.magnitude);
        uiField = transform as RectTransform;
        origin = uiField != null ? uiField.anchoredPosition3D : transform.localPosition;
        direction = flow;
        moveSpeed = unitsPerSecond;
        localAngle = angle;
    }

    void Start()
    {
        if (periodSize.x > 0f) return;
        targetImg = GetComponent<Image>();
        ResetOffset();
    }

    void Update()
    {
        float offset = Time.unscaledTime * moveSpeed;
        Vector2 moveOffset;
        if (direction == Direction.LeftTop_To_RightBottom) moveOffset = new Vector2(-offset, offset);
        else if (direction == Direction.RightTop_To_LeftBottom) moveOffset = new Vector2(offset, offset);
        else if (direction == Direction.LeftBottom_To_RightTop) moveOffset = new Vector2(-offset, -offset);
        else if (direction == Direction.Right_To_Left) moveOffset = new Vector2(offset, 0f);
        else if (direction == Direction.Left_To_Right) moveOffset = new Vector2(-offset, 0f);
        else moveOffset = new Vector2(offset, -offset);

        if (periodSize.x > 0f)
        {
            // The sampled texture coordinate moves opposite the visible geometry.
            Vector2 travel = Quaternion.Euler(0f, 0f, localAngle) * -moveOffset;
            float x = Mathf.Repeat(travel.x / periodSize.x + .5f, 1f) - .5f;
            float y = Mathf.Repeat(travel.y / periodSize.y + .5f, 1f) - .5f;
            Vector3 position = origin + horizontalPeriod * x + verticalPeriod * y;
            if (uiField != null) uiField.anchoredPosition3D = position;
            else transform.localPosition = position;
        }
        else if (targetImg != null)
        {
            targetImg.materialForRendering.SetTextureOffset("_MainTex", moveOffset);
        }
    }

    void OnApplicationQuit() => ResetOffset();
    void OnDestroy() => ResetOffset();
    void OnDisable() => ResetOffset();

    void ResetOffset()
    {
        if (targetImg != null) targetImg.material.SetTextureOffset("_MainTex", Vector2.zero);
    }
}
