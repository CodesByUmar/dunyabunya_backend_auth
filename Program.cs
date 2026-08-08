using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using AuthApi.Data;
using AuthApi.Services;
using Microsoft.OpenApi;
using AuthApi.Services;
// ...

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

builder.Services.AddScoped<IPhoneNormalizerService, PhoneNormalizerService>();
builder.Services.AddScoped<IEmailDomainValidatorService, EmailDomainValidatorService>();
builder.Services.AddScoped<IOdooService, NoOpOdooService>(); // Odoo tayyor bo'lganda shu qatorni almashtirasiz

// DbContext (PostgreSQL)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Controllers va Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AuthApi", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Faqat tokenni kiriting, 'Bearer' so'zini yozish shart emas."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

// CORS — frontend bilan ulanish uchun.
// Ro'yxat endi appsettings.json'dagi "Cors:AllowedOrigins" massividan olinadi,
// shuning uchun production domenini kodni o'zgartirmasdan qo'shish/o'chirish mumkin.
// Muhim: production frontend domeni (masalan Vercel) shu ro'yxatda bo'lishi SHART,
// aks holda browser CORS xatosi bilan API'ga so'rov yubora olmaydi.
const string CorsPolicyName = "AllowFrontends";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// --- Rate Limiting: login/register kabi endpointlarni brute-force'dan himoya qilish ---
// SlidingWindow FixedWindow'ga qaraganda yaxshiroq: FixedWindow'da oyna chegarasida
// (masalan 14:59 va 15:01) ketma-ket ikki marta limit to'lib, aslida 2x so'rovga
// ruxsat berilishi mumkin edi. SlidingWindow buni tekislaydi.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("AuthPolicy", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(15),
                SegmentsPerWindow = 3,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            message = "Juda ko'p urinish qildingiz. Iltimos, 15 daqiqadan keyin qayta urinib ko'ring."
        }, token);
    };
});

var app = builder.Build();

// --- Middleware pipeline (tartib muhim!) ---
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors(CorsPolicyName); // CORS UseAuthentication'dan oldin bo'lishi kerak

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

// Portni aniq belgilaymiz: local test uchun 3001, Render kabi hostinglar uchun PORT env variable ustunlik qiladi
var port = Environment.GetEnvironmentVariable("PORT") ?? "3001";
app.Run($"http://0.0.0.0:{port}");