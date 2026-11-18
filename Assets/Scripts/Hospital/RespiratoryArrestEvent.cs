using System.Collections;
using UnityEngine;

// Evento: parada cardiorrespiratoria. O paciente para de respirar e o coracao dispara.
// O aparelho na mao vira uma bag-valve-mask (ambu). O jogador SEGURA Z para encostar a mascara
// no rosto e ventilar; a barra azul enche enquanto segura e esvazia se soltar.
// Sao 3 ventilacoes, cada uma mais longa. Entre elas a mascara se afasta e o jogador precisa
// soltar o Z e apertar de novo. Tudo em 1 minuto.
public class RespiratoryArrestEvent : EcgEvent
{
    // Segundos segurando Z para encher a barra em cada round.
    static readonly float[] HoldSeconds = { 2.5f, 3.5f, 4.5f };
    // Quanto a barra esvazia por segundo com o Z solto.
    const float DecayPerSecond = 0.3f;
    const float RacingBpm = 145f;

    static AudioClip gasping;
    static AudioClip breathing;

    public override string Name => "Parada cardiorrespiratória";

    public override IEnumerator Run(EcgEventDirector d, System.Action<bool> finish)
    {
        var hud = d.Interactor;
        hud.SetObjective("EMERGÊNCIA: PARADA CARDIORRESPIRATÓRIA\nVentile o paciente com a máscara de ar antes que o tempo acabe");
        hud.Say("O paciente parou de respirar!", 4f);

        if (gasping == null)
            gasping = EcgEventDirector.LoadClip("Events/Gasping", EcgEventDirector.MakeGasping);
        if (breathing == null)
            breathing = EcgEventDirector.LoadClip("Events/Breathing", EcgEventDirector.MakeBreathing);

        // Os dois sons tocam o tempo todo; o volume de cada um troca conforme o jogador segura Z.
        AudioSource gasp = d.StartLoop(gasping, 0.55f, atMonitor: false);
        AudioSource calm = d.StartLoop(breathing, 0f, atMonitor: false);
        var mask = new BagValveMask(d);
        d.StartCoroutine(d.LockView(Vector3.Lerp(d.ChestPoint(), d.FacePoint(), 0.6f)));

        float timeLeft = EcgEventDirector.EventSeconds;
        float calmBlend = 0f;
        bool saved = false;

        for (int round = 0; round < HoldSeconds.Length; round++)
        {
            string label = round == 0 ? "Segure [Z] para aplicar a máscara de ar" : "Segure [Z] mais uma vez";
            // Do segundo round em diante o Z so vale depois de ser solto.
            bool waitingRelease = round > 0;
            float fill = 0f;

            while (timeLeft > 0f && fill < 1f)
            {
                float dt = Time.deltaTime;
                timeLeft -= dt;

                bool holding = EcgEventDirector.HoldingZ();
                if (waitingRelease)
                {
                    if (!holding)
                        waitingRelease = false;
                    holding = false;
                }

                fill = holding
                    ? Mathf.Min(1f, fill + dt / HoldSeconds[round])
                    : Mathf.Max(0f, fill - DecayPerSecond * dt);

                // Respira tranquilo so com a mascara encostada; senao, puxa o ar desesperado.
                calmBlend = Mathf.MoveTowards(calmBlend, holding && mask.OnFace ? 1f : 0f, dt * 3f);
                gasp.volume = 0.55f * (1f - calmBlend);
                calm.volume = 0.5f * calmBlend;

                d.KeepHeartRate(RacingBpm);
                d.ShowTimer(timeLeft);
                d.ShowBar(fill, "", label);
                mask.Follow(holding, dt);
                yield return null;
            }

            if (timeLeft <= 0f)
                break;

            if (round < HoldSeconds.Length - 1)
                hud.Say("Boa! Solte o [Z] e ventile mais uma vez.", 2.5f);
            else
                saved = true;
        }

        EcgEventDirector.StopLoop(gasp);
        EcgEventDirector.StopLoop(calm);
        mask.Destroy();

        if (saved)
        {
            if (d.Monitor != null)
                d.Monitor.Panic(6f, 100f);
            hud.Say("O paciente voltou a respirar! Termine o eletrocardiograma.", 6f);
        }
        else
        {
            hud.Say("O tempo acabou. O paciente parou de respirar.", 6f);
        }

        finish(saved);
    }

    // ---------------------------------------------------------------- a mascara (ambu)

    // Mascara + valvula + bolsa. Na mao fica na frente da camera; segurando Z desce ate o rosto
    // e a bolsa e apertada no ritmo da ventilacao.
    class BagValveMask
    {
        readonly EcgEventDirector d;
        readonly Transform root;
        Transform bag;
        readonly Material[] materials = new Material[0];
        float toFace;

        public bool OnFace => toFace > 0.9f;

        public BagValveMask(EcgEventDirector director)
        {
            d = director;
            root = EcgEventDirector.LoadProp("Events/BagValveMask");
            if (root == null)
            {
                materials = new[]
                {
                    d.MakeMaterial(new Color(0.75f, 0.88f, 0.95f), false),  // mascara
                    d.MakeMaterial(new Color(0.2f, 0.22f, 0.25f), false),   // valvula
                    d.MakeMaterial(new Color(0.12f, 0.45f, 0.75f), false),  // bolsa
                    d.MakeMaterial(new Color(0.95f, 0.95f, 0.95f), false),  // aro
                };
                root = Build();
            }
            else
            {
                bag = root.Find("Bag");
            }
            Follow(false, 1f);
        }

        // Origem = borda da mascara, que encosta no rosto. A abertura aponta para -Y e a bolsa para +Z.
        Transform Build()
        {
            var r = new GameObject("BagValveMask").transform;
            EcgEventDirector.Part(PrimitiveType.Cylinder, r, new Vector3(0f, 0.006f, 0f), new Vector3(0.1f, 0.006f, 0.12f), materials[3]);
            EcgEventDirector.Part(PrimitiveType.Sphere, r, new Vector3(0f, 0.022f, 0f), new Vector3(0.09f, 0.04f, 0.11f), materials[0]);
            EcgEventDirector.Part(PrimitiveType.Cylinder, r, new Vector3(0f, 0.052f, 0f), new Vector3(0.026f, 0.014f, 0.026f), materials[1]);
            EcgEventDirector.Part(PrimitiveType.Cylinder, r, new Vector3(0f, 0.068f, 0.03f), new Vector3(0.024f, 0.035f, 0.024f), materials[1], new Vector3(90f, 0f, 0f));
            var bagRoot = new GameObject("Bag").transform;
            bagRoot.SetParent(r, false);
            bagRoot.localPosition = new Vector3(0f, 0.068f, 0.16f);
            EcgEventDirector.Part(PrimitiveType.Capsule, bagRoot, Vector3.zero, new Vector3(0.085f, 0.1f, 0.085f), materials[2], new Vector3(90f, 0f, 0f));
            EcgEventDirector.Part(PrimitiveType.Cylinder, bagRoot, new Vector3(0f, 0f, 0.105f), new Vector3(0.03f, 0.012f, 0.03f), materials[1], new Vector3(90f, 0f, 0f));
            bag = bagRoot;
            return r;
        }

        public void Follow(bool applying, float dt)
        {
            toFace = Mathf.MoveTowards(toFace, applying ? 1f : 0f, dt * 3.5f);
            float k = Mathf.SmoothStep(0f, 1f, toFace);

            Transform cam = d.View.transform;
            float sway = Mathf.Sin(Time.time * 1.7f) * 0.006f;
            Vector3 held = cam.TransformPoint(new Vector3(0.06f, -0.18f + sway, 0.45f));
            Quaternion heldRotation = cam.rotation * Quaternion.Euler(-90f, 0f, 0f);

            Vector3 face = d.FacePoint();
            Quaternion onFace = Quaternion.LookRotation(d.FlatForward(), Vector3.up);

            EcgEventDirector.Glide(root, Vector3.Lerp(held, face, k), Quaternion.Slerp(heldRotation, onFace, k), dt);

            // A bolsa murcha e enche enquanto a mascara esta no rosto.
            if (bag != null)
            {
                float squeeze = OnFace ? 0.5f + 0.5f * Mathf.Sin(Time.time * 4f) : 0f;
                bag.localScale = new Vector3(1f - 0.35f * squeeze, 1f - 0.35f * squeeze, 1f);
            }
        }

        public void Destroy()
        {
            if (root != null)
                Object.Destroy(root.gameObject);
            foreach (var material in materials)
                Object.Destroy(material);
        }
    }
}
