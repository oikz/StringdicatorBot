namespace Stringdicator.Database;

public class Response {
    /// <summary>
    /// The Response's Id, also the Primary Key.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The Hero this response belongs to
    /// 
    public Hero Hero { get; set; }
    
    /// <summary>
    /// The response's text
    /// </summary>
    public string ResponseText { get; set; }
    
    /// <summary>
    /// The response's URL
    /// </summary>
    public string Url { get; set; }
}