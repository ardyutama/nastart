using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Auth.Commands.VerifyEmail;

public record VerifyEmailCommand(Guid UserId) : IRequest<ErrorOr<VerifyEmailResponse>>;