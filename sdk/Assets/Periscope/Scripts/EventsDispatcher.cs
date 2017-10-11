namespace Periscope
{
    public class EventsDispatcher : EventsProcessor
    {
        [System.Serializable]
        public class ChatReceivedEvent : UnityEngine.Events.UnityEvent<ChatEvent> { }
        public ChatReceivedEvent[] OnChatReceived;

        [System.Serializable]
        public class HeartReceivedEvent : UnityEngine.Events.UnityEvent<HeartEvent> { }
        public HeartReceivedEvent[] OnHeartReceived;

        [System.Serializable]
        public class JoinReceivedEvent : UnityEngine.Events.UnityEvent<JoinEvent> { }
        public JoinReceivedEvent[] OnJoinReceived;

        public override void OnPeriscopeJoinEvent(User user, string color)
        {
            foreach (JoinReceivedEvent ev in OnJoinReceived)
            {
                ev.Invoke(new JoinEvent(user, color));
            }
        }

        public override void OnPeriscopeChatEvent(User user, string color, string message)
        {
            foreach (ChatReceivedEvent ev in OnChatReceived)
            {
                ev.Invoke(new ChatEvent(user, color, message));
            }
        }

        public override void OnPeriscopeHeartEvent(User user, string color)
        {
            foreach (HeartReceivedEvent ev in OnHeartReceived)
            {
                ev.Invoke(new HeartEvent(user, color));
            }
        }

        void Start()
        {
            for (int i = 0; i < OnChatReceived.Length; i++)
            {
                if (OnChatReceived[i] == null)
                {
                    OnChatReceived[i] = new ChatReceivedEvent();
                }
            }
            for (int i = 0; i < OnHeartReceived.Length; i++)
            {
                if (OnHeartReceived[i] == null)
                {
                    OnHeartReceived[i] = new HeartReceivedEvent();
                }
            }
            for (int i = 0; i < OnJoinReceived.Length; i++)
            {
                if (OnJoinReceived[i] == null)
                {
                    OnJoinReceived[i] = new JoinReceivedEvent();
                }
            }
        }
    }
}