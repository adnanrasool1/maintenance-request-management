using FluentValidation;
using Mra.Domain.Requests;

namespace Mra.Application.Requests.GetRequests;

// Contract §3.7: an unknown status value returns 400.
public sealed class GetRequestsValidator : AbstractValidator<GetRequestsQuery>
{
    public GetRequestsValidator()
    {
        RuleFor(q => q.Status)
            .Must(status => GetRequestsQuery.ParseStatus(status) is not null)
            .When(q => q.Status is not null)
            .WithMessage($"'{{PropertyName}}' must be one of: {string.Join(", ", Enum.GetNames<RequestStatus>())}.");
    }
}
