using System;
using System.IO;
using System.Threading.Tasks;
using Discord;

namespace Stringdicator {
    class Stringdicator {
        public static void Main(string[] args)
            => new Stringdicator().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync() {
            //Load Token from env file
            string root = Directory.GetCurrentDirectory();
            string dotenv = Path.Combine(root, ".env");
            DotEnv.Load(dotenv);
            var toge = Environment.GetEnvironmentVariable("TOKEN");
            int hoge = 0;

        }

        private Task Log(LogMessage msg) {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}