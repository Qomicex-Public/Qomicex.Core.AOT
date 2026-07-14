using Qomicex.Core.AOT.Models.ParamsMeta;
using Qomicex.Core.AOT.Models.VersionMetadata;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.JsonContext
{
    [JsonSerializable(typeof(Config))]
    [JsonSerializable(typeof(Arguments))]
    [JsonSerializable(typeof(ParamEntry))]
    [JsonSerializable(typeof(Rule))]
    [JsonSerializable(typeof(OsRequirement))]
    internal partial class ParamsJsonContent : JsonSerializerContext
    {
    }
}
