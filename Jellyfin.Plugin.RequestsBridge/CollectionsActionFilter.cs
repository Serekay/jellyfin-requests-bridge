using System;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Jellyfin.Plugin.RequestsBridge
{
    /// <summary>
    /// Action filter that disables UserData for Collections to fix slow loading issues.
    /// Based on: https://github.com/pelluch/jellyfin-plugin-disable-user-data
    /// </summary>
    public sealed class CollectionsActionFilter : IAsyncActionFilter
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<CollectionsActionFilter> _logger;

        public CollectionsActionFilter(
            ILibraryManager libraryManager,
            ILogger<CollectionsActionFilter> logger)
        {
            _libraryManager = libraryManager;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var config = RequestsBridgePlugin.Instance?.Configuration;
            if (config is null || !config.DisableUserDataOnCollections)
            {
                await next();
                return;
            }

            var request = context.HttpContext.Request;
            _logger.LogDebug("RequestsBridge: Intercepting path {Path} to check for Collections", request.Path);

            if (DisabledForCollections(context, request))
            {
                _logger.LogInformation("RequestsBridge: Disabling UserData for Collections at path {Path}", request.Path);
            }

            await next();
        }

        private bool DisabledForCollections(
            ActionExecutingContext context,
            HttpRequest request)
        {
            // Handles cases where the parent is not the collections folder, but collections are included.
            // Applies for things like navigating to Wolphin's Movies, then selecting collections
            if (request.Query.TryGetValue("includeItemTypes", out StringValues includeItemTypes) &&
                includeItemTypes.Contains("BoxSet"))
            {
                DisableUserData(context);
                return true;
            }

            // Handles cases where the parent is the collections folder, such as navigating to collections from the home
            // on Jellyfin web, Jellyfin Media Player, and others
            if (request.Query.TryGetValue("parentId", out StringValues parentIdValues) &&
                Guid.TryParse(parentIdValues[0], out var parentId))
            {
                BaseItem? parent = _libraryManager.GetItemById(parentId);
                if (parent is CollectionFolder)
                {
                    DisableUserData(context);
                    return true;
                }
            }

            return false;
        }

        private void DisableUserData(ActionExecutingContext context)
        {
            context.ActionArguments["enableUserData"] = false;
        }
    }
}
