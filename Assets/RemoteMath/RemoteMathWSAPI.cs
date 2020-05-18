using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using WebSocketSharp;

public class RemoteMathWSAPI : MonoBehaviour {

    public class PuzzleCodeEventArgs : EventArgs
    {
        public string Code;
    }
    public delegate void PuzzleCodeEventHandler(PuzzleCodeEventArgs e);
    public class PuzzleLogEventArgs : EventArgs
    {
        public string Message;
    }
    public delegate void PuzzleLogEventHandler(PuzzleLogEventArgs e);
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
        private string PuzzleLogPrefix = "PuzzleLog::";
        private double lastPong=0;
        private bool reconnectMode = false;

        public event EmptyEventHandler PuzzleError;
        public event EmptyEventHandler PuzzleComplete;
        public event EmptyEventHandler PuzzleStrike;
        public event PuzzleCodeEventHandler PuzzleCode;
        public event PuzzleLogEventHandler PuzzleLog;

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

        private void OnPuzzleLog(string a)
        {
            PuzzleLogEventArgs e = new PuzzleLogEventArgs();
            e.Message = a;
            PuzzleLog.Invoke(e);
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
            if(reconnectMode)
            {
                OnReconnectMode(new EventArgs());
            } else
            {
                OnConnected(new EventArgs());
            }
        }

        private void onClose(object sender, CloseEventArgs e)
        {
            var sslProtocolHack = (System.Security.Authentication.SslProtocols)(SslProtocolsHack.Tls12 | SslProtocolsHack.Tls11 | SslProtocolsHack.Tls);
            //TlsHandshakeFailure
            if (e.Code == 1015 && ws.SslConfiguration.EnabledSslProtocols != sslProtocolHack)
            {
                Debug.Log("[RemoteMathWSAPI] Fixing SSL failure");
                ws.SslConfiguration.EnabledSslProtocols = sslProtocolHack;
                ws.Connect();
                return;
            }
            if(e.Code==1006)
            {
                Debug.Log("[RemoteMathWSAPI] This error should never occur");
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
            if (e.Data == "ping")
            {
                Send("pong");
                DateTime baseDate = new DateTime(1970, 1, 1);
                lastPong = (DateTime.Now - baseDate).TotalMilliseconds;
                UnityMainThreadDispatcher.Instance().Enqueue(StartPingChecker());
                return;
            } else if (e.Data == "PuzzleComplete")
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
            } else if(e.Data.StartsWith(PuzzleLogPrefix))
            {
                OnPuzzleLog(e.Data.Substring(PuzzleLogPrefix.Length));
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

        IEnumerator StartPingChecker()
        {
            UnityMainThreadDispatcher.Instance().StartCoroutine(IHandlePingCheck());
            yield return null;
        }

        IEnumerator IHandlePingCheck()
        {
            yield return new WaitForSeconds(7);
            DateTime baseDate = new DateTime(1970, 1, 1);
            double latestPong = (DateTime.Now - baseDate).TotalMilliseconds;
            if (latestPong-lastPong>6000)
            {
                Debug.Log("[RemoteMathWSAPI] Ping check failed");
                ws.Close();
            }
        }




        // Events

        public event EventHandler Connected;
        protected virtual void OnConnected(EventArgs e)
        {
            Connected.Invoke(this, e);
        }

        public event EventHandler Reconnected;
        protected virtual void OnReconnectMode(EventArgs e)
        {
            Reconnected.Invoke(this, e);
        }

        public event EventHandler Disconnected;
        protected virtual void OnDisconnected(EventArgs e)
        {
            Disconnected.Invoke(this, e);
        }
    }
}
