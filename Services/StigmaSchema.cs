namespace wwwmmm.Services;

internal static class StigmaSchema
{
    public const string Version = "1.1.0";

    public static string ToJson()
    {
        var active = StigmaProfiles.FromEnvironment();
        return $$"""
                {
                    "schema": "stigma-execution-profile",
                    "version": "{{Version}}",
                    "environmentVariable": "{{StigmaProfiles.EnvironmentVariable}}",
                    "activeProfile": {
                        "level": {{(int)active}},
                        "name": "{{active.ToString().ToLowerInvariant()}}"
                    },
                    "profiles": [
                        {"level":1,"name":"quality","scan":"NanoDecibel","processing":"Hybrid Brotli","output":"Stigma lossless"},
                        {"level":2,"name":"balanced","scan":"TideWave","processing":"Auto Brotli","output":"High lossless"},
                        {"level":3,"name":"lightning","scan":"Lightning","processing":"CLX fast path","output":"Normal lossless"}
                    ],
                    "phases": ["scan", "process", "output"]
                }
                """;
    }
}
