using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Jellyfin.Plugin.RequestsBridge
{
    /// <summary>
    /// Registriert Plugin-Services bei Jellyfin.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
        {
            // Registriert den EntryPoint als Hosted Service
            services.AddHostedService<RequestsBridgeEntryPoint>();

            // Registriert den Collections Action Filter Hosted Service
            services.AddHostedService<CollectionsFilterSetup>();
        }
    }

    /// <summary>
    /// Hosted service that injects the Collections action filter at startup.
    /// </summary>
    internal sealed class CollectionsFilterSetup : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IActionDescriptorCollectionProvider _actionDescriptorProvider;
        private readonly ILogger<CollectionsFilterSetup> _logger;

        public CollectionsFilterSetup(
            IServiceProvider serviceProvider,
            IActionDescriptorCollectionProvider actionDescriptorProvider,
            ILogger<CollectionsFilterSetup> logger)
        {
            _serviceProvider = serviceProvider;
            _actionDescriptorProvider = actionDescriptorProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                const string BaseName = "Jellyfin.Api.Controllers";
                var controllersMap = new Dictionary<string, List<string>>
                {
                    [$"{BaseName}.ItemsController"] = new List<string> { "GetItemsByUserIdLegacy", "GetItems" }
                };

                var count = _actionDescriptorProvider.AddDynamicFilter<CollectionsActionFilter>(
                    _serviceProvider,
                    cad =>
                    {
                        var controllerName = cad.ControllerTypeInfo.FullName;
                        var methodName = cad.MethodInfo.Name;

                        if (controllerName == null)
                        {
                            return false;
                        }

                        return controllersMap.TryGetValue(controllerName, out var methodNames)
                               && methodNames.Contains(methodName);
                    });

                _logger.LogInformation("RequestsBridge: Attached CollectionsActionFilter to {Count} actions", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestsBridge: Failed to attach Collections action filter");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
