using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Liga o eletrocardiograma as telas de fim de jogo: exame concluido mostra a vitoria,
// evento que falhou mostra a derrota. Vitoria volta ao menu; derrota recomeca a sala.
[RequireComponent(typeof(GameUIManager))]
public class FimDeJogoNoExame : MonoBehaviour
{
    [SerializeField] string menuScene = "CharacterSelect";
    [SerializeField] float victoryDelay = 2.5f;
    [SerializeField] float defeatDelay = 0.5f;
    [SerializeField] float inputGuard = 0.6f;

    GameUIManager ui;
    EcgExam exam;
    bool ended;
    bool won;
    bool restarting;
    float acceptInputAt = float.MaxValue;

    void Start()
    {
        ui = GetComponent<GameUIManager>();
        exam = FindFirstObjectByType<EcgExam>();
        ui.onRestartRequested.AddListener(Restart);
    }

    void Update()
    {
        if (!ended && exam != null)
        {
            if (exam.Completed)
                StartCoroutine(End(true, victoryDelay));
            else if (exam.Failed)
                StartCoroutine(End(false, defeatDelay));
            return;
        }

        if (ended && Time.unscaledTime >= acceptInputAt && PressedConfirm())
            ui.RestartGame();
    }

    IEnumerator End(bool victory, float delay)
    {
        ended = true;
        won = victory;
        yield return new WaitForSeconds(delay);

        StopPlayer();
        if (victory)
            ui.ShowVictory();
        else
            ui.ShowDefeat();

        yield return null;
        var button = FirstActiveButton();
        if (button != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        acceptInputAt = Time.unscaledTime + inputGuard;
    }

    void StopPlayer()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.enabled = false;

        var interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null)
            interactor.enabled = false;

        if (exam != null)
            exam.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    Button FirstActiveButton()
    {
        foreach (var button in GetComponentsInChildren<Button>())
            if (button.isActiveAndEnabled)
                return button;
        return null;
    }

    static bool PressedConfirm()
    {
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        return (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame)) ||
               (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame ||
                                    gamepad.startButton.wasPressedThisFrame));
    }

    void Restart()
    {
        if (restarting)
            return;
        restarting = true;
        SceneManager.LoadScene(won ? menuScene : SceneManager.GetActiveScene().name);
    }
}
