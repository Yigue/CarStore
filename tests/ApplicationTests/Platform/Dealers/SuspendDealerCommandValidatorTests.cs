using System;
using Application.Platform.Dealers.SuspendDealer;
using FluentAssertions;
using Xunit;

namespace Application.UnitTests.Platform.Dealers;

public class SuspendDealerCommandValidatorTests
{
    [Fact]
    public void SuspendDealerCommandValidator_RejectsEmptyActorId()
    {
        var validator = new SuspendDealerCommandValidator();
        var command = new SuspendDealerCommand(Guid.NewGuid(), "reason", "v0", Guid.Empty);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SuspendDealerCommand.ActorId));
    }
}
