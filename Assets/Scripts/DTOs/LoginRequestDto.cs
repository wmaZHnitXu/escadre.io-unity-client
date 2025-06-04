using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LoginRequestDto
{
    public string Identifier { get; set; }
    public string Password { get; set; }
}
