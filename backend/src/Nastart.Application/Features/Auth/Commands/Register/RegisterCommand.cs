using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Auth.Commands.Register;

public record RegisterCommand(
    string Email,
    string Password
) : IRequest<ErrorOr<RegisterResponse>>;