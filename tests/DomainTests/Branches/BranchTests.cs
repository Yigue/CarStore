using Domain.Branches;
using FluentAssertions;

namespace DomainTests.Branches;

/// <summary>sfc C2.1 / RN-048: a branch belongs to a dealer (DealerId stays the tenant); its name is normalized for uniqueness.</summary>
public class BranchTests
{
    [Fact]
    public void A_branch_is_scoped_to_its_dealer_with_a_normalized_name()
    {
        var dealerId = Guid.NewGuid();

        var branch = Branch.Create(dealerId, "  Norte ", isMain: false).Value;

        branch.DealerId.Should().Be(dealerId);
        branch.Name.Should().Be("Norte");
        branch.NameNormalized.Should().Be("NORTE");
        branch.IsMain.Should().BeFalse();
    }

    [Fact]
    public void A_branch_needs_a_name()
    {
        Branch.Create(Guid.NewGuid(), "  ", isMain: false).IsFailure.Should().BeTrue();
    }
}
