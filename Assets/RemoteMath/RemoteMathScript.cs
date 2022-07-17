using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using KModkit;
using UnityEngine;
using UnityEngine.Serialization;

namespace RemoteMath
{
    public class RemoteMathScript : MonoBehaviour
    {
        [FormerlySerializedAs("BombAudio")] public KMAudio bombAudio;
        [FormerlySerializedAs("BombInfo")] public KMBombInfo bombInfo;
        [FormerlySerializedAs("BombModule")] public KMBombModule bombModule;
        [FormerlySerializedAs("ModuleSelect")] public KMSelectable moduleSelect;
        [FormerlySerializedAs("MainButton")] public KMSelectable mainButton;

        [FormerlySerializedAs("SecretCodeText")]
        public GameObject secretCodeText;

        [FormerlySerializedAs("WelcomeText")] public GameObject welcomeText;
        public GameObject fakeStatusLitBoi;
        public GameObject realStatusLitBoi;
        [FormerlySerializedAs("Fruit1")] public GameObject fruit1;
        [FormerlySerializedAs("Fruit2")] public GameObject fruit2;
        [FormerlySerializedAs("FruitMats")] public Material[] fruitMats;
        [FormerlySerializedAs("Lights")] public Light[] lights;
        [FormerlySerializedAs("FruitNames")] public string[] fruitNames;
        private RemoteMathWsApi.Handler _remoteMathApi;
        private string _secretCode = "";
        private string _currentLed;

        private bool _moduleSolved;
        private bool _allowedToSolve;
        private bool _isConnected;
        private bool _moduleStartup;
        private bool _hasErrored;

        private bool _twitchPlaysMode;
#pragma warning disable CS0649
        // ReSharper disable once InconsistentNaming
        // ReSharper disable once ArrangeTypeMemberModifiers
        bool TwitchPlaysActive;
#pragma warning restore CS0649

        private bool TwitchPlaysMode
        {
            get { return TwitchPlaysActive || _twitchPlaysMode; }
            set { _twitchPlaysMode = value; }
        }

        private readonly List<string> _twitchPlaysCodes = new List<string>();

        private static int _moduleIdCounter = 1;
        private int _moduleId;
        internal string TwitchId;

        private void GetTwitchPlaysId()
        {
            Debug.Log("[RemoteMathCheck] A");
            var gType = ReflectionHelper.FindType("TwitchGame", "TwitchPlaysAssembly");
            Debug.Log("[RemoteMathCheck] B");
            object comp = FindObjectOfType(gType);
            Debug.Log("[RemoteMathCheck] C");
            if (comp == null) return;
            var twitchModules = comp.GetType().GetField("Modules", BindingFlags.Public | BindingFlags.Instance);
            if (twitchModules == null) return;
            var twitchPlaysObj = twitchModules.GetValue(comp);
            Debug.Log("[RemoteMathCheck] D");
            var twitchPlaysModules = (IEnumerable) twitchPlaysObj;
            Debug.Log("[RemoteMathCheck] E");
            foreach (var module in twitchPlaysModules)
            {
                var bombComponent = module.GetType().GetField("BombComponent", BindingFlags.Public | BindingFlags.Instance);
                if (bombComponent == null) continue;
                var behaviour = (MonoBehaviour) bombComponent.GetValue(module);
                Debug.Log("[RemoteMathCheck] F");
                var rMath = behaviour.GetComponent<RemoteMathScript>();
                Debug.Log("[RemoteMathCheck] G");
                if (rMath != this) continue;
                var moduleCode = module.GetType().GetProperty("Code", BindingFlags.Public | BindingFlags.Instance);
                if (moduleCode != null) TwitchId = (string) moduleCode.GetValue(module, null);
            }
        }

        private void Start()
        {
            _moduleId = _moduleIdCounter++;

            var scalar = transform.lossyScale.x;
            foreach (var t in lights)
                t.range *= scalar;

            SetSecretCode("");
            SetLed("Off");

            mainButton.OnInteract += delegate
            {
                if (!_moduleStartup)
                {
                    secretCodeText.SetActive(true);
                    welcomeText.SetActive(false);
                    _moduleStartup = true;
                    StartCoroutine(StartWebsocketClient());
                    return false;
                }

                if (_moduleSolved) return false;
                if (!_allowedToSolve) return false;
                _moduleSolved = true;
                HandlePass();
                return false;
            };

            fruit1.SetActive(false);
            fruit2.SetActive(false);
        }

        void OnDestroy()
        {
            if (_moduleStartup && _isConnected) _remoteMathApi.Stop();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private IEnumerator StartWebsocketClient()
        {
            _remoteMathApi = new RemoteMathWsApi.Handler(this);
            _remoteMathApi.PuzzleCode += ReceivedPuzzleCode;
            _remoteMathApi.PuzzleComplete += ReceivedPuzzleComplete;
            _remoteMathApi.PuzzleStrike += ReceivedPuzzleStrike;
            _remoteMathApi.PuzzleError += ReceivedPuzzleError;
            _remoteMathApi.PuzzleLog += ReceivedPuzzleLog;
            _remoteMathApi.Connected += WsConnected;
            _remoteMathApi.Disconnected += WsDisconnected;
            _remoteMathApi.PuzzleTwitchCode += ReceivedPuzzleTwitchPlaysCode;
            _remoteMathApi.Start();
            SetLed("Yellow");
            yield return WaitForWebsocketTimeout();
            yield return null;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void ReceivedPuzzleLog(RemoteMathWsApi.PuzzleLogEventArgs e)
        {
            Debug.LogFormat("[Remote Math #{0}] {2}: {1}", _moduleId, e.Message, e.FromServer ? "Server message" : "Websocket API");
        }

        private IEnumerator SendPuzzleFruit()
        {
            var fruitNumbers = new List<int>();
            for (var i = 0; i < 8; i++) fruitNumbers.Add(Convert.ToInt32(Math.Floor(UnityEngine.Random.Range(0f, fruitMats.Length))));

            fruit1.transform.Find("FruitImage").gameObject.GetComponent<MeshRenderer>().material = fruitMats[fruitNumbers[0]];
            fruit2.transform.Find("FruitImage").gameObject.GetComponent<MeshRenderer>().material = fruitMats[fruitNumbers[1]];
            fruit1.transform.Find("FruitText").gameObject.GetComponent<TextMesh>().text = fruitNames[fruitNumbers[2]];
            fruit2.transform.Find("FruitText").gameObject.GetComponent<TextMesh>().text = fruitNames[fruitNumbers[3]];
            fruit1.SetActive(true);
            fruit2.SetActive(true);
            Debug.LogFormat("[Remote Math #{0}] Puzzle Fruits: {1}", _moduleId, fruitNumbers.ToArray().Join(", "));
            _remoteMathApi.Send("PuzzleFruits::" + fruitNumbers.ToArray().Join("::"));
            yield return null;
        }

        IEnumerator SendBombDetails()
        {
            int batteryCount = bombInfo.GetBatteryCount();
            int portCount = bombInfo.GetPortCount();
            Debug.LogFormat("[Remote Math #{0}] Battery Count: {1}, Port Count: {2}", _moduleId, batteryCount, portCount);
            _remoteMathApi.Send("BombDetails::" + batteryCount.ToString() + "::" + portCount.ToString());
            yield return null;
        }

        void ReceivedPuzzleCode(RemoteMathWsApi.PuzzleCodeEventArgs e)
        {
            Debug.LogFormat("[Remote Math #{0}] Puzzle Code: {1}", _moduleId, e.Code);
            UnityMainThreadDispatcher.Instance().Enqueue(SendPuzzleFruit());
            UnityMainThreadDispatcher.Instance().Enqueue(SendBombDetails());
            _secretCode = e.Code;
            SetSecretCode(e.Code);
        }

        void ReceivedPuzzleTwitchPlaysCode(RemoteMathWsApi.PuzzleTwitchCodeEventArgs e)
        {
            _twitchPlaysCodes.Add(e.Code);
        }

        void TriggerModuleSolve()
        {
            _allowedToSolve = true;
        }

        void ReceivedPuzzleComplete()
        {
            Debug.LogFormat("[Remote Math #{0}] Puzzle Completed", _moduleId);
            SetSecretCode("DONE", true);
            SetLed("Orange");
            _remoteMathApi.Stop();
            TriggerModuleSolve();
        }

        void ReceivedPuzzleStrike()
        {
            Debug.LogFormat("[Remote Math #{0}] Puzzle Strike", _moduleId);
            HandleStrike();
        }

        void ReceivedPuzzleError()
        {
            Debug.LogFormat("[Remote Math #{0}] Puzzle Error", _moduleId);
            SetSecretCode("ERROR");
            SetLed("Blue");
            _hasErrored = true;
            _remoteMathApi.Stop();
            TriggerModuleSolve();
        }

        private void WsConnected(object sender, EventArgs e)
        {
            _remoteMathApi.Login();
            Debug.LogFormat("[Remote Math #{0}] WebSocket Connected", _moduleId);
            SetLed("White");
            _isConnected = true;
        }

        private void WsDisconnected(object sender, EventArgs e)
        {
            if (_allowedToSolve) return;
            Debug.LogFormat("[Remote Math #{0}] WebSocket Disconnected", _moduleId);
            SetSecretCode("ERROR");
            SetLed("Blue");
            _isConnected = false;
            _hasErrored = true;
            _remoteMathApi.Stop();
            TriggerModuleSolve();
        }


        private void SetLed(string led)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(ShowLed(led));
        }

        private void SetSecretCode(string code)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(ShowSecretCode(code, false));
        }

        private void SetSecretCode(string code, bool ignoreLineBreak)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(ShowSecretCode(code, ignoreLineBreak));
        }

        private IEnumerator ShowSecretCode(string code, bool ignoreLineBreak)
        {
            secretCodeText.GetComponent<TextMesh>().text = ignoreLineBreak || code.Length < 4 ? code : (code.Substring(0, 3) + "\n" + code.Substring(3));
            yield return null;
        }

        private IEnumerator WaitForWebsocketTimeout()
        {
            yield return new WaitForSeconds(15f);
            if (_isConnected) yield break;
            Debug.LogFormat("[Remote Math #{0}] WebSocket Connection Failed", _moduleId);
            SetLed("Blue");
            _remoteMathApi.Stop();
            TriggerModuleSolve();
        }

        private IEnumerator ShowLed(string led)
        {
            _currentLed = led;
            realStatusLitBoi.SetActive(false);
            var transformForFakeStatusLitBoi = fakeStatusLitBoi.transform;
            for (var i = 0; i < transformForFakeStatusLitBoi.childCount; i++) transformForFakeStatusLitBoi.GetChild(i).gameObject.SetActive(false);

            if (led != "Off") transformForFakeStatusLitBoi.Find(led).gameObject.SetActive(true);
            else realStatusLitBoi.SetActive(true);
            yield return null;
        }

        private void HandlePass()
        {
            UnityMainThreadDispatcher.Instance().Enqueue(HandlePassEnumerator());
        }

        private void HandleStrike()
        {
            UnityMainThreadDispatcher.Instance().Enqueue(HandleStrikeEnumerator());
        }

        private IEnumerator HandlePassEnumerator()
        {
            bombModule.HandlePass();
            SetLed("Green");
            yield return null;
        }

        private IEnumerator HandleStrikeEnumerator()
        {
            bombModule.HandleStrike();
            var savedLed = _currentLed;
            SetLed("Red");
            yield return new WaitForSeconds(1.5f);
            SetLed(savedLed);
            yield return null;
        }

#pragma warning disable 414
        // ReSharper disable once InconsistentNaming
        private readonly string TwitchHelpMessage = @"Use `!{0} go` to start the module and then use it again once you have solved it.";
#pragma warning restore 414

        // ReSharper disable once UnusedMember.Local
        private IEnumerator ProcessTwitchCommand(string command)
        {
            command = command.ToLowerInvariant().Trim();
            Debug.Log(command);
            if (command == "go")
            {
                yield return null;
                TwitchPlaysMode = true;
                GetTwitchPlaysId();
                if (!_hasErrored && _allowedToSolve)
                    // ReSharper disable once StringLiteralTypo
                    yield return "awardpointsonsolve -8";
                mainButton.OnInteract();
            }
            else if (Regex.IsMatch(command, @"^check +[0-9]{3}$"))
            {
                if (!TwitchPlaysMode) yield break;
                var vs = command.Split(' ');
                var code = vs.TakeLast(1).Join();
                yield return null;
                if (_twitchPlaysCodes.Contains(code))
                {
                    _remoteMathApi.Send("PuzzleActivateTwitchCode::" + code);
                    // ReSharper disable once StringLiteralTypo
                    yield return "sendtochat The requested expert module for Remote Math {1} has been activated";
                    yield return "strike";
                    yield return "solve";
                }
                else
                {
                    // ReSharper disable once StringLiteralTypo
                    yield return "sendtochat The requested expert module for Remote Math {1} doesn't exist";
                }
            }
        }
    }
}