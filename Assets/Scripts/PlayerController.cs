using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float velocidade = 4f;
    public float sensibilidadeOlhar = 90f;

    public Transform cameraJogador;

    private CharacterController controlador;
    private float rotacaoVertical = 0f;
    private float velocidadeVertical = 0f;

    void Start()
    {
        controlador = GetComponent<CharacterController>();
    }

    void Update()
    {
        Movimentar();
        Olhar();
    }

    void Movimentar()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.W))
            vertical += 1f;

        if (Input.GetKey(KeyCode.S))
            vertical -= 1f;

        if (Input.GetKey(KeyCode.D))
            horizontal += 1f;

        if (Input.GetKey(KeyCode.A))
            horizontal -= 1f;

        Vector3 movimento = transform.right * horizontal
                          + transform.forward * vertical;

        movimento = Vector3.ClampMagnitude(movimento, 1f);
        movimento *= velocidade;

        if (controlador.isGrounded && velocidadeVertical < 0f)
            velocidadeVertical = -2f;

        velocidadeVertical += Physics.gravity.y * Time.deltaTime;

        movimento.y = velocidadeVertical;

        controlador.Move(movimento * Time.deltaTime);
    }

    void Olhar()
    {
        float rotacaoHorizontal = 0f;
        float rotacaoVerticalInput = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
            rotacaoHorizontal -= 1f;

        if (Input.GetKey(KeyCode.RightArrow))
            rotacaoHorizontal += 1f;

        if (Input.GetKey(KeyCode.DownArrow))
            rotacaoVerticalInput -= 1f;

        if (Input.GetKey(KeyCode.UpArrow))
            rotacaoVerticalInput += 1f;

        transform.Rotate(
            Vector3.up,
            rotacaoHorizontal * sensibilidadeOlhar * Time.deltaTime
        );

        rotacaoVertical -= rotacaoVerticalInput
                         * sensibilidadeOlhar
                         * Time.deltaTime;

        rotacaoVertical = Mathf.Clamp(rotacaoVertical, -80f, 80f);

        cameraJogador.localRotation = Quaternion.Euler(
            rotacaoVertical, 0f, 0f
        );
    }
}
