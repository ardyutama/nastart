using MediatR;
using Nastart.Api.Extensions;
using Nastart.Application.Features.Auth.Commands.Login;
using Nastart.Application.Features.Auth.Commands.Register;
using Nastart.Application.Features.Auth.Commands.VerifyEmail;

namespace Nastart.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .AllowAnonymous();

        group.MapPost("/register", async (RegisterCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            if (result.IsError)
                return result.ToApiResult();

            return Results.Created($"/api/users/{result.Value!.UserId}", result.Value);
        });

        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.ToApiResult();
        });

        group.MapPost("/verify", async (VerifyEmailCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.ToApiResult();
        });
    }
}