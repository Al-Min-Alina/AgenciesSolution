using Agencies.API;
using Agencies.API.Filters;
using Agencies.API.Middleware;
using Agencies.API.Services;
using Agencies.API.Validators;
using Agencies.Infrastructure.Data;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Загрузка конфигурации в зависимости от окружения
var environment = builder.Environment;
builder.Configuration
    .SetBasePath(environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

Console.WriteLine($"Environment: {environment.EnvironmentName}");
Console.WriteLine($"Application Name: {builder.Environment.ApplicationName}");

// Configure logging based on environment
if (environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    // builder.Logging.AddApplicationInsights(); // Раскомментировать если используете Application Insights
}

// Add services to the container
//builder.Services.AddControllers(options =>
//{
//    options.Filters.Add<ValidationFilter>();
//})
//.AddFluentValidation(fv =>
//{
//    fv.RegisterValidatorsFromAssemblyContaining<CreatePropertyRequestValidator>();
//    fv.ImplicitlyValidateChildProperties = true;
//});

// Add services to the container
builder.Services.AddControllers();

// Configure Swagger only in development/staging
if (builder.Configuration.GetValue<bool>("Features:EnableSwagger", true))
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = $"Agencies API ({environment.EnvironmentName})",
            Version = "v1",
            Description = "API для системы управления риэлторскими агентствами",
            Contact = new OpenApiContact
            {
                Name = "Support",
                Email = "support@agencies.com",
                Url = new Uri("https://agencies.com/support")
            }
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] {}
            }
        });

        // Include XML comments if available
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });
}

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IReportsService, ReportsService>();

builder.Services.AddScoped<ValidationFilter>();

// HttpClient
builder.Services.AddHttpClient<IReportsService, ReportsService>();

// Validators
builder.Services.AddValidatorsFromAssemblyContaining<CreatePropertyRequestValidator>();

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=AgenciesDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False";
}

Console.WriteLine($"Database: {connectionString}");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);

    if (builder.Configuration.GetValue<bool>("Features:EnableDatabaseLogging") || builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

if (string.IsNullOrEmpty(secretKey))
{
    // Резервный ключ для разработки
    secretKey = "YourSuperSecretKeyHereAtLeast32CharactersLong!DevelopmentOnly";
    Console.WriteLine("Используется ключ JWT для разработки");
}

if (secretKey.Length < 32)
{
    throw new InvalidOperationException("JWT SecretKey must be at least 32 characters long.");
}

var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "AgenciesAPI",
        ValidAudience = jwtSettings["Audience"] ?? "AgenciesClient",
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };

    if (environment.IsDevelopment())
    {
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"Token validated for user: {context.Principal.Identity.Name}");
                return Task.CompletedTask;
            }
        };
    }
});

// Configure CORS
var corsSettings = builder.Configuration.GetSection("Cors");
var allowedOrigins = corsSettings.GetSection("AllowedOrigins").Get<string[]>();

if (allowedOrigins == null || allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "http://localhost:7149", "https://localhost:5168" };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    {
        builder.WithOrigins(allowedOrigins)
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials()
               .WithExposedHeaders("X-Pagination", "X-Total-Count");
    });
});

// Configure Rate Limiting
if (builder.Configuration.GetValue<bool>("RateLimiting:EnableRateLimiting", false))
{
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PermitLimit", 100),
                    Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimiting:Window", 1)),
                    QueueLimit = builder.Configuration.GetValue<int>("RateLimiting:QueueLimit", 2)
                }));

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
        };
    });
}

// Configure Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("Role", "Admin"));

    options.AddPolicy("UserOnly", policy =>
        policy.RequireClaim("Role", "User"));

    options.AddPolicy("AdminOrUser", policy =>
        policy.RequireClaim("Role", "Admin", "User"));
});

// Add Infrastructure services
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    if (app.Configuration.GetValue<bool>("Features:EnableSwagger", true))
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Agencies API v1");
            //c.RoutePrefix = "api-docs";
            c.RoutePrefix = "swagger";
            c.DocumentTitle = $"Agencies API Documentation ({environment.EnvironmentName})";
        });
    }
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Enable Rate Limiting
if (app.Configuration.GetValue<bool>("RateLimiting:EnableRateLimiting", false))
{
    app.UseRateLimiter();
}

app.UseHttpsRedirection();
app.UseCors("CorsPolicy");

// app.UseMiddleware<ExceptionHandlingMiddleware>();
// app.UseMiddleware<RequestLoggingMiddleware>();



app.Use(async (context, next) =>
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {context.Request.Method} {context.Request.Path}");
    await next();
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {context.Request.Method} {context.Request.Path} - {context.Response.StatusCode}");
});

// Детальное логирование запросов
app.Use(async (context, next) =>
{
    // Логируем только POST/PUT запросы к API
    if ((context.Request.Method == "POST" || context.Request.Method == "PUT")
        && context.Request.Path.StartsWithSegments("/api"))
    {
        Console.WriteLine($"=== ДЕТАЛЬНЫЙ ЛОГ ЗАПРОСА [{DateTime.Now:HH:mm:ss.fff}] ===");
        Console.WriteLine($"URL: {context.Request.Method} {context.Request.Path}");
        Console.WriteLine($"Query: {context.Request.QueryString}");
        Console.WriteLine($"Content-Type: {context.Request.ContentType}");
        Console.WriteLine($"Content-Length: {context.Request.ContentLength}");

        // ДОБАВЬТЕ ЭТУ ИНФОРМАЦИЮ:
        Console.WriteLine($"User-Agent: {context.Request.Headers["User-Agent"]}");
        Console.WriteLine($"Referer: {context.Request.Headers["Referer"]}");
        Console.WriteLine($"Origin: {context.Request.Headers["Origin"]}");
        Console.WriteLine($"Host: {context.Request.Host}");
        Console.WriteLine($"Remote IP: {context.Connection.RemoteIpAddress}");
        Console.WriteLine($"Remote Port: {context.Connection.RemotePort}");

        // Заголовки авторизации (маскируем токен)
        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authHeader))
        {
            if (authHeader.Length > 50)
            {
                Console.WriteLine($"Authorization: {authHeader.Substring(0, 50)}...");
            }
            else
            {
                Console.WriteLine($"Authorization: {authHeader}");
            }
        }

        // Сохраняем оригинальный поток
        context.Request.EnableBuffering();

        // Читаем тело запроса
        var bodyStream = context.Request.Body;
        using (var reader = new StreamReader(bodyStream, Encoding.UTF8, leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrEmpty(body) || body == "null")
            {
                Console.WriteLine($"Body: [EMPTY or NULL] (length: {body?.Length ?? 0})");
            }
            else
            {
                Console.WriteLine($"Body: {body}");
            }

            // Возвращаем поток в начало
            bodyStream.Position = 0;
        }

        Console.WriteLine("--- КОНЕЦ ЛОГА ---");

        // Записываем в файл для дальнейшего анализа
        try
        {
            var logDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, $"requests_{DateTime.Now:yyyyMMdd}.log");
            var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {context.Request.Method} {context.Request.Path} " +
                          $"RemoteIP:{context.Connection.RemoteIpAddress} " +
                          $"UserAgent:{context.Request.Headers["User-Agent"]} " +
                          $"BodySize:{context.Request.ContentLength}\n";

            File.AppendAllText(logFile, logEntry);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка записи в лог-файл: {ex.Message}");
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.Map("/error", (HttpContext context) =>
{
    var exceptionHandler = context.Features.Get<IExceptionHandlerFeature>();
    var exception = exceptionHandler?.Error;

    var response = new
    {
        StatusCode = context.Response.StatusCode,
        Message = exception?.Message ?? "An error occurred",
        Details = app.Environment.IsDevelopment() ? exception?.StackTrace : null
    };

    return Results.Json(response);
});

app.MapControllers();

// Метод для диагностики проблем с базой данных
static async Task<bool> DiagnoseDatabaseIssue(ApplicationDbContext context)
{
    Console.WriteLine("🔍 Диагностика проблемы с базой данных...");

    var connection = context.Database.GetDbConnection();

    try
    {
        Console.WriteLine($"Строка подключения: {connection.ConnectionString}");
        Console.WriteLine($"Источник данных: {connection.DataSource}");
        Console.WriteLine($"Имя базы данных: {connection.Database}");

        await connection.OpenAsync();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT @@VERSION";
            var version = await command.ExecuteScalarAsync();
            Console.WriteLine($"Версия SQL Server: {version}");
        }

        Console.WriteLine(" Диагностика завершена успешно");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($" Ошибка диагностики: {ex.Message}");
        return false;
    }
    finally
    {
        if (connection.State == System.Data.ConnectionState.Open)
        {
            await connection.CloseAsync();
        }
    }
}

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Console.WriteLine("Проверка подключения к базе данных...");

            // Проверяем подключение к базе данных
            if (await context.Database.CanConnectAsync())
            {
                Console.WriteLine(" Подключение к базе данных успешно установлено.");

                // Применяем миграции
                Console.WriteLine("Применение миграций...");
                try
                {
                    await context.Database.MigrateAsync();
                    Console.WriteLine(" Миграции успешно применены.");
                }
                catch (Exception migrateEx)
                {
                    Console.WriteLine($" Ошибка применения миграций: {migrateEx.Message}");
                    Console.WriteLine($"Детали: {migrateEx.InnerException?.Message}");
                    Console.WriteLine("Продолжение работы без миграций...");
                }

                // Заполняем начальными данными
                Console.WriteLine("Заполнение начальных данных...");
                try
                {
                    await SeedData.Initialize(context);
                    Console.WriteLine(" Начальные данные успешно заполнены.");
                }
                catch (Exception seedEx)
                {
                    Console.WriteLine($" Ошибка заполнения начальных данных: {seedEx.Message}");
                    Console.WriteLine($"Детали: {seedEx.InnerException?.Message}");
                    Console.WriteLine("Продолжение работы без начальных данных...");
                }
            }
            else
            {
                Console.WriteLine(" Не удалось подключиться к базе данных. Создание новой базы...");

                try
                {
                    await context.Database.EnsureCreatedAsync();
                    Console.WriteLine("База данных успешно создана.");

                    // Заполняем начальными данными
                    Console.WriteLine("Заполнение начальных данных...");
                    try
                    {
                        await SeedData.Initialize(context);
                        Console.WriteLine("Начальные данные успешно заполнены.");
                    }
                    catch (Exception seedEx)
                    {
                        Console.WriteLine($"Ошибка заполнения начальных данных: {seedEx.Message}");
                        Console.WriteLine($"Детали: {seedEx.InnerException?.Message}");
                        Console.WriteLine("Продолжение работы без начальных данных...");
                    }
                }
                catch (Exception createEx)
                {
                    Console.WriteLine($"Критическая ошибка при создании базы данных: {createEx.Message}");
                    Console.WriteLine($"Детали: {createEx.InnerException?.Message}");
                    Console.WriteLine("Проверьте:");
                    Console.WriteLine("1. Установлен ли SQL Server или LocalDB");
                    Console.WriteLine("2. Корректность строки подключения");
                    Console.WriteLine("3. Права доступа к серверу БД");

                    // Диагностика проблемы
                    await DiagnoseDatabaseIssue(context);

                    throw new InvalidOperationException("Не удалось инициализировать базу данных", createEx);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("----------");
        Console.WriteLine(" КРИТИЧЕСКАЯ ОШИБКА ИНИЦИАЛИЗАЦИИ БАЗЫ ДАННЫХ");
        Console.WriteLine("----------");
        Console.WriteLine($"Сообщение: {ex.Message}");
        Console.WriteLine($"Тип ошибки: {ex.GetType().Name}");

        if (ex.InnerException != null)
        {
            Console.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");

            if (ex.InnerException.InnerException != null)
            {
                Console.WriteLine($"Вложенная ошибка: {ex.InnerException.InnerException.Message}");
            }
        }

        Console.WriteLine("-----------------");
        Console.WriteLine("Стек вызовов:");
        Console.WriteLine(ex.StackTrace);
        Console.WriteLine("-----------------");
        Console.WriteLine("РЕКОМЕНДАЦИИ:");
        Console.WriteLine("1. Проверьте, запущен ли SQL Server / LocalDB");
        Console.WriteLine("2. Проверьте строку подключения в appsettings.json");
        Console.WriteLine("3. Убедитесь, что база данных существует");
        Console.WriteLine("4. Проверьте права доступа к БД");
        Console.WriteLine("-----------------");

        // Запрашиваем у пользователя действие (только если есть консоль)
        if (Environment.UserInteractive)
        {
            Console.WriteLine("Выберите действие:");
            Console.WriteLine("1 - Продолжить без базы данных (использовать InMemory)");
            Console.WriteLine("2 - Завершить работу приложения");
            Console.WriteLine("3 - Повторить попытку подключения");
            Console.Write("Ваш выбор (1/2/3): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    Console.WriteLine("Продолжение работы с InMemory базой данных...");

                    // Используем InMemory базу
                    var services = new ServiceCollection();
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase("AgenciesInMemoryDB"));

                    // Создаем новый контекст с InMemory базой
                    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseInMemoryDatabase("AgenciesInMemoryDB")
                        .Options;

                    using (var tempContext = new ApplicationDbContext(options))
                    {
                        await tempContext.Database.EnsureCreatedAsync();
                    }

                    Console.WriteLine(" InMemory база данных активирована.");
                    break;

                case "2":
                    Console.WriteLine("Завершение работы приложения...");
                    Environment.Exit(1);
                    return;

                case "3":
                    Console.WriteLine("Повторная попытка подключения через 5 секунд...");
                    await Task.Delay(5000);
                    break;

                default:
                    Console.WriteLine("Неверный выбор. Продолжение работы с ошибкой...");
                    break;
            }
        }
        else
        {
            Console.WriteLine("Завершение работы приложения из-за ошибки базы данных...");
            Environment.Exit(1);
            return;
        }
    }

    Console.WriteLine("-----------------");
    Console.WriteLine("ИНИЦИАЛИЗАЦИЯ БАЗЫ ДАННЫХ ЗАВЕРШЕНА");
    Console.WriteLine("-----------------");
    Console.WriteLine();
}

Console.WriteLine("Запуск приложения...");
Console.WriteLine($"Время запуска: {DateTime.Now:HH:mm:ss}");
Console.WriteLine($"Окружение: {app.Environment.EnvironmentName}");

// ПРАВИЛЬНОЕ ПОЛУЧЕНИЕ DbContext
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
        if (dbContext != null)
        {
            var connection = dbContext.Database.GetDbConnection();
            Console.WriteLine($"База данных: {connection.Database}");
            Console.WriteLine($"Сервер: {connection.DataSource}");
            Console.WriteLine($"Состояние: {connection.State}");
        }
        else
        {
            Console.WriteLine("База данных: Не удалось получить контекст");
        }
    }
}
catch (Exception dbEx)
{
    Console.WriteLine($"База данных: Ошибка получения информации - {dbEx.Message}");
}

Console.WriteLine("-----------------");
Console.WriteLine($"Application started in {environment.EnvironmentName} mode");
Console.WriteLine($"API available at: {app.Urls.FirstOrDefault()}");
if (app.Configuration.GetValue<bool>("Features:EnableSwagger", true))
{
    Console.WriteLine($"Swagger available at: {app.Urls.FirstOrDefault()}/api-docs");
}
Console.WriteLine("-----------------");

app.MapGet("/swagger", async context =>
    context.Response.Redirect("/api-docs"));

app.MapGet("/swagger/index.html", async context =>
    context.Response.Redirect("/api-docs/index.html"));

app.MapGet("/", async context =>
    context.Response.Redirect("/api-docs"));

app.Run();