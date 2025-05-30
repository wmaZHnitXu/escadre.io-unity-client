using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LoginResponseDto
{
    public string AccessToken { get; set; }
    public string AccessTokenExpirationISO { get; set; } // Для DateTime лучше передавать как ISO строку
    public System.DateTime AccessTokenExpiration // Свойство для парсинга
    {
        get { return System.DateTime.Parse(AccessTokenExpirationISO, null, System.Globalization.DateTimeStyles.RoundtripKind); }
        set { AccessTokenExpirationISO = value.ToString("o"); } // "o" - round-trip format specifier
    }
    public string RefreshToken { get; set; }
    public string UserId { get; set; }
    public string Nickname { get; set; }
}