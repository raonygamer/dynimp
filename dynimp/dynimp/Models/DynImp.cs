using Newtonsoft.Json;

namespace dynimp.Models;

public class DynImp
{
    public class ImportDescriptor
    {
        public class ImportPoint
        {
            [JsonProperty("version")] public string Version { get; set; } = string.Empty;
            [JsonProperty("type")] public string Type { get; set; } = string.Empty;
            [JsonProperty("value")] public string Value { get; set; } = string.Empty;
        }

        [JsonProperty("symbol")] public string Symbol { get; set; } = string.Empty;
        [JsonProperty("points")] public List<ImportPoint> Points { get; set; } = [];
    }

    [JsonProperty("target")] public string Target { get; set; } = string.Empty;
    [JsonProperty("imports")] public List<ImportDescriptor> Imports { get; set; } = [];
}