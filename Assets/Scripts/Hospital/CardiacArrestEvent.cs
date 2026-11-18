using System.Collections;
using UnityEngine;

// Evento: o paciente entra em parada cardiaca no meio do eletrocardiograma.
// O monitor faz flatline (linha reta + alarme continuo), o aparelho na mao vira um desfibrilador
// e o jogador tem 1 minuto para dar 3 choques (100, 150 e 200 volts) apertando Z repetidamente.
// Cada choque enche uma barra azul que esvazia sozinha, cada vez mais rapido.
public class CardiacArrestEvent : EcgEvent
{
    static readonly int[] Volts = { 100, 150, 200 };
    // Quanto cada aperto de Z enche a barra, e quanto ela esvazia por segundo.
    static readonly float[] GainPerPress = { 0.10f, 0.085f, 0.075f };
    static readonly float[] DecayPerSecond = { 0.22f, 0.32f, 0.42f };

    static AudioClip flatline;
    static AudioClip shock;

    public override string Name => "Parada cardíaca";

    public override IEnumerator Run(EcgEventDirector d, System.Action<bool> finish)
    {
        var hud = d.Interactor;
        hud.SetObjective("EMERGÊNCIA: PARADA CARDÍACA\nDê os choques com o desfibrilador antes que o tempo acabe");
        hud.Say("O paciente entrou em parada cardíaca!", 4f);

        if (flatline == null)
            flatline = EcgEventDirector.LoadClip("Events/Flatline", EcgEventDirector.MakeFlatline);
        if (shock == null)
            shock = EcgEventDirector.LoadClip("Events/Shock", EcgEventDirector.MakeShock);

        AudioSource alarm = d.StartLoop(flatline, 0.4f);
        var paddles = new Paddles(d);
        d.StartCoroutine(d.LockView(d.ChestPoint()));

        float timeLeft = EcgEventDirector.EventSeconds;
        bool saved = false;

        for (int round = 0; round < Volts.Length; round++)
        {
            string title = Volts[round] + " VOLTS";
            float charge = 0f;

            // Carregando: Z enche, o tempo esvazia.
            while (timeLeft > 0f && charge < 1f)
            {
                float dt = Time.deltaTime;
                timeLeft -= dt;
                if (EcgEventDirector.PressedZ())
                    charge += GainPerPress[round];
                charge = Mathf.Clamp01(charge - DecayPerSecond[round] * dt);

                d.ShowTimer(timeLeft);
                d.ShowBar(charge, title, "Aperte [Z] para usar o desfibrilador");
                paddles.Follow(0f, dt);
                yield return null;
            }

            if (timeLeft <= 0f)
                break;

            // Choque: as pas descem ate o peito, faiscam, o corpo da um tranco e elas voltam.
            d.ShowBar(1f, title, "CHOQUE!");
            bool fired = false;
            for (float elapsed = 0f; elapsed < 0.9f; elapsed += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                timeLeft -= dt;
                d.ShowTimer(timeLeft);

                // 0 a 0.25 s desce, segura ate 0.55 s, depois volta.
                float toChest = elapsed < 0.25f ? elapsed / 0.25f : elapsed < 0.55f ? 1f : 1f - (elapsed - 0.55f) / 0.35f;
                paddles.Follow(Mathf.Clamp01(toChest), dt);

                if (!fired && elapsed >= 0.25f)
                {
                    fired = true;
                    Vector3 chest = d.ChestPoint();
                    d.Sparks(chest);
                    AudioSource.PlayClipAtPoint(shock, chest, 1f);
                    d.Jolt();
                }
                yield return null;
            }

            if (round < Volts.Length - 1)
                hud.Say("Choque de " + Volts[round] + " volts. Ainda sem ritmo! Aumente para " + Volts[round + 1] + " volts.", 3.5f);
            else
                saved = true;
        }

        EcgEventDirector.StopLoop(alarm);
        paddles.Destroy();

        if (saved)
        {
            if (d.Monitor != null)
                d.Monitor.Panic(6f, 112f);
            hud.Say("O coração voltou a bater! Termine o eletrocardiograma.", 6f);
        }
        else
        {
            hud.Say("O tempo acabou. O paciente não resistiu.", 6f);
        }

        finish(saved);
    }

    // ---------------------------------------------------------------- as pas do desfibrilador

    // Duas pas na frente da camera. Follow(0) = na mao; Follow(1) = apoiadas no peito do paciente.
    class Paddles
    {
        readonly EcgEventDirector d;
        readonly Transform left;
        readonly Transform right;
        readonly Material[] materials = new Material[0];

        public Paddles(EcgEventDirector director)
        {
            d = director;

            // Modelo pronto em Resources/Events/Defibrillator: uma copia em cada mao.
            left = EcgEventDirector.LoadProp("Events/Defibrillator");
            if (left != null)
            {
                right = EcgEventDirector.LoadProp("Events/Defibrillator");
            }
            else
            {
                materials = new[]
                {
                    d.MakeMaterial(new Color(0.16f, 0.17f, 0.19f), false),  // corpo
                    d.MakeMaterial(new Color(0.75f, 0.77f, 0.8f), false),   // placa de metal
                    d.MakeMaterial(new Color(1f, 0.55f, 0.1f), true),       // botao
                    d.MakeMaterial(new Color(0.85f, 0.75f, 0.2f), false),   // faixa amarela
                };
                left = Build("PaddleApex");
                right = Build("PaddleSternum");
            }

            Follow(0f, 1f);
        }

        // Origem = centro da placa de metal, que e o que encosta no peito. A placa aponta para -Y.
        Transform Build(string name)
        {
            var root = new GameObject(name).transform;
            EcgEventDirector.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.003f, 0f), new Vector3(0.08f, 0.006f, 0.11f), materials[1]);
            EcgEventDirector.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.022f, 0f), new Vector3(0.086f, 0.032f, 0.116f), materials[0]);
            EcgEventDirector.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.039f, 0f), new Vector3(0.088f, 0.004f, 0.118f), materials[3]);
            EcgEventDirector.Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.075f, 0.01f), new Vector3(0.034f, 0.035f, 0.034f), materials[0]);
            EcgEventDirector.Part(PrimitiveType.Cube, root, new Vector3(0f, 0.112f, 0.01f), new Vector3(0.07f, 0.02f, 0.04f), materials[0]);
            EcgEventDirector.Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.124f, 0.01f), new Vector3(0.016f, 0.004f, 0.016f), materials[2]);
            return root;
        }

        public void Follow(float toChest, float dt)
        {
            Transform cam = d.View.transform;
            float k = Mathf.SmoothStep(0f, 1f, toChest);

            // Na mao: uma de cada lado, placas viradas para frente, com um leve balanco.
            float sway = Mathf.Sin(Time.time * 1.7f) * 0.006f;
            Quaternion held = cam.rotation * Quaternion.Euler(-90f, 0f, 0f);
            Vector3 heldLeft = cam.TransformPoint(new Vector3(-0.2f, -0.2f + sway, 0.48f));
            Vector3 heldRight = cam.TransformPoint(new Vector3(0.2f, -0.2f - sway, 0.48f));

            // No peito: placas para baixo, uma de cada lado do esterno, com tremida no contato.
            Vector3 chest = d.ChestPoint() + Vector3.up * 0.004f;
            Vector3 flat = d.FlatForward();
            Vector3 side = Vector3.Cross(Vector3.up, flat);
            Quaternion onChest = Quaternion.LookRotation(flat, Vector3.up);
            Vector3 shake = toChest > 0.95f ? Random.insideUnitSphere * 0.004f : Vector3.zero;

            EcgEventDirector.Glide(left, Vector3.Lerp(heldLeft, chest - side * 0.1f + flat * 0.05f + shake, k), Quaternion.Slerp(held, onChest, k), dt);
            EcgEventDirector.Glide(right, Vector3.Lerp(heldRight, chest + side * 0.1f - flat * 0.05f + shake, k), Quaternion.Slerp(held, onChest, k), dt);
        }

        public void Destroy()
        {
            if (left != null)
                Object.Destroy(left.gameObject);
            if (right != null)
                Object.Destroy(right.gameObject);
            foreach (var material in materials)
                Object.Destroy(material);
        }
    }
}
