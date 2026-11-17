using UnityEngine;

// Interruptor da sala: apaga e acende as lampadas do teto. A luz vermelha da porta nao depende dele.
public class LightSwitchInteractable : Interactable
{
    [SerializeField] FlickerLight[] lights;

    bool on = true;

    public override string Prompt => on ? "Apagar as luzes" : "Acender as luzes";

    public override void Interact(PlayerInteractor player)
    {
        on = !on;
        foreach (var light in lights)
            if (light != null)
                light.SetPowered(on);

        ProceduralAudio.Play(ProceduralAudio.Click, transform.position, 0.8f);
        player.Say(on ? "As luzes voltam, tremendo." : "Escuro. Só o vermelho da porta e o brilho do monitor.", 3.5f);
    }
}
