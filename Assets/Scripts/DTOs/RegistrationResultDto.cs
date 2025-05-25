// Scripts/DTOs/RegistrationResultDto.cs
[System.Serializable]
public class RegistrationResultDto
{
    public bool IsSuccess { get; set; }
    public string UserId { get; set; } // string? на сервере становится string на клиенте (может быть null)
    public string[] Errors { get; set; } // IEnumerable<string>? становится string[] (может быть null или пустым)
}