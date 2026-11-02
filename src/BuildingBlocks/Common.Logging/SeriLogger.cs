using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace Common.Logging
{
    public static class SeriLogger
    {
        public static Action<HostBuilderContext, LoggerConfiguration> Configure =>
           (context, configuration) =>
           {
               configuration
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .WriteTo.Debug()
                    .WriteTo.Console();

               // Elasticsearch is an optional log sink. Local development and CI must
               // remain able to start without requiring a separate Elasticsearch node.
               var elasticUri = context.Configuration["ElasticConfiguration:Uri"];
               if (Uri.TryCreate(elasticUri, UriKind.Absolute, out var elasticEndpoint) &&
                   (elasticEndpoint.Scheme == Uri.UriSchemeHttp || elasticEndpoint.Scheme == Uri.UriSchemeHttps))
               {
                   configuration.WriteTo.Elasticsearch(
                       new ElasticsearchSinkOptions(elasticEndpoint)
                       {
                           IndexFormat = $"applogs-{context.HostingEnvironment.ApplicationName?.ToLower().Replace(".", "-")}-{context.HostingEnvironment.EnvironmentName?.ToLower().Replace(".", "-")}-{DateTime.UtcNow:yyyy-MM}",
                           AutoRegisterTemplate = true,
                           NumberOfShards = 2,
                           NumberOfReplicas = 1
                       });
               }

               configuration
                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                    .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
                    .ReadFrom.Configuration(context.Configuration);
           };
    }
}
