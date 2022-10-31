// using TextUI;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<Main>();
        services.AddSingleton<Video>();
        services.AddSingleton<EventHandler>();
    })
    .Build();

try
{
    var main = host.Services.GetRequiredService<Main>();
    if (main == null)
        throw new Exception("Main service not found");
    main.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
}