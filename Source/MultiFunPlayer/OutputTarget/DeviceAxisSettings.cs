﻿using Newtonsoft.Json;
using Stylet;

namespace MultiFunPlayer.OutputTarget;

[JsonObject(MemberSerialization.OptIn)]
internal class DeviceAxisSettings : PropertyChangedBase
{
    [JsonProperty] public double Minimum { get; set; } = 0;
    [JsonProperty] public double Maximum { get; set; } = 100;
}