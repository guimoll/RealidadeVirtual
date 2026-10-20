using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu de selecao: cria um botao por personagem e mostra uma previa girando.
public class CharacterSelector : MonoBehaviour
{
    [SerializeField] CharacterDatabase database;
    [SerializeField] Transform previewSpot;
    [SerializeField] Transform buttonContainer;
    [SerializeField] Button buttonTemplate;
    [SerializeField] Text selectedNameLabel;
    [SerializeField] string nextScene = "SampleScene";
    [SerializeField] float previewRotationSpeed = 30f;

    static readonly Color SelectedColor = Color.white;
    static readonly Color UnselectedColor = new Color(0.6f, 0.6f, 0.6f);

    readonly List<Button> buttons = new List<Button>();
    GameObject preview;
    int selected;

    void Start()
    {
        buttonTemplate.gameObject.SetActive(false);

        for (int i = 0; i < database.Count; i++)
        {
            int index = i;
            CharacterOption option = database.characters[i];

            Button button = Instantiate(buttonTemplate, buttonContainer);
            button.name = option.displayName;
            button.image.sprite = option.thumbnail;
            button.GetComponentInChildren<Text>().text = option.displayName;
            button.onClick.AddListener(() => Select(index));
            button.gameObject.SetActive(true);
            buttons.Add(button);
        }

        if (database.Count > 0)
            Select(CharacterSelection.SelectedIndex);
    }

    void Update()
    {
        if (preview)
            preview.transform.Rotate(0f, previewRotationSpeed * Time.deltaTime, 0f);
    }

    public void Select(int index)
    {
        selected = Mathf.Clamp(index, 0, database.Count - 1);
        CharacterOption option = database.characters[selected];

        if (preview)
            Destroy(preview);
        preview = Instantiate(option.prefab, previewSpot.position, previewSpot.rotation);

        for (int i = 0; i < buttons.Count; i++)
            buttons[i].image.color = i == selected ? SelectedColor : UnselectedColor;

        if (selectedNameLabel)
            selectedNameLabel.text = option.displayName;
    }

    // Ligado ao botao "Confirmar".
    public void Confirm()
    {
        CharacterSelection.SelectedIndex = selected;

        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
    }
}
