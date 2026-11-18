using UnityEngine;

// Objeto que so mostra um texto. A cada E mostra a proxima frase da lista, em ciclo.
public class ExamineInteractable : Interactable
{
    [SerializeField, TextArea] string[] lines;

    int next;

    // Usado quando o objeto e criado por codigo (ex.: o paciente).
    public void Configure(string promptText, params string[] texts)
    {
        prompt = promptText;
        lines = texts;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (lines == null || lines.Length == 0)
            return;
        player.Say(lines[next]);
        next = (next + 1) % lines.Length;
    }
}
