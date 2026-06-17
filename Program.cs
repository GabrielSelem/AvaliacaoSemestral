using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var chaveJwt = "MINHA_CHAVE_SUPER_SECRETA_123456";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=salas.db"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(chaveJwt)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// LOGIN
app.MapPost("/login", (LoginRequest req) =>
{
    if (req.Email == "admin@email.com" && req.Senha == "123456")
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(chaveJwt);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, req.Email)
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Results.Ok(new { token = tokenHandler.WriteToken(token) });
    }

    return Results.Unauthorized();
});

// CRUD SALAS
app.MapGet("/salas", async (AppDbContext db) =>
    await db.Salas.ToListAsync())
    .RequireAuthorization();

app.MapPost("/salas", async (SalaReuniao sala, AppDbContext db) =>
{
    db.Salas.Add(sala);
    await db.SaveChangesAsync();
    return Results.Ok(sala);
}).RequireAuthorization();

app.MapPut("/salas/{id}", async (int id, SalaReuniao sala, AppDbContext db) =>
{
    var existente = await db.Salas.FindAsync(id);
    if (existente == null) return Results.NotFound();

    existente.Nome = sala.Nome;
    existente.Capacidade = sala.Capacidade;
    existente.PossuiProjetor = sala.PossuiProjetor;

    await db.SaveChangesAsync();
    return Results.Ok(existente);
}).RequireAuthorization();

app.MapDelete("/salas/{id}", async (int id, AppDbContext db) =>
{
    var sala = await db.Salas.FindAsync(id);
    if (sala == null) return Results.NotFound();

    db.Salas.Remove(sala);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.Run();

public class SalaReuniao
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public int Capacidade { get; set; }
    public bool PossuiProjetor { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; }
    public string Senha { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<SalaReuniao> Salas { get; set; }
}