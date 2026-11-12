using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Documents;
using Application.Abstractions.Email;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Leads.Activity;
using Application.Quotes.Send;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Documents;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.UnitTests.Quotes;

/// <summary>
/// sfc B4.3 / B10 / RN-018: sending a quote's proposal by email. The gate must reject a quote
/// with no email-carrying client BEFORE the renderer, storage or email sender are ever touched —
/// mirrors RN-028's discipline in GenerateSaleDocumentCommandHandlerTests.
/// <para>
/// <b>No "client with a blank email" test exists here on purpose.</b> <see cref="Email"/> throws
/// <see cref="DomainException"/> on construction if blank, and — unlike a plain domain invariant
/// that only guards the public API — <c>ClientConfiguration</c>'s <c>EmailValueConverter</c>
/// rehydrates every <see cref="Client.Email"/> through that same constructor on EVERY read from
/// the database. A spike that mutated an already-materialized <c>Email.Value</c> to blank via
/// reflection confirmed this empirically: the very next query the handler issues re-throws
/// <c>DomainException</c> while materializing the row, before the handler's own guard clause is
/// ever reached. A blank client email is therefore provably unreachable in this codebase, not
/// just via <see cref="Client"/>'s public API but through corrupted data too — the handler's
/// defensive <c>string.IsNullOrWhiteSpace</c> check is intentionally kept anyway (documents the
/// invariant explicitly, costs nothing), but no test exercises it because nothing CAN.
/// </para>
/// </summary>
public class SendQuoteCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly byte[] FakePdfBytes = { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
    private static readonly DateTime Now = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Car NewCar(Marca marca, Modelo modelo) => new(
        DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
        4, 5, 1600, 1000, 2021, "AB123CD", "desc", 15000m, Now.AddDays(-10));

    private static Client NewClient(string email = "juan.perez@example.com") =>
        new(DealerId, "Juan", "Pérez", "30111222", email, "+5491100000000", "Av. Siempre Viva 123", Now.AddDays(-5));

    private static Lead NewLead(string email = "lead@example.com") =>
        Lead.Create(DealerId, "Lead Persona", email, "444", LeadSource.Web, Now.AddDays(-5));

    private static Quote NewNumberedQuote(Car car, Client? client, Lead? lead, string number = "COT-2026-000001")
    {
        var quote = new Quote(DealerId, car, client, lead, 12000m, PaymentMethod.Contado, Now.AddDays(10), "comentario", Now.AddDays(-1));
        quote.AssignNumber(number);
        return quote;
    }

    private static (
        SendQuoteCommandHandler Handler,
        Mock<IStorageService> Storage,
        Mock<IDocumentRenderer> Renderer,
        Mock<ITransactionalEmailSender> EmailSender) CreateHandler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);

        var storage = new Mock<IStorageService>();
        storage
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string objectKey, string _, long? _, CancellationToken _) => objectKey);
        storage
            .Setup(s => s.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string objectKey, TimeSpan _, CancellationToken _) => new Uri($"https://storage.example.com/{objectKey}"));

        var renderer = new Mock<IDocumentRenderer>();
        renderer
            .Setup(r => r.RenderAsync<QuoteProposalDocument>(It.IsAny<DocumentTemplate>(), It.IsAny<QuoteProposalDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakePdfBytes);

        var emailSender = new Mock<ITransactionalEmailSender>();
        emailSender
            .Setup(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dateTimeProvider = new FakeDateTimeProvider { UtcNow = Now };
        var recorder = new LeadActivityRecorder(context);

        var handler = new SendQuoteCommandHandler(
            context, tenant.Object, dateTimeProvider, storage.Object, renderer.Object, emailSender.Object, recorder);

        return (handler, storage, renderer, emailSender);
    }

    private static async Task SeedDealerSettingsAsync(TestApplicationDbContext context)
    {
        context.DealerSettings.Add(new DealerSettingsEntity(DealerId, "Concesionaria Demo", "demo@example.com"));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_LeadOnlyQuote_ReturnsMissingContactEmail_AndDoesNotRenderUploadOrSendEmail()
    {
        using TestApplicationDbContext context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        Car car = NewCar(marca, modelo);
        Lead lead = NewLead();
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Leads.Add(lead);
        Quote quote = NewNumberedQuote(car, client: null, lead: lead);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        (SendQuoteCommandHandler handler, Mock<IStorageService> storage, Mock<IDocumentRenderer> renderer, Mock<ITransactionalEmailSender> emailSender) = CreateHandler(context);

        var result = await handler.Handle(new SendQuoteCommand(quote.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Quotes.MissingContactEmail");

        renderer.Verify(r => r.RenderAsync<QuoteProposalDocument>(It.IsAny<DocumentTemplate>(), It.IsAny<QuoteProposalDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
        emailSender.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
        (await context.Documents.Where(d => d.QuoteId == quote.Id).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnnumberedQuote_ReturnsNotNumbered_AndDoesNotRenderUploadOrSendEmail()
    {
        using TestApplicationDbContext context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        Car car = NewCar(marca, modelo);
        Client client = NewClient();
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        // No AssignNumber() call — simulates a pre-B4.3.3 quote whose backfill has not run yet.
        var quote = new Quote(DealerId, car, client, null, 12000m, PaymentMethod.Contado, Now.AddDays(10), "c", Now.AddDays(-1));
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        (SendQuoteCommandHandler handler, Mock<IStorageService> storage, _, Mock<ITransactionalEmailSender> emailSender) = CreateHandler(context);

        var result = await handler.Handle(new SendQuoteCommand(quote.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Quotes.NotNumbered");
        storage.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
        emailSender.Verify(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_QuoteNotFound_ReturnsNotFound()
    {
        using TestApplicationDbContext context = CreateContext();
        (SendQuoteCommandHandler handler, _, _, _) = CreateHandler(context);

        var result = await handler.Handle(new SendQuoteCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_ValidClientAndNumberedQuote_RendersUploadsCreatesDocumentAndSendsEmailWithExpectedTokens()
    {
        using TestApplicationDbContext context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        Car car = NewCar(marca, modelo);
        Client client = NewClient();
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        Quote quote = NewNumberedQuote(car, client, lead: null);
        context.Quotes.Add(quote);
        await SeedDealerSettingsAsync(context);
        await context.SaveChangesAsync();

        (SendQuoteCommandHandler handler, Mock<IStorageService> storage, Mock<IDocumentRenderer> renderer, Mock<ITransactionalEmailSender> emailSender) = CreateHandler(context);

        var result = await handler.Handle(new SendQuoteCommand(quote.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        renderer.Verify(r => r.RenderAsync<QuoteProposalDocument>(DocumentTemplate.QuoteProposal, It.IsAny<QuoteProposalDocument>(), It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(s => s.UploadFileAsync(
            It.IsAny<Stream>(),
            $"documents/{DealerId}/quotes/{quote.Id}/propuesta-v1.pdf",
            "application/pdf",
            It.IsAny<long?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        Document? document = await context.Documents.SingleOrDefaultAsync(d => d.Id == result.Value.DocumentId);
        document.Should().NotBeNull();
        document!.QuoteId.Should().Be(quote.Id);
        document.ClientId.Should().Be(client.Id);
        document.Type.Should().Be(DocumentType.QuoteProposal);
        document.Sha256Hash.Should().NotBeNullOrEmpty();

        emailSender.Verify(e => e.SendAsync(
            EmailTemplate.QuoteSent,
            "juan.perez@example.com",
            It.Is<IReadOnlyDictionary<string, string>>(m =>
                m["Name"] == "Juan Pérez" &&
                m["DealerName"] == "Concesionaria Demo" &&
                m["QuoteNumber"] == "COT-2026-000001" &&
                m["QuoteUrl"] == result.Value.QuoteUrl),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_QuoteWithLeadId_RecordsQuoteSentLeadActivity()
    {
        using TestApplicationDbContext context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        Car car = NewCar(marca, modelo);
        Client client = NewClient();
        Lead lead = NewLead();
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        context.Leads.Add(lead);
        Quote quote = NewNumberedQuote(car, client, lead);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        (SendQuoteCommandHandler handler, _, _, _) = CreateHandler(context);

        var result = await handler.Handle(new SendQuoteCommand(quote.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        LeadActivity? activity = await context.LeadActivities
            .SingleOrDefaultAsync(a => a.LeadId == lead.Id && a.Type == LeadActivityType.QuoteSent);
        activity.Should().NotBeNull();
        activity!.RelatedEntityId.Should().Be(quote.Id);
        activity.RelatedEntityType.Should().Be("Quote");
    }

    [Fact]
    public async Task Handle_QuoteWithNoLeadAndNoOriginLead_SucceedsWithoutRecordingAnyLeadActivity()
    {
        using TestApplicationDbContext context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        Car car = NewCar(marca, modelo);
        Client client = NewClient();
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        Quote quote = NewNumberedQuote(car, client, lead: null);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        (SendQuoteCommandHandler handler, _, _, _) = CreateHandler(context);

        var result = await handler.Handle(new SendQuoteCommand(quote.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.LeadActivities.CountAsync()).Should().Be(0);
    }
}
