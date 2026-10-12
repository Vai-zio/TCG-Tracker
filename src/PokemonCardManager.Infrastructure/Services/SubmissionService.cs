using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Submissions;
using PokemonCardManager.Core.Valuation;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

public sealed class SubmissionService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IValidator<SubmissionInput> validator,
    IClock clock,
    ILogger<SubmissionService> logger) : ISubmissionService
{
    private static readonly SubmissionStatus[] Closed = [SubmissionStatus.Returned, SubmissionStatus.Cancelled];

    public async Task<IReadOnlyList<SubmissionRow>> GetSubmissionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.GradingSubmissions.AsNoTracking()
            .Select(s => new
            {
                s.Id, s.GradingCompany.Code, s.ServiceTier, s.ExternalReference, s.Status, s.SubmittedOn, s.CreatedAt,
                s.Currency, s.GradingFees, s.ShippingCost, s.InsuranceCost, s.ActualTotalCost,
                Items = s.Items.Count, Graded = s.Items.Count(i => i.ReturnedGrade != null),
            })
            .ToListAsync(cancellationToken);
        return rows
            .OrderBy(r => Closed.Contains(r.Status)).ThenByDescending(r => r.SubmittedOn).ThenByDescending(r => r.CreatedAt)
            .Select(r => new SubmissionRow(r.Id, r.Code, r.ServiceTier, r.ExternalReference, r.Status, r.SubmittedOn, r.Items, r.Graded,
                new Money(r.ActualTotalCost ?? r.GradingFees + r.ShippingCost + r.InsuranceCost, r.Currency), r.ActualTotalCost is not null))
            .ToList();
    }

    public async Task<SubmissionDetails?> GetSubmissionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var s = await db.GradingSubmissions.AsNoTracking().Include(x => x.GradingCompany).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (s is null) return null;

        var items = await db.SubmissionItems.AsNoTracking()
            .Where(i => i.GradingSubmissionId == id)
            .Select(i => new SubmissionItemRow(
                i.Id, i.PhysicalCardId,
                i.PhysicalCard.CardReference.Name + " · " + i.PhysicalCard.CardReference.CardSet.Name + " " + i.PhysicalCard.CardReference.CollectorNumber,
                i.DeclaredValue, i.PredictionAssessmentId,
                i.PredictionAssessment != null ? i.PredictionAssessment.EstimatedGradeHigh : null,
                i.PredictionAssessment != null && i.PredictionAssessment.InsufficientEvidence,
                i.ReturnedGrade, i.CertificationNumber, i.Notes))
            .ToListAsync(cancellationToken);

        var total = new Money(s.ActualTotalCost ?? s.GradingFees + s.ShippingCost + s.InsuranceCost, s.Currency);
        var input = new SubmissionInput(s.GradingCompany.Code, s.ServiceTier, s.Currency, s.GradingFees, s.ShippingCost, s.InsuranceCost,
            s.ActualTotalCost, s.SubmittedOn, s.ExternalReference, s.OutboundTracking, s.ReturnTracking, s.Notes);
        return new SubmissionDetails(s.Id, input, s.Status, items.OrderBy(i => i.Card).ToList(), total,
            items.Count == 0 ? null : new Money(total.Amount / items.Count, s.Currency).Round(),
            SubmissionWorkflow.NextOptions(s.Status));
    }

    public async Task<Guid> CreateAsync(SubmissionInput input, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var submission = new GradingSubmission
        {
            ServiceTier = input.ServiceTier.Trim(),
            Currency = CurrencyCode.Normalize(input.Currency),
            Status = SubmissionStatus.Draft,
        };
        await ApplyAsync(db, submission, input, cancellationToken);
        db.GradingSubmissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created submission {SubmissionId} to {Company}", submission.Id, input.CompanyCode);
        return submission.Id;
    }

    public async Task UpdateAsync(Guid id, SubmissionInput input, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var submission = await FindAsync(db, id, cancellationToken);
        var company = await CompanyIdAsync(db, input.CompanyCode, cancellationToken);
        if (company != submission.GradingCompanyId && !SubmissionWorkflow.CanEditItems(submission.Status))
            throw new DomainRuleViolationException("The grading company can only change while the submission is a draft.");
        await ApplyAsync(db, submission, input, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var submission = await FindAsync(db, id, cancellationToken);
        if (submission.Status is not (SubmissionStatus.Draft or SubmissionStatus.Cancelled))
            throw new DomainRuleViolationException("Only a draft or cancelled submission can be deleted. Sent submissions are kept as history.");
        db.GradingSubmissions.Remove(submission);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubmittableCard>> FindCardsAsync(Guid submissionId, string? text, int limit = 200, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var companyId = await db.GradingSubmissions.Where(s => s.Id == submissionId).Select(s => s.GradingCompanyId).SingleAsync(cancellationToken);
        var query = db.PhysicalCards.AsNoTracking()
            .Where(c => c.Status != WorkflowStatus.Sold && c.Status != WorkflowStatus.Traded)
            .Where(c => !db.SubmissionItems.Any(i => i.PhysicalCardId == c.Id && !Closed.Contains(i.GradingSubmission.Status)));
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = "%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(c => EF.Functions.Like(c.CardReference.Name, pattern, "\\") ||
                                     EF.Functions.Like(c.CardReference.CardSet.Name, pattern, "\\") ||
                                     EF.Functions.Like(c.CardReference.CollectorNumber, pattern, "\\"));
        }
        return await query
            // Grading candidates first, then cards that have been inspected.
            .OrderByDescending(c => c.Status == WorkflowStatus.GradingCandidate).ThenByDescending(c => c.Status == WorkflowStatus.Inspected)
            .ThenBy(c => c.CardReference.Name)
            .Take(limit)
            .Select(c => new SubmittableCard(
                c.Id,
                c.CardReference.Name + " · " + c.CardReference.CardSet.Name + " " + c.CardReference.CollectorNumber,
                c.Status,
                db.ConditionAssessments
                    .Where(a => a.PhysicalCardId == c.Id && a.Method == AssessmentMethod.RuleBased && a.GradingCompanyId == companyId)
                    .OrderByDescending(a => a.AssessedAt).Select(a => a.EstimatedGradeHigh).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task AddCardsAsync(Guid submissionId, IReadOnlyCollection<Guid> physicalCardIds, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var submission = await FindAsync(db, submissionId, cancellationToken);
        if (!SubmissionWorkflow.CanEditItems(submission.Status))
            throw new DomainRuleViolationException("Cards can only be added while the submission is a draft.");

        var busy = await db.SubmissionItems
            .Where(i => physicalCardIds.Contains(i.PhysicalCardId) && !Closed.Contains(i.GradingSubmission.Status))
            .Select(i => i.PhysicalCardId).Distinct().ToListAsync(cancellationToken);
        if (busy.Count > 0)
            throw new DomainRuleViolationException($"{busy.Count} of these card(s) are already in an open submission.");

        var cards = await db.PhysicalCards.Where(c => physicalCardIds.Contains(c.Id)).ToListAsync(cancellationToken);
        if (cards.Count != physicalCardIds.Count) throw new DomainRuleViolationException("Some of these cards no longer exist.");
        if (cards.Any(c => c.Status is WorkflowStatus.Sold or WorkflowStatus.Traded))
            throw new DomainRuleViolationException("Sold or traded cards can't be submitted.");

        foreach (var card in cards)
        {
            var prediction = await db.ConditionAssessments
                .Where(a => a.PhysicalCardId == card.Id && a.Method == AssessmentMethod.RuleBased && a.GradingCompanyId == submission.GradingCompanyId)
                .OrderByDescending(a => a.AssessedAt).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(cancellationToken);
            db.SubmissionItems.Add(new SubmissionItem { GradingSubmissionId = submission.Id, PhysicalCardId = card.Id, PredictionAssessmentId = prediction });
            if (card.Status is WorkflowStatus.Cataloged or WorkflowStatus.AwaitingInspection or WorkflowStatus.Inspected)
                card.Status = WorkflowStatus.GradingCandidate;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.SubmissionItems.Include(i => i.GradingSubmission).FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken)
                   ?? throw new DomainRuleViolationException("That card is no longer in the submission.");
        if (!SubmissionWorkflow.CanEditItems(item.GradingSubmission.Status))
            throw new DomainRuleViolationException("Cards can only be removed while the submission is a draft.");
        db.SubmissionItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateItemAsync(Guid itemId, decimal? declaredValue, string? notes, CancellationToken cancellationToken = default)
    {
        if (declaredValue < 0) throw new DomainRuleViolationException("Declared value can't be negative.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.SubmissionItems.FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken)
                   ?? throw new DomainRuleViolationException("That card is no longer in the submission.");
        item.DeclaredValue = declaredValue;
        item.Notes = Blank(notes);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetStatusAsync(Guid id, SubmissionStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var submission = await db.GradingSubmissions.Include(s => s.Items).ThenInclude(i => i.PhysicalCard)
                             .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                         ?? throw new DomainRuleViolationException("The submission no longer exists.");
        var from = submission.Status;
        if (!SubmissionWorkflow.CanMove(from, status))
            throw new DomainRuleViolationException($"A {Words(from)} submission can't be marked {Words(status)}.");

        var leavingHome = from == SubmissionStatus.Draft && status != SubmissionStatus.Cancelled;
        if (leavingHome && submission.Items.Count == 0)
            throw new DomainRuleViolationException("Add at least one card before sending the submission.");

        if (leavingHome)
        {
            submission.SubmittedOn ??= DateOnly.FromDateTime(clock.UtcNow);
            foreach (var item in submission.Items) item.PhysicalCard.Status = WorkflowStatus.SubmittedForGrading;
        }
        if (status == SubmissionStatus.Cancelled)
            foreach (var item in submission.Items.Where(i => i.PhysicalCard.Status == WorkflowStatus.SubmittedForGrading))
                item.PhysicalCard.Status = WorkflowStatus.GradingCandidate;
        if (status == SubmissionStatus.Returned)
            foreach (var item in submission.Items)
                item.PhysicalCard.Status = item.ReturnedGrade is not null ? WorkflowStatus.Graded : WorkflowStatus.GradingCandidate;

        submission.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Submission {SubmissionId}: {From} → {To}", id, from, status);
    }

    public async Task RecordResultAsync(Guid itemId, decimal? grade, string? certificationNumber, string? notes, CancellationToken cancellationToken = default)
    {
        if (grade is { } g && !PriceObservationRules.IsValidGrade(g))
            throw new DomainRuleViolationException("Grade must be 1 to 10 in steps of 0.5.");
        if (certificationNumber?.Trim().Length > 50) throw new DomainRuleViolationException("Certificate number is too long.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.SubmissionItems.Include(i => i.GradingSubmission).Include(i => i.PhysicalCard)
                       .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken)
                   ?? throw new DomainRuleViolationException("That card is no longer in the submission.");
        if (!SubmissionWorkflow.CanRecordResults(item.GradingSubmission.Status))
            throw new DomainRuleViolationException("Grades can be recorded once the company has received the cards.");

        item.ReturnedGrade = grade;
        item.CertificationNumber = Blank(certificationNumber);
        item.Notes = Blank(notes);
        if (item.GradingSubmission.Status == SubmissionStatus.Returned)
            item.PhysicalCard.Status = grade is not null ? WorkflowStatus.Graded : WorkflowStatus.GradingCandidate;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task ApplyAsync(PokemonCardDbContext db, GradingSubmission s, SubmissionInput input, CancellationToken cancellationToken)
    {
        s.GradingCompanyId = await CompanyIdAsync(db, input.CompanyCode, cancellationToken);
        s.ServiceTier = input.ServiceTier.Trim();
        s.Currency = CurrencyCode.Normalize(input.Currency);
        s.GradingFees = input.GradingFees;
        s.ShippingCost = input.ShippingCost;
        s.InsuranceCost = input.InsuranceCost;
        s.ActualTotalCost = input.ActualTotalCost;
        s.SubmittedOn = input.SubmittedOn;
        s.ExternalReference = Blank(input.ExternalReference);
        s.OutboundTracking = Blank(input.OutboundTracking);
        s.ReturnTracking = Blank(input.ReturnTracking);
        s.Notes = Blank(input.Notes);
    }

    private static async Task<Guid> CompanyIdAsync(PokemonCardDbContext db, string code, CancellationToken cancellationToken) =>
        await db.GradingCompanies.Where(c => c.Code == code.Trim()).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken)
        ?? throw new DomainRuleViolationException($"Unknown grading company '{code}'.");

    private static async Task<GradingSubmission> FindAsync(PokemonCardDbContext db, Guid id, CancellationToken cancellationToken) =>
        await db.GradingSubmissions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
        ?? throw new DomainRuleViolationException("The submission no longer exists.");

    private static string Words(SubmissionStatus s) => s.ToString().ToLowerInvariant();
    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
