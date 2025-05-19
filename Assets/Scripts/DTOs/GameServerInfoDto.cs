// Рекомендуется создать отдельный файл, например, Scripts/DTOs/GameServerInfoDto.cs
// или поместить в тот же файл, где TokenResponseDto

[System.Serializable] // Для отображения в инспекторе и простой сериализации Unity
public class GameServerInfoDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; } // Этого поля не было в твоем скриншоте из AnonymousLoginUI, но оно есть в заглушке ServerListService
    public int Port { get; set; }       // Аналогично Address
    public int CurrentPlayers { get; set; } // Аналогично Address
    public int MaxPlayers { get; set; }   // Аналогично Address
    public int Ping { get; set; }
    // Добавьте или удалите поля, чтобы они точно соответствовали DTO вашего мастер-сервера
    // и тому, что вы используете в AnonymousLoginUI.cs
}