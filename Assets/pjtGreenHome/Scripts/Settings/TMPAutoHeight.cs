using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
[RequireComponent(typeof(RectTransform))]
public class TMPAutoHeight: MonoBehaviour
{
    private TextMeshProUGUI tmp;
    private RectTransform rectTransform;

    [Header("Optional Padding")]
    public float extraPadding = 5f;

    private void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();

        tmp.enableWordWrapping = true;

        SetupTopAnchor();
    }

    private void OnEnable()
    {
        UpdateHeight();
    }

    public void SetText(string newText)
    {
        tmp.text = newText;
        UpdateHeight();
    }

    public void UpdateHeight()
    {
        tmp.ForceMeshUpdate();

        float preferredHeight = tmp.preferredHeight;

        rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            preferredHeight + extraPadding
        );
    }

    private void SetupTopAnchor()
    {
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(0.5f, 1f);
    }
}