using KModkit;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RemoteMathScript : MonoBehaviour
{
    public KMAudio BombAudio;
    public KMBombInfo BombInfo;
    public KMBombModule BombModule;
    public KMSelectable ModuleSelect;
    public KMSelectable MainButton;
    public GameObject SecretCodeText;
    public GameObject fakeStatusLitBoi;
    public GameObject Fruit1;
    public GameObject Fruit2;
    public Material[] FruitMats;
    public string[] FruitNames;
    private RemoteMathWSAPI.Handler RemoteMathApi;

    bool moduleSolved = false;
    bool allowedToSolve = false;
    bool isConnected = false;

    static int moduleIdCounter = 1;
    int moduleId;

    void Start()
    {
        moduleId = moduleIdCounter++;

        SetSecretCode("");

        MainButton.OnInteract += delegate ()
        {
            if (allowedToSolve && !moduleSolved)
            {
                SetLED("Off");
                moduleSolved = true;
                HandlePass();
            }
            return false;
        };

        Fruit1.SetActive(false);
        Fruit2.SetActive(false);

        StartCoroutine(StartWebsocketClient());
    }

    void OnDestroy()
    {
        RemoteMathApi.Stop();
    }

    IEnumerator StartWebsocketClient()
    {
        RemoteMathApi = new RemoteMathWSAPI.Handler();
        RemoteMathApi.PuzzleCode += ReceivedPuzzleCode;
        RemoteMathApi.PuzzleComplete += ReceivedPuzzleComplete;
        RemoteMathApi.PuzzleStrike += ReceivedPuzzleStrike;
        RemoteMathApi.PuzzleError += ReceivedPuzzleError;
        RemoteMathApi.PuzzleLog += ReceivedPuzzleLog;
        RemoteMathApi.Connected += WSConnected;
        RemoteMathApi.Disconnected += WSDisconnected;
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
        Debug.LogFormat("[Remote Math #{0}] Server message: {1}", moduleId, e.Message);
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
        SetSecretCode(e.Code);
    }

    void TriggerModuleSolve()
    {
        allowedToSolve = true;
    }

    void ReceivedPuzzleComplete()
    {
        Debug.LogFormat("[Remote Math #{0}] Puzzle Completed", moduleId);
        SetSecretCode("DONE");
        SetLED("Orange");
        RemoteMathApi.Stop();
        TriggerModuleSolve();
    }

    void ReceivedPuzzleStrike()
    {
        Debug.LogFormat("[Remote Math #{0}] Puzzle Strike", moduleId);
        UnityMainThreadDispatcher.Instance().Enqueue(StartStrikeLEDAnimation());
        HandleStrike();
    }

    void ReceivedPuzzleError()
    {
        Debug.LogFormat("[Remote Math #{0}] Puzzle Error", moduleId);
        SetSecretCode("ERROR");
        SetLED("Blue");
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

    void WSDisconnected(object sender, EventArgs e)
    {
        if (allowedToSolve) return;
        Debug.LogFormat("[Remote Math #{0}] WebSocket Disconnected", moduleId);
        SetSecretCode("ERROR");
        SetLED("Blue");
        isConnected = false;
        RemoteMathApi.Stop();
        TriggerModuleSolve();
    }



    IEnumerator ButtonPressAnimation()
    {
        yield return null;
    }

    IEnumerator StartStrikeLEDAnimation()
    {
        StartCoroutine(StrikeLEDAnimation());
        yield return null;
    }

    IEnumerator StrikeLEDAnimation()
    {
        SetLED("Off");
        yield return new WaitForSeconds(1);
        SetLED("White");
    }

    void SetLED(string LED)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(ShowLED(LED));
    }

    void SetSecretCode(string code)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(ShowSecretCode(code));
    }

    IEnumerator ShowSecretCode(string code)
    {
        SecretCodeText.GetComponent<TextMesh>().text = code;
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
        Transform transformfordafakestatuslitboi = fakeStatusLitBoi.transform;
        for (int i = 0; i < transformfordafakestatuslitboi.childCount; i++)
        {
            transformfordafakestatuslitboi.GetChild(i).gameObject.SetActive(false);
        }
        if (LED != "Off")
        {
            transformfordafakestatuslitboi.Find(LED).gameObject.SetActive(true);
        }
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
        yield return null;
    }

    IEnumerator IHandleStrike()
    {
        BombModule.HandleStrike();
        yield return null;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Just go solve the module xD";
#pragma warning restore 414

    KMSelectable[] ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant().Trim();
        if (command == "go")
        {
            return new KMSelectable[] { MainButton.GetComponent<KMSelectable>() };
        }
        return null;
    }
}
