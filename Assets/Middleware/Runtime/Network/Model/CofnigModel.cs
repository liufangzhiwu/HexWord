using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class GameConfigResponse
{
    [JsonProperty("module_name")]
    public string ModuleName { get; set; }

    [JsonProperty("ab_group")]
    public string AbGroup { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("md5_hash")]
    public string Md5Hash { get; set; }

    [JsonProperty("csv_string")]
    public string CsvString { get; set; }
}
