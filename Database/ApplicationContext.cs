using System;
using Microsoft.EntityFrameworkCore;

namespace Stringdicator.Database;

public class ApplicationContext : DbContext {
    public DbSet<User> Users { get; set; }
    public DbSet<Channel> Channels { get; set; }
    public DbSet<Hero> Heroes { get; set; }
    public DbSet<Response> Responses { get; set; }


    private string DbPath { get; }

    public ApplicationContext() {
        DbPath = Environment.GetEnvironmentVariable("DATABASE_URL");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}



