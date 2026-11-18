using UnityEngine;

// Base de tudo que o jogador pode usar com E. O objeto precisa ter um Collider
// (pode ser trigger) para o PlayerInteractor conseguir mirar nele.
public abstract class Interactable : MonoBehaviour
{
    [SerializeField] protected string prompt = "Examinar";

    // Texto mostrado depois de [E] enquanto o jogador mira no objeto.
    public virtual string Prompt => prompt;

    public abstract void Interact(PlayerInteractor player);
}
