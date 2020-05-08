using System;
using System.Collections;
using UnityEngine;

public class RemoteMathScript : MonoBehaviour {
    public KMAudio BombAudio;
    public KMBombInfo BombInfo;
    public KMBombModule BombModule;
    public KMSelectable ModuleSelect;
    public KMSelectable MainButton;
    public GameObject SecretCodeText;
    public GameObject fakeStatusLitBoi;
    private RemoteMathWSAPI.Handler RemoteMathApi;

    bool moduleSolved = false;
    bool allowedToSolve = false;
    bool isConnected = false;

    static int moduleIdCounter = 1;
    int moduleId;

    void Start() {
        moduleId = moduleIdCounter++;

        SetSecretCode("");

        MainButton.OnInteract += delegate()
        {
            if (allowedToSolve)
            {
                SetLED("Off");
                moduleSolved = true;
                HandlePass();
            }
            return false;
        };

        StartCoroutine(StartWebsocketClient());
    }

    IEnumerator StartWebsocketClient()
    {
        RemoteMathApi = new RemoteMathWSAPI.Handler();
        RemoteMathApi.PuzzleCode += ReceivedPuzzleCode;
        RemoteMathApi.PuzzleComplete += ReceivedPuzzleComplete;
        RemoteMathApi.PuzzleStrike += ReceivedPuzzleStrike;
        RemoteMathApi.PuzzleError += ReceivedPuzzleError;
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

    void Update() {
        
    }

    void ReceivedPuzzleCode(RemoteMathWSAPI.PuzzleCodeEventArgs e)
    {
        Debug.LogFormat("[Remote Math #{0}] Puzzle Code: {1}", moduleId, e.Code);
        SetSecretCode(e.Code);
    }

    void TriggerModuleSolve()
    {
        allowedToSolve = true;
    }

    void ReceivedPuzzleComplete()
    {
        Debug.LogFormat("[Remote Math #{0}] Puzzle Completed", moduleId);
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
        SetLED("Blue");
        isConnected = false;
        RemoteMathApi.Stop();
        TriggerModuleSolve();
    }



    IEnumerator ButtonPressAnimation() {
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
        if(!isConnected)
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
        for(int i=0;i<transformfordafakestatuslitboi.childCount;i++)
        {
            transformfordafakestatuslitboi.GetChild(i).gameObject.SetActive(false);
        }
        if(LED!="Off")
        {
            transformfordafakestatuslitboi.Find(LED).gameObject.SetActive(true);
        }
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

    KMSelectable[] ProcessTwitchCommand(string command) {
        command = command.ToLowerInvariant().Trim();
        return null;
    }
}
