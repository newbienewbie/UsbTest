using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using StdUnit.Tags;
using StdUnit.Tags.BlazorLib;
using StdUnit.Tags.S7;
using StdUnit.Tags.SimpleFiles;
using StdUnit.Tags.WinUsbKeyboardCodeScanners;

namespace usbtest.Tags;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTags(this IServiceCollection services)
    {
        services.AddTagsProjectServices(builder =>
        {
            // 可选：开启 XML Schema 验证
            // builder.EnableXmlSchemaValidation();

            // S7 支持
            builder.AddS7Support();

            // 简单文件支持
            builder.AddSimpleFilesChannel()
                // 既然使用了WPF，就不支持AOT，所以下面这行没有必要启用
                //.AddJsonSerializerContext(MyJsonSerializerContext.Default)
                .AddSimpleFilesDirectTagBuilder(
                    configure: b => b.WithJsonTagFactory<MyJson>(),
                    predicate: b => b.TagDescriptor.TagKind == "MyJson"
                ) 
                // ... 可以以类似手法注册其它特化的节点
                // 内置的简单文件支持
                .AddSimpleFilesDirectTagBuilder()
                .AddUsbKeyboardCodeScannerSupport();
        });

        // blazor Tags/monitor 
        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        services.AddMudServices();
        services.AddTagsBlazorLibCore();

        return services;
    }
}