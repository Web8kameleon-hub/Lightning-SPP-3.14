namespace wwwmmm.Services;

internal static class StigmaSchema
{
    public const string Version = "1.0.0";

    public static string ToJson()
    {
        return $$"""
                {
                    "schema": "stigma-language",
                    "version": "{{Version}}",
                    "phases": ["scan", "resonance", "imprint", "enhancement", "stabilization", "print"],
                    "fields": [
                        {"name":"resonance","type":"float","range":"[0,1]"},
                        {"name":"ndb","type":"float","formula":"20*log10(max(resonance,1e-12))*1e9"},
                        {"name":"tideTension","type":"float"},
                        {"name":"imprintId","type":"string"},
                        {"name":"stigmaLevel","type":"string","values":["LIGHTNING","NORMAL","CONSERVATIVE"]}
                    ]
                }
                """;
    }
}
