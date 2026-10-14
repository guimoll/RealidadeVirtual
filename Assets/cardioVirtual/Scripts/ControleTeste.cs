using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ControleTeste : MonoBehaviour
{
    [SerializeField] private TMP_Text texto;

    private readonly StringBuilder saida = new StringBuilder();

    private void Update()
    {
        saida.Clear();

        var gamepad = Gamepad.current;
        if (gamepad == null)
            MostrarDispositivos();
        else
            MostrarGamepad(gamepad);

        texto.text = saida.ToString();
    }

    private void MostrarDispositivos()
    {
        saida.AppendLine("Nenhum gamepad conectado.");
        saida.AppendLine();
        saida.AppendLine("Dispositivos detectados:");
        foreach (var dispositivo in InputSystem.devices)
            saida.AppendLine($"- {dispositivo.displayName} ({dispositivo.layout})");
    }

    private void MostrarGamepad(Gamepad gamepad)
    {
        saida.AppendLine($"Controle: {gamepad.displayName}");
        saida.AppendLine($"Layout: {gamepad.layout}");
        saida.AppendLine();
        saida.AppendLine($"Analogico esquerdo: {gamepad.leftStick.ReadValue():F2}");
        saida.AppendLine($"Analogico direito: {gamepad.rightStick.ReadValue():F2}");
        saida.AppendLine($"Gatilho esquerdo: {gamepad.leftTrigger.ReadValue():F2}");
        saida.AppendLine($"Gatilho direito: {gamepad.rightTrigger.ReadValue():F2}");
        saida.AppendLine();
        saida.Append("Botoes apertados: ");
        foreach (var controle in gamepad.allControls)
        {
            if (controle is ButtonControl botao && botao.isPressed && !(controle.parent is StickControl))
                saida.Append($"{botao.path.Substring(gamepad.path.Length + 1)} ");
        }
    }
}
