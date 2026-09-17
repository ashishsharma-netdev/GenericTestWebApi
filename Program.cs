using System.Text;
using GenericTestWebApi.Auth;
using GenericTestWebApi.Data;
using GenericTestWebApi.Payments;
using GenericTestWebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers(); builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<TestPrepDbContext>(o=>o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt")); builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHttpClient(); builder.Services.AddSingleton<RazorpayService>();
builder.Services.AddScoped<SubscriptionLifecycleService>(); builder.Services.AddHostedService<SubscriptionLifecycleHostedService>();
var jwt=builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{o.TokenValidationParameters=new TokenValidationParameters{ValidateIssuer=true,ValidateAudience=true,ValidateLifetime=true,ValidateIssuerSigningKey=true,ValidIssuer=jwt.Issuer,ValidAudience=jwt.Audience,IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),ClockSkew=TimeSpan.FromSeconds(30)};});
builder.Services.AddAuthorization();
builder.Services.AddCors(o=>o.AddPolicy("frontend",p=>p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app=builder.Build();
if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}
app.UseHttpsRedirection();app.UseCors("frontend");app.UseAuthentication();app.UseAuthorization();app.MapControllers();
using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<TestPrepDbContext>();await DbInitializer.InitializeAsync(db);}
app.MapGet("/api/health",async(TestPrepDbContext db)=>Results.Ok(new{status="ok",service="GenericTestWebApi",database=await db.Database.CanConnectAsync()}));
app.Run();