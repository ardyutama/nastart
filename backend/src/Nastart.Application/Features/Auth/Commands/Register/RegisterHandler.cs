using System.Security.Cryptography;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Entities;

namespace Nastart.Application.Features.Auth.Commands.Register;

public class RegisterHandler(
    IAppDbContext db,
    IPasswordHasher hasher,
    IEmailService email
) : IRequestHandler<RegisterCommand, ErrorOr<RegisterResponse>>
{
    public async Task<ErrorOr<RegisterResponse>> Handle(
        RegisterCommand command, CancellationToken ct)
    {
        var emailTaken = await db.Users
            .AnyAsync(u => u.Email == command.Email.ToLowerInvariant(), ct)
            .ConfigureAwait(false);

        if (emailTaken)
            return Error.Conflict("User.EmailTaken", "An account with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = command.Email.ToLowerInvariant(),
            PasswordHash = hasher.Hash(command.Password),
            IsEmailVerified = false
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        await email.SendAsync(
            user.Email,
            "Verify your Nastart account",
            $"Click here to verify: /api/auth/verify?token={verificationToken}&userId={user.Id}"
        ).ConfigureAwait(false);

        return new RegisterResponse(user.Id, "Check your email to verify your account");
    }
}