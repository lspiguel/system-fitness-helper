using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Ipc.Pipes;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Service;
using SystemFitnessHelper.Service.Handlers;
using SystemFitnessHelper.Service.Pipes;
using SystemFitnessHelper.Services;

bool isWindowsService = WindowsServiceHelpers.IsWindowsService();

string logPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "SystemFitnessHelper", "logs", "sfh-.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .WriteTo.Conditional(_ => !isWindowsService, wt => wt.Console())
    .CreateLogger();

try
{
    IHost host = Host.CreateDefaultBuilder(args)
        .UseWindowsService(options => options.ServiceName = "SystemFitnessHelper")
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            services.Configure<ServiceConfig>(context.Configuration.GetSection("ServiceConfig"));

            // Documented override; takes precedence over appsettings.json.
            string? configPathOverride = Environment.GetEnvironmentVariable("SFH_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(configPathOverride))
                services.Configure<ServiceConfig>(c => c.ConfigPath = configPathOverride);

            services.PostConfigure<ServiceConfig>(c => c.Resolve());

            // Core services
            services.AddSingleton<IProcessScanner, WindowsProcessScanner>();
            services.AddSingleton<IRuleMatcher, RuleMatcher>();
            services.AddSingleton<IActionExecutor, WindowsActionExecutor>();
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IListService, ListService>();
            services.AddSingleton<IActionsService, ActionsService>();
            services.AddSingleton<IExecuteService, ExecuteService>();

            // IPC
            services.AddSingleton<EventPipeServer>();
            services.AddSingleton<CommandPipeServer>();
            services.AddSingleton<HandlerDispatcher>();
            services.AddSingleton<IRequestHandler, ConfigHandler>();
            services.AddSingleton<IRequestHandler, ListProcessHandler>();
            services.AddSingleton<IRequestHandler, ListTemplateHandler>();
            services.AddSingleton<IRequestHandler, ActionsHandler>();
            services.AddSingleton<IRequestHandler, ExecuteHandler>();
            services.AddSingleton<IRequestHandler, ConfigSaveHandler>();
            services.AddSingleton<IRequestHandler, PingHandler>();

            services.AddHostedService<ServiceWorker>();
        })
        .Build();

    ServiceConfig resolvedConfig = host.Services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceConfig>>().Value;
    Log.Information("Using rules file: {ConfigPath}", resolvedConfig.ConfigPath);

    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
