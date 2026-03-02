using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject panelA;
    [SerializeField] private GameObject panelB;
    [SerializeField] private GameObject panelC;
    [SerializeField] private GameObject panelD;
    [SerializeField] private GameObject panelE;

    private GameObject[] allPanels;

    private void Awake()
    {
        allPanels = new GameObject[]
        {
            panelA,
            panelB,
            panelC,
            panelD,
            panelE
        };
    }

    public void OpenPanel(GameObject panelToOpen)
    {
        foreach (GameObject panel in allPanels)
        {
            panel.SetActive(false);
        }

        panelToOpen.SetActive(true);
    }
}