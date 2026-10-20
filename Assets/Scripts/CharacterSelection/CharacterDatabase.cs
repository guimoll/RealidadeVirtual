using UnityEngine;

[System.Serializable]
public class CharacterOption
{
    public string displayName;
    public GameObject prefab;
    public Sprite thumbnail;
}

// Lista de personagens compartilhada entre o menu e a cena do jogo.
[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Realidade Virtual/Character Database")]
public class CharacterDatabase : ScriptableObject
{
    public CharacterOption[] characters;

    public int Count => characters == null ? 0 : characters.Length;

    public CharacterOption Get(int index)
    {
        return characters[Mathf.Clamp(index, 0, Count - 1)];
    }
}
