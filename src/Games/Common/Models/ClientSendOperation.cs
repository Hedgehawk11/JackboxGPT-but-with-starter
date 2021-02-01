﻿using Newtonsoft.Json;

namespace JackboxGPT3.Games.Common.Models
{
    public struct ClientSendOperation<TBody>
    {
        [JsonProperty("from")]
        public int From { get; set; }

        [JsonProperty("to")]
        public int To { get; set; }

        [JsonProperty("body")]
        public TBody Body { get; set; }
    }
}
