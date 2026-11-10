using UnityEngine;

// Guarda o indice do personagem escolhido entre as cenas.
public static class CharacterSelection
{
    const string Key = "SelectedCharacter";

    public static int SelectedIndex
    {
        get => PlayerPrefs.GetInt(Key, 0);
        set
        {
            PlayerPrefs.SetInt(Key, value);
            PlayerPrefs.Save();
        }
    }
}
