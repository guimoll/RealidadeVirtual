using UnityEngine;

// O eletrocardiografo de mao, em cima da mesa. Interagir com ele o pega (a logica fica no EcgExam).
// A origem do objeto e a ponta do eletrodo, que e o que encosta no peito.
public class EcgDevice : Interactable
{
    [SerializeField] Renderer led;

    Collider[] colliders;
    Material ledMaterial;
    EcgExam exam;

    public override string Prompt => "Pegar o eletrocardiógrafo";

    void Awake()
    {
        colliders = GetComponentsInChildren<Collider>();
        if (led)
            ledMaterial = led.material;
    }

    void Start()
    {
        exam = FindFirstObjectByType<EcgExam>();
    }

    public override void Interact(PlayerInteractor player)
    {
        if (exam != null)
            exam.PickUp(player);
    }

    // Na mao do jogador o colisor atrapalharia a mira, entao fica desligado.
    public void SetCollidersEnabled(bool on)
    {
        foreach (var collider in colliders)
            collider.enabled = on;
    }

    public void SetLed(Color color)
    {
        if (ledMaterial)
            ledMaterial.color = color;
    }
}
