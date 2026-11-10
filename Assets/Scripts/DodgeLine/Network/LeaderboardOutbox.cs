using System;
using System.Collections.Generic;
using UnityEngine;

namespace DodgeLine.Network
{
    [Serializable]
    public sealed class LeaderboardOutbox
    {
        public List<VictoryScore> pending = new List<VictoryScore>();
        public bool Queue(VictoryScore result)
        {
            if (!LeaderboardRules.Valid(result)) return false;
            var previous = pending.Find(p => p.level == result.level);
            if (previous != null && previous.score >= result.score) return false;
            if (previous != null) pending.Remove(previous);
            pending.Add(result); return true;
        }
        public void Acknowledge(VictoryScore sent)
        {
            pending.RemoveAll(p => p.level == sent.level && p.score <= sent.score);
        }
        public void Save(string key) { PlayerPrefs.SetString(key, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public static LeaderboardOutbox Load(string key)
        {
            LeaderboardOutbox result;
            try { result = JsonUtility.FromJson<LeaderboardOutbox>(PlayerPrefs.GetString(key, "{}")); }
            catch (ArgumentException) { result = null; }
            var clean = new LeaderboardOutbox();
            if (result?.pending != null) foreach (var entry in result.pending) clean.Queue(entry);
            return clean;
        }
    }
}
