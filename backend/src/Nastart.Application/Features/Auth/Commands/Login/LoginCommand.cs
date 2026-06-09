using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Auth.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<ErrorOr<LoginResponse>>;