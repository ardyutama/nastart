using Nastart.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

// builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// builder.Services.AddMediatR(cfg =>
// {
//     cfg.RegisterServicesFromAssemblyContaining<Program>();

//     cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
//     cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
// });

var app = builder.Build();

// app.UseExceptionHandler(exceptionApp =>
// {
//     exceptionApp.Run(async context =>
//     {
//         var exceptionHandlerFeature = context.Features
//             .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

//         if (exceptionHandlerFeature?.Error is ValidationException validationEx)
//         {
//             var errors = validationEx.Errors
//                 .GroupBy(e => e.PropertyName)
//                 .ToDictionary(
//                     g => g.Key,
//                     g => g.Select(e => e.ErrorMessage).ToArray()
//                 );

//             context.Response.StatusCode = 400;
//             context.Response.ContentType = "application/problem+json";

//             await context.Response.WriteAsJsonAsync(new
//             {
//                 type = "https://tools.ietf.org/html/rfc7807",
//                 title = "Validation failed",
//                 status = 400,
//                 errors
//             });

//             return;
//         }

//         context.Response.StatusCode = 500;
//         context.Response.ContentType = "application/problem+json";
//         await context.Response.WriteAsJsonAsync(new
//         {
//             type = "https://tools.ietf.org/html/rfc7807",
//             title = "An unexpected error occurred",
//             status = 500
//         });
//     });
// });

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "Nastart API is running!");
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
// app.MapIngredientsEndpoints();
// app.MapRecipesEndpoints();

app.Run();
