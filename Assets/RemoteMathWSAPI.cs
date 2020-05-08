using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using WebSocketSharp;

public class RemoteMathWSAPI : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public class PuzzleCodeEventArgs : EventArgs
    {
        public string Code;
    }
    public delegate void PuzzleCodeEventHandler(PuzzleCodeEventArgs e);
    public delegate void EmptyEventHandler();

    public enum SslProtocolsHack
    {
        Tls = 192,
        Tls11 = 768,
        Tls12 = 3072
    }
    public class Handler
    {
        private string websocketaddress = "ws://remote-math.onpointcoding.net";
        private Thread t = null;
        private bool shouldBeRunning = false;
        private WebSocket ws = null;
        private string PuzzleCodePrefix = "PuzzleCode::";

        public event EmptyEventHandler PuzzleError;
        public event EmptyEventHandler PuzzleComplete;
        public event EmptyEventHandler PuzzleStrike;
        public event PuzzleCodeEventHandler PuzzleCode;

        public void Start()
        {
            if (!isRunning())
            {
                shouldBeRunning = true;
                t = new Thread(rawStart);
                t.IsBackground = true;
                t.Start();
            }
        }

        public void Stop()
        {
            shouldBeRunning = false;
        }

        public bool isRunning()
        {
            return ws != null && ws.IsAlive;
        }

        private void rawStart()
        {
            Debug.Log("[RemoteMathWSAPI] Connecting to " + websocketaddress);
            ws = new WebSocket(websocketaddress);
            using (ws)
            {
                ws.OnError += onError;
                ws.OnMessage += onMessage;
                ws.OnClose += onClose;
                ws.OnOpen += onOpen;
                ws.Connect();
                while (shouldBeRunning)
                {
                    Thread.Sleep(100);
                }
            }
            Debug.Log("[RemoteMathWSAPI] Reached end of websocket handler. Stopping...");
            ws = null;
        }

        public void Login()
        {
            if (!isRunning()) return;
            ws.Send("stephanie");
        }

        private void OnPuzzleError()
        {
            PuzzleError.Invoke();
        }

        private void OnPuzzleComplete()
        {
            PuzzleComplete.Invoke();
        }

        private void OnPuzzleStrike()
        {
            PuzzleStrike.Invoke();
        }

        private void OnPuzzleCode(string IncomingCode)
        {
            PuzzleCodeEventArgs e = new PuzzleCodeEventArgs();
            e.Code = IncomingCode;
            PuzzleCode.Invoke(e);
        }

        private void onError(object sender, ErrorEventArgs e)
        {
            Debug.Log("[RemoteMathWSAPI] Websocket client error");
            Debug.Log(e.Exception);
            Debug.Log(e.Message);
            OnPuzzleError();
        }

        private void onOpen(object sender, object e)
        {
            Debug.Log("[RemoteMathWSAPI] Connected to server");
            OnConnected(new EventArgs());
        }

        private void onClose(object sender, CloseEventArgs e)
        {
            var sslProtocolHack = (System.Security.Authentication.SslProtocols)(SslProtocolsHack.Tls12 | SslProtocolsHack.Tls11 | SslProtocolsHack.Tls);
            //TlsHandshakeFailure
            if (e.Code == 1015 && ws.SslConfiguration.EnabledSslProtocols != sslProtocolHack)
            {
                Debug.Log("Fixing SSL failure");
                ws.SslConfiguration.EnabledSslProtocols = sslProtocolHack;
                ws.Connect();
                return;
            }
            Debug.Log("[RemoteMathWSAPI] Disconnected from server");
            Debug.Log(e.Code);
            Debug.Log(e.Reason);
            Debug.Log(e.WasClean);
            if (e.Code == 1002)
                shouldBeRunning = false;
            OnDisconnected(new EventArgs());
        }

        private void onMessage(object sender, MessageEventArgs e)
        {
            if (e.Data == "PuzzleComplete")
            {
                Debug.Log("[RemoteMathWSAPI] Received data: PuzzleComplete");
                OnPuzzleComplete();
            } else if (e.Data == "PuzzleStrike")
            {
                Debug.Log("[RemoteMathWSAPI] Received data: PuzzleStrike");
                OnPuzzleStrike();
            } else if (e.Data.StartsWith(PuzzleCodePrefix))
            {
                if (e.Data.Length == (PuzzleCodePrefix.Length + 6))
                {
                    string CodeValue = e.Data.Substring(PuzzleCodePrefix.Length);
                    Debug.Log("[RemoteMathWSAPI] Received data: " + PuzzleCodePrefix + e.Data);
                    OnPuzzleCode(CodeValue);
                } else
                {
                    Debug.Log("[RemoteMathWSAPI] Invalid packet received");
                    OnPuzzleError();
                }
            } else if(e.Data=="ClientSelected")
            {
                Debug.Log("[RemoteMathWSAPI] Client selected on server end");
            } else
            {
                Debug.Log("[RemoteMathWSAPI] Invalid packet received");
                OnPuzzleError();
            }
        }

        public void Send(string data)
        {
            ws.SendAsync(data, delegate (bool d) { });
        }




        // Events

        public event EventHandler Connected;
        protected virtual void OnConnected(EventArgs e)
        {
            Connected.Invoke(this, e);
        }

        public event EventHandler Disconnected;
        protected virtual void OnDisconnected(EventArgs e)
        {
            Disconnected.Invoke(this, e);
        }
    }
}
