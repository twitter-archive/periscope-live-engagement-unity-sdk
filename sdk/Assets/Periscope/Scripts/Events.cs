using System.Collections.Generic;
using UnityEngine;

namespace Periscope
{
    public abstract class Event
    {
        public enum Type
        {
            Heart,
            Chat,
            Join,
            DirectMessage,
            ViewerCount,
            Unknown,
        };
        public Type eventType;
        public User user;
        string colorHexCode;

        static Dictionary<string, string> colorNameToHexCode = new Dictionary<string, string>
        {
            // For more info: https://www.periscope.tv/brand-4
            { "fuel yellow", "#F5A623" },
            { "rich lilac", "#AB70D4" },
            { "pistachio", "#99CE62" },
            { "indigo", "#5C75DC" },
            { "cranberry", "#D54D81" },
            { "chenin", "#DED569" },
            { "medium aquamarine", "#5ED5B1" },
            { "light orchid", "#E697DC" },
            { "mckenzie", "#92643E" },
            { "vivid tangerine", "#FFA98A" },
            { "sky blue", "#84E1EB" },
            { "fern", "#69AB63" },
            { "carnation", "#F85B5B" },
        };

        static Dictionary<string, int> colorHexCodeToIdx = new Dictionary<string, int>
        {
            { "#F5A623", 1 },  // fuel yellow
            { "#AB70D4", 2 },  // rich lilac
            { "#99CE62", 3 },  // pistachio
            { "#5C75DC", 4 },  // indigo
            { "#D54D81", 5 },  // cranberry
            { "#DED569", 6 },  // chenin
            { "#5ED5B1", 7 },  // medium aquamarine
            { "#E697DC", 8 },  // light orchid
            { "#92643E", 9 },  // mckenzie
            { "#FFA98A", 10 }, // vivid tangerine
            { "#84E1EB", 11 }, // sky blue
            { "#69AB63", 12 }, // fern
            { "#F85B5B", 13 }, // carnation
        };

        public int ColorIdx
        {
            get
            {
                return colorHexCodeToIdx[ColorHexCode];
            }
        }

        public string ColorHexCode
        {
            get
            {
                return colorHexCode;
            }

            set
            {
                if (value.StartsWith("#", System.StringComparison.Ordinal))
                {
                    colorHexCode = value;
                }
                else if (colorNameToHexCode.ContainsKey(value))
                {
                    colorHexCode = colorNameToHexCode[value];
                }
                else
                {
                    colorHexCode = colorNameToHexCode["fuel yellow"];
                }
            }
        }

        public Color Color()
        {
            Color color;
            bool success = ColorUtility.TryParseHtmlString(ColorHexCode, out color);
            if (success && colorHexCode != null)
            {
                return color;
            }
            return new Color();
        }
    }

    public class HeartEvent : Event
    {
        public HeartEvent(User user, string color)
        {
            this.user = user;
            this.ColorHexCode = color;
            this.eventType = Type.Heart;
        }
    }

    public class ChatEvent : Event
    {
        public string message;
        public ChatEvent(User user, string color, string message)
        {
            this.user = user;
            this.ColorHexCode = color;
            this.message = message;
            this.eventType = Type.Chat;
        }
    }
    public class JoinEvent : Event
    {
        public JoinEvent(User user, string color)
        {
            this.user = user;
            this.ColorHexCode = color;
            this.eventType = Type.Join;
        }
    }

    public class DirectMessageEvent : Event
    {
        public string recipientUserIds; // comma seperated
        public string message;
        public DirectMessageEvent(User user, string color, string recipientUserIds, string message)
        {
            this.user = user;
            this.ColorHexCode = color;
            this.recipientUserIds = recipientUserIds;
            this.message = message;
        }
    }
}
