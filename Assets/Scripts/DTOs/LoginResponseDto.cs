using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LoginResponseDto : TokenResponseDto
{
    public string AccessToken { get; set; }
    public System.DateTime AccessTokenExpiration { get; set; }
    public string RefreshToken { get; set; }
}
