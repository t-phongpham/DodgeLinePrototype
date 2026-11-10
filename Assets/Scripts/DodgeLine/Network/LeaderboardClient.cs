using System;
using System.Collections;
using UnityEngine;

namespace DodgeLine.Network
{
    [DefaultExecutionOrder(500)]
    public sealed class LeaderboardClient : MonoBehaviour
    {
        public static LeaderboardClient Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.Visible;
        public bool Visible { get; private set; }
        public bool Busy { get; private set; }
        public int SelectedLevel { get; private set; } = 1;
        public string Status { get; private set; } = "Select a stage, then refresh the online board.";
        public string DisplayName { get; private set; }
        public LeaderboardPage Page { get; private set; }
        public LeaderboardEntry OwnEntry { get; private set; }
        public LeaderboardApi Api { get; private set; }
        public int PendingCount => outbox?.pending.Count ?? 0;
        private LeaderboardOutbox outbox;
        private string prefix, outboxKey, nameInput;
        private bool readRequested;
        private GUIStyle label, heading, row;
        private Vector2 noticesScroll;
        private bool showNotices;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool CaptureIsolatedTestVictories { get; set; }
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }
        public static LeaderboardClient Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("Leaderboard network client").AddComponent<LeaderboardClient>();
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            var config = LeaderboardConfig.Load();
            string scope = "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dodgeLineLeaderboardEndpoint");
            if (index >= 0 && index + 1 < args.Length && Uri.TryCreate(args[index + 1], UriKind.Absolute, out var uri) &&
                uri.IsLoopback && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                config.authenticationUrl = config.scoresUrl = uri.GetLeftPart(UriPartial.Authority) + "/v1";
                scope = ".LocalTest." + uri.Port;
            }
#endif
            Configure(config, "DodgeLine.Network." + config.projectId + "." + config.environment + ".v1" + scope + ".");
        }
        private void Configure(LeaderboardConfig config, string savePrefix)
        {
            prefix = savePrefix; Api = new LeaderboardApi(config, prefix);
            outboxKey = prefix + "Outbox." + config.rulesVersion;
            outbox = LeaderboardOutbox.Load(outboxKey);
            DisplayName = PlayerPrefs.GetString(prefix + "Name", ""); nameInput = DisplayName;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void ConfigureForTests(LeaderboardConfig config, string savePrefix)
        {
            if (Busy) throw new InvalidOperationException("Wait for the current request before changing test configuration.");
            Configure(config, savePrefix); CaptureIsolatedTestVictories = true;
            Page = null; OwnEntry = null;
        }
#endif
        public bool SaveName(string name)
        {
            string clean = LeaderboardRules.CleanName(name);
            if (clean.Length < 2) { Status = "Use a name with 2-18 letters, numbers, spaces, _ or -."; return false; }
            DisplayName = nameInput = clean;
            PlayerPrefs.SetString(prefix + "Name", clean); PlayerPrefs.Save();
            Status = "Name saved. It is included with your next uploaded best score."; return true;
        }
        public void RecordVictory(GameManager game)
        {
            // Regression bots and isolated saves must never publish into the user's board.
            bool capture = CampaignProgress.SaveNamespace == CampaignProgress.DefaultSaveNamespace;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            capture |= CaptureIsolatedTestVictories;
#endif
            if (!capture || game.Stage == null || game.Phase != MatchPhase.Victory) return;
            if (outbox.Queue(VictoryScore.FromGame(game))) outbox.Save(outboxKey);
            Status = "Victory saved. Sending your best score...";
            Pump();
        }
        public void Open(int level = 1)
        {
            Visible = true; showNotices = false; SelectLevel(level);
        }
        public void Close() { Visible = false; }
        public void SelectLevel(int level)
        {
            SelectedLevel = Mathf.Clamp(level, 1, 5); Page = null; OwnEntry = null;
            readRequested = true; Pump();
        }
        public void Retry() { readRequested = true; Pump(); }
        private void Pump()
        {
            if (!Busy) StartCoroutine(Process());
        }
        private IEnumerator Process()
        {
            Busy = true;
            try
            {
                Status = "Connecting to online leaderboard...";
                while (outbox.pending.Count > 0)
                {
                    var result = outbox.pending[0];
                    bool accepted = false;
                    yield return Api.Submit(result, DisplayName, entry => accepted = true);
                    if (!accepted) { Status = Api.Error ?? "Score could not be sent. Retry later."; yield break; }
                    outbox.Acknowledge(result); outbox.Save(outboxKey);
                    Status = "Best score sent successfully.";
                }
                while (readRequested)
                {
                    readRequested = false; int level = SelectedLevel;
                    Status = "Loading stage " + level + " leaderboard...";
                    LeaderboardPage page = null;
                    yield return Api.GetTop(level, value => page = value);
                    if (Api.Error != null) { Status = Api.Error; yield break; }
                    LeaderboardEntry own = null;
                    yield return Api.GetOwn(level, value => own = value);
                    if (Api.Error != null) { Status = Api.Error; yield break; }
                    if (SelectedLevel != level) continue;
                    Page = page; OwnEntry = own;
                    Status = page.total == 0 ? "No scores yet. Win this stage to join the board." : "Online leaderboard updated.";
                }
            }
            finally { Busy = false; }
        }
        private void OnGUI()
        {
            if (!Visible) return;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, richText = false };
                heading = new GUIStyle(label) { fontSize = 26, fontStyle = FontStyle.Bold };
                row = new GUIStyle(label) { fontSize = 18, wordWrap = false };
            }
            GUI.enabled = true;
            GUI.color = new Color(.035f, .055f, .045f, 1);
            GUI.DrawTexture(new Rect(50, 25, 1180, 670), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Box(new Rect(50, 25, 1180, 670), GUIContent.none);
            bool localFixture = Uri.TryCreate(Api.Config.scoresUrl, UriKind.Absolute, out var endpoint) && endpoint.IsLoopback;
            GUI.Label(new Rect(80, 42, 850, 40), (localFixture ? "LOCAL TEST LEADERBOARD" : "ONLINE LEADERBOARD") + "  /  best score per stage", heading);
            if (GUI.Button(new Rect(1090, 43, 110, 36), "Close")) Close();
            for (int level = 1; level <= 5; level++)
            {
                GUI.enabled = !Busy && level != SelectedLevel;
                if (GUI.Button(new Rect(80 + (level - 1) * 222, 92, 210, 36), "Stage " + level)) SelectLevel(level);
            }
            GUI.enabled = !Busy;
            GUI.Label(new Rect(80, 142, 120, 30), "Your name", label);
            nameInput = GUI.TextField(new Rect(200, 142, 300, 30), nameInput ?? "", 18);
            if (GUI.Button(new Rect(515, 142, 130, 30), "Save name")) SaveName(nameInput);
            if (GUI.Button(new Rect(870, 142, 330, 30), Busy ? "Working..." : "Refresh / send pending")) Retry();
            GUI.enabled = true;
            GUI.Label(new Rect(80, 182, 1120, 58), Status + (PendingCount > 0 ? "  Pending stages: " + PendingCount : ""), label);
            GUI.Label(new Rect(85, 248, 100, 30), "RANK", row);
            GUI.Label(new Rect(220, 248, 650, 30), "PLAYER", row);
            GUI.Label(new Rect(980, 248, 210, 30), "SCORE", row);
            if (Page != null)
                for (int index = 0; index < Mathf.Min(10, Page.results.Length); index++)
                {
                    var entry = Page.results[index];
                    GUI.color = entry.playerId == Api.PlayerId ? new Color(.65f, 1, .7f) : Color.white;
                    float y = 281 + index * 26;
                    GUI.Label(new Rect(85, y, 100, 27), (entry.rank + 1).ToString(), row);
                    GUI.Label(new Rect(220, y, 650, 27), entry.DisplayName, row);
                    GUI.Label(new Rect(980, y, 210, 27), entry.score.ToString("0"), row);
                }
            GUI.color = Color.white;
            GUI.Label(new Rect(80, 553, 1120, 35), Page == null ? "Your rank: waiting for leaderboard data." : OwnEntry == null ? "Your rank: no submitted score on this stage yet." :
                $"Your rank: {OwnEntry.rank + 1}  |  Best: {OwnEntry.score:0}  |  {OwnEntry.DisplayName}", label);
            GUI.Label(new Rect(80, 600, 1110, 54), "Scores are shared online. Local progress stays on this device.\nAn anonymous profile is device/browser-specific; clearing its data loses access to that profile.", label);
            if (!string.IsNullOrEmpty(Api.AccountNotices) && GUI.Button(new Rect(80, 655, 230, 28), "Account notifications")) showNotices = !showNotices;
            if (showNotices)
            {
                GUI.Box(new Rect(120, 180, 1040, 410), GUIContent.none);
                noticesScroll = GUI.BeginScrollView(new Rect(145, 210, 990, 315), noticesScroll, new Rect(0, 0, 950, Mathf.Max(315, label.CalcHeight(new GUIContent(Api.AccountNotices), 940))));
                GUI.Label(new Rect(0, 0, 940, Mathf.Max(315, label.CalcHeight(new GUIContent(Api.AccountNotices), 940))), Api.AccountNotices, label);
                GUI.EndScrollView();
                if (GUI.Button(new Rect(850, 540, 240, 30), "Close notifications")) showNotices = false;
            }
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
