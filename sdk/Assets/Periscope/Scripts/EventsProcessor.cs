using UnityEngine;

namespace Periscope
{
    abstract public class EventsProcessor : MonoBehaviour
    {
        public abstract void OnPeriscopeChatEvent(User user, string color, string message);
        public abstract void OnPeriscopeHeartEvent(User user, string color);
        public abstract void OnPeriscopeJoinEvent(User user, string color);
    }
}
