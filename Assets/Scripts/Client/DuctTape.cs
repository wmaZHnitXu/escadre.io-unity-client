using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DuctTape : MonoBehaviour
{
    public static DuctTape Instance;
    [SerializeField] private ClientComposer _composer;

    void Awake()
    {
        Instance = this;
    }
    public void GoPlayTheGame()
    {
        UIManager.Instance.SwitchToScreen(Assets.Scripts.UILogic.UIScreenType.GameUI);
        _composer.ConnectToTheServer();
    }
}
