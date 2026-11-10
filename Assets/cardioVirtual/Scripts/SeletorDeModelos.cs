using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class SeletorDeModelos : MonoBehaviour
{
    [SerializeField] private GameObject[] modelos;
    [SerializeField] private float alturaAlvo = 1.5f;

    private int indice;
    private GameObject atual;

    private void Start()
    {
        if (modelos == null || modelos.Length == 0)
            return;

        Array.Sort(modelos, (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.name, b.name));
        Mostrar();
    }

    private void Update()
    {
        if (modelos == null || modelos.Length == 0)
            return;

        if (Apertou(Gamepad.current?.rightTrigger, Keyboard.current?.rightArrowKey))
            Avancar(1);
        else if (Apertou(Gamepad.current?.leftTrigger, Keyboard.current?.leftArrowKey))
            Avancar(-1);
    }

    private static bool Apertou(ButtonControl gatilho, ButtonControl tecla)
    {
        return (gatilho != null && gatilho.wasPressedThisFrame) || (tecla != null && tecla.wasPressedThisFrame);
    }

    private void Avancar(int passo)
    {
        indice = (indice + passo + modelos.Length) % modelos.Length;
        Mostrar();
    }

    private void Mostrar()
    {
        if (atual != null)
            Destroy(atual);

        atual = Instantiate(modelos[indice], transform);
        AjustarTamanhoEPosicao(atual.transform);
    }

    private void AjustarTamanhoEPosicao(Transform modelo)
    {
        var renderers = modelo.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        var limites = renderers[0].bounds;
        foreach (var r in renderers)
            limites.Encapsulate(r.bounds);

        if (limites.size.y <= 0f)
            return;

        var escala = alturaAlvo / limites.size.y;
        var pivo = modelo.position;
        var baseDoModelo = new Vector3(limites.center.x, limites.min.y, limites.center.z);
        var baseEscalada = pivo + (baseDoModelo - pivo) * escala;

        modelo.localScale *= escala;
        modelo.position += transform.position - baseEscalada;
    }
}
