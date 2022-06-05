using System;
using System.IO;

namespace Stringdicator {
    //Yoinked from https://dusted.codes/dotenv-in-dotnet
    /// <summary>
    /// Loads Values from a .env file to be provided by the user inside the current directory of the bot
    /// </summary>
    public static class DotEnv {
        /// <summary>
        /// Load the values from the file
        /// </summary>
        /// <param name="filePath">The path of the file to be read</param>
        public static void Load(string filePath) {
            //Read each line and save them as environment variables
            foreach (var line in File.ReadAllLines(filePath)) {
                var parts = line.Split(
                    '=',
                    StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2) {
                    continue;
                }

                Environment.SetEnvironmentVariable(parts[0], parts[1]);
            }
        }
    }
}