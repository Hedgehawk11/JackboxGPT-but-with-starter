﻿using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models
{
    public struct ChoiceRequest
    {
        public ChoiceRequest(int choice)
        {
            Value = choice;
        }
        
        [JsonProperty("action")]
        public static string Action => "choice";

        [JsonProperty("value")]
        public int Value { get; set; }
    }
}
