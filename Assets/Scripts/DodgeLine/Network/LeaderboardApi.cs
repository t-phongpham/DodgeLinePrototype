using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DodgeLine.Network
{
    // Player API only. No Unity service-account/admin secret belongs in this client.
    public sealed class LeaderboardApi
    {
        public LeaderboardConfig Config { get; }
        public string PlayerId { get; private set; }
        public string Error { get; private set; }
        public long HttpStatus { get; private set; }
        public string AccountNotices { get; private set; }
        public float RetryAt { get; private set; }
        private readonly string prefix;
        private string idToken;
        private float expiresAt;
        private bool authenticated;
        private sealed class Response { public long status; public string body, error; }
        public LeaderboardApi(LeaderboardConfig config, string savePrefix) { Config = config; prefix = savePrefix; }
        public void ForgetAccessToken() { authenticated = false; idToken = null; }

        public IEnumerator SignIn()
        {
            Error = null;
            if (!Config.IsValid) { Error = "Online leaderboard is not configured. Local play is available."; yield break; }
            if (authenticated && Time.realtimeSinceStartup < expiresAt) yield break;
            if (Time.realtimeSinceStartup < RetryAt) { Error = "Too many requests. Please wait before retrying."; yield break; }
            string session = PlayerPrefs.GetString(prefix + "Session", "");
            var response = new Response();
            yield return Request("POST", Config.authenticationUrl + "/authentication/" + (session.Length > 0 ? "session-token" : "anonymous"),
                session.Length > 0 ? JsonUtility.ToJson(new SessionRequest { sessionToken = session }) : "{}", false, response);
            if (response.error != null) { Fail(response, true); yield break; }
            AuthResponse auth = null;
            try { auth = JsonUtility.FromJson<AuthResponse>(response.body); } catch (ArgumentException) { }
            if (auth == null || string.IsNullOrEmpty(auth.userId) || string.IsNullOrEmpty(auth.idToken) || string.IsNullOrEmpty(auth.sessionToken) || auth.expiresIn <= 0)
            { Error = "Sign-in returned an invalid response. Please retry."; yield break; }
            PlayerId = auth.userId; idToken = auth.idToken;
            expiresAt = Time.realtimeSinceStartup + Mathf.Max(1, auth.expiresIn - 60);
            authenticated = true;
            PlayerPrefs.SetString(prefix + "Session", auth.sessionToken); PlayerPrefs.Save();
            if (!string.IsNullOrEmpty(auth.lastNotificationDate) && auth.lastNotificationDate != "0")
            {
                var notices = new Response();
                yield return Request("GET", Config.authenticationUrl + "/users/" + Escape(PlayerId) + "/notifications", null, true, notices);
                if (notices.error == null)
                {
                    try
                    {
                        var notifications = JsonUtility.FromJson<NotificationsResponse>(notices.body)?.notifications;
                        if (notifications != null) AccountNotices = string.Join("\n", Array.ConvertAll(notifications, n => n.message));
                    }
                    catch (ArgumentException) { AccountNotices = "Account notices could not be read. Please retry signing in."; }
                }
                else AccountNotices = "Account notices could not be loaded. Please retry later.";
            }
        }
        public IEnumerator Submit(VictoryScore victory, string displayName, Action<LeaderboardEntry> received)
        {
            if (!LeaderboardRules.Valid(victory)) { Error = "Only a completed stage can be submitted."; yield break; }
            yield return SignIn(); if (Error != null) yield break;
            var request = new ScoreRequest { score = victory.score, metadata = new ScoreMetadata {
                displayName = LeaderboardRules.CleanName(displayName), baseHp = victory.baseHp,
                kills = victory.kills, combatSeconds = victory.combatSeconds, rulesVersion = Config.rulesVersion
            } };
            var response = new Response();
            yield return Authorized("POST", PlayerUrl(victory.level), JsonUtility.ToJson(request), response);
            if (response.error != null) { Fail(response); yield break; }
            var entry = ParseEntry(response.body);
            if (entry == null) { Error = "Score response was invalid; it is saved for retry."; yield break; }
            received?.Invoke(entry);
        }
        public IEnumerator GetTop(int level, Action<LeaderboardPage> received)
        {
            yield return SignIn(); if (Error != null) yield break;
            var response = new Response();
            yield return Authorized("GET", BoardUrl(level) + "/scores?offset=0&limit=10&includeMetadata=true", null, response);
            if (response.error != null) { Fail(response); yield break; }
            LeaderboardPage page = null;
            try { page = JsonUtility.FromJson<LeaderboardPage>(response.body); } catch (ArgumentException) { }
            if (page?.results == null || page.total < 0 || !Array.TrueForAll(page.results, ValidEntry)) { Error = "Leaderboard response was invalid. Please retry."; yield break; }
            received?.Invoke(page);
        }
        public IEnumerator GetOwn(int level, Action<LeaderboardEntry> received)
        {
            yield return SignIn(); if (Error != null) yield break;
            var response = new Response();
            yield return Authorized("GET", PlayerUrl(level) + "?includeMetadata=true", null, response);
            if (response.status == 404) { received?.Invoke(null); yield break; }
            if (response.error != null) { Fail(response); yield break; }
            var entry = ParseEntry(response.body);
            if (entry == null) { Error = "Your rank could not be read. Please retry."; yield break; }
            received?.Invoke(entry);
        }
        private static LeaderboardEntry ParseEntry(string body)
        {
            try
            {
                var entry = JsonUtility.FromJson<LeaderboardEntry>(body);
                return ValidEntry(entry) ? entry : null;
            }
            catch (ArgumentException) { return null; }
        }
        private static bool ValidEntry(LeaderboardEntry entry) => entry != null && !string.IsNullOrEmpty(entry.playerId) && entry.rank >= 0 &&
            !double.IsNaN(entry.score) && !double.IsInfinity(entry.score);
        private IEnumerator Authorized(string method, string url, string body, Response response)
        {
            yield return Request(method, url, body, true, response);
            if (response.status != 401) yield break;
            ForgetAccessToken(); yield return SignIn();
            if (Error != null) { response.error = Error; yield break; }
            yield return Request(method, url, body, true, response);
        }
        private IEnumerator Request(string method, string url, string body, bool authorize, Response response)
        {
            response.error = null; response.body = null; response.status = 0;
            if (Time.realtimeSinceStartup < RetryAt) { response.error = "Wait before retrying."; yield break; }
            using (var request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Mathf.Clamp(Config.timeoutSeconds, 1, 30);
                request.SetRequestHeader("Accept", "application/json");
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (authorize) request.SetRequestHeader("Authorization", "Bearer " + idToken);
                if (!authorize || url.StartsWith(Config.authenticationUrl + "/", StringComparison.Ordinal))
                { request.SetRequestHeader("ProjectId", Config.projectId); request.SetRequestHeader("UnityEnvironment", Config.environment); }
                yield return request.SendWebRequest();
                HttpStatus = response.status = request.responseCode;
                response.body = request.downloadHandler.text;
                if (request.result != UnityWebRequest.Result.Success) response.error = request.error ?? "Request failed.";
                if (response.status == 429)
                {
                    float seconds = 30;
                    if (float.TryParse(request.GetResponseHeader("Retry-After"), out float parsed)) seconds = Mathf.Clamp(parsed, 1, 300);
                    RetryAt = Time.realtimeSinceStartup + seconds;
                }
            }
        }
        private void Fail(Response response, bool signingIn = false)
        {
            Error = response.status == 404 ? "Leaderboard is not set up yet. Local play and saved results are available." :
                response.status == 429 ? "Too many requests. Please wait before retrying." :
                response.status == 401 || response.status == 403 ? signingIn ? "Online sign-in was refused. Check the account/settings; local play is available." : "Online access was refused. Your score is saved for retry." :
                response.status >= 500 ? "Leaderboard service is temporarily unavailable. Please retry later." :
                response.status == 0 ? "Unable to reach leaderboard. Check your connection and retry." : "Leaderboard request failed. Please retry.";
        }
        private string BoardUrl(int level) => Config.scoresUrl + "/projects/" + Escape(Config.projectId) + "/leaderboards/" + Escape(Config.BoardId(level));
        private string PlayerUrl(int level) => BoardUrl(level) + "/scores/players/" + Escape(PlayerId);
        private static string Escape(string value) => UnityWebRequest.EscapeURL(value ?? "");
    }
}
