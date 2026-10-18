using System.Text.Json;
using wwwmmm.Web;

namespace wwwmmm.Workflow;

/// <summary>Existing Stripe infrastructure, GET only, account ownership resolved server-side.</summary>
internal static class StripeWorkspaceTools
{
    internal static IEnumerable<IWorkflowTool> Create(StripeGateway? gateway = null, Func<string, string?>? customerLookup = null)
    {
        yield return new DelegateTool(new("billing.stripe-invoices", "Read the authenticated customer's existing Stripe invoices. This is not a bank statement or inbox connector.", ["billing.read"], [], ["after"]), async (a, c, token) =>
        {
            if (c.Owner is null || !Guid.TryParseExact(c.Owner, "N", out _)) throw new UnauthorizedAccessException("Customer workspace identity required.");
            var customer = (customerLookup ?? CustomerAccounts.Store.CustomerId)(c.Owner) ?? throw new InvalidOperationException("No Stripe customer is linked to this account.");
            var cursor = a.TryGetProperty("after", out var after) ? after.GetString() : null;
            if (cursor is not null && !System.Text.RegularExpressions.Regex.IsMatch(cursor, "^in_[A-Za-z0-9]{1,100}$")) throw new ArgumentException("Existing invoice cursor required.");
            var query = "invoices?customer=" + Uri.EscapeDataString(customer) + "&limit=100" + (cursor is null ? "" : "&starting_after=" + Uri.EscapeDataString(cursor));
            token.ThrowIfCancellationRequested(); using var response = await (gateway ?? new StripeGateway()).GetAsync(query);
            var rows = response.RootElement.GetProperty("data").EnumerateArray().ToArray();
            if (rows.Any(row => StripeGateway.Reference(row, "customer") != customer)) throw new UnauthorizedAccessException("Stripe returned an invoice for another customer.");
            var invoices = rows.Select(row => new {
                id = StripeGateway.String(row, "id"), number = StripeGateway.String(row, "number"), status = StripeGateway.String(row, "status"),
                currency = StripeGateway.String(row, "currency"), totalMinorUnits = row.GetProperty("total").GetInt64(),
                paidMinorUnits = row.GetProperty("amount_paid").GetInt64(), remainingMinorUnits = row.GetProperty("amount_remaining").GetInt64(),
                created = row.GetProperty("created").GetInt64(), dueDate = row.TryGetProperty("due_date", out var due) ? due.Clone() : default(JsonElement?),
                billingSource = "STRIPE_INVOICE", paymentEvidence = "PROVIDER_REPORTED", bankReconciliation = "NOT_PERFORMED"
            }).ToArray();
            var more = response.RootElement.GetProperty("has_more").GetBoolean();
            return new ToolResult(WorkflowJson.Value(new { invoices, hasMore = more, nextCursor = more ? invoices.LastOrDefault()?.id : null, retrievedUtc = DateTimeOffset.UtcNow }),
                [new("stripe:customer-invoices", WorkflowJson.Hash(response.RootElement.GetRawText()))], "STRIPE_API_RESPONSE");
        });
    }
}
