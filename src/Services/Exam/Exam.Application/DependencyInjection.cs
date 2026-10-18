using Exam.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Exam.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ExamService>();
        services.AddScoped<ExamSessionService>();
        return services;
    }
}
