using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Mra.Application.Common.Behaviors;

namespace Mra.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        AddValidators(services, assembly);

        return services;
    }

    // FluentValidation.DependencyInjectionExtensions is not an approved package, so validators
    // are registered with a small assembly scan: every concrete IValidator<T> in this assembly.
    private static void AddValidators(IServiceCollection services, Assembly assembly)
    {
        var registrations =
            from type in assembly.GetTypes()
            where type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
            from contract in type.GetInterfaces()
            where contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IValidator<>)
            select (contract, type);

        foreach (var (contract, type) in registrations)
        {
            services.AddTransient(contract, type);
        }
    }
}
