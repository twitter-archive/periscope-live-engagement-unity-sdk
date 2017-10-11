using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Periscope {
	
    public class EmojiHandler
	{
        static readonly Regex emojiRegex = new Regex(@"emj_(\w+)");
        readonly Dictionary<string, string> supportedEmojis = new Dictionary<string, string>()
        {
            { "wind", "1F4A8" },
            { "blackheart", "1F5A4" },
            { "yellowheart", "1F49B" },
            { "redheart", "1F496" },
            { "fuelpump", "26FD" },
            { "biceps", "1F4AA" },
            { "trophy", "1F3C6" },
            { "crazyface", "1F61C" },
            { "silvermedal", "1F948" },
            { "bronzemedal", "1F949" },
            { "alert", "26A0" },
            { "siren", "1F6A8" },
            { "bus", "1F68C" },
            { "redcar", "1F697" },
            { "rain", "2614" },
            { "sadface", "1F61E" },
            { "flag", "1F3C1" },
        };
        static EmojiHandler instance;

        public static EmojiHandler Instance {
            get{
                if (instance == null)
                {
                    instance = new EmojiHandler();
                }
                return instance;
            }
        }

        string HandleEmoji(string emojiName)
		{
			if (supportedEmojis.ContainsKey(emojiName))
			{
				var hexVal = Convert.ToInt32(supportedEmojis[emojiName], 16);
				return Char.ConvertFromUtf32(hexVal);
			}
			else
			{
				return "*" + emojiName + "*";
			}
		}

        public string Emojify(string content)
        {
            Match m = emojiRegex.Match(content);
            while (m.Success)
            {
                content = content.Replace(m.Groups[0].Value, Emojify(m.Groups[1].Value));
                m = m.NextMatch();
            }
            return content;
        }
	}
}
