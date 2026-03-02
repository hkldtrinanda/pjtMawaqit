using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TMPAutoResizer : MonoBehaviour
{
[Header("References")]
    [SerializeField] private TextMeshProUGUI tmpA;
    [SerializeField] private TextMeshProUGUI tmpB;
    [SerializeField] private RectTransform contentRect;

    [Header("Layout Settings")]
    [SerializeField] private float spacing = 20f;

    private void LateUpdate()
    {
        ResizeAndReposition();
    }

    private void ResizeAndReposition()
    {
        if (tmpA == null || tmpB == null) return;

        ResizeTMP(tmpA);
        ResizeTMP(tmpB);

        RectTransform rtA = tmpA.rectTransform;
        RectTransform rtB = tmpB.rectTransform;

        // FORCE PIVOT TOP
        rtA.pivot = new Vector2(0.5f, 1f);
        rtB.pivot = new Vector2(0.5f, 1f);

        // TMP A selalu di atas (Y = 0)
        rtA.anchoredPosition = new Vector2(0, 0);

        // TMP B di bawah TMP A
        float newY = -rtA.rect.height - spacing;
        rtB.anchoredPosition = new Vector2(0, newY);

        // Update total content height supaya scroll jalan
        float totalHeight = rtA.rect.height + spacing + rtB.rect.height;

        contentRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            totalHeight
        );
    }

    private void ResizeTMP(TextMeshProUGUI tmp)
    {
        tmp.ForceMeshUpdate();

        float preferredHeight = tmp.preferredHeight;

        RectTransform rt = tmp.rectTransform;
        rt.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            preferredHeight
        );
    }
}