using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Auth.Commands.VerifyEmail;

public class VerifyEmailHander(IAppDbContext db) : IRequestHandler<VerifyEmailCommand, ErrorOr<VerifyEmailResponse>>
{
    public async Task<ErrorOr<VerifyEmailResponse>> Handle(
        VerifyEmailCommand command, CancellationToken ct)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == command.UserId, ct)
            .ConfigureAwait(false);

        if(user is null)
            return Error.NotFound("User.NotFound", "User not found.");
        
        if(user.IsEmailVerified)
            return Error.Conflict("User.AlreadyVerified", "Email is already verified.");

        user.IsEmailVerified = true;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new VerifyEmailResponse("Email verified. You can now log in");
    }
}