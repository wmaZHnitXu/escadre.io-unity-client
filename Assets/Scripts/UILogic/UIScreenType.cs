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
        Registration,        // <--- Экран регистрации (скриншот 1)
        PasswordRestoration, // <--- Экран восстановления пароля (скриншот 2)
        UserProfileUI, 
        GameUI,
        EmailConfirmationSent,      // Скриншот 1: Письмо для подтверждения email отправлено
        PasswordResetEmailSent,   // Скриншот 2: Письмо для сброса пароля отправлено (с таймером)
        EnterNewPassword,         // Скриншот 3: Ввод нового пароля после подтверждения сброса
        AccountNotFound, 
    }
}

