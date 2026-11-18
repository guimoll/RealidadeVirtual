using UnityEngine;
using UnityEngine.Events;

public class GameUIManager : MonoBehaviour
{
    [Header("End Game Panels")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;

    [Header("Events")]
    public UnityEvent onRestartRequested;

    private void Start()
    {
        HideEndScreens();
    }

    public void ShowVictory()
    {
        HideEndScreens();
        victoryPanel.SetActive(true);
    }

    public void ShowDefeat()
    {
        HideEndScreens();
        defeatPanel.SetActive(true);
    }

    public void HideEndScreens()
    {
        victoryPanel.SetActive(false);
        defeatPanel.SetActive(false);
    }

    public void RestartGame()
    {
        HideEndScreens();
        onRestartRequested?.Invoke();
    }
}