// Scripts/DTOs/EmailConfirmationResultDto.cs
[System.Serializable]
public class EmailConfirmationResultDto
{
    public bool IsSuccess { get; set; }
    public string Error { get; set; }
    public string[] Errors { get; set; }
}