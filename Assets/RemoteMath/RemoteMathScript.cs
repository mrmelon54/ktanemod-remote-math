using KModkit;
using RemoteMath;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

public class RemoteMathScript : MonoBehaviour
{
    public KMAudio BombAudio;
    public KMBombInfo BombInfo;
    public KMBombModule BombModule;
    public KMSelectable ModuleSelect;
    public KMSelectable MainButton;
    public GameObject SecretCodeText;
    public GameObject WelcomeText;
    public GameObject fakeStatusLitBoi;
    public GameObject realStatusLitBoi;
    public GameObject Fruit1;
    public GameObject Fruit2;
    public Material[] FruitMats;
    public Light[] Lights;
    public string[] FruitNames;
    private RemoteMathWSAPI.Handler RemoteMathApi;
    private string SecretCode = "";
    private string CurrentLed;

    bool moduleSolved = false;
    bool allowedToSolve = false;
    bool isConnected = false;
    bool moduleStartup = false;
    bool hasErrored = false;

    bool __TwitchPlaysMode;
    bool TwitchPlaysActive;
    internal bool TwitchPlaysMode
    {
        get
        {
            return TwitchPlaysActive || __TwitchPlaysMode;
        }
        set
        {
            __TwitchPlaysMode = value;
        }
    }
    readonly List<string> TwitchPlaysCodes = new List<string>();

    static int moduleIdCounter = 1;
    int moduleId;
    internal string TwitchId;

    void GetTwitchPlaysId()
    {
        Debug.Log("[RemoteMathCheck] A");
        var gType = ReflectionHelper.FindType("TwitchGame", "TwitchPlaysAssembly");
        Debug.Log("[RemoteMathCheck] B");
        object comp = FindObjectOfType(gType);
        Debug.Log("[RemoteMathCheck] C");
        var TwitchPlaysObj = comp.GetType().GetField("Modules", BindingFlags.Public | BindingFlags.Instance).GetValue(comp);
        Debug.Log("[RemoteMathCheck] D");
        IEnumerable TwitchPlaysModules = (IEnumerable)TwitchPlaysObj;
        Debug.Log("[RemoteMathCheck] E");
        foreach (object Module in TwitchPlaysModules)
        {
            var Behaviour = (MonoBehaviour)(Module.GetType().GetField("BombComponent", BindingFlags.Public | BindingFlags.Instance).GetValue(Module));
            Debug.Log("[RemoteMathCheck] F");
            var RMath = Behaviour.GetComponent<RemoteMathScript>();
            Debug.Log("[RemoteMathCheck] G");
            if (RMath == this)
            {
                TwitchId = (string)Module.GetType().GetProperty("Code", BindingFlags.Public | BindingFlags.Instance).GetValue(Module, null);
            }
        }
    }

    void Start()
    {
        moduleId = moduleIdCounter++;

        float scalar = transform.lossyScale.x;
        for (var i = 0; i < Lights.Length; i++)
            Lights[i].range *= scalar;

        SetSecretCode("");
        SetLED("Off");

        MainButton.OnInteract += delegate ()
        {
            if (!moduleStartup)
            {
                SecretCodeText.SetActive(true);
                WelcomeText.SetActive(false);
                moduleStartup = true;
                StartCoroutine(StartWebsocketClient());
                return false;
            }
            if (moduleSolved) return false;
            if (allowedToSolve)
            {
                moduleSolved = true;
                HandlePass();
            }
            return false;
        };

        Fruit1.SetActive(false);
        Fruit2.SetActive(false);
    }

    void OnDestroy()
    {
        if (moduleStartup && isConnected) RemoteMathApi.Stop();
    }

    IEnumerator StartWebsocketClient()
    {
        RemoteMathApi = new RemoteMathWSAPI.Handler(this);
        RemoteMathApi.PuzzleCode += ReceivedPuzzleCode;
        RemoteMathApi.PuzzleComplete += ReceivedPuzzleComplete;
        RemoteMathApi.PuzzleStrike += ReceivedPuzzleStrike;
        RemoteMathApi.PuzzleError += ReceivedPuzzleError;
        RemoteMathApi.PuzzleLog += ReceivedPuzzleLog;
        RemoteMathApi.Connected += WSConnected;
        RemoteMathApi.Reconnected += WSReconnected;
        RemoteMathApi.Disconnected += WSDisconnected;
        RemoteMathApi.PuzzleTwitchCode += ReceivedPuzzleTwitchPlaysCode;
        RemoteMathApi.Start();
        SetLED("Yellow");
        WaitForWebsocketTimeout();
        yield return null;
    }

    GameObject GetStatusLED(string colour)
    {
        return fakeStatusLitBoi.transform.Find(colour).gameObject;
    }

    void Update()
    {

    }

    void ReceivedPuzzleLog(RemoteMathWSAPI.PuzzleLogEventArgs e)
    {
        Debug.LogFormat("[Remote Math #{0}] {2}: {1}", moduleId, e.Message, e.FromServer ? "Server message" : "Websocket API");
    }

    IEnumerator SendPuzzleFruit()
    {
        List<int> fruitNumbers = new List<int>();
        for (int i = 0; i < 8; i++)
        {
            fruitNumbers.Add(Convert.ToInt32(Math.Floor(UnityEngine.Random.Range(0f, FruitMats.Length))));
        }
        Fruit1.transform.Find("FruitImage").gameObject.GetComponent<MeshRenderer>().material = FruitMats[fruitNumbers[0]];
        Fruit2.transform.Find("FruitImage").gameObject.GetComponent<MeshRenderer>().material = FruitMats[fruitNumbers[1]];
        Fruit1.transform.Find("FruitText").gameObject.GetComponent<TextMesh>().text = FruitNames[fruitNumbers[2]];
        Fruit2.transform.Find("FruitText").gameObject.GetComponent<TextMesh>().text = FruitNames[fruitNumbers[3]];
        Fruit1.SetActive(true);
        Fruit2.SetActive(true);
        Debug.LogFormat("[Remote Math #{0}] Puzzle Fruits: {1}", moduleId, fruitNumbers.ToArray().Join<int>(", "));
        RemoteMathApi.Send("PuzzleFruits::" + fruitNumbers.ToArray().Join<int>("::"));
        yield return null;
    }

    IEnumerator SendBombDetails()
    {
        int batteryCount = BombInfo.GetBatteryCount();
        int portCount = BombInfo.GetPortCount();
        Debug.LogFormat("[Remote Math #{0}] Battery Count: {1}, Port Count: {2}", moduleId, batteryCount, portCount);
        RemoteMathApi.Send("BombDetails::" + batteryCount.ToString() + "::" + portCount.ToString());
        yield return null;
    }

    void ReceivedPuzzleCode(RemoteMathWSAPI.PuzzleCodeEventArgs e)
    {
        Debug.LogFormat("[Remote Math #{0}] Puzzle Code: {1}", moduleId, e.Code);
        UnityMainThreadDispatcher.Instance().Enqueue(SendPuzzleFruit());
        UnityMainThreadDispatcher.Instance().Enqueue(SendBombDetails());
        SecretCode = e.Code;
        SetSecretCode(e.Code);
    }

    void ReceivedPuzzleTwitchPlaysCode(RemoteMathWSAPI.PuzzleTwitchCodeEventArgs e)
    {
        TwitchPlaysCodes.Add(e.Code);
    }

    void TriggerModuleSolve()
    {
        allowedToSolve = true;
    }

    void ReceivedPuzzleComplete()
    {
        Debug.LogFormat("[Remote Math #{0}] Puzzle Completed", moduleId);
        SetSecretCode("DONE", true);
        SetLED("Orange");
        RemoteMathApi.Stop();
        TriggerModuleSolve();
    }

    void ReceivedPuzzleStrike()
    {
        Debug.LogFormat("[Remote Math #{0}] Puzzle Strike", moduleId);
        HandleStrike();
    }

    void ReceivedPuzzleError()
    {
        Debug.LogFormat("[Remote Math #{0}] Puzzle Error", moduleId);
        SetSecretCode("ERROR");
        SetLED("Blue");
        hasErrored = true;
        RemoteMathApi.Stop();
        TriggerModuleSolve();
    }

    void WSConnected(object sender, EventArgs e)
    {
        RemoteMathApi.Login();
        Debug.LogFormat("[Remote Math #{0}] WebSocket Connected", moduleId);
        SetLED("White");
        isConnected = true;
    }

    void WSReconnected(object sender, EventArgs e)
    {
        RemoteMathApi.Send("timothy::" + SecretCode);
        Debug.LogFormat("[Remote Math #{0}] WebSocket Reconnected", moduleId);
        SetLED("White");
        isConnected = true;
    }

    void WSDisconnected(object sender, EventArgs e)
    {
        if (allowedToSolve) return;
        Debug.LogFormat("[Remote Math #{0}] WebSocket Disconnected", moduleId);
        SetSecretCode("ERROR");
        SetLED("Blue");
        isConnected = false;
        hasErrored = true;
        RemoteMathApi.Stop();
        TriggerModuleSolve();
    }



    IEnumerator ButtonPressAnimation()
    {
        yield return null;
    }

    void SetLED(string LED)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(ShowLED(LED));
    }

    void SetSecretCode(string code)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(ShowSecretCode(code, false));
    }

    void SetSecretCode(string code, bool dontDoTheLineBreakBecauseItDefinitelyIsntNeededAtAll)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(ShowSecretCode(code, true));
    }

    IEnumerator ShowSecretCode(string code, bool dontDoTheLineBreakBecauseItDefinitelyIsntNeededAtAll)
    {
        SecretCodeText.GetComponent<TextMesh>().text = (dontDoTheLineBreakBecauseItDefinitelyIsntNeededAtAll || code.Length < 4) ? code : (code.Substring(0, 3) + "\n" + code.Substring(3));
        yield return null;
    }

    IEnumerator WaitForWebsocketTimeout()
    {
        yield return new WaitForSeconds(15f);
        if (!isConnected)
        {
            Debug.LogFormat("[Remote Math #{0}] WebSocket Connection Failed", moduleId);
            SetLED("Blue");
            RemoteMathApi.Stop();
            TriggerModuleSolve();
        }
    }

    IEnumerator ShowLED(string LED)
    {
        CurrentLed = LED;
        realStatusLitBoi.SetActive(false);
        Transform transformfordafakestatuslitboi = fakeStatusLitBoi.transform;
        for (int i = 0; i < transformfordafakestatuslitboi.childCount; i++)
        {
            transformfordafakestatuslitboi.GetChild(i).gameObject.SetActive(false);
        }
        if (LED != "Off") transformfordafakestatuslitboi.Find(LED).gameObject.SetActive(true);
        else realStatusLitBoi.SetActive(true);
        yield return null;
    }

    void SetMaterial(GameObject a, Material b)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(ShowMaterial(a, b));
    }

    IEnumerator ShowMaterial(GameObject a, Material b)
    {
        a.GetComponent<MeshRenderer>().material = b;
        yield return null;
    }

    void SetActive(GameObject a, bool b)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(ShowActive(a, b));
    }

    IEnumerator ShowActive(GameObject a, bool b)
    {
        a.SetActive(b);
        yield return null;
    }

    void HandlePass()
    {
        UnityMainThreadDispatcher.Instance().Enqueue(IHandlePass());
    }

    void HandleStrike()
    {
        UnityMainThreadDispatcher.Instance().Enqueue(IHandleStrike());
    }

    IEnumerator IHandlePass()
    {
        BombModule.HandlePass();
        SetLED("Green");
        yield return null;
    }

    IEnumerator IHandleStrike()
    {
        BombModule.HandleStrike();
        string SavedLed = CurrentLed;
        SetLED("Red");
        yield return new WaitForSeconds(1.5f);
        SetLED(SavedLed);
        yield return null;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use `!{0} go` to start the module and then use it again once you have solved it.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant().Trim();
        Debug.Log(command);
        if (command == "go")
        {
            yield return null;
            TwitchPlaysMode = true;
            GetTwitchPlaysId();
            if (!hasErrored && allowedToSolve)
                yield return "awardpointsonsolve 8";
            MainButton.OnInteract();
        }
        else if (Regex.IsMatch(command, @"^check +[0-9]{3}$"))
        {
            if (TwitchPlaysMode)
            {
                string[] vs = command.Split(' ');
                string Code = vs.TakeLast(1).Join();
                yield return null;
                if (TwitchPlaysCodes.Contains(Code))
                {
                    RemoteMathApi.Send("PuzzleActivateTwitchCode::" + Code);
                    yield return "sendtochat The requested expert module for Remote Math {1} has been activated";
                    yield return "strike";
                    yield return "solve";
                }
                else
                {
                    yield return "sendtochat The requested expert module for Remote Math {1} doesn't exist";
                }
            }
        }
    }
}
