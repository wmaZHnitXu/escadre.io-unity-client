// Scripts/DTOs/PasswordResetRequestResultDto.cs
[System.Serializable]
public class PasswordResetRequestResultDto
{
    public bool IsSuccess { get; set; }
    public string Error { get; set; } // string? становится string
}