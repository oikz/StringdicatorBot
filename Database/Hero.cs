using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Stringdicator.Database; 

public class Hero {
    /// <summary>
    /// The Hero's in game Name
    /// </summary>
    [Key]
    public string Name { get; set; }
    
    /// <summary>
    /// Initially used to store the Page Title before being converted to the page id
    /// </summary>
    public string Page { get; set; }

    /// <summary>
    /// A list of all responses that this hero has
    /// </summary>
    public List<Response> Responses { get; set; }
}