using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ManipuladorDeObjeto : MonoBehaviour
{
    [SerializeField] private Transform referencia;
    [SerializeField] private float velocidade = 2f;
    [SerializeField] private float velocidadeGiro = 120f;
    [SerializeField] private float velocidadeEscala = 1f;
    [SerializeField] private float escalaMinima = 0.25f;
    [SerializeField] private float escalaMaxima = 4f;

    private Vector3 posicaoInicial;
    private Quaternion rotacaoInicial;
    private Vector3 escalaInicial;
    private float fatorEscala = 1f;

    private void Start()
    {
        if (referencia == null && Camera.main != null)
            referencia = Camera.main.transform;

        posicaoInicial = transform.position;
        rotacaoInicial = transform.rotation;
        escalaInicial = transform.localScale;
    }

    private void Update()
    {
        var gamepad = Gamepad.current;
        var teclado = Keyboard.current;

        if (Apertou(gamepad?.buttonSouth, teclado?.rKey))
        {
            Resetar();
            return;
        }

        Mover(LerMovimento(gamepad, teclado));
        Girar(LerGiro(gamepad, teclado));
        Escalar(LerEscala(gamepad, teclado));
    }

    private static Vector3 LerMovimento(Gamepad gamepad, Keyboard teclado)
    {
        var plano = gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;
        var altura = 0f;

        if (gamepad != null)
            altura += (gamepad.rightShoulder.isPressed ? 1f : 0f) - (gamepad.leftShoulder.isPressed ? 1f : 0f);

        if (teclado != null)
        {
            plano += Eixo(teclado.aKey, teclado.dKey, teclado.sKey, teclado.wKey);
            altura += (teclado.eKey.isPressed ? 1f : 0f) - (teclado.qKey.isPressed ? 1f : 0f);
        }

        return new Vector3(plano.x, altura, plano.y);
    }

    private static Vector2 LerGiro(Gamepad gamepad, Keyboard teclado)
    {
        var giro = gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;

        if (teclado != null)
            giro += Eixo(teclado.leftArrowKey, teclado.rightArrowKey, teclado.downArrowKey, teclado.upArrowKey);

        return giro;
    }

    private static float LerEscala(Gamepad gamepad, Keyboard teclado)
    {
        var escala = 0f;

        if (gamepad != null)
            escala += gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();

        if (teclado != null)
            escala += (teclado.xKey.isPressed ? 1f : 0f) - (teclado.zKey.isPressed ? 1f : 0f);

        return escala;
    }

    private static Vector2 Eixo(KeyControl esquerda, KeyControl direita, KeyControl baixo, KeyControl cima)
    {
        return new Vector2(
            (direita.isPressed ? 1f : 0f) - (esquerda.isPressed ? 1f : 0f),
            (cima.isPressed ? 1f : 0f) - (baixo.isPressed ? 1f : 0f));
    }

    private static bool Apertou(ButtonControl botao, KeyControl tecla)
    {
        return (botao != null && botao.wasPressedThisFrame) || (tecla != null && tecla.wasPressedThisFrame);
    }

    private void Mover(Vector3 entrada)
    {
        entrada = Vector3.ClampMagnitude(entrada, 1f);
        if (entrada == Vector3.zero)
            return;

        var frente = referencia != null ? Vector3.ProjectOnPlane(referencia.forward, Vector3.up).normalized : Vector3.forward;
        if (frente == Vector3.zero)
            frente = Vector3.forward;
        var direita = Vector3.Cross(Vector3.up, frente);

        var direcao = direita * entrada.x + Vector3.up * entrada.y + frente * entrada.z;
        transform.position += direcao * (velocidade * Time.deltaTime);
    }

    private void Girar(Vector2 entrada)
    {
        entrada = Vector2.ClampMagnitude(entrada, 1f);
        if (entrada == Vector2.zero)
            return;

        var eixoLateral = referencia != null ? referencia.right : Vector3.right;
        transform.Rotate(Vector3.up, -entrada.x * velocidadeGiro * Time.deltaTime, Space.World);
        transform.Rotate(eixoLateral, entrada.y * velocidadeGiro * Time.deltaTime, Space.World);
    }

    private void Escalar(float entrada)
    {
        if (Mathf.Approximately(entrada, 0f))
            return;

        fatorEscala = Mathf.Clamp(fatorEscala * (1f + entrada * velocidadeEscala * Time.deltaTime), escalaMinima, escalaMaxima);
        transform.localScale = escalaInicial * fatorEscala;
    }

    private void Resetar()
    {
        transform.SetPositionAndRotation(posicaoInicial, rotacaoInicial);
        transform.localScale = escalaInicial;
        fatorEscala = 1f;
    }
}
