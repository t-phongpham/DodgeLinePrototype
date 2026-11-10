using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DodgeLine.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DodgeLine.Tests
{
    public sealed class LeaderboardNetworkTests
    {
        private Process fixture;
        private int port;
        private string prefix, previousSave;
        private readonly List<string> prefixes = new List<string>();
        private LevelSettings clonedSettings;
        private string Root => "http://127.0.0.1:" + port;
        [OneTimeSetUp]
        public void StartHttpFixture()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            try
            {
                fixture = Process.Start(new ProcessStartInfo("node", "tools/leaderboard-fixture.mjs --port " + port) {
                    WorkingDirectory = Directory.GetParent(Application.dataPath).FullName, UseShellExecute = false, CreateNoWindow = true
                });
            }
            catch (System.ComponentModel.Win32Exception) { Assert.Ignore("Node.js is required for the local HTTP integration fixture."); }
            bool ready = false;
            for (int attempt = 0; attempt < 100 && !ready; attempt++)
            {
                try { using (var client = new TcpClient()) client.Connect(IPAddress.Loopback, port); ready = true; }
                catch (SocketException) { System.Threading.Thread.Sleep(20); }
            }
            Assert.That(ready, Is.True, "HTTP fixture did not start.");
        }
        [OneTimeTearDown]
        public void StopHttpFixture() { if (fixture != null) { if (!fixture.HasExited) fixture.Kill(); fixture.Dispose(); } }
        [SetUp]
        public void SetUp()
        {
            prefix = "DodgeLine.Test.Network." + Guid.NewGuid() + "."; prefixes.Add(prefix);
            previousSave = CampaignProgress.SaveNamespace; CampaignProgress.SaveNamespace = prefix;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (LeaderboardClient.Instance != null) Object.Destroy(LeaderboardClient.Instance.gameObject);
            var game = Object.FindAnyObjectByType<GameManager>(); if (game != null) game.gameObject.SetActive(false);
            if (clonedSettings != null) Object.Destroy(clonedSettings);
            foreach (string key in prefixes)
            {
                foreach (string suffix in new[] { "Session", "Outbox", "Outbox.v1", "Outbox.v2", "Name", "Unlocked" }) PlayerPrefs.DeleteKey(key + suffix);
                for (int level = 1; level <= 5; level++) { PlayerPrefs.DeleteKey(key + "Stars" + level); PlayerPrefs.DeleteKey(key + "Score" + level); }
            }
            prefixes.Clear(); PlayerPrefs.Save(); CampaignProgress.SaveNamespace = previousSave;
            yield return null;
        }
        private LeaderboardConfig Config()
        {
            var config = LeaderboardConfig.Load(); config.authenticationUrl = config.scoresUrl = Root + "/v1"; config.timeoutSeconds = 1; return config;
        }
        private LeaderboardApi Api(string key = null) => new LeaderboardApi(Config(), key ?? prefix);
        private static VictoryScore Win(int level, int score) => new VictoryScore { level = level, score = score, baseHp = 20, kills = 10, combatSeconds = 60 };
        private IEnumerator Control(string json)
        {
            using (var request = new UnityWebRequest(Root + "/__test/control", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)); request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json"); yield return request.SendWebRequest();
                Assert.That(request.responseCode, Is.EqualTo(200));
            }
        }
        private static IEnumerator WaitForClient(LeaderboardClient client)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (client.Busy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(client.Busy, Is.False, "Network operation did not finish.");
        }
        [UnityTest]
        public IEnumerator AnonymousIdentitySurvivesTokenRefreshAndNewApiInstance()
        {
            var api = Api(); yield return api.SignIn(); Assert.That(api.Error, Is.Null);
            Assert.That(api.AccountNotices, Does.Contain("Test account notification"));
            string id = api.PlayerId; api.ForgetAccessToken(); yield return api.SignIn(); Assert.That(api.PlayerId, Is.EqualTo(id));
            var returning = Api(); yield return returning.SignIn(); Assert.That(returning.PlayerId, Is.EqualTo(id));
        }
        [UnityTest]
        public IEnumerator BestScoreTopTenOwnRankAndFiveStagesUseRealHttp()
        {
            var api = Api();
            yield return api.Submit(Win(1, 1000), "First", null); Assert.That(api.Error, Is.Null);
            yield return api.Submit(Win(1, 100), "First", null);
            LeaderboardEntry own = null; yield return api.GetOwn(1, value => own = value);
            Assert.That(own.score, Is.EqualTo(1000));
            Assert.That(own.metadata.rulesVersion, Is.EqualTo("v2"));
            for (int index = 0; index < 11; index++)
            {
                string key = prefix + index; prefixes.Add(key); var other = Api(key);
                yield return other.Submit(Win(1, 100000 + index), "Other", null); Assert.That(other.Error, Is.Null);
            }
            LeaderboardPage page = null; yield return api.GetTop(1, value => page = value);
            Assert.That(page.results.Length, Is.EqualTo(10)); Assert.That(page.results[0].score, Is.GreaterThanOrEqualTo(page.results[9].score));
            yield return api.GetOwn(1, value => own = value); Assert.That(own.rank, Is.GreaterThanOrEqualTo(11));
            for (int level = 2; level <= 5; level++)
            {
                yield return api.Submit(Win(level, 2000 + level), "First", null); yield return api.GetOwn(level, value => own = value);
                Assert.That(own.score, Is.EqualTo(2000 + level));
            }
        }
        [UnityTest]
        public IEnumerator UnauthorizedRequestRefreshesSessionAndRetriesOnce()
        {
            var api = Api(); yield return api.SignIn(); string id = api.PlayerId;
            yield return Control("{\"nextStatus\":401}");
            LeaderboardPage page = null; yield return api.GetTop(1, value => page = value);
            Assert.That(api.Error, Is.Null); Assert.That(page, Is.Not.Null); Assert.That(api.PlayerId, Is.EqualTo(id));
        }
        [UnityTest]
        public IEnumerator RateLimitAndServerFailureCanBeRetriedWithoutLosingIdentity()
        {
            var api = Api(); yield return api.SignIn();
            yield return Control("{\"nextStatus\":429}"); yield return api.GetTop(1, null);
            Assert.That(api.Error, Does.Contain("wait")); yield return new WaitForSecondsRealtime(1.1f);
            yield return Control("{\"nextStatus\":503}"); yield return api.GetTop(1, null);
            Assert.That(api.Error, Does.Contain("unavailable")); yield return api.GetTop(1, null); Assert.That(api.Error, Is.Null);
        }
        [UnityTest]
        public IEnumerator MalformedAndMissingBoardResponsesShowRecoverableErrors()
        {
            var api = Api(); yield return api.SignIn();
            yield return Control("{\"malformed\":true}"); yield return api.GetTop(1, null); Assert.That(api.Error, Does.Contain("invalid"));
            yield return api.GetTop(1, null); Assert.That(api.Error, Is.Null);
            var config = Config(); config.leaderboardIds[0] = "not-created"; var missing = new LeaderboardApi(config, prefix);
            yield return missing.GetTop(1, null); Assert.That(missing.Error, Does.Contain("not set up"));
        }
        [UnityTest]
        public IEnumerator TimeoutFinishesWhileGameplayTimeScaleIsZero()
        {
            var api = Api(); yield return api.SignIn();
            yield return Control("{\"delayMs\":2200}");
            Time.timeScale = 0; yield return api.GetTop(1, null); Assert.That(api.Error, Is.Not.Null); Time.timeScale = 1;
        }
        [UnityTest]
        public IEnumerator ActualVictorySavesLocallyAndFailedUploadSurvivesClientRecreation()
        {
            yield return SceneManager.LoadSceneAsync("Level1");
            var game = Object.FindAnyObjectByType<GameManager>(); clonedSettings = Object.Instantiate(game.Settings); game.Settings = clonedSettings; clonedSettings.waves = null;
            var client = LeaderboardClient.Ensure(); client.ConfigureForTests(Config(), prefix);
            yield return Control("{\"nextStatus\":503}");
            game.StartWave(); game.EvaluateResolution(); yield return WaitForClient(client);
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Victory)); Assert.That(client.PendingCount, Is.EqualTo(1));
            Assert.That(CampaignProgress.BestScore(1), Is.EqualTo(game.Score));
            Object.Destroy(client.gameObject); yield return null;
            client = LeaderboardClient.Ensure(); client.ConfigureForTests(Config(), prefix);
            Assert.That(client.PendingCount, Is.EqualTo(1)); client.Open(1); yield return WaitForClient(client);
            Assert.That(client.PendingCount, Is.Zero); Assert.That(client.OwnEntry.score, Is.EqualTo(game.Score)); client.Close();
        }
        [UnityTest]
        public IEnumerator PadRulesUseANewBoardAndOutboxWithoutDiscardingPlayerIdentity()
        {
            var api = Api(); yield return api.SignIn(); string id = api.PlayerId;
            var oldConfig = Config(); oldConfig.rulesVersion = "v1";
            for (int i = 0; i < oldConfig.leaderboardIds.Length; i++) oldConfig.leaderboardIds[i] = oldConfig.leaderboardIds[i].Replace("-v2", "-v1");
            var oldApi = new LeaderboardApi(oldConfig, prefix);
            yield return oldApi.Submit(Win(1, 9000), "Old free terrain", null);
            var legacy = new LeaderboardOutbox(); legacy.Queue(Win(1, 9000));
            legacy.Save(prefix + "Outbox"); legacy.Save(prefix + "Outbox.v1");
            PlayerPrefs.SetString(prefix + "Name", "Saved name");
            var client = LeaderboardClient.Ensure(); client.ConfigureForTests(Config(), prefix);
            Assert.That(client.PendingCount, Is.Zero, "Old free-placement results must not enter the new rules board.");
            Assert.That(client.DisplayName, Is.EqualTo("Saved name"));
            client.Open(1); yield return WaitForClient(client);
            Assert.That(client.Api.PlayerId, Is.EqualTo(id)); Assert.That(client.OwnEntry, Is.Null);
            client.Close();
        }
        [UnityTest]
        public IEnumerator DefeatNeverQueuesAVictoryScore()
        {
            yield return SceneManager.LoadSceneAsync("Level1");
            var game = Object.FindAnyObjectByType<GameManager>(); var client = LeaderboardClient.Ensure(); client.ConfigureForTests(Config(), prefix);
            game.StartWave(); game.RequiredBases[0].TakeDamage(20); client.RecordVictory(game);
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Defeat)); Assert.That(client.PendingCount, Is.Zero); Assert.That(client.Busy, Is.False);
        }
    }
}
