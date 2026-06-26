using System;
using Application.Abstractions.Messaging;

namespace Application.Queries.Financial.GetSummary;

public sealed record GetFinancialSummaryQuery(DateTime? From = null, DateTime? To = null) : IQuery<FinancialSummaryResponse>;