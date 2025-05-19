using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.UILogic
{
    public enum UIScreenType
    {
        None,
        AnonymousLogin,
        RegisteredLogin, // Если будет отдельный экран для входа с аккаунтом
        GameUI,
        Loading, // Экран загрузки между сценами или операциями
            // ... другие экраны
    }
}

