namespace Stringdicator.Database;

/// <summary>
/// Channel model representing a single Discord channel in a server.
/// </summary>
public class Channel {
    /// <summary>
    /// The server's Discord Id, also the Primary Key.
    /// </summary>
    public ulong Id { get; set; }
    
    /// <summary>
    /// Whether or not this server has been blacklisted from commands.
    /// </summary>
    public bool Blacklisted { get; set; }
    
    /// <summary>
    /// Whether or not this server has been blacklisted from Image Classification checks.
    /// </summary>
    public bool ImageBlacklisted { get; set; }
}