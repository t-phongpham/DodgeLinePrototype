using System;
using DodgeLine.Network;
using NUnit.Framework;
using UnityEngine;

namespace DodgeLine.Tests
{
    public sealed class LeaderboardRulesTests
    {
        [Test]
        public void ConfigurationHasFiveDistinctBoardsAndProductionHttpsEndpoints()
        {
            var config = LeaderboardConfig.Load();
            Assert.That(config.IsValid, Is.True);
            Assert.That(config.rulesVersion, Is.EqualTo("v2"));
            Assert.That(config.leaderboardIds, Has.All.EndsWith("-v2"));
            Assert.That(new System.Collections.Generic.HashSet<string>(config.leaderboardIds).Count, Is.EqualTo(5));
            Assert.That(config.authenticationUrl, Does.StartWith("https://"));
            Assert.That(config.scoresUrl, Does.StartWith("https://"));
        }
        [Test]
        public void PendingScoresKeepBestPerStageAndLateAcknowledgementCannotEraseNewerWin()
        {
            var outbox = new LeaderboardOutbox();
            var first = new VictoryScore { level = 1, score = 100, baseHp = 20 };
            Assert.That(outbox.Queue(first), Is.True);
            Assert.That(outbox.Queue(new VictoryScore { level = 1, score = 50, baseHp = 10 }), Is.False);
            Assert.That(outbox.Queue(new VictoryScore { level = 1, score = 200, baseHp = 20 }), Is.True);
            outbox.Acknowledge(first);
            Assert.That(outbox.pending.Count, Is.EqualTo(1));
            Assert.That(outbox.pending[0].score, Is.EqualTo(200));
            for (int level = 2; level <= 5; level++) outbox.Queue(new VictoryScore { level = level, score = level, baseHp = 1 });
            Assert.That(outbox.pending.Count, Is.EqualTo(5));
        }
        [Test]
        public void OutboxRoundTripsAndRecoversFromCorruptStorage()
        {
            string key = "DodgeLine.Test.Outbox." + Guid.NewGuid();
            try
            {
                var outbox = new LeaderboardOutbox(); outbox.Queue(new VictoryScore { level = 3, score = 900, baseHp = 13 }); outbox.Save(key);
                Assert.That(LeaderboardOutbox.Load(key).pending[0].score, Is.EqualTo(900));
                PlayerPrefs.SetString(key, "broken json");
                Assert.That(LeaderboardOutbox.Load(key).pending.Count, Is.Zero);
                PlayerPrefs.SetString(key, "{\"pending\":[null,{\"level\":99,\"baseHp\":1},{\"level\":1,\"score\":7,\"baseHp\":1}]}");
                Assert.That(LeaderboardOutbox.Load(key).pending.Count, Is.EqualTo(1));
            }
            finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        }
        [TestCase("  Player One  ", "Player One")]
        [TestCase("<b>Alice</b>!", "bAliceb")]
        [TestCase("12345678901234567890", "123456789012345678")]
        public void DisplayNamesCannotInjectMarkupAndHaveBoundedLength(string input, string expected)
        { Assert.That(LeaderboardRules.CleanName(input), Is.EqualTo(expected)); }
        [Test]
        public void InvalidOrIncompleteResultsNeverEnterOutbox()
        {
            var outbox = new LeaderboardOutbox();
            Assert.That(outbox.Queue(new VictoryScore { level = 1, baseHp = 0 }), Is.False);
            Assert.That(outbox.Queue(new VictoryScore { level = 6, baseHp = 20 }), Is.False);
            Assert.That(outbox.Queue(new VictoryScore { level = 1, baseHp = 20, score = -1 }), Is.False);
            Assert.That(outbox.Queue(new VictoryScore { level = 1, baseHp = 20, combatSeconds = float.NaN }), Is.False);
            Assert.That(outbox.pending, Is.Empty);
        }
    }
}
