using UnityEngine;
using UnityEngine.InputSystem;

// Jogador em primeira pessoa: WASD (ou analogico esquerdo) anda, mouse (ou analogico direito) olha,
// Shift corre. O mouse fica preso na tela enquanto a sala estiver aberta.
// Tambem tem o "modo foco": a camera voa ate um ponto (ex.: diante do monitor) e o jogador fica parado.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] Transform head;
    [SerializeField] float walkSpeed = 1.8f;
    [SerializeField] float runSpeed = 3.4f;
    [SerializeField] float mouseSensitivity = 0.1f;
    [SerializeField] float stickSensitivity = 140f;
    [SerializeField] float maxPitch = 80f;
    [SerializeField] float focusSeconds = 0.5f;

    CharacterController body;
    Vector3 headRestPosition;
    Transform focusPoint;
    bool focusRequested;
    float focusBlend;
    float yaw;
    float pitch;
    float verticalSpeed;

    // Camera presa em um ponto de foco (ou voando de/para ele).
    public bool Frozen => focusRequested || focusBlend > 0f;
    // O jogador pediu foco e ainda nao saiu dele.
    public bool IsFocused => focusRequested;

    public void FocusOn(Transform point)
    {
        focusPoint = point;
        focusRequested = true;
    }

    public void ReleaseFocus()
    {
        focusRequested = false;
    }

    void Awake()
    {
        body = GetComponent<CharacterController>();
    }

    void Start()
    {
        // Continua olhando para onde a cena deixou a camera.
        yaw = transform.eulerAngles.y;
        pitch = head.localEulerAngles.x;
        if (pitch > 180f)
            pitch -= 360f;
        headRestPosition = head.localPosition;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (Frozen)
            return;

        Look();
        Move();
    }

    void LateUpdate()
    {
        focusBlend = Mathf.MoveTowards(focusBlend, focusRequested ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, focusSeconds));

        if (focusBlend > 0f && focusPoint != null)
        {
            float k = Mathf.SmoothStep(0f, 1f, focusBlend);
            Vector3 restPosition = transform.TransformPoint(headRestPosition);
            Quaternion restRotation = transform.rotation * Quaternion.Euler(pitch, 0f, 0f);
            head.SetPositionAndRotation(
                Vector3.Lerp(restPosition, focusPoint.position, k),
                Quaternion.Slerp(restRotation, focusPoint.rotation, k));
        }
        else
        {
            head.localPosition = headRestPosition;
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    void Look()
    {
        Vector2 look = Vector2.zero;

        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            look += Mouse.current.delta.ReadValue() * mouseSensitivity;
        if (Gamepad.current != null)
            look += Gamepad.current.rightStick.ReadValue() * stickSensitivity * Time.deltaTime;

        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        Vector2 input = Vector2.zero;
        bool run = false;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            run = keyboard.leftShiftKey.isPressed;
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            input += gamepad.leftStick.ReadValue();
            run |= gamepad.leftStickButton.isPressed;
        }

        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 planar = (transform.right * input.x + transform.forward * input.y) * (run ? runSpeed : walkSpeed);

        verticalSpeed = body.isGrounded ? -1f : verticalSpeed + Physics.gravity.y * Time.deltaTime;
        body.Move((planar + Vector3.up * verticalSpeed) * Time.deltaTime);
    }
}
