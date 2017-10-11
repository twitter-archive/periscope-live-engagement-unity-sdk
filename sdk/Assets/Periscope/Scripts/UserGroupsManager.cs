using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Periscope
{
    public class AggregatedHeartsEvent
    {
        public string groupName;
        public int numHearts;
        public AggregatedHeartsEvent(string groupName, int numHearts)
        {
            this.groupName = groupName;
            this.numHearts = numHearts;
        }
    }

	[System.Serializable]
	public class AnnounceAggregatedHearts : UnityEngine.Events.UnityEvent<AggregatedHeartsEvent>
    {
        
    }

	public class UserGroupsManager : EventsProcessor
    {
		#region Public Fields / Functions
		/* 
         * Public Fields / Functions
         */
		public void CreateUserGroup(
	        string name,
	        string color,
	        string profileImgUrl,
	        string[] joinMessages,
	        string[] leaveMessages,
	        string[] groupFullMessages,
	        string[] periodicMessages,
	        string[] heartResponseMessages,
	        string[] chatResponseMessages,
	        int maxSize = 0)
		{
			if (!groups.ContainsKey(name))
			{
				UserGroup newGroup = new UserGroup(
					name,
					color,
					profileImgUrl,
					joinMessages,
					leaveMessages,
					groupFullMessages,
					periodicMessages,
					heartResponseMessages,
					chatResponseMessages,
					maxSize);
				groups.Add(name, newGroup);
				groupNames.Add(name);

                if (maxSize >= 0)
				{
					groupLimitsImposed = true;
				}
			} // else group already exists
		}

		public void ResetCaches()
		{
			APIManager.Instance.FlushUserCache();
		}

        public int GetNumberOfUsers(string groupName)
		{
			if (groups.ContainsKey(groupName))
			{
				return groups[groupName].users.Count;
			}
			return 0;
		}

		public void SendMessageToGroupMember(User user, string message)
		{
			if (String.IsNullOrEmpty(message))
			{
				return;
			}
			if (user != null && !String.IsNullOrEmpty(user.username))
			{
				message = String.Format("@{0} {1}", user.username, message);
			}
			UserGroup gp = HashUserToGroup(user);
			if (gp == null)
			{
				return;
			}
			APIManager.Instance.OnSendDirectMessage(new DirectMessageEvent(gp.leader, gp.tintColor, string.Format(@"""{0}""", user.id), message));
		}

		public void SendMessageToEntireGroup(string groupName, string message)
		{
			if (groups.ContainsKey(groupName))
			{
				UserGroup gp = groups[groupName];
				APIManager.Instance.OnSendDirectMessage(new DirectMessageEvent(gp.leader, gp.tintColor, gp.UserIds, message));
			}
		}

		#endregion

		#region EventsProcessor Callbacks
		/*
         * EventsProcessor Callbacks
         */
		public override void OnPeriscopeJoinEvent(User user, string color)
		{
			//Debug.Log(string.Format("Received JoinEvent: {0}   userId: {1}   hash: {2}", user.username, user.id, user.hash));
			UserGroup gp = HashUserToGroup(user);
			if (gp == null)
			{
				return;
			}
			if (gp.Contains(user, true, !GroupLimitsExist) != UserGroup.UserStatus.NotInGroup)  // try add user to a group upon join
			{
				SendMessageToGroupMember(user, RandMessage(gp.joinMessages));
			}
			else
			{
				SendMessageToGroupMember(user, RandMessage(gp.groupFullMessages));
			}

			activeUsers.Add(user.Hash, user);
		}

		public override void OnPeriscopeChatEvent(User user, string color, string message)
		{
			//Debug.Log(string.Format("Received ChatEvent: {0}   userId: {1}   hash: {2}   comment: {3}", user.username, user.id, user.hash, message));
			UserGroup gp = HashUserToGroup(user);
			if (gp == null)
			{
				return;
			}
			if (gp.Contains(user) == UserGroup.UserStatus.Exists)
			{
				// already in the group - text back once in a while
				if (UnityEngine.Random.Range(0, chatResponseProbability) == 0)
				{
					SendMessageToGroupMember(user, RandMessage(gp.chatResponseMessages));
				}
			}

			activeUsers.Add(user.Hash, user);
		}

		public override void OnPeriscopeHeartEvent(User user, string color)
		{
			//Debug.Log(string.Format("Received HeartEvent: userId: {0}   hash: {1}", user.id, user.hash));
			// aggregate in the user's UserGroup
			UserGroup gp = HashUserToGroup(user);
			if (gp == null)
			{
				return;
			}

			var status = gp.Contains(user, true, !GroupLimitsExist); // try add user to a group upon hearting
			if (status != UserGroup.UserStatus.NotInGroup)
			{
				gp.heartCount += 1;
				if (status == UserGroup.UserStatus.Added)
				{
					SendMessageToGroupMember(user, RandMessage(gp.joinMessages));
				}
				else
				{
					// already in the group - show appreciation once in a while
					if (UnityEngine.Random.Range(0, heartResponseProbability) == 0)
					{
						SendMessageToGroupMember(user, RandMessage(gp.heartResponseMessages));
					}
				}
			}
			else if (GroupLimitsExist && !activeUsers.ContainsKey(user.Hash))
			{
				SendMessageToGroupMember(user, RandMessage(gp.groupFullMessages));
			}

			activeUsers.Add(user.Hash, user);
		}

		void OnUserLeave(User user)
		{
			//Debug.Log(string.Format("User Left: {0}   userId: {1}   partIx: {2}", user.username, user.id, user.hash));
			UserGroup gp = HashUserToGroup(user);
			if (gp == null)
			{
				return;
			}

			if (gp.Contains(user) != UserGroup.UserStatus.NotInGroup)
			{
				lock (usersLeaving)
				{
					usersLeaving.Add(user);
				}
				gp.RemoveUser(user);
			}
		}

		#endregion

		#region Unity Editor Visible Fields
		/*
         * Unity Editor Visible Fields
         */
		public AnnounceAggregatedHearts OnAnnounceAggregatedHearts;

		[Header("Params")]
		public bool disregardGroupLimits;                  // disregard group limits
		public int maxTrackedUsers = 200000;               // used to track active/stale users - unused if no group limits imposed
		public int userTimeoutInMs = 60000;                // how long a user needs to be inactive before we remove him from a group
        public int periodicDMIntervalInMs = 15000;         // how often to send periodic messages to group members
        public int heartResponseProbability = 100;         // we don't actually keep track of individual heart contribution
														   // per user, users get a message with 1/Probability probability
        public int chatResponseProbability = 100;          // same as heart response interval but for chat messages

		[Header("Information [Read Only]")]
		[ReadOnly] public List<string> groupNames;

		#endregion

		#region Private Fields / Functions
		/* 
         * Private Fields / Functions
         */
		DateTime timeOfLastPeriodicMessage;
		bool groupLimitsImposed;
		Dictionary<string, UserGroup> groups;
		static LRUCache<int, User> activeUsers;
		static List<User> usersLeaving;
		bool GroupLimitsExist
		{
			get
			{
				return !disregardGroupLimits && groupLimitsImposed;
			}
		}

		UserGroup HashUserToGroup(User user)
		{
			if (groupNames.Count == 0)
			{
				return null;
			}
			int gpIx = user.Hash % groupNames.Count;
			string gpName = groupNames[gpIx];
			return groups[gpName];
		}

		string RandMessage(string[] messages)
		{
			if (messages.Length == 0)
			{
				return null;
			}
			var i = UnityEngine.Random.Range(0, messages.Length);
			return messages[i];
		}

		#endregion

		#region Helper Classes
		/* 
         * Helper Classes
         */
		class UserGroup
        {
            public User leader;
            public string tintColor;
            public Dictionary<int, User> users;           // hashed by UserHasher
            public int heartCount;
            public string[] joinMessages;                 // sent in DM when users join the group
            public string[] leaveMessages;                // sent in DM when users leave the group
            public string[] groupFullMessages;            // sent in DM when group is full and user can't join
            public string[] periodicMessages;             // sent in DM periodically
            public string[] heartResponseMessages;        // sent in DM when a user in the group is hearting
            public string[] chatResponseMessages;         // sent in DM when a user in the group is sending chat messages
            public AggregatedHeartsEvent ev;              // to avoid creating a new event every time
            public string UserIds { get { return GetUserIds(); } } // comma seperated user list

			int maxUsers;

			public UserGroup(
                string name,
                string color,                             // one of the 13 Periscope tint colors defined in colorToTintIndex
                string profileImageUrl,
                string[] joinMessages,
                string[] leaveMessages,
                string[] groupFullMessages,
                string[] periodicMessages,
                string[] heartResponseMessages,
                string[] chatResponseMessages,
                int maxUsers = 0)
            {
                this.leader = new User(name, name, profileImageUrl);
                this.tintColor = color;
                this.maxUsers = maxUsers;
                this.users = new Dictionary<int, User>();
                this.joinMessages = joinMessages;
                this.leaveMessages = leaveMessages;
                this.groupFullMessages = groupFullMessages;
                this.periodicMessages = periodicMessages;
                this.heartResponseMessages = heartResponseMessages;
                this.chatResponseMessages = chatResponseMessages;
                this.ev = new AggregatedHeartsEvent(name, 0);
            }

            public enum UserStatus
            {
                Exists,
                Added,
                NotInGroup
            };

            public UserStatus AddUser(User user, bool force = false)
            {
                UserStatus result = UserStatus.NotInGroup;
                if (users.ContainsKey(user.Hash))
                {
                    if (String.IsNullOrEmpty(users[user.Hash].username) && !String.IsNullOrEmpty(user.username))
                    {
                        users[user.Hash].username = user.username;
                        users[user.Hash].profileImageUrls = user.profileImageUrls;
                    }
                    result = UserStatus.Exists;
                }
                else if (maxUsers == 0 || users.Count < maxUsers || force)
                {
                    lock (users)
                    {
                        users.Add(user.Hash, user);
                    }
                    result = UserStatus.Added;
                }
                return result;
            }

            public UserStatus Contains(User user, bool autovivify = false, bool force = false)
            {
                // autovivify flag will attempt to add user to group if they don't already belong
                UserStatus result = UserStatus.NotInGroup;
                if (autovivify)
                {
                    result = AddUser(user, force);
                }
                else if (users.ContainsKey(user.Hash))
                {
                    if (String.IsNullOrEmpty(users[user.Hash].username) && !String.IsNullOrEmpty(user.username))
                    {
                        users[user.Hash].username = user.username;
                        users[user.Hash].profileImageUrls = user.profileImageUrls;
                    }
                    result = UserStatus.Exists;
                }
                return result;
            }

            public void RemoveUser(User user)
            {
                if (users.ContainsKey(user.Hash))
                {
                    lock (users)
                    {
                        users.Remove(user.Hash);
                    }
                }
            }

            string GetUserIds()
            {
                bool first = true;
                StringBuilder sb = new StringBuilder();
                lock (users)
                {
                    foreach (KeyValuePair<int, User> item in users)
                    {
                        var user = item.Value;
                        if (!first)
                        {
                            sb.Append(", ");
                        }
                        sb.Append(@"""");
                        sb.Append(user.id);
                        sb.Append(@"""");
                        first = false;
                    }
                }
                return sb.ToString();
            }
        }

		#endregion

		#region Unity Callbacks
		/*
         * Unity Callbacks
         */
		void Start()
        {
            activeUsers = new LRUCache<int, User>(maxTrackedUsers, userTimeoutInMs, OnUserLeave);
            groups = new Dictionary<string, UserGroup>();
            groupNames = new List<string>();
            usersLeaving = new List<User>();
            timeOfLastPeriodicMessage = DateTime.UtcNow;
        }

		void Update()
		{
			foreach (KeyValuePair<string, UserGroup> item in groups)
			{
				string groupName = item.Key;
				UserGroup gp = item.Value;
				gp.ev.numHearts = gp.heartCount;
				OnAnnounceAggregatedHearts.Invoke(gp.ev);
				gp.heartCount = 0;
			}

			lock (usersLeaving)
			{
				foreach (User user in usersLeaving)
				{
					UserGroup gp = HashUserToGroup(user);
					if (gp == null)
					{
						continue;
					}
					SendMessageToGroupMember(user, RandMessage(gp.leaveMessages));
				}
				usersLeaving.Clear();
			}

			if ((DateTime.UtcNow - timeOfLastPeriodicMessage).TotalMilliseconds > periodicDMIntervalInMs)
			{
				foreach (KeyValuePair<string, UserGroup> item in groups)
				{
					string groupName = item.Key;
					UserGroup gp = item.Value;
					SendMessageToEntireGroup(groupName, RandMessage(gp.periodicMessages));
				}
				timeOfLastPeriodicMessage = DateTime.UtcNow;
			}

			if (Input.GetKeyDown(KeyCode.D))
			{
				// debug info
				StringBuilder sb = new StringBuilder();
				sb.Append("Groups\n");
				sb.Append("------\n");
				foreach (string groupName in groupNames)
				{
					UserGroup gp = groups[groupName];
					sb.Append(string.Format("{0, -20}: {1} members\n", groupName, gp.users.Count));

					foreach (KeyValuePair<int, User> user in gp.users)
					{
						sb.Append(string.Format("    @{0, -20}: {1}\n", user.Value.username, user.Value.Hash));
					}
				}
				sb.Append(string.Format("------\n"));
				sb.Append(string.Format("Total active users: {0}\n", activeUsers.Count));
				Debug.Log(sb.ToString());
			}
		}

		#endregion
	}
}
