using Victoria;

namespace Stringdicator.Util;

/// <summary>
/// Override the LavaLink Configuration to set the default hostname
/// </summary>
public record LavaLinkConfiguration : Configuration {
    public LavaLinkConfiguration() {
        Hostname = "lavalink";
    }
}