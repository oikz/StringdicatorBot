# Stringdicator Discord Bot

Random Discord bot using .NET/C# that does random things like google image searching and posting the image  
Main feature: !string - googles a picture of string and sends it.  
- Secondary main feature - !stringsearch - googles a picture based on the users search terms and sends it.  
- Can also detect user sent images of Anime using Image Classification and Azure Custom Vision and record them as No Anime Violations, should other users agree.  
- Now also serves as a basic music playing bot using [Victoria](https://github.com/Yucked/Victoria) / [LavaLink](https://github.com/freyacodes/Lavalink) (LavaLink must always be running in the background).  

ENV variables required inside executable directory .env file:  
* TOKEN - Discord bot Token
* API_KEY - Google API Key
* SEARCH_ENGINE_ID - Google Custom Search Engine ID
* DEV_GUILD_ID - Discord Guild/Server ID for Interaction command development purposes
* DATABASE_URL - Database URL for storing persistent data
* DELETED_MESSAGE_CHANNEL_ID - Channel for sending deleted/edited message events

Container Built using `docker buildx build --platform linux/arm64,linux/amd64 --load -t stringdicator .` with containderd set in Docker Desktop and a custom buildx agent created based on the default using `docker buildx create --use desktop-linux` and enabling it in Docker Desktop 