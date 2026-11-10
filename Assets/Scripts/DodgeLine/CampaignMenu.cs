using UnityEngine;

namespace DodgeLine
{
    public sealed class CampaignMenu : MonoBehaviour
    {
        public CampaignStage[] stages;
        private bool help;
        private GUIStyle title, text, button;
        private void Awake() { Time.timeScale = 1; Network.LeaderboardClient.Ensure(); }
        private void OnGUI()
        {
            if (Network.LeaderboardClient.IsOpen) return;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            GUI.enabled = !Network.LeaderboardClient.IsOpen;
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
                title = new GUIStyle(text) { fontSize = 36, fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { fontSize = 21, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(22, 12, 10, 10) };
            }
            GUI.Box(new Rect(55, 30, 1170, 655), GUIContent.none);
            GUI.Label(new Rect(85, 48, 1000, 54), "DODGE LINE  /  MIST VALLEY", title);
            GUI.Label(new Rect(88, 105, 1090, 34), "Solo campaign  |  Protect the outpost. Read the warning. Hold the final line.", text);
            if (help)
            {
                GUI.Label(new Rect(95, 175, 1060, 360),
                    "1 / 2 / 3: Thorn Bow / Dust Cannon / Wire Trap. Left click places. Right click cancels.\n\n" +
                    "Towers must use the gold build pads: one tower per pad. Traps belong on the road and never block enemies.\n" +
                    "Cancel building, then click a tower: U upgrades it (max level 3), X sells it for 70% of all energy paid.\n\n" +
                    "Space starts a wave early (+5). P or Esc pauses. R retries the current stage.\n" +
                    "Wisps warn in yellow then may retreat. They are still vulnerable. Cannon splash and traps help.\n\n" +
                    "Enemies reaching the outpost keep attacking until killed. Clear every wave to win.\n" +
                    "Win to unlock the next stage. Stars depend on base HP: 18+ = 3, 10+ = 2, surviving = 1.\n" +
                    "Energy and towers reset per stage. Your unlocks, best score and stars are saved locally.", text);
            }
            else
            {
                for (int i = 0; i < stages.Length; i++)
                {
                    var stage = stages[i];
                    GUI.enabled = stage.number <= CampaignProgress.Unlocked && !Network.LeaderboardClient.IsOpen;
                    string status = GUI.enabled ? $"{CampaignProgress.Stars(stage.number)}/3 stars   Best {CampaignProgress.BestScore(stage.number)}" : "LOCKED - clear the preceding stage";
                    if (GUI.Button(new Rect(85, 160 + i * 83, 1110, 72),
                        $"{stage.number}.  {stage.title}     /     {stage.settings.WaveCount} waves\n{status}", button))
                        CampaignProgress.Load(stage.number);
                }
                GUI.enabled = !Network.LeaderboardClient.IsOpen;
            }
            if (GUI.Button(new Rect(90, 605, 350, 52), help ? "Back to campaign" : "How to play", button)) help = !help;
            if (GUI.Button(new Rect(460, 605, 350, 52), "Online leaderboard", button)) Network.LeaderboardClient.Ensure().Open();
            if (GUI.Button(new Rect(830, 605, 350, 52), "Quit game", button)) Application.Quit();
            GUI.enabled = true;
        }
    }
}
