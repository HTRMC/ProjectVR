using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TaskScreenController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform tvObject;
    [SerializeField] TextMeshProUGUI laptopTitle;
    [SerializeField] TextMeshProUGUI laptopInstructions;
    [SerializeField] TextMeshProUGUI tvTitle;
    [SerializeField] TextMeshProUGUI tvInstructions;

    [Header("TV Popup")]
    [SerializeField] Vector3 tvHiddenLocalPos = new Vector3(0f, 0.12f, 0f);
    [SerializeField] Vector3 tvShownLocalPos = new Vector3(0f, 0.85f, 0f);
    [SerializeField] Vector3 tvShownLocalRot = new Vector3(0f, 0f, 0f);
    [SerializeField] Vector3 tvShownLocalScale = new Vector3(2f, 1f, 1f);
    [SerializeField] float tvPopDuration = 0.5f;

    [Header("Scoring")]
    [SerializeField] int basePoints = 100;
    [SerializeField] float fastBonusTime = 30f;
    [SerializeField] int fastBonusPoints = 50;
    [SerializeField] int streakBonus = 25;
    [SerializeField] int urgentMultiplier = 2;

    [System.Serializable]
    public class CableTask
    {
        public string cableColor;
        public Color displayColor;
        public string sideA;
        public int serverA;
        public int portA;
        public string sideB;
        public int serverB;
        public int portB;

        [System.NonSerialized] public ServerSocket socketARef;
        [System.NonSerialized] public ServerSocket socketBRef;
        [System.NonSerialized] public TextMeshProUGUI labelARef;
        [System.NonSerialized] public TextMeshProUGUI labelBRef;
        [System.NonSerialized] public bool aConnected;
        [System.NonSerialized] public bool bConnected;
        [System.NonSerialized] public bool isUrgent;

        public string LabelA => $"{sideA}-S{serverA}-P{portA}";
        public string LabelB => $"{sideB}-S{serverB}-P{portB}";
        public bool IsComplete => aConnected && bConnected;
    }

    List<CableTask> allTasks = new List<CableTask>();
    Dictionary<ServerSocket, CableTask> socketLookup = new Dictionary<ServerSocket, CableTask>();

    int currentTaskIndex = -1;
    float taskStartTime;
    float totalStartTime;
    int totalScore;
    int currentStreak;
    bool tvShown;
    bool animating;
    bool gameStarted;
    bool gameComplete;

    XRBaseInteractable laptopInteractable;

    void Awake()
    {
        laptopInteractable = GetComponentInChildren<XRBaseInteractable>();
        if (laptopInteractable != null)
            laptopInteractable.selectEntered.AddListener(OnLaptopInteract);

        if (tvObject != null)
        {
            tvObject.SetParent(transform, true);
            tvObject.localPosition = tvHiddenLocalPos;
            tvObject.localScale = Vector3.zero;
        }
    }

    void Start()
    {
        DefineTasks();
        AssignSocketColors();
        FindSocketLabels();
        SubscribeToSockets();
        ShowIdleScreen();
    }

    void OnDestroy()
    {
        if (laptopInteractable != null)
            laptopInteractable.selectEntered.RemoveListener(OnLaptopInteract);

        foreach (var task in allTasks)
        {
            if (task.socketARef != null)
            {
                task.socketARef.plugConnected.RemoveListener(OnPlugChanged);
                task.socketARef.plugDisconnected.RemoveListener(OnPlugChanged);
            }
            if (task.socketBRef != null)
            {
                task.socketBRef.plugConnected.RemoveListener(OnPlugChanged);
                task.socketBRef.plugDisconnected.RemoveListener(OnPlugChanged);
            }
        }
    }

    void Update()
    {
        if (gameStarted && !gameComplete && currentTaskIndex >= 0 && currentTaskIndex < allTasks.Count)
            UpdateTimerDisplay();
    }

    // ─── Task Definitions (ordered by difficulty: nearby racks first) ───

    void DefineTasks()
    {
        // Easy: same rack side
        allTasks.Add(new CableTask { cableColor = "Green",  displayColor = Color.green,                        sideA = "A3", serverA = 1, portA = 3, sideB = "B1", serverB = 4, portB = 2 });
        allTasks.Add(new CableTask { cableColor = "Red",    displayColor = Color.red,                          sideA = "A1", serverA = 3, portA = 2, sideB = "B2", serverB = 1, portB = 1 });
        allTasks.Add(new CableTask { cableColor = "Yellow", displayColor = Color.yellow,                       sideA = "A1", serverA = 7, portA = 1, sideB = "B3", serverB = 2, portB = 3 });
        // Medium: crossing racks
        allTasks.Add(new CableTask { cableColor = "White",  displayColor = Color.white,                        sideA = "A2", serverA = 4, portA = 3, sideB = "B1", serverB = 9, portB = 1, isUrgent = true });
        allTasks.Add(new CableTask { cableColor = "Orange", displayColor = new Color(1f, 0.65f, 0f),           sideA = "A5", serverA = 9, portA = 1, sideB = "B2", serverB = 6, portB = 2 });
        allTasks.Add(new CableTask { cableColor = "Blue",   displayColor = new Color(0.26f, 0.26f, 1f),        sideA = "A2", serverA = 5, portA = 1, sideB = "B4", serverB = 7, portB = 3 });
        // Hard: far racks + urgent
        allTasks.Add(new CableTask { cableColor = "Cyan",   displayColor = Color.cyan,                         sideA = "A3", serverA = 6, portA = 2, sideB = "B4", serverB = 3, portB = 3, isUrgent = true });
        allTasks.Add(new CableTask { cableColor = "Purple", displayColor = new Color(0.6f, 0.2f, 0.93f),       sideA = "A4", serverA = 2, portA = 2, sideB = "B5", serverB = 8, portB = 1 });
        allTasks.Add(new CableTask { cableColor = "Pink",   displayColor = new Color(1f, 0.41f, 0.71f),        sideA = "A5", serverA = 1, portA = 1, sideB = "B3", serverB = 5, portB = 2 });
        allTasks.Add(new CableTask { cableColor = "Black",  displayColor = new Color(0.3f, 0.3f, 0.3f),        sideA = "A4", serverA = 8, portA = 3, sideB = "B5", serverB = 2, portB = 1, isUrgent = true });
    }

    // ─── Socket Setup ───

    ServerSocket FindSocket(string side, int server, int port)
    {
        string rackSide = side.StartsWith("A") ? "Side_A" : "Side_B";
        string rackName = "Rack_" + side;
        string socketName = $"CableSocket{server}_{port}";

        var rack = GameObject.Find($"Servers/{rackSide}/{rackName}");
        if (rack == null) return null;

        var socketTf = rack.transform.Find(socketName);
        if (socketTf == null)
        {
            // Also check nested under Server objects
            for (int i = 0; i < rack.transform.childCount; i++)
            {
                var child = rack.transform.GetChild(i);
                var nested = child.Find(socketName);
                if (nested != null) return nested.GetComponent<ServerSocket>();
            }
            return null;
        }

        return socketTf.GetComponent<ServerSocket>();
    }

    void AssignSocketColors()
    {
        foreach (var task in allTasks)
        {
            task.socketARef = FindSocket(task.sideA, task.serverA, task.portA);
            task.socketBRef = FindSocket(task.sideB, task.serverB, task.portB);

            if (task.socketARef != null)
            {
                task.socketARef.SetColorID(task.cableColor);
                socketLookup[task.socketARef] = task;
            }
            if (task.socketBRef != null)
            {
                task.socketBRef.SetColorID(task.cableColor);
                socketLookup[task.socketBRef] = task;
            }
        }
    }

    void FindSocketLabels()
    {
        foreach (var task in allTasks)
        {
            task.labelARef = FindLabel(task.LabelA);
            task.labelBRef = FindLabel(task.LabelB);
        }
    }

    TextMeshProUGUI FindLabel(string labelName)
    {
        var labelObj = GameObject.Find("SocketLabel_" + labelName);
        if (labelObj != null)
            return labelObj.GetComponentInChildren<TextMeshProUGUI>();
        return null;
    }

    void SubscribeToSockets()
    {
        foreach (var task in allTasks)
        {
            if (task.socketARef != null)
            {
                task.socketARef.plugConnected.AddListener(OnPlugChanged);
                task.socketARef.plugDisconnected.AddListener(OnPlugChanged);
            }
            if (task.socketBRef != null)
            {
                task.socketBRef.plugConnected.AddListener(OnPlugChanged);
                task.socketBRef.plugDisconnected.AddListener(OnPlugChanged);
            }
        }
    }

    // ─── Laptop Interaction & TV Animation ───

    void OnLaptopInteract(SelectEnterEventArgs args)
    {
        if (animating) return;

        if (!tvShown)
        {
            StartCoroutine(AnimateTV(true));
            if (!gameStarted)
            {
                gameStarted = true;
                totalStartTime = Time.time;
                StartCoroutine(ShowNextTaskSequence());
            }
        }
        else
        {
            StartCoroutine(AnimateTV(false));
        }
    }

    IEnumerator AnimateTV(bool show)
    {
        if (tvObject == null) yield break;
        animating = true;
        tvShown = show;

        Vector3 startPos = tvObject.localPosition;
        Vector3 endPos = show ? tvShownLocalPos : tvHiddenLocalPos;
        Vector3 startScale = tvObject.localScale;
        Vector3 endScale = show ? tvShownLocalScale : Vector3.zero;
        Quaternion startRot = tvObject.localRotation;
        Quaternion endRot = show ? Quaternion.Euler(tvShownLocalRot) : Quaternion.identity;

        float elapsed = 0f;
        while (elapsed < tvPopDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tvPopDuration;

            // Overshoot ease for pop effect
            float ease;
            if (show)
                ease = 1f + 1.2f * Mathf.Sin(t * Mathf.PI) * (1f - t); // bounce pop
            else
                ease = 1f - t; // simple ease out for hide

            float lerp = show ? EaseOutBack(t) : EaseInBack(t);
            tvObject.localPosition = Vector3.Lerp(startPos, endPos, lerp);
            tvObject.localScale = Vector3.Lerp(startScale, endScale, lerp);
            tvObject.localRotation = Quaternion.Slerp(startRot, endRot, lerp);
            yield return null;
        }

        tvObject.localPosition = endPos;
        tvObject.localScale = endScale;
        tvObject.localRotation = endRot;
        animating = false;
    }

    float EaseOutBack(float t)
    {
        float c = 1.70158f;
        float c3 = c + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
    }

    float EaseInBack(float t)
    {
        float c = 1.70158f;
        float c3 = c + 1f;
        return c3 * t * t * t - c * t * t;
    }

    // ─── Game Loop ───

    void ShowIdleScreen()
    {
        string title = "SERVER ROOM MGMT";
        string body = "<size=70%>Click the laptop to start your shift.\n\n" +
            "You'll receive trouble tickets\none at a time.\n\n" +
            "Connect the correct cable\nto the right ports.\n\n" +
            "<color=#AAAAAA>Faster = More Points\n" +
            "Streaks = Bonus Multiplier\n" +
            "Urgent tickets = Double Points</color></size>";

        SetScreenText(title, body);
    }

    IEnumerator ShowNextTaskSequence()
    {
        yield return new WaitForSeconds(0.8f);

        currentTaskIndex = 0;
        ShowCurrentTask();
    }

    void ShowCurrentTask()
    {
        if (currentTaskIndex >= allTasks.Count)
        {
            ShowGameComplete();
            return;
        }

        var task = allTasks[currentTaskIndex];
        taskStartTime = Time.time;

        // Highlight the active socket labels
        UpdateSocketLabels();

        StartCoroutine(TicketIncomingAnimation(task));
    }

    IEnumerator TicketIncomingAnimation(CableTask task)
    {
        string urgentTag = task.isUrgent ? "<color=red><b>!! URGENT !!</b></color>\n" : "";
        string ticketNum = $"TICKET #{currentTaskIndex + 1}/{allTasks.Count}";

        // Flash "INCOMING" for dramatic effect
        SetScreenText("INCOMING", $"\n\n<size=120%><b>{ticketNum}</b></size>");
        yield return new WaitForSeconds(0.3f);
        SetScreenText("INCOMING", "");
        yield return new WaitForSeconds(0.15f);
        SetScreenText("INCOMING", $"\n\n<size=120%><b>{ticketNum}</b></size>");
        yield return new WaitForSeconds(0.3f);

        // Show the actual task
        UpdateTaskDisplay(task);
    }

    void UpdateTaskDisplay(CableTask task)
    {
        string urgentTag = task.isUrgent ? "<color=red><b>!! URGENT !!</b></color>\n" : "";
        string hex = ColorUtility.ToHtmlStringRGB(task.displayColor);
        string ticketHeader = $"TICKET #{currentTaskIndex + 1}/{allTasks.Count}";

        // Progress bar
        int done = currentTaskIndex;
        string progressBar = "<size=60%>[";
        for (int i = 0; i < allTasks.Count; i++)
            progressBar += i < done ? "<color=green>\u2588</color>" : "<color=#333333>\u2588</color>";
        progressBar += "]</size>";

        float elapsed = Time.time - taskStartTime;
        string timer = $"<size=60%>{elapsed:F0}s</size>";

        string scoreText = $"<size=60%>Score: {totalScore}  |  Streak: x{currentStreak}</size>";

        string body = $"{urgentTag}" +
            $"{progressBar}\n\n" +
            $"<size=80%>Connect the <color=#{hex}><b>{task.cableColor}</b></color> cable:</size>\n\n" +
            $"<size=110%><b><color=#{hex}>{task.LabelA}</color></b></size>\n" +
            $"<size=80%>\u25BC</size>\n" +
            $"<size=110%><b><color=#{hex}>{task.LabelB}</color></b></size>\n\n" +
            $"{scoreText}";

        string statusA = task.aConnected ? "<color=green>\u2713</color>" : "<color=red>\u2717</color>";
        string statusB = task.bConnected ? "<color=green>\u2713</color>" : "<color=red>\u2717</color>";
        body += $"\n\n<size=70%>Side A: {statusA}  |  Side B: {statusB}</size>";

        SetScreenText(ticketHeader, body);
    }

    void UpdateTimerDisplay()
    {
        if (currentTaskIndex < 0 || currentTaskIndex >= allTasks.Count) return;
        // Refresh the display to update timer - only every 1 second to avoid spam
        var task = allTasks[currentTaskIndex];
        UpdateTaskDisplay(task);
    }

    void OnPlugChanged(CablePlug plug)
    {
        // Update connection state for ALL tasks (not just current)
        foreach (var task in allTasks)
        {
            task.aConnected = task.socketARef != null && task.socketARef.IsConnected
                && task.socketARef.ConnectedPlug != null
                && task.socketARef.ConnectedPlug.ColorID == task.cableColor;

            task.bConnected = task.socketBRef != null && task.socketBRef.IsConnected
                && task.socketBRef.ConnectedPlug != null
                && task.socketBRef.ConnectedPlug.ColorID == task.cableColor;
        }

        UpdateSocketLabels();

        if (!gameStarted || gameComplete) return;
        if (currentTaskIndex < 0 || currentTaskIndex >= allTasks.Count) return;

        var current = allTasks[currentTaskIndex];
        UpdateTaskDisplay(current);

        if (current.IsComplete)
            StartCoroutine(TaskCompletedSequence());
    }

    IEnumerator TaskCompletedSequence()
    {
        float elapsed = Time.time - taskStartTime;
        int points = basePoints;
        if (elapsed <= fastBonusTime)
            points += fastBonusPoints;

        currentStreak++;
        points += streakBonus * (currentStreak - 1);

        var task = allTasks[currentTaskIndex];
        if (task.isUrgent)
            points *= urgentMultiplier;

        totalScore += points;

        string urgentBonus = task.isUrgent ? "\n<color=red><b>URGENT BONUS x2!</b></color>" : "";
        string streakText = currentStreak > 1 ? $"\n<color=yellow>STREAK x{currentStreak}!</color>" : "";

        SetScreenText("<color=green>RESOLVED!</color>",
            $"\n\n<size=120%><b>+{points} pts</b></size>" +
            $"\n<size=70%>Time: {elapsed:F1}s</size>" +
            $"{urgentBonus}{streakText}" +
            $"\n\n<size=80%>Total: {totalScore}</size>");

        yield return new WaitForSeconds(2f);

        currentTaskIndex++;
        ShowCurrentTask();
    }

    void ShowGameComplete()
    {
        gameComplete = true;
        float totalTime = Time.time - totalStartTime;
        int minutes = (int)(totalTime / 60f);
        int seconds = (int)(totalTime % 60f);

        string grade;
        if (totalScore >= 1500) grade = "<color=yellow><b>S RANK</b></color>";
        else if (totalScore >= 1200) grade = "<color=green><b>A RANK</b></color>";
        else if (totalScore >= 900) grade = "<color=#4488FF><b>B RANK</b></color>";
        else grade = "<color=white><b>C RANK</b></color>";

        string body = $"\n<size=130%>{grade}</size>\n\n" +
            $"<size=90%>Final Score: <b>{totalScore}</b></size>\n" +
            $"<size=70%>Total Time: {minutes}m {seconds}s\n" +
            $"Best Streak: x{currentStreak}</size>\n\n" +
            $"<size=80%><color=green>All servers online!</color></size>";

        SetScreenText("SHIFT COMPLETE!", body);

        // Enable win confetti
        var confetti = transform.Find("WinConfetti");
        if (confetti != null)
            confetti.gameObject.SetActive(true);
    }

    // ─── Display Helpers ───

    void SetScreenText(string title, string body)
    {
        if (laptopTitle != null) laptopTitle.text = title;
        if (laptopInstructions != null) laptopInstructions.text = body;
        if (tvTitle != null) tvTitle.text = title;
        if (tvInstructions != null) tvInstructions.text = body;
    }

    void UpdateSocketLabels()
    {
        foreach (var task in allTasks)
        {
            bool isActive = gameStarted && !gameComplete && currentTaskIndex >= 0
                && currentTaskIndex < allTasks.Count && allTasks[currentTaskIndex] == task;

            if (task.labelARef != null)
            {
                if (task.aConnected)
                    task.labelARef.color = Color.green;
                else if (isActive)
                    task.labelARef.color = Color.yellow;
                else
                    task.labelARef.color = new Color(0.5f, 0.5f, 0.5f);
            }
            if (task.labelBRef != null)
            {
                if (task.bConnected)
                    task.labelBRef.color = Color.green;
                else if (isActive)
                    task.labelBRef.color = Color.yellow;
                else
                    task.labelBRef.color = new Color(0.5f, 0.5f, 0.5f);
            }
        }
    }
}
