using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<NastartDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

builder.Services.AddValidatorsFromAssemblyContaining<CreateIngredientValidator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapGet("/", ()=> "Nastart API is running!");
app.MapIngredientsEndpoints();

app.Run();
