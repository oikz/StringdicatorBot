﻿using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Stringdicator.Database;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;

namespace Stringdicator.Util;

/// <summary>
///  Class to query the MediaWiki Dota 2 API and parse the responses to create a List of Heroes and Lists of each Hero's
/// Responses 
/// </summary>
public static class ResponseUtils {
    private const string TemplateResponsesUrl = "https://liquipedia.net/dota2game/Template:VoiceNavSidebar";
    private const string Responseurl = "https://liquipedia.net/dota2game/";

    /// <summary>
    /// Refresh the Heroes and Responses by using the Dota 2 Wiki
    /// </summary>
    /// <param name="httpClient">HTTPClient to reuse throughout the bot</param>
    /// <param name="applicationContext">Database Access</param>
    public static async Task RefreshResponses(HttpClient httpClient, ApplicationContext applicationContext) {
        // Get the list of heroes from the API
        var heroes = await GetHeroes(httpClient);
        // For each hero, get the responses from the page by scraping and parsing it
        foreach (var hero in heroes) {
            var dbHero = await applicationContext.Heroes.FindAsync(hero.Name);
            dbHero ??= applicationContext.Heroes.Add(hero).Entity;

            var responses = await GetResponses(httpClient, dbHero);
            var dbResponses = applicationContext.Responses.Where(response => response.Hero.Equals(dbHero));
            foreach (var response in responses.Where(response =>
                         !dbResponses.Any(dbResponse => dbResponse.Id.Equals(response.Id)))) {
                applicationContext.Responses.Add(response);
            }

            await applicationContext.SaveChangesAsync();

            dbResponses = applicationContext.Responses.Where(response => response.Hero.Equals(dbHero));
            dbHero.Responses = dbResponses.ToList();

            await applicationContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Get a list of heroes from the wiki
    /// </summary>
    /// <param name="httpClient">HTTPClient to reuse throughout the bot</param>
    /// <returns></returns>
    private static async Task<List<Hero>> GetHeroes(HttpClient httpClient) {
        // Send request to get all of the available heroes/responses
        var response = await httpClient.GetAsync(TemplateResponsesUrl);
        var responseString = await response.Content.ReadAsStringAsync();

        // Find the table on the page
        var doc = new HtmlDocument();
        doc.LoadHtml(responseString);
        // find all the links in the page that end with /responses
        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/Responses')]");
        var titles = links.Select(link => link.GetAttributeValue("title", "")).ToList();
        
        // Return new Hero objects with empty responses lists and ids
        return titles.Select(hero => new Hero {
            Name = hero.Replace("/Responses", ""),
            Responses = new List<Response>(),
            Page = hero
        }).ToList();
    }


    /// <summary>
    /// Get a list of responses from the wiki for a specific hero
    /// </summary>
    /// <param name="httpClient">HTTPClient to reuse throughout the bot</param>
    /// <param name="hero">The specific hero to find responses for</param>
    /// <returns></returns>
    private static async Task<List<Response>> GetResponses(HttpClient httpClient, Hero hero) {
        // Get the raw HTML for the page
        var response = httpClient.GetAsync(Responseurl + hero.Page);
        var responseString = await response.Result.Content.ReadAsStringAsync();

        // Parse the HTML of the string to get the list of responses
        var responses = new List<Response>();
        // Find all li elements
        var liElements = responseString.Split("<li");
        // For each li element, find the first audio element and get its source src
        var i = 0;
        foreach (var element in liElements) {
            if (!element.Contains("<audio hidden=\"\" class=\"ext-audiobutton\"")) continue;
            var audioElement = element.Split("<audio")[1];
            var src = audioElement.Split("src=\"")[1].Split("\"")[0];

            // Get the Response Text from the HTML
            var doc = new HtmlDocument();
            doc.LoadHtml(element);
            var responseText = doc.DocumentNode.GetDirectInnerText().Replace("\n", "").Remove(0, 1).Trim();

            // Create a new Response object with the src as the URL
            responses.Add(new Response {
                Id = hero.Name + i, // Unique Id for tracking later
                Url = src.Split("/revision/")[0],
                Hero = hero,
                ResponseText = responseText
            });
            i++;
        }

        return responses;
    }

    /// <summary>
    /// Retrieve a specific response from the database based on its Response Text
    /// </summary>
    /// <param name="query">The response text string to find</param>
    /// <param name="applicationContext">Database Access</param>
    /// <returns></returns>
    public static Response GetResponse(string query, ApplicationContext applicationContext) {
        // Take the query, remove any !, ., or ? characters and then create a list of queries, with one each using !, ., and ?
        query = query.Replace("!", "").Replace(".", "").Replace("?", "");
        query = query.ToLower();
        var queries = new List<string> {
            query + "!",
            query + ".",
            query + "?",
            query + "...?",
            query + "...!",
            query + "..."
        };

        // For each query, check if it exists in the database and return the first one that does
        return queries
            .Select(q => applicationContext.Responses.Include(x => x.Hero)
                .FirstOrDefault(x => x.ResponseText.ToLower().Equals(q)))
            .FirstOrDefault(response => response is not null);
    }
}