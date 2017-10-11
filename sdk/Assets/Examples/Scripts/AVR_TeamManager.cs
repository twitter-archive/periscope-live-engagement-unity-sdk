using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Team
{
    public string teamName;
    public string color;
    public string[] joinMessages;
    public string[] leaveMessages;
    public string[] groupFullMessages;
    public string[] periodicMessages;
    public string[] heartResponseMessages;
    public string[] chatResponseMessages;
    public string imageUrl;
    public int maxUsers;
    public AVR_HeartHandler heartHandler;
}

public class AVR_TeamManager : MonoBehaviour
{
    public Periscope.UserGroupsManager groupsManager;
    bool initialized;
    public List<Team> teams;

    // Use this for initialization
    void Start()
    {
        groupsManager = FindObjectOfType<Periscope.UserGroupsManager>();
    }

    void Initialize()
    {
        if (groupsManager != null)
        {
            foreach (Team t in teams)
            {
                groupsManager.CreateUserGroup(
                    t.teamName,
                    t.color,
                    t.imageUrl,
                    t.joinMessages,
                    t.leaveMessages,
                    t.groupFullMessages,
                    t.periodicMessages,
                    t.heartResponseMessages,
                    t.chatResponseMessages,
                    t.maxUsers);
            }
            groupsManager.OnAnnounceAggregatedHearts.AddListener(OnAggregatedHeartsEvent);
        }
        initialized = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!initialized)
        {
            Initialize();
        }
    }

    private void OnAggregatedHeartsEvent(Periscope.AggregatedHeartsEvent ev)
    {
        foreach (Team t in teams)
        {
            if (ev.groupName == t.teamName)
            {
                if (ev.numHearts > 0)
                {
                    Debug.Log(string.Format("Received AggregatedHeartEvent: team: {0}   hearts: {1}", ev.groupName, ev.numHearts));
                }

                t.heartHandler.HandleHearts(ev.numHearts);
            }
        }
    }
}