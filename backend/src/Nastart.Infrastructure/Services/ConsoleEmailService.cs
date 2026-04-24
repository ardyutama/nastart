using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Infrastructure.Services;

public class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger = logger;

    public Task SendAsync(string to, string subject, string body)
    {
        _logger.LogInformation(
            "== EMAIL ==\nTo: {To}\nSubject: {Subject}\nBody:\n{Body}\n== END EMAIL ==",
            to, subject, body);
        return Task.CompletedTask;
    }
}