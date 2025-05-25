// Scripts/DTOs/AuthResultDto.cs
[System.Serializable]
public class AuthResultDto
{
    public bool IsSuccess { get; set; }
    public string UserId { get; set; }
    public string Error { get; set; }
}