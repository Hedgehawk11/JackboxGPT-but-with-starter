using Newtonsoft.Json;

namespace JackboxGPT.Games.SurviveTheInternet.Models
{
    public struct SurviveTheInternetRoom
    {
        [JsonProperty("state")]
        public RoomState State { get; set; }
    }

    public enum RoomState
    {
        Lobby,
        Logo,
        Gameplay,
        EnterSingleText,
        Voting,
        MakeSingleChoice
    }
}