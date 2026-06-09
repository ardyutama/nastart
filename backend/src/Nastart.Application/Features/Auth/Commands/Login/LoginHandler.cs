using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Auth.Commands.Login;

public class LoginHandler(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokenService)
    : IRequestHandler<LoginCommand, ErrorOr<LoginResponse>>
{
    public async Task<ErrorOr<LoginResponse>> Handle(
        LoginCommand command, CancellationToken ct)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email.ToLowerInvariant(), ct)
            .ConfigureAwait(false);

        if (user is null)
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

        if (!hasher.Verify(command.Password, user.PasswordHash))
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

        if (!user.IsEmailVerified)
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

        var token = tokenService.GenerateToken(user.Id, user.Email);
        return new LoginResponse(token);
    }

}