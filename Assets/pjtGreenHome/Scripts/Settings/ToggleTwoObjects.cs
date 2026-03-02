using UnityEngine;

public class ToggleTwoObjects : MonoBehaviour
{
    public GameObject objectA;
    public GameObject objectB;

    private bool isAActive = true;

    private void Start()
    {
        SetState(isAActive);
    }

    public void Toggle()
    {
        isAActive = !isAActive;
        SetState(isAActive);
    }

    private void SetState(bool showA)
    {
        objectA.SetActive(showA);
        objectB.SetActive(!showA);
    }
}