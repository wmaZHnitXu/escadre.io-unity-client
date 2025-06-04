using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Scripts/DTOs/AnonymousTokenRequestDto.cs
[System.Serializable] // Если хотите видеть в инспекторе или использовать с JsonUtility
public class AnonymousTokenRequestDto
{
    public string Nickname { get; set; } // Убрал 'required' для совместимости со старыми C# версиями в Unity
}
