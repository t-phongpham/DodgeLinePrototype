using System;
using UnityEngine;

namespace DodgeLine.Network
{
    [Serializable]
    public sealed class LeaderboardConfig
    {
        public bool enabled = true;
        public string projectId;
        public string environment = "production";
        public string rulesVersion = "v2";
        public string[] leaderboardIds;
        public int timeoutSeconds = 10;
        [NonSerialized] public string authenticationUrl = "https://player-auth.services.api.unity.com/v1";
        [NonSerialized] public string scoresUrl = "https://leaderboards.services.api.unity.com/v1";
        public string BoardId(int level) => leaderboardIds != null && level >= 1 && level <= leaderboardIds.Length ? leaderboardIds[level - 1] : null;
        public bool IsValid => enabled && Guid.TryParse(projectId, out _) && !string.IsNullOrWhiteSpace(environment) &&
            leaderboardIds != null && leaderboardIds.Length == 5 && Array.TrueForAll(leaderboardIds, id => !string.IsNullOrWhiteSpace(id));
        public static LeaderboardConfig Load()
        {
            var asset = Resources.Load<TextAsset>("LeaderboardConfig");
            LeaderboardConfig result = null;
            try { if (asset != null) result = JsonUtility.FromJson<LeaderboardConfig>(asset.text); }
            catch (ArgumentException) { }
            if (result == null) result = new LeaderboardConfig { enabled = false };
            if (string.IsNullOrWhiteSpace(result.rulesVersion)) result.rulesVersion = "v1";
            result.authenticationUrl = "https://player-auth.services.api.unity.com/v1";
            result.scoresUrl = "https://leaderboards.services.api.unity.com/v1";
            return result;
        }
    }

    [Serializable] public sealed class ScoreMetadata
    {
        public string displayName;
        public int baseHp, kills;
        public float combatSeconds;
        public string rulesVersion = "v1";
    }
    [Serializable] public sealed class LeaderboardEntry
    {
        public string playerId, playerName;
        public int rank;
        public double score;
        public ScoreMetadata metadata;
        public string DisplayName => !string.IsNullOrWhiteSpace(metadata?.displayName) ? metadata.displayName :
            !string.IsNullOrWhiteSpace(playerName) ? playerName : "Player-" + (playerId ?? "unknown").Substring(0, Math.Min(6, (playerId ?? "unknown").Length));
    }
    [Serializable] public sealed class LeaderboardPage { public int total; public LeaderboardEntry[] results; }
    [Serializable] public sealed class VictoryScore
    {
        public int level, score, baseHp, kills;
        public float combatSeconds;
        public static VictoryScore FromGame(GameManager game) => new VictoryScore {
            level = game.Stage.number, score = game.Score, baseHp = game.RequiredBases[0].Health,
            kills = game.Kills, combatSeconds = game.SimulationTime
        };
    }
    [Serializable] internal sealed class AuthResponse
    {
        public string userId, idToken, sessionToken, lastNotificationDate;
        public int expiresIn;
    }
    [Serializable] internal sealed class SessionRequest { public string sessionToken; }
    [Serializable] internal sealed class ScoreRequest { public int score; public ScoreMetadata metadata; }
    [Serializable] internal sealed class AccountNotification { public string id, message; }
    [Serializable] internal sealed class NotificationsResponse { public AccountNotification[] notifications; }

    public static class LeaderboardRules
    {
        public static string CleanName(string name)
        {
            var result = new System.Text.StringBuilder();
            foreach (char character in (name ?? "").Trim())
                if (char.IsLetterOrDigit(character) || character == ' ' || character == '_' || character == '-')
                { result.Append(character); if (result.Length == 18) break; }
            return result.ToString().Trim();
        }
        public static bool Valid(VictoryScore score) => score != null && score.level >= 1 && score.level <= 5 &&
            score.score >= 0 && score.baseHp >= 1 && score.baseHp <= 20 && score.kills >= 0 &&
            !float.IsNaN(score.combatSeconds) && !float.IsInfinity(score.combatSeconds) && score.combatSeconds >= 0;
    }
}
