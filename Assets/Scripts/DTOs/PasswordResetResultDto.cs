// Scripts/DTOs/PasswordResetResultDto.cs
[System.Serializable]
public class PasswordResetResultDto
{
    public bool IsSuccess { get; set; }
    public string Error { get; set; }      // string? становится string
    public string[] Errors { get; set; } // IEnumerable<string>? становится string[]
}