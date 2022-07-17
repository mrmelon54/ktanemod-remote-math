﻿using System;
using System.Collections;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using UnityEngine;
using WebSocketSharp;

namespace RemoteMath
{
    public class RemoteMathWsApi : MonoBehaviour
    {
        public class PuzzleCodeEventArgs : EventArgs
        {
            public string Code;
        }

        public delegate void PuzzleCodeEventHandler(PuzzleCodeEventArgs e);

        public class PuzzleLogEventArgs : EventArgs
        {
            public bool FromServer;
            public string Message;
        }

        public delegate void PuzzleLogEventHandler(PuzzleLogEventArgs e);

        public class PuzzleTwitchCodeEventArgs : EventArgs
        {
            public string Code;
        }

        public delegate void PuzzleTwitchCodeEventHandler(PuzzleTwitchCodeEventArgs e);

        public delegate void EmptyEventHandler();

        public sealed class Handler
        {
            //private const string Address = "ws://remote-math.mrmelon54.xyz:13695";
            private const string Address = "ws://localhost:8080";
            private Thread _t;
            private bool _shouldBeRunning;
            private WebSocket _ws;
            private const string PuzzleCodePrefix = "PuzzleCode::";
            private const string PuzzleLogPrefix = "PuzzleLog::";
            private const string PuzzleTwitchCodePrefix = "PuzzleTwitchCode::";
            private double _lastPong;
            private readonly RemoteMathScript _rMath;

            public event EmptyEventHandler PuzzleError;
            public event EmptyEventHandler PuzzleComplete;
            public event EmptyEventHandler PuzzleStrike;
            public event PuzzleCodeEventHandler PuzzleCode;
            public event PuzzleLogEventHandler PuzzleLog;
            public event PuzzleTwitchCodeEventHandler PuzzleTwitchCode;

            public Handler(RemoteMathScript rMath)
            {
                _rMath = rMath;
            }

            public void Start()
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType) 3072;

                if (IsRunning()) return;
                _shouldBeRunning = true;
                _t = new Thread(RawStart) {IsBackground = true};
                _t.Start();
            }

            public void Stop()
            {
                _shouldBeRunning = false;
            }

            private bool IsRunning()
            {
                return _ws != null && _ws.IsAlive;
            }

            private void RawStart()
            {
                Debug.Log("[RemoteMathWsApi] Connecting to " + Address);
                _ws = new WebSocket(Address);
                _ws.SslConfiguration.EnabledSslProtocols = (SslProtocols) 3072;
                using (_ws)
                {
                    _ws.OnError += OnError;
                    _ws.OnMessage += OnMessage;
                    _ws.OnClose += OnClose;
                    _ws.OnOpen += OnOpen;
                    _ws.Connect();
                    while (_shouldBeRunning)
                    {
                        Thread.Sleep(100);
                    }
                }

                Debug.Log("[RemoteMathWsApi] Reached end of websocket handler. Stopping...");
                _ws = null;
            }

            public void Login()
            {
                if (!IsRunning()) return;
                _ws.Send("stephanie");
                UnityMainThreadDispatcher.Instance().Enqueue(StartPingChecker());
            }

            private void OnPuzzleError()
            {
                if (PuzzleError != null) PuzzleError.Invoke();
            }

            private void OnPuzzleComplete()
            {
                if (PuzzleComplete != null) PuzzleComplete.Invoke();
            }

            private void OnPuzzleStrike()
            {
                if (PuzzleStrike != null) PuzzleStrike.Invoke();
            }

            private void OnPuzzleLog(string a, bool fromServer = false)
            {
                var e = new PuzzleLogEventArgs {FromServer = fromServer, Message = a};
                if (PuzzleLog != null) PuzzleLog.Invoke(e);
            }

            private void OnPuzzleCode(string incomingCode)
            {
                var e = new PuzzleCodeEventArgs {Code = incomingCode};
                if (PuzzleCode != null) PuzzleCode.Invoke(e);
            }

            private void OnPuzzleTwitchCode(string incomingCode)
            {
                var e = new PuzzleTwitchCodeEventArgs {Code = incomingCode};
                if (PuzzleTwitchCode != null) PuzzleTwitchCode.Invoke(e);
            }

            private void OnError(object sender, ErrorEventArgs e)
            {
                OnPuzzleLog("Websocket client error");
                OnPuzzleLog(e.Message);
                OnPuzzleError();
            }

            private void OnOpen(object sender, object e)
            {
                Debug.Log("[RemoteMathWsApi] Connected to server");
                OnConnected(EventArgs.Empty);
            }

            private void OnClose(object sender, CloseEventArgs e)
            {
                switch (e.Code)
                {
                    //TlsHandshakeFailure
                    case 1015:
                        OnPuzzleLog("SSL failure");
                        break;
                    case 1006:
                        OnPuzzleLog("This error should never occur");
                        break;
                }

                OnPuzzleLog("Disconnected from server");
                OnPuzzleLog(e.Code.ToString());
                OnPuzzleLog(e.Reason);
                OnPuzzleLog(e.WasClean.ToString());
                if (e.Code == 1002)
                    _shouldBeRunning = false;
                OnDisconnected(EventArgs.Empty);
            }

            private void OnMessage(object sender, MessageEventArgs e)
            {
                try
                {
                    switch (e.Data)
                    {
                        case "ping":
                        {
                            Send("pong");
                            var baseDate = new DateTime(1970, 1, 1);
                            _lastPong = (DateTime.Now - baseDate).TotalMilliseconds;
                            return;
                        }
                        case "PuzzleComplete":
                            OnPuzzleLog("Received data: PuzzleComplete");
                            OnPuzzleComplete();
                            return;
                        case "PuzzleStrike":
                            OnPuzzleLog("Received data: PuzzleStrike");
                            OnPuzzleStrike();
                            return;
                    }

                    if (e.Data.StartsWith(PuzzleCodePrefix))
                    {
                        if (e.Data.Length == (PuzzleCodePrefix.Length + 6))
                        {
                            var codeValue = e.Data.Substring(PuzzleCodePrefix.Length);
                            OnPuzzleLog("Received data: " + e.Data);
                            Send("PuzzleTwitchPlaysMode::" + _rMath.TwitchId);
                            OnPuzzleCode(codeValue);
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
                        if (e.Data.Length != PuzzleTwitchCodePrefix.Length + 3) return;
                        var codeValue = e.Data.Substring(PuzzleTwitchCodePrefix.Length);
                        OnPuzzleLog("Received data: " + e.Data);
                        OnPuzzleTwitchCode(codeValue);

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
                catch
                {
                    // ignored
                }
            }

            public void Send(string data)
            {
                _ws.SendAsync(data, delegate { });
            }

            private IEnumerator StartPingChecker()
            {
                UnityMainThreadDispatcher.Instance().StartCoroutine(HandlePingCheck());
                yield return null;
            }

            private IEnumerator HandlePingCheck()
            {
                yield return new WaitForSeconds(7);
                var baseDate = new DateTime(1970, 1, 1);
                var latestPong = (DateTime.Now - baseDate).TotalMilliseconds;
                if (!(latestPong - _lastPong > 6000)) yield break;
                OnPuzzleLog("Last ping was at " + _lastPong);
                OnPuzzleLog("Ping check failed");
                if (_ws != null) _ws.Close();
                UnityMainThreadDispatcher.Instance().StopCoroutine(HandlePingCheck());
            }


            // Events

            public event EventHandler Connected;

            private void OnConnected(EventArgs e)
            {
                if (Connected != null) Connected.Invoke(this, e);
            }

            public event EventHandler Disconnected;

            private void OnDisconnected(EventArgs e)
            {
                if (Disconnected != null) Disconnected.Invoke(this, e);
            }
        }
    }
}