﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stringdicator.Database;

#nullable disable

namespace Stringdicator.Migrations
{
    [DbContext(typeof(ApplicationContext))]
    partial class ApplicationContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.5");

            modelBuilder.Entity("Stringdicator.Database.Channel", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Blacklisted")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("ImageBlacklisted")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("Stringdicator.Database.Hero", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<string>("Page")
                        .HasColumnType("TEXT");

                    b.HasKey("Name");

                    b.ToTable("Heroes");
                });

            modelBuilder.Entity("Stringdicator.Database.Response", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("HeroName")
                        .HasColumnType("TEXT");

                    b.Property<string>("ResponseText")
                        .HasColumnType("TEXT");

                    b.Property<string>("Url")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("HeroName");

                    b.ToTable("Responses");
                });

            modelBuilder.Entity("Stringdicator.Database.User", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("GorillaMoments")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Violations")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("Stringdicator.Database.Response", b =>
                {
                    b.HasOne("Stringdicator.Database.Hero", "Hero")
                        .WithMany("Responses")
                        .HasForeignKey("HeroName");

                    b.Navigation("Hero");
                });

            modelBuilder.Entity("Stringdicator.Database.Hero", b =>
                {
                    b.Navigation("Responses");
                });
#pragma warning restore 612, 618
        }
    }
}
