using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.RequestsBridge
{
    /// <summary>
    /// Helper class to dynamically inject action filters into controller actions.
    /// Based on: https://github.com/pelluch/jellyfin-plugin-disable-user-data
    /// </summary>
    public static class InjectActionFilter
    {
        /// <summary>
        /// Attach an action filter instance to all actions that match the matcher function.
        /// </summary>
        /// <typeparam name="T">The filter type to inject.</typeparam>
        /// <param name="provider">The action descriptor collection provider.</param>
        /// <param name="serviceProvider">The service provider for dependency injection.</param>
        /// <param name="matcher">A function to determine which actions to inject the filter into.</param>
        /// <returns>The number of actions the filter was attached to.</returns>
        public static int AddDynamicFilter<T>(
            this IActionDescriptorCollectionProvider provider,
            IServiceProvider serviceProvider,
            Func<ControllerActionDescriptor, bool> matcher)
            where T : IFilterMetadata
        {
            var targetActions = provider.ActionDescriptors.Items
                .OfType<ControllerActionDescriptor>()
                .Where(matcher)
                .ToArray();

            foreach (var action in targetActions)
            {
                // Let DI construct the filter so constructor dependencies work.
                var filter = ActivatorUtilities.CreateInstance<T>(serviceProvider);

                action.FilterDescriptors.Add(
                    new FilterDescriptor(filter, FilterScope.Global));
            }

            return targetActions.Length;
        }
    }
}
