using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

namespace Periscope
{
    public class WebSocket
    {
        Uri uri;
        string accessToken;
        int capacity;
        WebSocketSharp.WebSocket websocket;
        Queue<byte[]> events = new Queue<byte[]>();
        bool isConnected;
        string error;
        int numDroppedEvents;

        public WebSocket(Uri uri, int _capacity = 0, string _accessToken = null)
        {
            this.uri = uri;
            accessToken = _accessToken;

            string protocol = uri.Scheme;
            if (!protocol.Equals("ws") && !protocol.Equals("wss"))
            {
                throw new ArgumentException("Unsupported protocol: " + protocol);
            }

            capacity = _capacity;
        }

        public string Pop()
        {
            byte[] bytes = ReceiveBytes();
            if (bytes == null)
            {
                return null;
            }
            return Encoding.UTF8.GetString(bytes);
        }

        public IEnumerator Connect()
        {
            websocket = new WebSocketSharp.WebSocket(uri.ToString());
            if (!String.IsNullOrEmpty(accessToken))
            {
                websocket.SetCredentials("token", accessToken, true);
            }
            websocket.OnMessage += (sender, e) =>
            {
                if (capacity == 0 || events.Count < capacity)
                {
                    lock (events)
                    {
                        events.Enqueue(e.RawData);
                    }
                }
                else
                {
                    numDroppedEvents++;
                }
            };
            websocket.OnOpen += (sender, e) =>
            {
                isConnected = true;
            };
            websocket.OnClose += (sender, e) =>
            {
                isConnected = false;
                if (!e.WasClean)
                {
                    error = e.Reason;
                }
            };
            websocket.OnError += (sender, e) =>
            {
                isConnected = false;
                error = e.Message;
            };
            websocket.ConnectAsync();
            while (!isConnected && String.IsNullOrEmpty(error))
            {
                yield return 0;
            }
        }

        public void Send(string buffer)
        {
            websocket.Send(buffer);
        }

        public void Close()
        {
            websocket.Close();
            lock (events)
            {
                events.Clear();
            }
        }

        public string Error
        {
            get
            {
                return error;
            }
        }

        public bool IsConnected
        {
            get
            {
                return isConnected;
            }
        }

        public int NumQueuedEvents
        {
            get
            {
                return events.Count;
            }
        }

        public int NumDroppedEvents
        {
            get
            {
                return numDroppedEvents;
            }
        }

        byte[] ReceiveBytes()
        {
            lock (events)
            {
                if (events.Count == 0)
                {
                    return null;
                }
                return events.Dequeue();
            }
        }
    }
}