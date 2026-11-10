#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using DodgeLine.Network;

namespace DodgeLine
{
    // Opt-in, development-only paid-command driver. No instance exists in normal play.
    public sealed class CampaignSmokeCheck : MonoBehaviour
    {
        private string output;
        private bool error;
        private bool verifyNetwork, verifyOffline;
        private LeaderboardClient network;
        private string networkPrefix;
        private bool probeOnly;
        [Serializable] private sealed class CloudProbeResult { public bool authenticated, leaderboardAccessible; public long httpStatus; public string status; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dodgeLineCampaignCheck");
            if (index < 0) index = Array.IndexOf(args, "-dodgeLineProbeLeaderboard");
            if (index < 0) return;
            var go = new GameObject("Opt-in campaign verification"); DontDestroyOnLoad(go);
            go.AddComponent<CampaignSmokeCheck>().output = args[index + 1];
        }
        public static void Opening(GameManager game)
        {
            int level = game.Stage.number;
            if (level == 1 || level == 3)
            {
                Require(game.Placement.TryPlace(BuildChoice.ThornBow, level == 1 ? new Vector2(-1, 1.5f) : new Vector2(-6.5f, .2f)), "Opening bow rejected.");
                Require(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 1.5f)), "Final bow rejected.");
                Require(game.Towers[0].TryUpgrade(), "Opening upgrade rejected.");
            }
            else
            {
                Require(game.Placement.TryPlace(BuildChoice.DustCannon, new Vector2(-1, 0)), "Opening cannon rejected.");
                Require(game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, 0)), "Final bow rejected.");
            }
        }
        public static void TickStrategy(GameManager game)
        {
            if (game.Phase == MatchPhase.Preparation) game.StartWave();
            if (!game.IsCombat) return;
            foreach (Vector2 point in game.Stage.suggestedCannons)
                if (!game.Towers.Exists(t => Vector2.Distance(t.Position, point) < .7f) && game.Placement.TryPlace(BuildChoice.DustCannon, point)) return;
            foreach (Vector2 point in game.Stage.suggestedBows)
                if (!game.Towers.Exists(t => Vector2.Distance(t.Position, point) < .7f) && game.Placement.TryPlace(BuildChoice.ThornBow, point)) return;
            foreach (var tower in game.Towers) if (tower.Level < 3 && tower.TryUpgrade()) return;
            foreach (Vector2 point in game.Stage.suggestedTraps)
                if (!game.Traps.Exists(t => Vector2.Distance(t.Position, point) < .6f) && game.Placement.TryPlace(BuildChoice.WireTrap, point)) return;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.logMessageReceived += Track;
            CampaignProgress.SaveNamespace = "DodgeLine.Validation." + Guid.NewGuid() + ".";
            var args = Environment.GetCommandLineArgs();
            verifyNetwork = Array.IndexOf(args, "-dodgeLineVerifyLeaderboard") >= 0;
            verifyOffline = Array.IndexOf(args, "-dodgeLineVerifyLeaderboardOffline") >= 0;
            probeOnly = Array.IndexOf(args, "-dodgeLineProbeLeaderboard") >= 0;
            if (verifyNetwork || verifyOffline || probeOnly)
            {
                network = LeaderboardClient.Ensure();
                var config = network.Api.Config;
                if (!probeOnly) Require(Uri.TryCreate(config.scoresUrl, UriKind.Absolute, out var uri) && uri.IsLoopback, "Network validation must use the local fixture, never a live board.");
                networkPrefix = CampaignProgress.SaveNamespace + "Network.";
                network.ConfigureForTests(config, networkPrefix);
                network.SaveName("Native test");
            }
            var routine = Run();
            while (true)
            {
                object next;
                try { if (!routine.MoveNext()) break; next = routine.Current; }
                catch (Exception exception)
                {
                    File.WriteAllText(Path.Combine(output, "campaign-smoke.txt"), "FAILED\n" + exception);
                    Debug.LogException(exception); Application.Quit(1); yield break;
                }
                yield return next;
            }
        }
        private void Track(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        private IEnumerator Run()
        {
            yield return null;
            if (probeOnly)
            {
                network.Open(1); float deadline = Time.realtimeSinceStartup + 35;
                while (network.Busy && Time.realtimeSinceStartup < deadline) yield return null;
                var probe = new CloudProbeResult { authenticated = !string.IsNullOrEmpty(network.Api.PlayerId),
                    leaderboardAccessible = network.Page != null, httpStatus = network.Api.HttpStatus, status = network.Status };
                File.WriteAllText(Path.Combine(output, "cloud-probe.json"), JsonUtility.ToJson(probe, true));
                network.Close(); Application.Quit(probe.authenticated ? 0 : 1); yield break;
            }
            Require(FindAnyObjectByType<CampaignMenu>() != null && CampaignProgress.Unlocked == 1, "Menu/fresh progress missing.");
            var result = new StringBuilder("Unity " + Application.unityVersion + "\n");
            for (int level = 1; level <= 5; level++)
            {
                CampaignProgress.Load(level); yield return null; yield return null;
                var game = FindAnyObjectByType<GameManager>();
                Require(game != null && game.Stage.number == level && game.Resources.Energy == 150, "Stage load/initial state failed.");
                var unmarkedGround = level == 1 || level == 3 ? new Vector2(-7, 2.7f) : new Vector2(-7, 0);
                Require(game.Placement.FitsTowerTerrain(unmarkedGround, .45f, out _), "Unmarked-ground fixture is not clear terrain.");
                Require(!game.Placement.TryPlace(BuildChoice.ThornBow, unmarkedGround) &&
                    !game.Placement.TryPlace(BuildChoice.DustCannon, unmarkedGround) && game.Resources.Energy == 150,
                    "Unmarked ground allowed a tower or spent money.");
                Require(game.Placement.BuildPads.Length > 0 &&
                    game.Placement.CanPlace(BuildChoice.ThornBow, game.Placement.BuildPads[0].Position + Vector2.right * .2f, out var center, out _) &&
                    Vector2.Distance(center, game.Placement.BuildPads[0].Position) < .00001f, "Click did not snap to the build pad center.");
                Capture(game.WorldCamera, "stage" + level + "-map.png");
                Opening(game); game.SetPaused(true);
                Require(!game.Placement.TryPlace(BuildChoice.ThornBow, new Vector2(5, -1.5f)), "Pause allowed purchase.");
                game.SetPaused(false); Time.timeScale = 20;
                float deadline = Time.realtimeSinceStartup + 75;
                bool captured = false;
                while (!game.IsTerminal && Time.realtimeSinceStartup < deadline)
                {
                    TickStrategy(game);
                    if (!captured && game.Enemies.Count >= 3 && game.SimulationTime > 6)
                    { Capture(game.WorldCamera, "stage" + level + "-combat.png"); captured = true; }
                    yield return null;
                }
                Require(game.Phase == MatchPhase.Victory, "Paid strategy did not win stage " + level + ", phase " + game.Phase);
                Require(game.Towers.TrueForAll(tower => Array.Exists(game.Placement.BuildPads, pad => Vector2.Distance(pad.Position, tower.Position) < .00001f)),
                    "Winning strategy built a tower outside a marked pad.");
                Require(CampaignProgress.Unlocked >= Mathf.Min(5, level + 1), "Victory did not unlock next stage.");
                result.AppendLine($"Stage {level}: Victory wave {game.WaveNumber}/{game.Settings.WaveCount}, HP {game.RequiredBases[0].Health}, energy {game.Resources.Energy}, score {game.Score}, kills {game.Kills}, retreats {game.EvadeStarts}, blasts {game.Explosions}, combat {game.SimulationTime:F2}s.");
                result.AppendLine($"Stage {level}: {game.Placement.BuildPads.Length} shared build pads; unmarked ground rejected, center snap and all towers on pads PASSED.");
                if (verifyNetwork || verifyOffline)
                {
                    float networkDeadline = Time.realtimeSinceStartup + 20;
                    while (network.Busy && Time.realtimeSinceStartup < networkDeadline) yield return null;
                    Require(!network.Busy, "Network request never completed.");
                    if (verifyNetwork)
                    {
                        Require(network.PendingCount == 0, "Victory was not uploaded: " + network.Status);
                        network.Open(level);
                        networkDeadline = Time.realtimeSinceStartup + 20;
                        while (network.Busy && Time.realtimeSinceStartup < networkDeadline) yield return null;
                        Require(network.OwnEntry != null && network.OwnEntry.score == game.Score && network.Page != null, "Online rank/score did not match the actual game result.");
                        result.AppendLine($"Stage {level}: actual victory score {game.Score} uploaded and rank {network.OwnEntry.rank + 1} read over HTTP PASSED.");
                        network.Close();
                    }
                    else
                    {
                        Require(network.PendingCount == level, "Offline score was not persisted for retry.");
                        result.AppendLine($"Stage {level}: offline victory preserved; pending stage count {network.PendingCount} PASSED.");
                    }
                }
                Capture(game.WorldCamera, "stage" + level + "-victory.png");
                game.Restart(); yield return null; yield return null;
                game = FindAnyObjectByType<GameManager>();
                Require(game.Resources.Energy == 150 && game.RequiredBases[0].Health == 20 && game.Towers.Count == 0 && Time.timeScale == 1, "Retry did not reset stage.");
                Time.timeScale = 20; game.StartWave(); deadline = Time.realtimeSinceStartup + 20;
                while (!game.IsTerminal && Time.realtimeSinceStartup < deadline) yield return null;
                Require(game.Phase == MatchPhase.Defeat, "No-defense scenario did not lose stage " + level);
                foreach (var enemy in game.Enemies)
                    if (enemy.State == EnemyState.AttackBase)
                        Require(Physics2D.Distance(enemy.GetComponent<Collider2D>(), game.RequiredBases[0].GetComponent<Collider2D>()).distance <= .003f, "Ranged base attacker found.");
                result.AppendLine($"Stage {level}: no-defense Defeat at {game.SimulationTime:F2}s; physical base contact checked; retry passed.");
            }
            CampaignProgress.Menu(); yield return null; yield return null;
            Require(FindAnyObjectByType<CampaignMenu>() != null && Time.timeScale == 1, "Return to menu failed.");
            Require(!error, "Runtime errors logged; inspect player.log.");
            result.AppendLine("All five stages: paid win, no-defense loss, retry, local unlock/stars and return to menu PASSED. Scripted commands; human HUD/input playtest not performed.");
            if (network != null)
            {
                string id = network.Api.PlayerId;
                var config = network.Api.Config;
                Destroy(network.gameObject); yield return null;
                network = LeaderboardClient.Ensure(); network.ConfigureForTests(config, networkPrefix);
                if (verifyNetwork)
                {
                    network.Open(5); float deadline = Time.realtimeSinceStartup + 20;
                    while (network.Busy && Time.realtimeSinceStartup < deadline) yield return null;
                    Require(network.Api.PlayerId == id && network.OwnEntry != null, "Returning anonymous profile lost identity or rank.");
                    result.AppendLine("Anonymous session restored after client recreation PASSED.");
                }
                else Require(network.PendingCount == 5, "Offline outbox did not survive client recreation.");
                network.Close();
            }
            File.WriteAllText(Path.Combine(output, "campaign-smoke.txt"), result.ToString());
            Debug.Log(result.ToString()); Application.Quit(0);
        }
        private void Capture(Camera camera, string file)
        {
            var target = new RenderTexture(1280, 720, 24); var previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                target.Create(); RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, file), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; target.Release(); Destroy(target); if (pixels != null) Destroy(pixels); }
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= Track;
            if (!string.IsNullOrEmpty(networkPrefix))
            {
                foreach (string suffix in new[] { "Name", "Session", "Outbox", "Outbox.v1", "Outbox.v2" }) PlayerPrefs.DeleteKey(networkPrefix + suffix);
                PlayerPrefs.Save();
            }
        }
    }
}
#endif
