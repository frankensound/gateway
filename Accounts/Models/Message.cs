using Newtonsoft.Json;

public class ActivityMessage
{
    [JsonProperty("user_id")]
    public int UserId { get; set; }

    [JsonProperty("activity")]
    public string Activity { get; set; }

    [JsonProperty("song_id")]
    public int SongId { get; set; }
}