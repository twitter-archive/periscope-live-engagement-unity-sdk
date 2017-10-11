using System;
using System.Collections;
using UnityEngine;

public class AVR_PeriscopeMenu : MonoBehaviour
{
    const KeyCode menuToggleKey = KeyCode.M;
    const string accessTokenKey = "access_token";
    bool guiVisible = true;

    // x-axis
    const int indent = 5;
    const int buttonWidth = 140;
    const int labelWidth = 80;
    int FullWidth { get { return Screen.width - 2 * indent; } }

    // y-axis
    const int itemHeight = 20;
    const int authenticationYoffset = 35;
    const int broadcastYOffset = 65;
    const int connectionYOffset = 95;
    const int copyField1YOffset = 200;
    const int copyField2YOffset = 220;
    int ErrorYOffset { get { return Screen.height - indent - itemHeight; } }

    private void OnGUI()
    {
        if (guiVisible)
        {
            // Top labels
            GUI.Label(new Rect(indent, indent, FullWidth, itemHeight), String.Format("Press {0} to toggle the menu", menuToggleKey));

            // Auth info
            switch (Periscope.APIManager.Instance.AuthenticationStatus)
            {
                case Periscope.AuthenticationStatus.NotAuthenticated:
                    if (GUI.Button(new Rect(indent, authenticationYoffset, buttonWidth, itemHeight), "Authenticate"))
                    {
                        Periscope.APIManager.Instance.Authenticate();
                        StartCoroutine(CheckAuthentication());
                    }
                    break;
                case Periscope.AuthenticationStatus.Waiting:
                    if (!String.IsNullOrEmpty(Periscope.APIManager.Instance.AuthUrl) && !String.IsNullOrEmpty(Periscope.APIManager.Instance.AuthCode))
                    {
                        GUI.Label(new Rect(indent, copyField1YOffset, labelWidth, itemHeight), "Go to this url:");
                        GUI.TextField(new Rect(2 * indent + labelWidth, copyField1YOffset, FullWidth - labelWidth - indent, itemHeight), Periscope.APIManager.Instance.AuthUrl);
                        GUI.Label(new Rect(indent, copyField2YOffset, labelWidth, itemHeight), "Enter this code:");
                        GUI.TextField(new Rect(2 * indent + labelWidth, copyField2YOffset, FullWidth - labelWidth - indent, itemHeight), Periscope.APIManager.Instance.AuthCode);
                    }
                    else
                    {
                        GUI.Label(new Rect(indent, authenticationYoffset, FullWidth, itemHeight), "Authenticating...");
                    }
                    break;
                case Periscope.AuthenticationStatus.Authenticated:
                    if (GUI.Button(new Rect(indent, authenticationYoffset, buttonWidth, itemHeight), "Unauthenticate"))
                    {
                        Periscope.APIManager.Instance.Unauthenticate();
                        if (PlayerPrefs.HasKey(accessTokenKey))
                        {
                            PlayerPrefs.DeleteKey(accessTokenKey);
                        }
                    }
                    break;
            }

            // Broadcast create
            switch (Periscope.APIManager.Instance.AuthenticationStatus)
            {
                case Periscope.AuthenticationStatus.NotAuthenticated:
                case Periscope.AuthenticationStatus.Waiting:
                    break;
                case Periscope.AuthenticationStatus.Authenticated:
                    switch (Periscope.APIManager.Instance.BroadcastStatus)
                    {
                        case Periscope.BroadcastStatus.NotStarted:
                            if (GUI.Button(new Rect(indent, broadcastYOffset, buttonWidth, itemHeight), "Get Stream Info"))
                            {
                                Periscope.APIManager.Instance.GetStreamInfo();
                            }
                            break;
                        case Periscope.BroadcastStatus.ReadyToStream:
                            if (GUI.Button(new Rect(indent, broadcastYOffset, buttonWidth, itemHeight), "Go Live!"))
                            {
                                Periscope.APIManager.Instance.GoLive();
                            }
                            if (!String.IsNullOrEmpty(Periscope.APIManager.Instance.StreamUrl) && 
                                !String.IsNullOrEmpty(Periscope.APIManager.Instance.StreamKey))
                            {
                                GUI.Label(new Rect(indent, copyField1YOffset, labelWidth, itemHeight), "Stream url:");
                                GUI.TextField(new Rect(2 * indent + labelWidth, copyField1YOffset, FullWidth - labelWidth - indent, itemHeight), Periscope.APIManager.Instance.StreamUrl);
                                GUI.Label(new Rect(indent, copyField2YOffset, labelWidth, itemHeight), "Stream key:");
                                GUI.TextField(new Rect(2 * indent + labelWidth, copyField2YOffset, FullWidth - labelWidth - indent, itemHeight), Periscope.APIManager.Instance.StreamKey);
                            }
                            break;
                        case Periscope.BroadcastStatus.Live:
                            if (GUI.Button(new Rect(indent, broadcastYOffset, buttonWidth, itemHeight), "End Broadcast"))
                            {
                                Periscope.APIManager.Instance.EndBroadcast();
                            }
                            break;
                    }
                    break;
            }

            // Broadcast info
            switch (Periscope.APIManager.Instance.AuthenticationStatus)
            {
                case Periscope.AuthenticationStatus.NotAuthenticated:
                case Periscope.AuthenticationStatus.Waiting:
                    break;
                case Periscope.AuthenticationStatus.Authenticated:
                    switch (Periscope.APIManager.Instance.ConnectionStatus)
                    {
                        case Periscope.ConnectionStatus.Connecting:
                            GUI.Label(new Rect(indent, connectionYOffset, FullWidth, itemHeight), "Connecting...");
                            break;
                        case Periscope.ConnectionStatus.NotConnected:
                            if (GUI.Button(new Rect(indent, connectionYOffset, buttonWidth, itemHeight), "Connect"))
                            {
                                Periscope.APIManager.Instance.Connect();
                            }
                            break;
                        case Periscope.ConnectionStatus.Connected:
                            if (GUI.Button(new Rect(indent, connectionYOffset, buttonWidth, itemHeight), "Disconnect"))
                            {
                                Periscope.APIManager.Instance.Disconnect();
                            }
                            break;
                    }
                    break;
            }

            if (!String.IsNullOrEmpty(Periscope.APIManager.Instance.Error))
            {
                GUI.Label(new Rect(indent, ErrorYOffset, FullWidth, itemHeight), Periscope.APIManager.Instance.Error);
            }
        }
    }

    IEnumerator CheckAuthentication()
    {
        while (String.IsNullOrEmpty(Periscope.APIManager.Instance.AuthUrl) &&
            Periscope.APIManager.Instance.AuthenticationStatus == Periscope.AuthenticationStatus.Waiting)
        {
            yield return new WaitForSecondsRealtime(1.0f);
        }

        if (Periscope.APIManager.Instance.AuthenticationStatus == Periscope.AuthenticationStatus.Waiting)
        {
            Application.OpenURL(Periscope.APIManager.Instance.AuthUrl);
        }
    }

    private void Start()
    {
        if (PlayerPrefs.HasKey(accessTokenKey))
        {
            Periscope.APIManager.Instance.Authenticate(PlayerPrefs.GetString(accessTokenKey));
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(menuToggleKey))
        {
            guiVisible = !guiVisible;
        }

        // save access token
        if (!String.IsNullOrEmpty(Periscope.APIManager.Instance.AccessToken))
        {
            if (!PlayerPrefs.HasKey(accessTokenKey) ||
                (PlayerPrefs.HasKey(accessTokenKey) &&
                !String.IsNullOrEmpty(Periscope.APIManager.Instance.AccessToken) &&
                !PlayerPrefs.GetString(accessTokenKey).Equals(Periscope.APIManager.Instance.AccessToken)))
            {
                PlayerPrefs.SetString(accessTokenKey, Periscope.APIManager.Instance.AccessToken);
            }
        }
    }
}
