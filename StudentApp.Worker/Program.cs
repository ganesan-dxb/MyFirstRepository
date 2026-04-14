using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StudentApp.Worker.Consumers;

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: false);
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((hostContext, services) =>
    {
        var config = hostContext.Configuration;
        var connStr = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required");

        var rabbitHost = config["RabbitMq:Host"]     ?? "rabbitmq";
        var rabbitUser = config["RabbitMq:Username"] ?? "guest";
        var rabbitPass = config["RabbitMq:Password"] ?? "guest";

        // Register connection string as a named string so consumers can receive it
        services.AddSingleton(connStr);

        services.AddMassTransit(x =>
        {
            x.AddConsumer<RegisterStudentConsumer>();
            x.AddConsumer<RegisterStudentFaultConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                cfg.ReceiveEndpoint("register-student-queue", e =>
                {
                    e.UseMessageRetry(r =>
                    {
                        r.Exponential(
                            retryLimit:    5,
                            minInterval:   TimeSpan.FromSeconds(2),
                            maxInterval:   TimeSpan.FromSeconds(60),
                            intervalDelta: TimeSpan.FromSeconds(5));

                        r.Ignore<InvalidOperationException>();
                    });

                    e.ConfigureConsumer<RegisterStudentConsumer>(ctx);
                });

                cfg.ReceiveEndpoint("register-student-fault-queue", e =>
                {
                    e.ConfigureConsumer<RegisterStudentFaultConsumer>(ctx);
                });
            });
        });
    })
    .Build()
    .RunAsync();
