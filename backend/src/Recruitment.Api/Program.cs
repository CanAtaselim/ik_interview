using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Recruitment.Api.Auth;
using Recruitment.Api.Hubs;
using Recruitment.Api.Middleware;
using Recruitment.Application;
using Recruitment.Application.Abstractions;
using Recruitment.Infrastructure;
using Recruitment.Infrastructure.Identity;
using Recruitment.Infrastructure.Jobs;
using Recruitment.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Sunucuya özel değerler (bağlantı dizesi, imza anahtarı): depoya girmez, yayın klasörüne kopyalanır.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services
    // Boş alanlar için Türkçe mesajı kendi validator'larımız üretir.
    .AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = TurkishProblemDetails.Apply);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        // Claim adları token'daki haliyle kalsın ("sub", "role"); uzun XML şema adlarına çevrilmesin.
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AuthClaims.Name,
            RoleClaimType = AuthClaims.Role,
        };

        // WebSocket isteği başlık taşıyamaz; SignalR token'ı yalnızca hub adresinde sorgu dizesinden okunur.
        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments(NotificationHub.Path) &&
                    context.Request.Query.TryGetValue("access_token", out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization(Policies.Configure);

// Bildirimler API ile aynı biçimde (tür adları metin) iletilir.
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddSingleton<INotificationPusher, SignalRNotificationPusher>();

builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IConfiguration>((cors, configuration) => cors.AddDefaultPolicy(policy => policy
        .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        // Refresh token cookie'si için gerekli.
        .AllowCredentials()));

builder.Services.AddOpenApi();

var app = builder.Build();

// Rotalar "/api/v1/..." ve "/hubs/..." biçimindedir. API, IIS'te sitenin altında "/api" uygulaması
// olarak çalışırsa IIS bu öneki PathBase'e taşır ve rotalar eşleşmez; önek Path'e geri eklenir.
// Bildirim kanalı hem "/hubs/..." hem "/api/hubs/..." adresiyle erişilebilir.
app.Use((context, next) =>
{
    var request = context.Request;
    if (request.PathBase == "/api" && request.Path.StartsWithSegments("/v1"))
        request.Path = "/api" + request.Path;
    else if (request.Path.StartsWithSegments("/api/hubs", out var rest))
        request.Path = "/hubs" + rest;

    return next();
});

// Yönlendirme, yukarıdaki Path düzeltmesinden sonra çalışmalı; aksi halde çerçeve onu en başa kendisi ekler.
app.UseRouting();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    await app.Services.InitializeDatabase();

if (app.Configuration.GetValue("Jobs:Enabled", true))
    app.Services.RegisterRecurringJobs();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<RequestLogContextMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>(NotificationHub.Path);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.Run();
