using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;

class PlaywrightHelpers
{
    public static async Task<List<string>> GetWebSearchResultUrlsAsync(string searchEngine, string query, int maxResults, bool headless)
    {
        // Initialize Playwright
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // Determine the search URL based on the chosen search engine
        var searchUrl = searchEngine == "bing"
            ? $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}"
            : $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";

        // Navigate to the URL
        await page.GotoAsync(searchUrl);

        // Extract search result URLs
        var urls = (searchEngine == "google")
            ? await ExtractGoogleSearchResults(page, maxResults)
            : await ExtractBingSearchResults(page, maxResults);

        return urls;
    }

    public static async Task<(string, string)> GetPageAndTitle(string url, bool stripHtml, string saveToFolder, bool headless)
    {
        // Initialize Playwright
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // Navigate to the URL
        await page.GotoAsync(url);

        // Fetch the page content and title
        var content = await FetchPageContent(page, url, stripHtml, saveToFolder);
        var title = await page.TitleAsync();

        // Return the content and title
        return (content, title);
    }

    private static async Task<List<string>> ExtractGoogleSearchResults(IPage page, int maxResults)
    {
        var urls = new List<string>();
        while (urls.Count < maxResults)
        {
            var elements = await page.QuerySelectorAllAsync("div#search a[href]");
            foreach (var element in elements)
            {
                var href = await element.GetAttributeAsync("href");
                if (href != null && href.StartsWith("http") && !href.Contains("google"))
                {
                    if (!urls.Contains(href))
                    {
                        urls.Add(href);
                    }
                }
                if (urls.Count >= maxResults) break;
            }

            if (urls.Count >= maxResults) break;

            var nextButton = await page.QuerySelectorAsync("a#pnnext");
            if (nextButton != null)
            {
                await nextButton.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
            else
            {
                break;
            }
        }
        return urls.Take(maxResults).ToList();
    }

    private static async Task<List<string>> ExtractBingSearchResults(IPage page, int maxResults)
    {
        var urls = new List<string>();
        while (urls.Count < maxResults)
        {
            var elements = await page.QuerySelectorAllAsync("li.b_algo a[href]");
            foreach (var element in elements)
            {
                var href = await element.GetAttributeAsync("href");
                if (href != null && href.StartsWith("http"))
                {
                    if (!urls.Contains(href))
                    {
                        urls.Add(href);
                    }
                }
                if (urls.Count >= maxResults) break;
            }

            if (urls.Count >= maxResults) break;

            var nextButton = await page.QuerySelectorAsync("a.sb_pagN");
            if (nextButton != null)
            {
                await nextButton.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
            else
            {
                break;
            }
        }
        return urls.Take(maxResults).ToList();
    }

    private static async Task<string> FetchPageContent(IPage page, string url, bool stripHtml, string saveToFolder)
    {
        try
        {
            // Navigate to the URL
            await page.GotoAsync(url);

            // Get the main content text
            var content = await FetchPageContentWithRetries(page);

            if (content.Contains("Rate limit is exceeded. Try again in"))
            {
                // Rate limit exceeded, wait and try again
                var seconds = int.Parse(content.Split("Try again in ")[1].Split(" seconds.")[0]);
                await Task.Delay(seconds * 1000);
                return await FetchPageContent(page, url, stripHtml, saveToFolder);
            }

            if (stripHtml)
            {
                content = HtmlHelpers.StripHtmlContent(content);
            }

            if (!string.IsNullOrEmpty(saveToFolder))
            {
                var fileName = FileHelpers.GenerateUniqueFileNameFromUrl(url, saveToFolder);
                File.WriteAllText(fileName, content);
            }

            return content;
        }
        catch (Exception ex)
        {
            return $"Error fetching content from {url}: {ex.Message}\n{ex.StackTrace}";
        }
    }

    private static async Task<string> FetchPageContentWithRetries(IPage page, int retries = 3)
    {
        var tryCount = retries + 1;
        while (true)
        {
            try
            {
                return await page.ContentAsync();
            }
            catch (Exception ex)
            {
                var rethrow = --tryCount == 0 || !ex.Message.Contains("navigating");
                if (rethrow) throw;
                await Task.Delay(1000);
            }
        }
    }
}