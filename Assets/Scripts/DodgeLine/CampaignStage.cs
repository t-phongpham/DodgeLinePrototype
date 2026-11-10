using UnityEngine;

namespace DodgeLine
{
    [CreateAssetMenu(menuName = "Dodge Line/Campaign Stage")]
    public sealed class CampaignStage : ScriptableObject
    {
        public int number = 1;
        public string title;
        [TextArea] public string briefing;
        public LevelSettings settings;
        public Sprite background;
        [Tooltip("Preferred Bow purchase order for the test strategy. Actual build permission comes from TowerBuildPad scene objects.")]
        public Vector2[] suggestedBows;
        [Tooltip("Preferred Cannon purchase order for the test strategy. Pads are shared by Bow and Cannon.")]
        public Vector2[] suggestedCannons;
        public Vector2[] suggestedTraps;
    }

    public static class CampaignProgress
    {
        public const string DefaultSaveNamespace = "DodgeLine.SoloCampaign.v1.";
        public static string SaveNamespace { get; set; } = DefaultSaveNamespace;
        private static string Prefix => SaveNamespace;
        public static int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Unlocked", 1), 1, 5);
        public static int Stars(int level) => PlayerPrefs.GetInt(Prefix + "Stars" + level, 0);
        public static int BestScore(int level) => PlayerPrefs.GetInt(Prefix + "Score" + level, 0);
        public static string SceneName(int level) => "Level" + Mathf.Clamp(level, 1, 5);
        public static void RecordWin(int level, int hp, int score)
        {
            int stars = hp >= 18 ? 3 : hp >= 10 ? 2 : 1;
            PlayerPrefs.SetInt(Prefix + "Unlocked", Mathf.Max(Unlocked, Mathf.Min(5, level + 1)));
            PlayerPrefs.SetInt(Prefix + "Stars" + level, Mathf.Max(Stars(level), stars));
            PlayerPrefs.SetInt(Prefix + "Score" + level, Mathf.Max(BestScore(level), score));
            PlayerPrefs.Save();
        }
        public static void Load(int level)
        {
            if (level < 1 || level > Unlocked) return;
            Time.timeScale = 1;
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneName(level));
        }
        public static void Menu()
        {
            Time.timeScale = 1;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }
}
