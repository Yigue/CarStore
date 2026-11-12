using Application.Abstractions.Billing;
using Application.Abstractions.Messaging;
using SharedKernel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Billing.Queries.GetPublicPlans;

internal sealed class GetPublicPlansQueryHandler : IQueryHandler<GetPublicPlansQuery, IReadOnlyList<PublicPlanResponse>>
{
    private readonly IPlanCatalog _planCatalog;

    public GetPublicPlansQueryHandler(IPlanCatalog planCatalog)
    {
        _planCatalog = planCatalog;
    }

    public async Task<Result<IReadOnlyList<PublicPlanResponse>>> Handle(GetPublicPlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await _planCatalog.GetAllAsync(cancellationToken);

        IReadOnlyList<PublicPlanResponse> response = plans
            .Select(p => new PublicPlanResponse(p.Code, p.AmountMinor, p.Currency, p.Interval, p.TrialDays))
            .ToList();

        return Result.Success(response);
    }
}
