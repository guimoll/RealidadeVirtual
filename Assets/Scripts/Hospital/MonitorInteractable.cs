using UnityEngine;

// Monitor cardiaco: ao interagir a camera se aproxima da tela (modo foco) e um painel mostra
// a frequencia e o andamento do exame. E ou Esc voltam.
public class MonitorInteractable : Interactable
{
    [SerializeField] Transform viewpoint;
    [SerializeField] VitalMonitor monitor;

    EcgExam exam;
    PlayerInteractor active;

    void Start()
    {
        exam = FindFirstObjectByType<EcgExam>();
    }

    public override void Interact(PlayerInteractor player)
    {
        if (player.Player == null || viewpoint == null)
            return;

        active = player;
        player.Player.FocusOn(viewpoint);
    }

    void Update()
    {
        if (active == null)
            return;

        if (!active.Player.Frozen)
        {
            active.PanelText = "";
            active = null;
            return;
        }

        active.PanelText = BuildPanel();
    }

    string BuildPanel()
    {
        string rate = monitor != null && monitor.HasSignal ? Mathf.RoundToInt(monitor.CurrentBpm) + " bpm" : "--";

        string status = "";
        if (exam != null)
        {
            if (exam.Completed)
                status = "EXAME CONCLUÍDO  -  RITMO SINUSAL";
            else if (exam.Progress > 0.001f)
                status = "EXAME EM ANDAMENTO  " + Mathf.RoundToInt(exam.Progress * 100f) + "%";
            else if (exam.Holding)
                status = "SEM SINAL  -  encoste o aparelho no peito do paciente";
            else
                status = "SEM SINAL  -  pegue o eletrocardiógrafo na mesa";
        }

        return "MONITOR CARDÍACO\nFC: " + rate + "\n" + status;
    }
}
