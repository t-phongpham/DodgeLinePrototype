using UnityEngine;

namespace DodgeLine
{
    public sealed class CampaignHUD : MonoBehaviour
    {
        public GameManager Game;
        private GUIStyle text, title, button;
        private void Update() { Game.WorldCamera.orthographicSize = Mathf.Max(6, 10f / Mathf.Max(.1f, Game.WorldCamera.aspect)); }
        private void OnGUI()
        {
            if (Network.LeaderboardClient.IsOpen) return;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                title = new GUIStyle(text) { fontSize = 23, fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
            }
            GUI.Box(new Rect(8, 8, 1264, 150), GUIContent.none);
            GUI.Label(new Rect(24, 15, 850, 34), $"{Game.Stage.number}. {Game.Stage.title}     BASE {Game.RequiredBases[0].Health}/20     ENERGY {Game.Resources.Energy}", title);
            GUI.Label(new Rect(24, 50, 1010, 27), $"WAVE {Game.WaveNumber}/{Game.Settings.WaveCount}  |  {Game.Phase}  |  Enemies {Game.Enemies.Count}  |  Score {Game.Score}", text);
            GUI.Label(new Rect(24, 78, 1010, 28), Game.Phase == MatchPhase.Preparation ?
                $"{Game.PreparationRemaining:F0}s  {Game.Spawner.CurrentWave?.label}" : Game.Stage.briefing, text);
            GUI.enabled = !Game.IsTerminal && !Game.IsPaused;
            BuildButton(new Rect(24, 111, 205, 34), "1  Thorn Bow", BuildChoice.ThornBow, Game.Settings.bow.cost);
            GUI.enabled = !Game.IsTerminal && !Game.IsPaused && Game.Settings.levelNumber >= 2;
            BuildButton(new Rect(239, 111, 225, 34), "2  Dust Cannon", BuildChoice.DustCannon, Game.Settings.cannon.cost);
            GUI.enabled = !Game.IsTerminal && !Game.IsPaused;
            BuildButton(new Rect(474, 111, 200, 34), "3  Wire Trap", BuildChoice.WireTrap, Game.Settings.trapCost);
            GUI.enabled = Game.Phase == MatchPhase.Preparation && !Game.IsPaused;
            if (GUI.Button(new Rect(690, 111, 230, 34), "Space  Start wave (+5)", button)) Game.StartWave();
            GUI.enabled = !Game.IsTerminal;
            if (GUI.Button(new Rect(1030, 24, 215, 38), Game.IsPaused ? "Resume (P)" : "Pause (P)", button)) Game.SetPaused(!Game.IsPaused);
            GUI.enabled = true;
            GUI.Box(new Rect(8, 636, 1264, 76), GUIContent.none);
            var tower = Game.Placement.SelectedTower;
            var trap = Game.Placement.SelectedTrap;
            GUI.Label(new Rect(24, 644, 800, 58), tower != null ?
                $"{tower.Data.Label} L{tower.Level}  |  Damage {tower.Damage}  Range {tower.Range:F1}\nClick clear ground to deselect; right click cancels building." :
                trap != null ? $"Wire Trap: {trap.RemainingUses}/6 uses. Moving enemies only: slow 50%, 2s.\nX: sell for {Mathf.FloorToInt(Game.Settings.trapCost * .7f)} energy." :
                $"{Game.Placement.Feedback}\n1/2/3 build | Right click cancel | Click a defense to inspect | P pause", text);
            GUI.enabled = tower != null && tower.Level < 3 && Game.Resources.Energy >= tower.UpgradeCost && !Game.IsTerminal && !Game.IsPaused;
            if (GUI.Button(new Rect(840, 650, 210, 42), tower != null && tower.Level < 3 ? $"U  Upgrade ({tower.UpgradeCost})" : "Upgrade", button)) Game.Placement.UpgradeSelected();
            GUI.enabled = (tower != null || trap != null) && !Game.IsTerminal && !Game.IsPaused;
            if (GUI.Button(new Rect(1060, 650, 185, 42), $"X  Sell ({(tower != null ? tower.SellRefund : Mathf.FloorToInt(Game.Settings.trapCost * .7f))})", button)) Game.Placement.SellSelected();
            GUI.enabled = true;
            if (Game.Placement.Choice != BuildChoice.None && !Game.IsTerminal && !Game.IsPaused)
                WorldLabel(Game.Placement.PreviewPosition + Vector2.up * .75f,
                    Game.Placement.PreviewValid ? "Click to place" : Game.Placement.PreviewReason);
            if (Game.IsPaused || Game.IsTerminal) Overlay();
        }
        private void BuildButton(Rect rect, string name, BuildChoice choice, int cost)
        {
            bool previous = GUI.enabled;
            GUI.enabled &= Game.Resources.Energy >= cost;
            if (GUI.Button(rect, $"{name} ({cost})", button)) Game.Placement.Select(choice);
            GUI.enabled = previous;
        }
        private void Overlay()
        {
            GUI.Box(new Rect(320, 210, 640, 350), GUIContent.none);
            bool won = Game.Phase == MatchPhase.Victory;
            string heading = Game.IsPaused ? "PAUSED" : won ?
                Game.Stage.number == 5 ? "MIST VALLEY DEFENDED!" : "OUTPOST SECURED" : "OUTPOST LOST";
            GUI.Label(new Rect(350, 230, 580, 40), heading, title);
            GUI.Label(new Rect(350, 279, 580, 95), Game.IsPaused ?
                "P / Esc to resume. Build, upgrade and combat wait until you return.\n" + Game.Stage.briefing :
                $"Base HP: {Game.RequiredBases[0].Health}/20   Score: {Game.Score}\n" +
                (won ? "Progress saved. Replay for a better score and more stars." : "Try a tower near the base; upgrade before the armored group arrives."), text);
            if (Game.IsPaused && GUI.Button(new Rect(350, 385, 580, 40), "Resume", button)) Game.SetPaused(false);
            if (won && Game.Stage.number < 5 && GUI.Button(new Rect(350, 385, 580, 40), "Next stage", button)) CampaignProgress.Load(Game.Stage.number + 1);
            if (GUI.Button(new Rect(350, 435, 280, 40), "Retry stage (R)", button)) Game.Restart();
            if (GUI.Button(new Rect(650, 435, 280, 40), "Campaign menu", button)) CampaignProgress.Menu();
            if (won)
            {
                if (GUI.Button(new Rect(350, 485, 220, 38), "Online leaderboard")) Network.LeaderboardClient.Ensure().Open(Game.Stage.number);
                GUI.Label(new Rect(585, 485, 335, 62), Network.LeaderboardClient.Ensure().Status, text);
            }
        }
        private void WorldLabel(Vector2 position, string message)
        {
            Vector3 point = Game.WorldCamera.WorldToScreenPoint(position);
            float y = (1 - point.y / Screen.height) * 720;
            if (y < 166 || y > 606) return;
            GUI.Box(new Rect(point.x / Screen.width * 1280 - 210, y, 420, 26), message, text);
        }
    }
}
