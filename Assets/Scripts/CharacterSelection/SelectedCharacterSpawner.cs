using UnityEngine;

// Coloque na cena do jogo: cria o personagem escolhido no menu.
public class SelectedCharacterSpawner : MonoBehaviour
{
    [SerializeField] CharacterDatabase database;
    [SerializeField] Transform spawnPoint;

    public GameObject Spawned { get; private set; }

    void Start()
    {
        if (database == null || database.Count == 0)
        {
            Debug.LogWarning("SelectedCharacterSpawner: CharacterDatabase vazio ou nao definido.");
            return;
        }

        Transform point = spawnPoint ? spawnPoint : transform;
        CharacterOption option = database.Get(CharacterSelection.SelectedIndex);
        Spawned = Instantiate(option.prefab, point.position, point.rotation);
        Spawned.name = option.displayName;
    }
}
