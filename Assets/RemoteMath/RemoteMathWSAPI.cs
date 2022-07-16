using System;
using System.Collections;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using UnityEngine;
using WebSocketSharp;

public class RemoteMathWSAPI : MonoBehaviour
{
    public class PuzzleCodeEventArgs : EventArgs
    {
        public string Code;
    }

    public delegate void PuzzleCodeEventHandler(PuzzleCodeEventArgs e);

    public class PuzzleLogEventArgs : EventArgs
    {
        public bool FromServer = false;
        public string Message;
    }

    public delegate void PuzzleLogEventHandler(PuzzleLogEventArgs e);

    public class PuzzleTwitchCodeEventArgs : EventArgs
    {
        public string Code;
    }

    public delegate void PuzzleTwitchCodeEventHandler(PuzzleTwitchCodeEventArgs e);

    public delegate void EmptyEventHandler();

    public class Handler
    {
        private readonly string websocketaddress = "ws://remote-math.mrmelon54.xyz";
        private Thread t = null;
        private bool shouldBeRunning = false;
        private WebSocket ws = null;
        private string PuzzleCodePrefix = "PuzzleCode::";
        private string PuzzleLogPrefix = "PuzzleLog::";
        private string PuzzleTwitchCodePrefix = "PuzzleTwitchCode::";
        private double lastPong = 0;
        private readonly bool reconnectMode = false;
        private RemoteMathScript RMath;

        public bool TwitchPlaysMode
        {
            get { return RMath.TwitchPlaysMode; }
        }

        public event EmptyEventHandler PuzzleError;
        public event EmptyEventHandler PuzzleComplete;
        public event EmptyEventHandler PuzzleStrike;
        public event PuzzleCodeEventHandler PuzzleCode;
        public event PuzzleLogEventHandler PuzzleLog;
        public event PuzzleTwitchCodeEventHandler PuzzleTwitchCode;

        public Handler(RemoteMathScript RMath)
        {
            this.RMath = RMath;
        }

        public void Start()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

            if (!isRunning())
            {
                shouldBeRunning = true;
                t = new Thread(rawStart)
                {
                    IsBackground = true
                };
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
            ws.SslConfiguration.EnabledSslProtocols =  (SslProtocols) 3072;
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
            UnityMainThreadDispatcher.Instance().Enqueue(StartPingChecker());
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
            OnPuzzleLog(a, false);
        }

        private void OnPuzzleLog(string a, bool FromServer)
        {
            PuzzleLogEventArgs e = new PuzzleLogEventArgs
            {
                FromServer = FromServer,
                Message = a
            };
            PuzzleLog.Invoke(e);
        }

        private void OnPuzzleCode(string IncomingCode)
        {
            PuzzleCodeEventArgs e = new PuzzleCodeEventArgs
            {
                Code = IncomingCode
            };
            PuzzleCode.Invoke(e);
        }

        private void OnPuzzleTwitchCode(string IncomingCode)
        {
            PuzzleTwitchCodeEventArgs e = new PuzzleTwitchCodeEventArgs
            {
                Code = IncomingCode
            };
            PuzzleTwitchCode.Invoke(e);
        }

        private void onError(object sender, ErrorEventArgs e)
        {
            OnPuzzleLog("Websocket client error");
            OnPuzzleLog(e.Message);
            OnPuzzleError();
        }

        private void onOpen(object sender, object e)
        {
            Debug.Log("[RemoteMathWSAPI] Connected to server");
            if (reconnectMode)
            {
                OnReconnectMode(new EventArgs());
            }
            else
            {
                OnConnected(new EventArgs());
            }
        }

        private void onClose(object sender, CloseEventArgs e)
        {
            //TlsHandshakeFailure
            if (e.Code == 1015) OnPuzzleLog("SSL failure");
            if (e.Code == 1006) OnPuzzleLog("This error should never occur");
            OnPuzzleLog("Disconnected from server");
            OnPuzzleLog(e.Code.ToString());
            OnPuzzleLog(e.Reason);
            OnPuzzleLog(e.WasClean.ToString());
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
                return;
            }

            if (e.Data == "PuzzleComplete")
            {
                OnPuzzleLog("Received data: PuzzleComplete");
                OnPuzzleComplete();
                return;
            }

            if (e.Data == "PuzzleStrike")
            {
                OnPuzzleLog("Received data: PuzzleStrike");
                OnPuzzleStrike();
                return;
            }

            if (e.Data.StartsWith(PuzzleCodePrefix))
            {
                if (e.Data.Length == (PuzzleCodePrefix.Length + 6))
                {
                    string CodeValue = e.Data.Substring(PuzzleCodePrefix.Length);
                    OnPuzzleLog("Received data: " + e.Data);
                    Send("PuzzleTwitchPlaysMode::" + RMath.TwitchId);
                    OnPuzzleCode(CodeValue);
                }
                else
                {
                    OnPuzzleLog("Invalid packet received");
                    OnPuzzleError();
                }

                return;
            }

            if (e.Data.StartsWith(PuzzleTwitchCodePrefix))
            {
                if (e.Data.Length == (PuzzleTwitchCodePrefix.Length + 3))
                {
                    string CodeValue = e.Data.Substring(PuzzleTwitchCodePrefix.Length);
                    OnPuzzleLog("Received data: " + e.Data);
                    OnPuzzleTwitchCode(CodeValue);
                }

                return;
            }

            if (e.Data.StartsWith(PuzzleLogPrefix)) OnPuzzleLog(e.Data.Substring(PuzzleLogPrefix.Length));
            else if (e.Data == "ClientSelected") OnPuzzleLog("Client selected on server end");
            else
            {
                OnPuzzleLog("Invalid packet received");
                OnPuzzleError();
            }
        }

        public void Send(string data)
        {
            ws.SendAsync(data, delegate(bool d) { });
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
            if (latestPong - lastPong > 6000)
            {
                OnPuzzleLog("Last ping was at " + lastPong.ToString());
                OnPuzzleLog("Ping check failed");
                if (ws != null) ws.Close();
                UnityMainThreadDispatcher.Instance().StopCoroutine(IHandlePingCheck());
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