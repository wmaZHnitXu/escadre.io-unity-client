using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameUI : UIScreen
{
    protected override void OnShow()    
    {
        base.OnShow(); // Хорошая практика - вызывать метод базового класса
        Debug.Log("GameUI Shown. Ready for input.");
        // Например, можно обновить список серверов, если он мог измениться
        // FetchAndDisplayServerList();
        // Или установить фокус на поле ввода ника
        // nicknameInputField.Select();
        // nicknameInputField.ActivateInputField();
    }
    protected override void OnHide()
    {
        base.OnHide();
        Debug.Log("GameUI Hidden.");
        // Например, сбросить поля ввода, если это необходимо
        // nicknameInputField.text = "";
    }
}
