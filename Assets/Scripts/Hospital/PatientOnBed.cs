using System.Collections;
using UnityEngine;

// Cria o personagem escolhido no menu e deita ele de costas na maca, respirando.
public class PatientOnBed : MonoBehaviour
{
    [SerializeField] CharacterDatabase database;
    [Tooltip("Centro da superficie do colchao. O eixo Z deste ponto e o comprimento da maca.")]
    [SerializeField] Transform restingPoint;
    [SerializeField] RuntimeAnimatorController breathingMale;
    [SerializeField] RuntimeAnimatorController breathingFemale;

    // Osso do peito do paciente. Ele se move com a respiracao.
    Transform chestBone;
    // Altura da pele do peito acima do osso (medida na malha, pois depende do personagem e da roupa).
    float chestOffset = 0.2f;

    public GameObject Patient { get; private set; }

    // Ponto na pele do peito (o paciente esta de barriga para cima). Falso ate o paciente existir.
    public bool TryGetChest(out Vector3 surfacePoint)
    {
        surfacePoint = default;
        if (chestBone == null)
            return false;
        surfacePoint = chestBone.position + Vector3.up * chestOffset;
        return true;
    }

    // Procura o ponto mais alto da malha logo acima do osso do peito: essa e a pele.
    void MeasureChestOffset()
    {
        if (chestBone == null)
            return;

        Vector3 bone = chestBone.position;
        float top = float.MinValue;
        var baked = new Mesh();

        foreach (var skinned in Patient.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skinned.BakeMesh(baked, true);
            Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
            foreach (Vector3 vertex in baked.vertices)
            {
                Vector3 world = toWorld.MultiplyPoint3x4(vertex);
                float dx = world.x - bone.x;
                float dz = world.z - bone.z;
                if (dx * dx + dz * dz < 0.12f * 0.12f && world.y > top)
                    top = world.y;
            }
        }
        Destroy(baked);

        if (top > float.MinValue)
            chestOffset = Mathf.Clamp(top - bone.y, 0.05f, 0.4f);
    }

    static Transform FindBone(GameObject root, string suffix)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>())
            if (t.name.EndsWith(suffix))
                return t;
        return null;
    }

    IEnumerator Start()
    {
        if (database == null || database.Count == 0)
        {
            Debug.LogWarning("PatientOnBed: CharacterDatabase vazio ou nao definido.", this);
            yield break;
        }

        CharacterOption option = database.Get(CharacterSelection.SelectedIndex);
        if (option.prefab == null)
        {
            Debug.LogWarning("PatientOnBed: " + option.displayName + " sem modelo.", this);
            yield break;
        }

        // Girar -90 graus em X deita o personagem de costas, cabeca para o lado -Z da maca.
        Patient = Instantiate(option.prefab, restingPoint.position,
            restingPoint.rotation * Quaternion.Euler(-90f, 0f, 0f));
        Patient.name = option.displayName;
        chestBone = FindBone(Patient, "Spine2") ?? FindBone(Patient, "Spine1") ?? FindBone(Patient, "Spine");

        bool female = option.prefab.name.StartsWith("Female");
        // Os avatares sao rigs Generic (esqueleto Bip01) e vem sem Animator: adiciona um.
        // As animacoes de respirar usam o mesmo esqueleto, entao encaixam pelos nomes dos ossos.
        var animator = Patient.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = Patient.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = female ? breathingFemale : breathingMale;
        animator.Update(0f);

        // Ajusta agora e de novo no frame seguinte, quando os bounds ja refletem a pose final.
        Settle();
        yield return null;
        Settle();
        AddInteractionZone();
    }

    // Caixa invisivel em volta do corpo, para o jogador poder mirar e examinar o paciente.
    void AddInteractionZone()
    {
        MeasureChestOffset();

        if (!TryMeasure(out Bounds bounds))
            return;

        var zone = new GameObject("PatientInteraction");
        zone.transform.SetParent(restingPoint, true);
        zone.transform.position = bounds.center;
        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = bounds.size;

        zone.AddComponent<ExamineInteractable>().Configure("Examinar o paciente",
            "Respira devagar. Não reage à sua voz.",
            "Sem pulseira e sem prontuário. Ninguém sabe quem é.",
            "O monitor marca batimentos, mas o peito quase não se mexe.");
    }

    // Centraliza o corpo no colchao e apoia as costas na superficie.
    void Settle()
    {
        if (!TryMeasure(out Bounds bounds))
            return;

        Vector3 target = restingPoint.position;
        Patient.transform.position += new Vector3(target.x - bounds.center.x, target.y - bounds.min.y, target.z - bounds.center.z);
    }

    // Mede o corpo pelos vertices da pose atual. Os bounds de um SkinnedMeshRenderer
    // sao fixos (pose T) e deixariam o paciente flutuando ou afundado no colchao.
    bool TryMeasure(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        var baked = new Mesh();

        foreach (var skinned in Patient.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skinned.BakeMesh(baked, true);
            // Com useScale o mesh baked ja inclui a escala; so falta posicao e rotacao.
            Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
            foreach (Vector3 vertex in baked.vertices)
            {
                Vector3 world = toWorld.MultiplyPoint3x4(vertex);
                if (!any)
                {
                    bounds = new Bounds(world, Vector3.zero);
                    any = true;
                }
                else
                    bounds.Encapsulate(world);
            }
        }
        Destroy(baked);

        if (any)
            return true;

        // Sem malha deformavel: usa os bounds dos renderers.
        foreach (var renderer in Patient.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }
        return any;
    }
}
