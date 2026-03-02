using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class ButtonChangeTMPColor : MonoBehaviour
{
    private static ButtonChangeTMPColor currentSelected;

    private Button button;
    private TMP_Text text;

    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color clickedColor = Color.green;

    private void Awake()
    {
        button = GetComponent<Button>();
        text = GetComponentInChildren<TMP_Text>();

        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        // Jika ada button yang sedang aktif, reset ke normal
        if (currentSelected != null && currentSelected != this)
        {
            currentSelected.ResetColor();
        }

        // Set button ini jadi aktif
        text.color = clickedColor;
        currentSelected = this;
    }

    private void ResetColor()
    {
        text.color = normalColor;
    }
}