namespace Stringdicator.Database; 

/// <summary>
/// The User model represents a Discord user, with their ID and any additional data fields related to that user.
/// E.g., The number of No Anime Violations and the number of Gorilla Moments they have.
/// </summary>
public class User {
    /// <summary>
    /// The user's Discord Id, also the Primary Key.
    /// </summary>
    public ulong Id { get; set; }
    
    /// <summary>
    /// The number of No Anime Violations that the user has received.
    /// </summary>
    public int Violations { get; set; }
    
    /// <summary>
    /// The number of Gorilla Moments that the user has received.
    /// </summary>
    public int GorillaMoments { get; set; }
}