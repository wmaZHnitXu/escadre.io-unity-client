// Scripts/DTOs/PlayerStatsDto.cs (в Unity)
using System.Text.Json.Serialization; // Нужен этот using

[System.Serializable]
public class PlayerStatsDto
{
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } // Рекомендую использовать свойства

    [JsonPropertyName("kills")]
    public int Kills { get; set; }

    [JsonPropertyName("deaths")]
    public int Deaths { get; set; }

    [JsonPropertyName("playTime")]
    public string PlayTime { get; set; }

    [JsonPropertyName("kdRatio")]
    public float KDRatio { get; set; }
}