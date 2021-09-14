# Stringdicator Discord Bot

Random Discord bot using .NET/C# that does random things like google image searching and posting the image  
Main feature: !string - googles a picture of string and sends it.  
- Secondary meain feature - !stringsearch - googles a picture based on the users search terms and sends it.  
- Can also detect user sent images of String using Image Classification and Azure Custom Vision.  
- Now also serves as a basic music playing bot using Victoria / LavaLink (Lavalink must always be running in the background).  

ENV variables required inside executable directory .env file:  
* TOKEN - Discord bot Token
* API_KEY - Google API Key
* SEARCH_ENGINE_ID - Google Custom Search Engine ID
