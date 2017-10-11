using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace Periscope
{
    public enum AuthenticationStatus
    {
        NotAuthenticated,
        Waiting,
        Authenticated
    }

    public enum BroadcastStatus
    {
        NotStarted,
        ReadyToStream,
        Live
    }

    public enum ConnectionStatus
    {
        NotConnected,
        Connecting,
        Connected
    };

    public class APIManager : MonoBehaviour
    {
        #region Public Fields / Functions
        /* 
         * Public Fields / Functions
         */

        public static APIManager Instance { get { return singleton; } }
        public string Error { get { return error; } }

        // authentication related
        public string AuthCode
        {
            get
            {
                if (AuthenticationStatus == AuthenticationStatus.Waiting && 
                    !String.IsNullOrEmpty(authCode))
                {
                    return authCode;
                }
                return null;
            }
        }
        public string AuthUrl { get { return String.IsNullOrEmpty(AuthCode) ? null : authUrl; } }
        public string AccessToken { get { return accessToken; } }
        public AuthenticationStatus AuthenticationStatus
        {
            get
            {
                return useLocalhost ? AuthenticationStatus.Authenticated : authenticationStatus;
            }
        }

        public void Authenticate(string accessToken = "")
        {
            authCode = null;
            authUrl = null;
            deviceCode = null;
            authenticationStatus = AuthenticationStatus.NotAuthenticated;
            if (String.IsNullOrEmpty(accessToken))
            {
                StartCoroutine(SendDeviceCodeCreateRequest());
            }
            else
            {
                StartCoroutine(ValidateAccessToken(accessToken));
            }
        }

        public void Unauthenticate()
        {
            authCode = null;
            authUrl = null;
            deviceCode = null;
            accessToken = null;
            streamKey = null;
            streamUrl = null;
            authenticationStatus = AuthenticationStatus.NotAuthenticated;
            broadcastStatus = BroadcastStatus.NotStarted;
            connectionStatus = ConnectionStatus.NotConnected;
        }

        // broadcast related
        public BroadcastStatus BroadcastStatus { get { return broadcastStatus; } }
        public ConnectionStatus ConnectionStatus { get { return connectionStatus; } }
        public string StreamUrl { get { return streamUrl; } }
        public string StreamKey { get { return streamKey; } }

        public void GetStreamInfo()
        {
            Assert.IsTrue(AuthenticationStatus == AuthenticationStatus.Authenticated);
            if (useLocalhost)
            {
                return;
            }
            StartCoroutine(CreateBroadcast());
        }

        public void GoLive(string title = "", bool shouldNotTweet = false)
        {
            Assert.IsTrue(AuthenticationStatus == AuthenticationStatus.Authenticated);
            Assert.IsTrue(BroadcastStatus == BroadcastStatus.ReadyToStream);
            StartCoroutine(PublishBroadcast(title, shouldNotTweet));
            StartCoroutine(ConnectToBroadcast());
        }

        public void EndBroadcast()
        {
            Assert.IsTrue(AuthenticationStatus == AuthenticationStatus.Authenticated);
            Assert.IsTrue(BroadcastStatus == BroadcastStatus.Live);
            StartCoroutine(StopBroadcast());
        }

        // events (chats, hearts etc.) related
        public void FlushUserCache()
        {
            lock (userCache)
            {
                userCache.Clear();
            }
        }

        public void Connect()
        {
            Assert.IsTrue(AuthenticationStatus == AuthenticationStatus.Authenticated);
            StartCoroutine(ConnectToBroadcast());
        }

        public void Disconnect()
        {
            broadcastId = null;
            connectionStatus = ConnectionStatus.NotConnected;
            broadcastStatus = BroadcastStatus.NotStarted;
            StopAllCoroutines();
            if (websocket != null)
            {
                websocket.Close();
            }
        }

        // DMs related
        public void OnSendDirectMessage(DirectMessageEvent ev)
        {
            if (string.IsNullOrEmpty(accessToken)) return;
            if (ev.recipientUserIds.Contains(", ") && priorityDmOutbox.Count < maxQueuedDms)
            {
                // place all group DMs to priority outbox as long as there's room
                lock (priorityDmOutbox)
                {
                    priorityDmOutbox.Enqueue(ev);
                }
            }
            else if (regularDmOutbox.Count < maxQueuedDms)
            {
                // place all the rest regular outbox as long as there's room
                lock (regularDmOutbox)
                {
                    regularDmOutbox.Enqueue(ev);
                }
            }
            else
            {
                // drop DMs if there's no room in either outbox
                droppedDmCount++;
            }
        }

        #endregion

        #region Unity Editor Visible Fields
        /*
         * Unity Editor Visible Fields
         */
        [Header("Dev Params")]
        public string clientId = "";
        public bool useDevServers;
        public bool useLocalhost;
        public int localhostPort = 8080;

        [Header("General Stats")]
        [ReadOnly] public double fps;

        [Header("FPS-based Event Throttling Params")]
        public bool eventThrottlingOff;               // CAUTION: enabling this in production will risk performance
        public double minAcceptableFps = 50.0;        // ideal value depends on if mobile (20.0), desktop(50.0) or VR (75.0)
        public int maxQueuedEvents = 1000;            // when queue is full events are dropped
        public int maxBatchSize = 150;                // maximum number of events processed in batch
        public float maxSnoozeTimeInSec = 0.25f;      // amount of time to snooze between each 
                                                      // batch if fps < minAcceptableFps

        [Header("Incoming Event Stats")]
        [ReadOnly] public int processedEventCount;
        [ReadOnly] public int queuedEventCount;
        [ReadOnly] public int droppedEventCount;
        [ReadOnly] public double processedEventsPerSec;
        [ReadOnly] public float currentBatchSize = 1.0f;
        [ReadOnly] public float currentSnoozeTimeInSec;
        [ReadOnly] public int numHeartsProcessed;
        [ReadOnly] public int numChatsProcessed;
        [ReadOnly] public int numJoinsProcessed;

        [Header("Outgoing DM Params")]
        public int maxQueuedDms = 1000;               // only affects regular DMs
        public int maxDmsInFlight = 100;              // max number of simulataneous HTTP requests for DMs

        [Header("Outgoing DM Stats")]
        [ReadOnly] public int dmsInFlight;
        [ReadOnly] public int processedDmCount;
        [ReadOnly] public int deliveredDmCount;
        [ReadOnly] public int droppedDmCount;
        [ReadOnly] public int queuedRegularDmCount;
        [ReadOnly] public int queuedPriorityDmCount;  // these DMs are always processed, 
                                                      // never dropped, take priority over regular DMs

        #endregion

        #region Private Fields / Functions
        /* 
         * Private Fields / Functions
         */
        static APIManager singleton;
        EventsProcessor[] eventProcessors;
        Dictionary<int, User> userCache = new Dictionary<int, User>();
        string error;
        WebSocket websocket;
        const string domain = "pscp.tv";
        string APIHttpHost
        {
            get
            {
                return String.Format("https://{0}api.{1}", useDevServers ? "dev-" : "", domain);
            }
        }
        string APIWebsocketHost
        {
            get
            {
                return String.Format("wss://{0}api-ws.{1}", useDevServers ? "dev-" : "", domain);
            }
        }
        static string meEndpointPath = "/v1/me";
        static string deviceCodeCreateEndpointPath = "/v1/device_code/create";
        static string deviceCodeCheckEndpointPath = "/v1/device_code/check";
        static string broadcastsEndpointPath = "/v1/me/broadcasts";
        static string regionEndpointPath = "/v1/region";
        static string broadcastCreateEndpointPath = "/v1/broadcast/create";
        static string broadcastPublishEndpointPath = "/v1/broadcast/publish";
        static string broadcastStopEndpointPath = "/v1/broadcast/stop";
        static string dmEndpointPath = "/v1/chat/dm";
        static string chatConnectEndpointPath = "/v1/chat/connect";

        Uri ChatConnectEndpointUri(string _broadcastId)
        {
            if (useLocalhost)
            {
                return new Uri(String.Format("ws://localhost:{0}", localhostPort));
            }
            else
            {
                string form = "broadcast_id=" + _broadcastId;
                return new Uri(APIWebsocketHost + chatConnectEndpointPath + "?" + form);
            }
        }

        delegate void HandleSuccessFunc<ResponseType>(ResponseType obj);
        delegate void HandleFailureFunc(APIResponse req);

        IEnumerator HandleAPIRequest<ResponseType>(
            string host,
            string endpoint,
            string payload,
            string token,
            HandleSuccessFunc<ResponseType> successFunc,
            HandleFailureFunc failureFunc)
        {
            var req = new APIRequest(host, endpoint, payload, token);
            yield return req.Send();
            var resp = GetAPIResponseWithAuthCheck(req);

            if (resp.success)
            {
                successFunc(JsonUtility.FromJson<ResponseType>(resp.json));
            }
            else
            {
                failureFunc(resp);
            }
        }

        // authentication related
        string authCode;
        string authUrl;
        string deviceCode;
        int deviceCodeCheckInterval;
        string accessToken = "";
        AuthenticationStatus authenticationStatus = AuthenticationStatus.NotAuthenticated;

        APIResponse GetAPIResponseWithAuthCheck(APIRequest req)
        {
            Assert.IsTrue(req.Completed);
            var resp = new APIResponse(req);
            if (req.authenticated && resp.authFailed)
            {
                authenticationStatus = AuthenticationStatus.NotAuthenticated;
                error = "Authentication failed. Error: " + resp.error;
                Disconnect();
            }
            return resp;
        }

        IEnumerator ValidateAccessToken(string token)
        {
            Assert.IsTrue(!String.IsNullOrEmpty(token));
            yield return HandleAPIRequest<User>(
                APIHttpHost,
                meEndpointPath,
                "",
                token,
                (User u) =>
                {
                    authenticationStatus = AuthenticationStatus.Authenticated;
                    accessToken = token;
                },
                (APIResponse r) =>
                {
                    authenticationStatus = AuthenticationStatus.NotAuthenticated;
                }
            );
        }

        // broadcast related
        string region;
        string broadcastId;
        string streamUrl;
        string streamKey;
        BroadcastStatus broadcastStatus = BroadcastStatus.NotStarted;
        ConnectionStatus connectionStatus = ConnectionStatus.NotConnected;

        IEnumerator ConnectToBroadcast()
        {
            connectionStatus = ConnectionStatus.Connecting;

            int retryCount = 0;
            yield return StartCoroutine(SearchForLiveBroadcast());
            while (retryCount < 3 && (broadcastStatus != BroadcastStatus.Live))
            {
                yield return new WaitForSecondsRealtime(1.0f);
                yield return StartCoroutine(SearchForLiveBroadcast());
                retryCount++;
            }

            string err = null;
            if (broadcastStatus == BroadcastStatus.Live)
            {
                // Try to open the websocket
                websocket = new WebSocket(
                    ChatConnectEndpointUri(broadcastId), 
                    maxQueuedEvents,
                    accessToken);
                yield return StartCoroutine(websocket.Connect());

                // Check if websocket is connected
                if (String.IsNullOrEmpty(websocket.Error) && websocket.IsConnected)
                {
                    // Start receiving messages
                    connectionStatus = ConnectionStatus.Connected;
                    lastEventReceivedTime = DateTime.UtcNow;
                    UserHasher.Instance.SetSeed(broadcastId);
                    Debug.Log("Started connection to broadcast.");
                }
                else
                {
                    err = "Cannot connect to broadcast. Error: " + websocket.Error;
                }
            }
            else
            {
                err = "Cannot connect to broadcast. Error: No live broadcast found.";
            }

            if (!String.IsNullOrEmpty(err))
            {
                error = err;
                Disconnect();
            }
            else if (connectionStatus == ConnectionStatus.Connected)
            {
                StartCoroutine(ReceiveEvents());
                StartCoroutine(ProcessQueuedDMs());
            }
        }

        IEnumerator ReceiveEvents()
        {
            eventProcessors = FindObjectsOfType<EventsProcessor>();
            int batchedEvents = 0;
            while (connectionStatus == ConnectionStatus.Connected)
            {
                Assert.IsNotNull(websocket);

                string str = websocket.Pop();
                if (str != null)
                {
                    lastEventReceivedTime = DateTime.UtcNow;
                    ProcessEvent(str);

                    if (eventThrottlingOff || batchedEvents < currentBatchSize)
                    {
                        batchedEvents++;
                    }
                    else
                    {
                        batchedEvents = 0;
                        if (currentSnoozeTimeInSec > 0.0)
                        {
                            yield return new WaitForSecondsRealtime(currentSnoozeTimeInSec);
                        }
                        else
                        {
                            yield return null;
                        }
                    }
                }
                else
                {
                    if (!websocket.IsConnected || !String.IsNullOrEmpty(websocket.Error))
                    {
                        error = "Disconnected from websocket. Error: " + websocket.Error;
                        connectionStatus = ConnectionStatus.NotConnected;
                        break;
                    }
                    var timeDiff = (DateTime.UtcNow - lastEventReceivedTime).TotalMilliseconds;
                    if (timeDiff > 60000)
                    {
                        error = "Websocket connection has been stale. Shutting down connection.";
                        connectionStatus = ConnectionStatus.NotConnected;
                    }
                    yield return new WaitForSecondsRealtime(0.1f);
                }
            }
            if (websocket != null)
            {
                websocket.Close();
            }
            websocket = null;

            if (broadcastStatus == BroadcastStatus.Live)
            {
                StartCoroutine(ConnectToBroadcast());
            }
        }

        // events (chats, hearts etc.) related
        double secondsSinceLastUpdateCall;
        DateTime lastEventReceivedTime;
        DateTime lastStatUpdateTime;
        int numberOfProcessedEventsPrev;
        readonly static Regex eventErrorRegex = new Regex(
            @"{" + 
            @"""type"":""error""," + 
            @"""description"":""(?<error>.+?)""" + 
            @"}");
        readonly static Regex viewerRegex = new Regex(
            @"{" + 
            @"""type"":""viewer_count""," + 
            @"""live"":(?<live>\d+?)," + 
            @"""total"":(?<total>\d+?)" + 
            @"}");
        readonly static Regex heartRegex = new Regex(
            @"{" + 
            @"""id"":""(?<msg_id>[\w-]+?)""," + 
            @"""type"":""heart""," + 
            @"""user"":{""id"":""(?<user_id>\w+?)""}," + 
            @"""color"":""#(?<color>\w+?)""" + 
            @"}");
        readonly static Regex chatRegex = new Regex(
            @"{" + 
            @"""id"":""(?<msg_id>[\w-]+?)""," + 
            @"""type"":""chat""," + 
            @"""text"":""(?<text>.+?)""," + 
            @"""user"":{""id"":""(?<user_id>\w+?)""," + 
            @"""username"":""(?<username>.+?)""," + 
            @"""display_name"":""(?<display_name>.+?)""," + 
            @"""profile_image_urls"":\[{""url"":""(?<profile_image_url>.+?)""}\],.*}," + 
            @"""color"":""#(?<color>\w+)""" + 
            @"}");
        readonly static Regex joinRegex = new Regex(
            @"{" + 
            @"""id"":""(?<msg_id>[\w-]+?)""," + 
            @"""type"":""join""," + 
            @"""user"":{""id"":""(?<user_id>\w+?)""," + 
            @"""username"":""(?<username>.+?)""," + 
            @"""display_name"":""(?<display_name>.+?)""," + 
            @"""profile_image_urls"":\[{""url"":""(?<profile_image_url>.+?)""}\],.*}," + 
            @"""color"":""#(?<color>\w+)""" + 
            @"}");

        void CacheOrUpdateUser(int userHash, User user, string id, string username, string profile_image_url)
        {
            // either provide id, username, profile_image_url combo, or user (user has precedence)
            if (userCache.ContainsKey(userHash))
            {
                if (String.IsNullOrEmpty(userCache[userHash].username))
                {
                    if (user != null && !String.IsNullOrEmpty(user.username))
                    {
                        userCache[userHash].username = user.username;
                        userCache[userHash].profileImageUrls = user.profileImageUrls;
                    }
                    else if (String.IsNullOrEmpty(username))
                    {
                        userCache[userHash].username = username;
                        userCache[userHash].profileImageUrls = new ProfileImageUrl[1] {
                            new ProfileImageUrl(profile_image_url)
                        };
                    }
                }
            }
            else
            {
                if (user != null)
                {
                    userCache[userHash] = user;
                }
                else
                {
                    userCache[userHash] = new User(id, username, profile_image_url);
                }
            }
        }

        void ProcessEvent(string content)
        {
            processedEventCount++;
            if (!ProcessEventUsingRegex(content))
            {
                ProcessEventUsingJson(content);
            }
        }

        Event.Type EvaluateRegex(
            string content, 
            out string id, 
            out string username, 
            out string profile_image_url, 
            out string color, 
            out string message)
        {
            id = "";
            username = "";
            profile_image_url = "";
            color = "";
            message = "";
            Match m = heartRegex.Match(content);
            if (m.Success)
            {
                id = m.Groups["user_id"].Value;
                color = String.Format("#{0}", m.Groups["color"].Value);
                return Event.Type.Heart;
            }

            m = chatRegex.Match(content);
            if (m.Success)
            {
                id = m.Groups["user_id"].Value;
                username = m.Groups["username"].Value;
                profile_image_url = m.Groups["profile_image_url"].Value;
                color = String.Format("#{0}", m.Groups["color"].Value);
                message = m.Groups["message"].Value;
                return Event.Type.Chat;
            }

            m = joinRegex.Match(content);
            if (m.Success)
            {
                id = m.Groups["user_id"].Value;
                username = m.Groups["username"].Value;
                profile_image_url = m.Groups["profile_image_url"].Value;
                color = String.Format("#{0}", m.Groups["color"].Value);
                return Event.Type.Join;
            }

            m = viewerRegex.Match(content);
            if (m.Success)
            {
                return Event.Type.ViewerCount;
            }

            m = eventErrorRegex.Match(content);
            if (m.Success)
            {
                error = "Error message received: " + m.Groups["error"];
            }

            return Event.Type.Unknown;
        }

        bool ProcessEventUsingRegex(string content)
        {
            // optimization to avoid json parsing if possible
            string id;
            string username;
            string profile_image_url;
            string color;
            string message;
            Event.Type type = EvaluateRegex(
                content, 
                out id, 
                out username, 
                out profile_image_url, 
                out color, 
                out message);
            if (type != Event.Type.Unknown)
            {
                var userHash = UserHasher.Instance.ComputeUserHash(id);
                CacheOrUpdateUser(userHash, null, id, username, profile_image_url);
                switch (type)
                {
                    case Event.Type.Heart:
                        numHeartsProcessed++;
                        foreach (EventsProcessor processor in this.eventProcessors)
                        {
                            processor.OnPeriscopeHeartEvent(userCache[userHash], color);
                        }
                        break;
                    case Event.Type.Chat:
                        numChatsProcessed++;
                        foreach (EventsProcessor processor in this.eventProcessors)
                        {
                            processor.OnPeriscopeChatEvent(userCache[userHash], color, message);
                        }
                        break;
                    case Event.Type.Join:
                        numChatsProcessed++;
                        foreach (EventsProcessor processor in this.eventProcessors)
                        {
                            processor.OnPeriscopeJoinEvent(userCache[userHash], color);
                        }
                        break;
                }
                return true;
            }
            return false;
        }

        void ProcessEventUsingJson(string content)
        {
            var msg = JsonUtility.FromJson<WebSocketEvent>(content);
            if (msg.user == null || String.IsNullOrEmpty(msg.user.id))
            {
                return;
            }
            CacheOrUpdateUser(msg.user.Hash, msg.user, null, null, null);

            switch (msg.type)
            {
                case "heart":
                case "super_heart":
                    numHeartsProcessed++;
                    foreach (EventsProcessor processor in this.eventProcessors)
                    {
                        processor.OnPeriscopeHeartEvent(userCache[msg.user.Hash], msg.color);
                    }
                    break;
                case "chat":
                    if (String.IsNullOrEmpty(msg.text))
                    {
                        return;
                    }
                    numChatsProcessed++;
                    foreach (EventsProcessor processor in this.eventProcessors)
                    {
                        processor.OnPeriscopeChatEvent(userCache[msg.user.Hash], msg.color, msg.text);
                    }
                    break;
                case "join":
                    numJoinsProcessed++;
                    foreach (EventsProcessor processor in this.eventProcessors)
                    {
                        processor.OnPeriscopeJoinEvent(userCache[msg.user.Hash], msg.color);
                    }
                    break;
            }
        }

        void UpdateStats()
        {
            var t = (DateTime.UtcNow - lastStatUpdateTime).TotalMilliseconds;
            if (t > 1000.0f)
            {
                lastStatUpdateTime = DateTime.UtcNow;
                processedEventsPerSec = (processedEventCount - numberOfProcessedEventsPrev) * 1000.0f / t;
                numberOfProcessedEventsPrev = processedEventCount;
            }
        }

        // DMs related
        System.Object dmLock = new System.Object();
        Queue<DirectMessageEvent> regularDmOutbox;
        Queue<DirectMessageEvent> priorityDmOutbox;

        IEnumerator ProcessQueuedDMs()
        {
            while (connectionStatus == ConnectionStatus.Connected)
            {
                if (regularDmOutbox.Count > 0 || priorityDmOutbox.Count > 0)
                {
                    lock (dmLock)
                    {
                        if (dmsInFlight < maxDmsInFlight)
                        {
                            // send a DM
                            if (priorityDmOutbox.Count > 0)
                            {
                                // prioritize priority outbox DMs
                                lock (priorityDmOutbox)
                                {
                                    StartCoroutine(SendDM(priorityDmOutbox.Dequeue()));
                                }
                            }
                            else
                            {
                                lock (regularDmOutbox)
                                {
                                    StartCoroutine(SendDM(regularDmOutbox.Dequeue()));
                                }
                            }
                            dmsInFlight++;
                        }
                    }
                }
                yield return null;
            }
        }

        #endregion

        #region Helper Classes
        /* 
         * Helper Classes
         */
        class APIRequest
        {
            readonly UnityWebRequest web;
            public bool authenticated;

            public bool Completed { get { return web.isDone; } }
            public string Error { get { return web.error; } }
            public long ResponseCode { get { return web.responseCode; } }
            public string ResponseText { get { return web.downloadHandler.text; } }

            APIRequest(UnityWebRequest web, bool authenticated)
            {
                this.web = web;
                this.authenticated = authenticated;
            }
            public APIRequest(string host, string endpointPath, string payload, string accessToken)
            {
                string url = host + endpointPath;
                web = UnityWebRequest.Post(url, payload);
                web.uploadHandler = new UploadHandlerRaw(
                    String.IsNullOrEmpty(payload) ? null : Encoding.UTF8.GetBytes(payload));
                authenticated = !String.IsNullOrEmpty(accessToken);
                if (authenticated)
                {
                    web.SetRequestHeader("Authorization", "Bearer " + accessToken);
                }
            }
            public AsyncOperation Send()
            {
                return web.Send();
            }
        }

        class APIResponse
        {
            readonly static Regex errorRegex = new Regex(
                @"{" + 
                @"""error"":""(?<error>.+?)""," + 
                @"""error_description"":""(?<error_description>.+?)""" + 
                @"}");
            public bool success;
            public string json;
            public string error;
            public bool authFailed;
            public APIResponse(APIRequest req)
            {
                if (req != null)
                {
                    if (req.Completed)
                    {
                        if (String.IsNullOrEmpty(req.Error))
                        {
                            string resp = req.ResponseText;
                            if (req.ResponseCode >= 200 && req.ResponseCode < 300)
                            {
                                // success
                                success = true;
                                json = resp;
                            }
                            else if (req.authenticated && req.ResponseCode == 401)
                            {
                                authFailed = true;
                                error = "authentication failed";
                            }
                            else
                            {
                                // try to parse the response based on Periscope error json structure
                                var m = errorRegex.Match(resp);
                                if (m.Success)
                                {
                                    error = String.Format(
                                        "{0}: {1}",
                                        m.Groups["error"],
                                        m.Groups["error_description"]);
                                }
                                else
                                {
                                    error = String.Format("response code {0}", req.ResponseCode);
                                }
                            }
                        }
                        else
                        {
                            error = req.Error;
                        }
                    }
                    else
                    {
                        error = "request is not complete";
                    }
                }
                else
                {
                    error = "request is null";
                }
            }
        }

        #endregion

        #region API Structures
        /* 
         * API Structures
         */
        [System.Serializable]
        class EmptyResponse
        {

        }

        [Serializable]
        class DeviceCodeCreateResponse
        {
            public string user_code;
            public string device_code;
            public int expires_in;
            public int interval;
            public string associate_url;
        }

        [Serializable]
        class DeviceCodeCheckResponse
        {
            public string state;
            public string access_token;
            public int expires_in;
            public string refresh_token;
            public User user;
            public string scope;
        }

        [System.Serializable]
        class RegionResponse
        {
            public string region;
        }

        [System.Serializable]
        class Broadcast
        {
            public string id;
            public string state;
            public string title;
        }

        [System.Serializable]
        class Encoder
        {
            public string stream_key;
            public string rtmp_url;
        }

        [System.Serializable]
        class BroadcastCreateResponse
        {
            public Broadcast broadcast;
            public Encoder encoder;
        }

        [System.Serializable]
        class BroadcastPublishResponse
        {
            public Broadcast broadcast;
        }

        [System.Serializable]
        class BroadcastsResponse
        {
            public Broadcast[] broadcasts;
        }

        [System.Serializable]
        class WebSocketEvent
        {
            public string id;
            public string type;
            public string text; // for chat message only
            public User user;
            public string color;
            public int amount; // super heart message only
            public int tier; // super heart message only
            public int live; // viewer count message only
            public int total; // viewer count message only
        }


        #endregion

        #region API Calls
        /* 
         * API Calls
         */
        IEnumerator SendDeviceCodeCreateRequest()
        {
            if (!String.IsNullOrEmpty(clientId))
            {
                authenticationStatus = AuthenticationStatus.Waiting;
                string payload = String.Format(
                    @"{{""client_id"":""{0}"",""scope"":""chat chat_dm""}}",
                    clientId);
                yield return HandleAPIRequest<DeviceCodeCreateResponse>(
                    APIHttpHost,
                    deviceCodeCreateEndpointPath,
                    payload,
                    null,
                    (DeviceCodeCreateResponse r) =>
                    {
                        if (r != null &&
                        !String.IsNullOrEmpty(r.user_code) &&
                        !String.IsNullOrEmpty(r.device_code) &&
                        !String.IsNullOrEmpty(r.associate_url) &&
                        r.interval > 0)
                        {
                            authCode = r.user_code;
                            authUrl = "https://" + r.associate_url + "?user_code=" + r.user_code;
                            deviceCode = r.device_code;
                            deviceCodeCheckInterval = r.interval;
                            StartCoroutine(CheckDeviceCode());
                        }
                    },
                    (APIResponse r) =>
                    {
                        authenticationStatus = AuthenticationStatus.NotAuthenticated;
                        error = "Authentication failed. Error: " + r.error;
                    });
            }
        }

        IEnumerator CheckDeviceCode()
        {
            string err = "";
            if (!String.IsNullOrEmpty(deviceCode) && deviceCodeCheckInterval > 0)
            {
                yield return new WaitForSecondsRealtime(deviceCodeCheckInterval);
                string payload = String.Format(@"{{""device_code"":""{0}""}}", deviceCode);
                yield return HandleAPIRequest<DeviceCodeCheckResponse>(
                    APIHttpHost,
                    deviceCodeCheckEndpointPath,
                    payload,
                    null,
                    (DeviceCodeCheckResponse r) =>
                    {
                        if (r != null && !String.IsNullOrEmpty(r.state))
                        {
                            if (r.state == "authorization_pending")
                            {
                                StartCoroutine(CheckDeviceCode());
                            }
                            else if (r.state == "associated" && !String.IsNullOrEmpty(r.access_token))
                            {
                                accessToken = r.access_token;
                                authenticationStatus = AuthenticationStatus.Authenticated;
                            }
                            else
                            {
                                authenticationStatus = AuthenticationStatus.NotAuthenticated;
                                err = String.Format("device code " + r.state);
                            }
                        }
                    },
                    (APIResponse r) =>
                    {
                        authenticationStatus = AuthenticationStatus.NotAuthenticated;
                        err = r.error;
                    });
            }
            if (authenticationStatus == AuthenticationStatus.NotAuthenticated)
            {
                error = "Authentication failed. Reason:" + err;
            }
            yield return null;
        }

        IEnumerator GetRegion()
        {
            yield return HandleAPIRequest<RegionResponse>(
                APIHttpHost,
                regionEndpointPath,
                "",
                AccessToken,
                (RegionResponse r) =>
                {
                    if (r != null && r.region != null)
                    {
                        region = r.region;
                    }
                },
                (APIResponse r) => { });
        }

        IEnumerator CreateBroadcast()
        {
            yield return StartCoroutine(GetRegion());
            Assert.IsTrue(!String.IsNullOrEmpty(region));

            bool is360 = false;
            string payload = String.Format(
                @"{{""region"":""{0}"",""is_360"":{1}}}",
                region,
                is360 ? "true" : "false");

            yield return HandleAPIRequest<BroadcastCreateResponse>(
                APIHttpHost,
                broadcastCreateEndpointPath,
                payload,
                AccessToken,
                (BroadcastCreateResponse r) =>
                {
                    if (r != null && r.broadcast != null && r.encoder != null)
                    {
                        broadcastId = r.broadcast.id;
                        streamKey = r.encoder.stream_key;
                        streamUrl = r.encoder.rtmp_url;
                        broadcastStatus = BroadcastStatus.ReadyToStream;
                    }
                },
                (APIResponse r) => { });
        }

        IEnumerator PublishBroadcast(string title, bool shouldNotTweet)
        {
            Assert.IsTrue(!String.IsNullOrEmpty(broadcastId));
            string payload = String.Format(
                @"{{""broadcast_id"":""{0}"",""title"":""{1}"",""should_not_tweet"":{2}}}",
                broadcastId,
                title,
                shouldNotTweet ? "true" : "false");

            yield return HandleAPIRequest<BroadcastPublishResponse>(
                APIHttpHost,
                broadcastPublishEndpointPath,
                payload,
                AccessToken,
                (BroadcastPublishResponse r) =>
                {
                    Assert.IsTrue(broadcastId.Equals(r.broadcast.id));
                    broadcastStatus = BroadcastStatus.Live;
                },
                (APIResponse r) => { });
        }

        IEnumerator StopBroadcast()
        {
            Assert.IsTrue(!String.IsNullOrEmpty(broadcastId));
            string payload = String.Format(@"{{""broadcast_id"":""{0}""}}", broadcastId);

            yield return HandleAPIRequest<EmptyResponse>(
                APIHttpHost, broadcastStopEndpointPath, payload, AccessToken,
                (EmptyResponse json) =>
                {
                    broadcastId = "";
                    broadcastStatus = BroadcastStatus.NotStarted;
                    Disconnect();
                },
                (APIResponse r) => { });
        }

        IEnumerator SearchForLiveBroadcast()
        {
            if (useLocalhost)
            {
                broadcastId = "localhost";
                broadcastStatus = BroadcastStatus.Live;
            }
            else if (broadcastStatus != BroadcastStatus.Live || String.IsNullOrEmpty(broadcastId))
            {
                yield return HandleAPIRequest<BroadcastsResponse>(
                    APIHttpHost, broadcastsEndpointPath, "", AccessToken,
                    (BroadcastsResponse r) =>
                    {
                        if (r != null && r.broadcasts != null)
                        {
                            foreach (Broadcast broadcast in r.broadcasts)
                            {
                                if (broadcast.state == "running")
                                {
                                    broadcastId = broadcast.id;
                                    broadcastStatus = BroadcastStatus.Live;
                                    break;
                                }
                            }
                        }
                    },
                    (APIResponse r) => { });
            }
        }

        static string unformattedDmPayload = @"{{" +
            @"""broadcast_id"":""{0}""," +
            @"""recipient_user_ids"":[{1}]," +
            @"""message"":""{2}""," +
            @"""sender_user_id"":""{3}""," +
            @"""sender_username"":""{4}""," +
            @"""sender_profile_image_url"":""{5}""," +
            @"""sender_participant_index"":{6}" +
            @"}}";

        IEnumerator SendDM(DirectMessageEvent ev)
        {
            
            if (!String.IsNullOrEmpty(ev.recipientUserIds) && !String.IsNullOrEmpty(ev.message))
            {
                string broadcasterId = "broadcaster";
                var emojifiedMessage = EmojiHandler.Instance.Emojify(ev.message);
                string payload = String.Format(
                    unformattedDmPayload,
                    broadcastId,
                    ev.recipientUserIds,
                    emojifiedMessage,
                    broadcasterId,
                    ev.user.username,
                    ev.user.ProfileImageUrl,
                    ev.ColorIdx);

                yield return HandleAPIRequest<EmptyResponse>(
                    APIHttpHost,
                    dmEndpointPath,
                    payload,
                    AccessToken,
                    (EmptyResponse r) =>
                    {
                        deliveredDmCount++;
                    },
                    (APIResponse r) => { });
                processedDmCount++;
            }
            lock (dmLock)
            {
                dmsInFlight--;
            }
            yield return null;
        }

        #endregion

        #region Unity Callbacks
        /*
         * Unity Callbacks
         */
        void Awake()
        {
            if (singleton == null)
            {
                DontDestroyOnLoad(gameObject);
                singleton = this;
            }
            else if (singleton != this)
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            regularDmOutbox = new Queue<DirectMessageEvent>();
            priorityDmOutbox = new Queue<DirectMessageEvent>();
            lastStatUpdateTime = DateTime.UtcNow;
            if (eventThrottlingOff)
            {
                maxQueuedEvents = 0;
            }
        }

        void Update()
        {
            double prevFps = 1.0 / secondsSinceLastUpdateCall;
            // next line is the moving average:
            // y = 0.9 * y + 0.1 * x
            // y == secondsSinceLastUpdateCall
            // x == Time.deltaTime
            secondsSinceLastUpdateCall += (Time.deltaTime - secondsSinceLastUpdateCall) * 0.1f; 
            fps = 1.0 / secondsSinceLastUpdateCall;

            if (!eventThrottlingOff)
            {
                if (fps < minAcceptableFps)
                {
                    if (Math.Abs(currentBatchSize - 1.0f) < Double.Epsilon)
                    {
                        currentSnoozeTimeInSec = Math.Min(maxSnoozeTimeInSec, currentSnoozeTimeInSec + 0.001f);
                    }
                    currentBatchSize = Math.Max(1.0f, currentBatchSize - 1.0f);
                }
                else if (prevFps < fps)
                {
                    if (currentSnoozeTimeInSec > 0.0f)
                    {
                        currentSnoozeTimeInSec = Math.Max(0.0f, currentSnoozeTimeInSec - 0.001f);
                    }
                    else
                    {
                        currentBatchSize = Math.Min(maxBatchSize, currentBatchSize + 0.05f);
                    }
                }
            }

            if (websocket != null)
            {
                droppedEventCount = websocket.NumDroppedEvents;
                queuedEventCount = websocket.NumQueuedEvents;
            }

            UpdateStats();
            queuedRegularDmCount = regularDmOutbox.Count;
            queuedPriorityDmCount = priorityDmOutbox.Count;
        }

        void OnDestroy()
        {
            Disconnect();
        }

        #endregion
    }
}