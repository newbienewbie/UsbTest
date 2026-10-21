using StdUnit.Tags.BlazorLib.Apps;
using StdUnit.Tags.McpServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using usbtest.Tags;

namespace usbtest;

public static class STARTUP
{
    public static void ConfigureServies(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddSerilog((sp, lc) => lc
            .ReadFrom.Services(sp)
            .WriteTo.Console()
            .WriteTo.File("logs/log.txt", outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [{SourceContext}] {Message}{NewLine}{Exception}", rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext()
        );
        services.AddTags();

        services.AddMcpServer()
            .WithHttpTransport(opts => {
                opts.Stateless = true;
            })
            .AddTagsMcp();
        services.AddAuthentication();
    }

    public static void ConfigureMiddlewares(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            // ...
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseAntiforgery();
        app.MapRazorComponents<TagsApp>()
            .AddInteractiveServerRenderMode();
        app.MapMcp("/mcp");
    }
}
