using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Mra.Application;
using Mra.Application.Common.Behaviors;
using Xunit;

namespace Mra.Api.IntegrationTests.Platform;

public sealed class ValidationBehaviorTests
{
    public sealed record SampleCommand(string Name, decimal Cost) : IRequest<string>;

    private sealed class NameValidator : AbstractValidator<SampleCommand>
    {
        public NameValidator() => RuleFor(c => c.Name).NotEmpty();
    }

    private sealed class CostValidator : AbstractValidator<SampleCommand>
    {
        public CostValidator() => RuleFor(c => c.Cost).GreaterThan(0);
    }

    [Fact]
    public async Task Invalid_request_throws_with_failures_from_every_validator_and_skips_the_handler()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([new NameValidator(), new CostValidator()]);
        var handlerCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new SampleCommand("", 0),
            _ => { handlerCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None));

        Assert.False(handlerCalled);
        Assert.Equal(["Name", "Cost"], exception.Errors.Select(e => e.PropertyName));
    }

    [Fact]
    public async Task Valid_request_reaches_the_handler()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([new NameValidator(), new CostValidator()]);

        var result = await behavior.Handle(new SampleCommand("Fix tap", 10), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public void AddApplication_registers_MediatR_and_the_validation_behavior()
    {
        using var services = new ServiceCollection().AddApplication().BuildServiceProvider();

        Assert.NotNull(services.GetService<IMediator>());
        Assert.Contains(
            services.GetServices<IPipelineBehavior<SampleCommand, string>>(),
            behavior => behavior is ValidationBehavior<SampleCommand, string>);
    }
}
