using System.Collections;
using UnityEngine;

// Evento: ressuscitacao manual (massagem cardiaca). O monitor comeca em flatline e o aparelho na mao
// vira as maos do jogador sobre o peito do paciente.
// Quick time event: uma tecla Z vem correndo na horizontal ate a bolinha do meio da tela; apertar Z
// quando ela esta dentro da bolinha e um acerto (check verde) e as maos comprimem o peito.
// Apertar fora da hora ou deixar passar e um erro (X vermelho). Sao precisos 10 acertos em 1 minuto.
// A cada acerto o batimento no monitor volta um pouco mais rapido.
public class ManualResuscitationEvent : EcgEvent
{
    const int HitsNeeded = 10;
    // Onde a tecla Z nasce (pixels de referencia a direita do centro) e a tolerancia do acerto.
    const float StartX = 560f;
    const float HitWindow = 40f;
    const float BaseSpeed = 380f;
    const float SpeedPerHit = 14f;
    const float RespawnDelay = 0.35f;
    const float CompressSeconds = 0.28f;

    static AudioClip flatline;
    static AudioClip compression;

    public override string Name => "Ressuscitação manual";

    public override IEnumerator Run(EcgEventDirector d, System.Action<bool> finish)
    {
        var hud = d.Interactor;
        hud.SetObjective("EMERGÊNCIA: RESSUSCITAÇÃO MANUAL\nFaça " + HitsNeeded + " compressões no peito no ritmo certo");
        hud.Say("O coração parou! Aperte [Z] quando a tecla chegar na bolinha.", 4.5f);

        if (flatline == null)
            flatline = EcgEventDirector.LoadClip("Events/Flatline", EcgEventDirector.MakeFlatline);
        if (compression == null)
            compression = EcgEventDirector.LoadClip("Events/Compression", EcgEventDirector.MakeCompression);

        AudioSource alarm = d.StartLoop(flatline, 0.4f);
        var hands = new Hands(d);
        d.StartCoroutine(d.LockView(d.ChestPoint()));

        float timeLeft = EcgEventDirector.EventSeconds;
        int hits = 0;
        float noteX = StartX;
        bool noteVisible = true;
        float respawnAt = 0f;
        float compressStart = -10f;
        bool thumped = true;

        while (timeLeft > 0f && hits < HitsNeeded)
        {
            float dt = Time.deltaTime;
            timeLeft -= dt;

            if (noteVisible)
            {
                noteX -= (BaseSpeed + SpeedPerHit * hits) * dt;

                bool hit = false, miss = false;
                if (EcgEventDirector.PressedZ())
                {
                    if (Mathf.Abs(noteX) <= HitWindow)
                        hit = true;
                    else
                        miss = true;    // apertou cedo demais (ou tarde)
                }
                else if (noteX < -HitWindow)
                {
                    miss = true;        // deixou passar
                }

                if (hit || miss)
                {
                    d.FlashMark(hit);
                    noteVisible = false;
                    respawnAt = Time.time + RespawnDelay;
                    if (hit)
                    {
                        hits++;
                        compressStart = Time.time;
                        thumped = false;
                        // O primeiro acerto ja tira o monitor da linha reta.
                        if (alarm != null)
                        {
                            EcgEventDirector.StopLoop(alarm);
                            alarm = null;
                        }
                    }
                }
            }
            else if (Time.time >= respawnAt)
            {
                noteX = StartX;
                noteVisible = true;
            }

            // Compressao: as maos descem ate o peito, fazem o baque e sobem.
            float press = Mathf.Clamp01((Time.time - compressStart) / CompressSeconds);
            float depth = press < 1f ? Mathf.Sin(press * Mathf.PI) : 0f;
            if (!thumped && press >= 0.4f)
            {
                thumped = true;
                AudioSource.PlayClipAtPoint(compression, d.ChestPoint(), 1f);
                d.Jolt(0.012f);
            }
            hands.Follow(depth, dt);

            // Batimento volta aos poucos: de nada (flatline) ate ~75 bpm no ultimo acerto.
            if (hits > 0)
                d.KeepHeartRate(Mathf.Lerp(18f, 75f, hits / (float)HitsNeeded));

            d.ShowTimer(timeLeft);
            d.ShowQte(noteX, noteVisible, "COMPRESSÕES  " + hits + " / " + HitsNeeded);
            yield return null;
        }

        bool saved = hits >= HitsNeeded;

        // Deixa a ultima compressao terminar antes de tirar as maos.
        for (float t = 0f; t < CompressSeconds; t += Time.deltaTime)
        {
            float press = Mathf.Clamp01((Time.time - compressStart) / CompressSeconds);
            if (!thumped && press >= 0.4f)
            {
                thumped = true;
                AudioSource.PlayClipAtPoint(compression, d.ChestPoint(), 1f);
            }
            hands.Follow(press < 1f ? Mathf.Sin(press * Mathf.PI) : 0f, Time.deltaTime);
            yield return null;
        }

        EcgEventDirector.StopLoop(alarm);
        hands.Destroy();

        if (saved)
        {
            if (d.Monitor != null)
                d.Monitor.Panic(6f, 88f);
            hud.Say("O coração voltou a bater! Termine o eletrocardiograma.", 6f);
        }
        else
        {
            hud.Say("O tempo acabou. O paciente não resistiu.", 6f);
        }

        finish(saved);
    }

    // ---------------------------------------------------------------- as maos

    // Duas maos espalmadas, uma sobre a outra, com os antebracos subindo em direcao ao jogador.
    // Follow(0) = paradas logo acima do peito; Follow(1) = afundando o peito.
    class Hands
    {
        readonly EcgEventDirector d;
        readonly Transform root;
        readonly Material[] materials = new Material[0];

        public Hands(EcgEventDirector director)
        {
            d = director;
            root = EcgEventDirector.LoadProp("Events/Hands");
            if (root == null)
            {
                materials = new[]
                {
                    d.MakeMaterial(new Color(0.86f, 0.66f, 0.52f), false),  // pele
                    d.MakeMaterial(new Color(0.22f, 0.5f, 0.55f), false),   // manga do jaleco
                };
                root = new GameObject("CprHands").transform;
                BuildHand(root, new Vector3(0f, 0f, 0f), -8f);
                BuildHand(root, new Vector3(0.004f, 0.024f, 0.006f), 14f);
            }

            // Comeca na frente da camera e desliza ate o peito.
            Transform cam = d.View.transform;
            root.SetPositionAndRotation(cam.TransformPoint(new Vector3(0f, -0.2f, 0.4f)), Quaternion.LookRotation(d.FlatForward(), Vector3.up));
        }

        // Origem = base da palma. Dedos para +Z, antebraco subindo para tras (-Z), em direcao a camera.
        void BuildHand(Transform parent, Vector3 offset, float yaw)
        {
            var hand = new GameObject("Hand").transform;
            hand.SetParent(parent, false);
            hand.localPosition = offset;
            hand.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Material skin = materials[0];
            EcgEventDirector.Part(PrimitiveType.Cube, hand, new Vector3(0f, 0.012f, 0.02f), new Vector3(0.085f, 0.024f, 0.095f), skin);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.03f + i * 0.02f;
                float length = i == 0 || i == 3 ? 0.055f : 0.07f;
                EcgEventDirector.Part(PrimitiveType.Cube, hand, new Vector3(x, 0.01f, 0.068f + length * 0.5f), new Vector3(0.017f, 0.018f, length), skin);
            }
            EcgEventDirector.Part(PrimitiveType.Cube, hand, new Vector3(0.05f, 0.012f, 0.03f), new Vector3(0.02f, 0.02f, 0.06f), skin, new Vector3(0f, -35f, 0f));
            // Pulso e antebraco (cilindro inclinado para cima e para tras).
            EcgEventDirector.Part(PrimitiveType.Cylinder, hand, new Vector3(0f, 0.022f, -0.04f), new Vector3(0.055f, 0.02f, 0.05f), skin, new Vector3(-53f, 0f, 0f));
            EcgEventDirector.Part(PrimitiveType.Cylinder, hand, new Vector3(0f, 0.11f, -0.16f), new Vector3(0.065f, 0.15f, 0.065f), materials[1], new Vector3(-53f, 0f, 0f));
        }

        public void Follow(float depth, float dt)
        {
            Vector3 chest = d.ChestPoint();
            Quaternion rotation = Quaternion.LookRotation(d.FlatForward(), Vector3.up);
            Vector3 rest = chest + Vector3.up * 0.1f;
            Vector3 pressed = chest - Vector3.up * 0.025f;

            if (depth > 0f)
                root.SetPositionAndRotation(Vector3.Lerp(rest, pressed, depth), rotation);
            else
                EcgEventDirector.Glide(root, rest, rotation, dt, 12f);
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
