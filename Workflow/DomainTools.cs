using System.Text.Json;

namespace wwwmmm.Workflow;

/// <summary>Deterministic checks over explicit structured inputs. No clinical thresholds or engineering conclusions are invented.</summary>
internal static class DomainTools
{
    internal static IEnumerable<IWorkflowTool> Create()
    {
        yield return new DelegateTool(new("finance.reconcile", "Decimal allocation checks for explicitly normalized, reviewed invoice/payment/credit/refund records.", ["files.read"], ["path"], []), Reconcile);
        yield return new DelegateTool(new("economy.indicators", "Measure changes in supplied dated indicators; no recession prediction or causal claim.", ["files.read"], ["path"], []), Indicators);
        yield return new DelegateTool(new("review.rules", "Compare verified structured observations against supplied versioned rules; produce review findings with evidence.", ["files.read"], ["path", "rulesPath"], []), Rules);
        yield return new DelegateTool(new("requirements.trace", "Check documented requirements against supplied test evidence, revisions and explicit pass criteria.", ["files.read"], ["path"], []), Trace);
    }
    private static async Task<(JsonDocument Json, SourceReference Source)> Load(string key, JsonElement a, ToolContext c, CancellationToken token)
    { var input = await c.Workspace.ReadAsync(WorkflowJson.Text(a, key), token); return (JsonDocument.Parse(input.Bytes), input.Source); }
    private static decimal Positive(JsonElement a, string key)
    { var value = a.GetProperty(key).GetDecimal(); if (value <= 0) throw new ArgumentException("Positive decimal required: " + key); return value; }
    private static Dictionary<string, JsonElement> Unique(JsonElement a, string key)
    {
        var records = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var record in a.GetProperty(key).EnumerateArray())
            if (!records.TryAdd(WorkflowJson.Text(record, "id"), record)) throw new ArgumentException("Duplicate record ID.");
        return records;
    }
    private static async Task<ToolResult> Reconcile(JsonElement a, ToolContext c, CancellationToken token)
    {
        var input = await Load("path", a, c, token); using var json = input.Json; var root = json.RootElement;
        // Export conventions must be normalized by the importer. We never infer debit/credit semantics from a sign.
        if (WorkflowJson.Text(root, "amountConvention") != "positive_amount_explicit_kind") throw new ArgumentException("Document the normalized bank amount convention.");
        var documents = Unique(root, "documents"); var transactions = Unique(root, "transactions");
        foreach (var d in documents.Values)
        {
            _ = Positive(d, "total"); _ = WorkflowJson.Text(d, "currency"); _ = WorkflowJson.Text(d, "counterparty");
            if (WorkflowJson.Text(d, "kind") is not ("invoice" or "credit" or "return")) throw new ArgumentException("Unsupported document kind.");
        }
        foreach (var t in transactions.Values)
        {
            _ = Positive(t, "amount"); _ = WorkflowJson.Text(t, "currency"); _ = WorkflowJson.Text(t, "counterparty");
            if (WorkflowJson.Text(t, "kind") is not ("payment" or "refund")) throw new ArgumentException("Explicit payment/refund kind required.");
        }
        var allocations = root.GetProperty("allocations").EnumerateArray().ToArray(); var seen = new HashSet<string>();
        var paid = new Dictionary<string, decimal>(); var refunded = new Dictionary<string, decimal>(); var used = new Dictionary<string, decimal>();
        foreach (var allocation in allocations)
        {
            token.ThrowIfCancellationRequested();
            if (!allocation.GetProperty("reviewed").GetBoolean()) throw new ArgumentException("Unreviewed allocations cannot establish payment state.");
            var docId = WorkflowJson.Text(allocation, "documentId"); var txId = WorkflowJson.Text(allocation, "transactionId");
            if (!seen.Add(docId + "\0" + txId) || !documents.TryGetValue(docId, out var d) || !transactions.TryGetValue(txId, out var t)) throw new ArgumentException("Duplicate or unknown allocation.");
            var amount = Positive(allocation, "amount");
            if (WorkflowJson.Text(d, "kind") != "invoice" || WorkflowJson.Text(d, "currency") != WorkflowJson.Text(t, "currency") ||
                !string.Equals(WorkflowJson.Text(d, "counterparty"), WorkflowJson.Text(t, "counterparty"), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Allocation currency, counterparty or document mismatch.");
            used[txId] = used.GetValueOrDefault(txId) + amount;
            if (used[txId] > Positive(t, "amount")) throw new ArgumentException("Transaction over-allocated.");
            var target = WorkflowJson.Text(t, "kind") == "payment" ? paid : refunded; target[docId] = target.GetValueOrDefault(docId) + amount;
        }
        var credits = new Dictionary<string, decimal>(); var creditUsed = new Dictionary<string, decimal>();
        foreach (var allocation in root.GetProperty("credits").EnumerateArray())
        {
            if (!allocation.GetProperty("reviewed").GetBoolean()) throw new ArgumentException("Credit allocation must be reviewed.");
            var creditId = WorkflowJson.Text(allocation, "creditId"); var invoiceId = WorkflowJson.Text(allocation, "invoiceId"); var amount = Positive(allocation, "amount");
            if (!seen.Add("credit:" + creditId + "\0" + invoiceId) || !documents.TryGetValue(creditId, out var credit) || !documents.TryGetValue(invoiceId, out var invoice) ||
                WorkflowJson.Text(credit, "kind") != "credit" || WorkflowJson.Text(invoice, "kind") != "invoice" || WorkflowJson.Text(credit, "currency") != WorkflowJson.Text(invoice, "currency") ||
                !string.Equals(WorkflowJson.Text(credit, "counterparty"), WorkflowJson.Text(invoice, "counterparty"), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Invalid credit relationship.");
            creditUsed[creditId] = creditUsed.GetValueOrDefault(creditId) + amount;
            if (creditUsed[creditId] > Positive(credit, "total")) throw new ArgumentException("Credit over-allocated.");
            credits[invoiceId] = credits.GetValueOrDefault(invoiceId) + amount;
        }
        var output = new List<object>();
        foreach (var (id, d) in documents)
        {
            var total = Positive(d, "total"); var payment = paid.GetValueOrDefault(id); var refund = refunded.GetValueOrDefault(id); var credit = credits.GetValueOrDefault(id);
            if (refund > payment || payment + credit > total) throw new ArgumentException("Document over-allocation or refund without recorded payment.");
            var remaining = total - payment - credit + refund; var kind = WorkflowJson.Text(d, "kind");
            var overdue = d.TryGetProperty("dueDate", out var due) && DateOnly.ParseExact(due.GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) < DateOnly.FromDateTime(DateTime.UtcNow);
            var state = kind == "return" ? "needs_review" : kind == "credit" ? "credited" : refund > 0 ? "refunded" : credit == total ? "credited" : remaining == 0 ? "paid" : payment > 0 ? "partially_paid" : overdue ? "overdue" : "open";
            output.Add(new { id, kind, currency = WorkflowJson.Text(d, "currency"), total, payment, refund, credit, remaining, state });
        }
        return new(WorkflowJson.Value(new { documents = output, unmatchedTransactions = transactions.Keys.Where(id => used.GetValueOrDefault(id) != Positive(transactions[id], "amount")), basis = "explicit_reviewed_allocations", bankConnectionPerformed = false }), [input.Source]);
    }
    private static async Task<ToolResult> Indicators(JsonElement a, ToolContext c, CancellationToken token)
    {
        var input = await Load("path", a, c, token); using var json = input.Json;
        var series = json.RootElement.GetProperty("series").EnumerateArray().Select(series =>
        {
            var id = WorkflowJson.Text(series, "id"); var unit = WorkflowJson.Text(series, "unit");
            var points = series.GetProperty("points").EnumerateArray().Select(p => new { date = DateOnly.ParseExact(WorkflowJson.Text(p, "date"), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), value = p.GetProperty("value").GetDecimal(), source = WorkflowJson.Text(p, "source") }).OrderBy(p => p.date).ToArray();
            if (points.Length < 2 || points.Select(p => p.date).Distinct().Count() != points.Length) throw new ArgumentException("Two or more uniquely dated sourced observations required.");
            var changes = points.Skip(1).Select((p, i) => new { p.date, delta = p.value - points[i].value, percent = points[i].value == 0 ? (decimal?)null : (p.value - points[i].value) / Math.Abs(points[i].value) * 100, p.source });
            return new { id, unit, changes, interpretation = "MEASURED_CHANGES_ONLY" };
        }).ToArray();
        return new(WorkflowJson.Value(new { series, recessionPrediction = "NOT_ESTABLISHED", causalInference = "NOT_PERFORMED" }), [input.Source]);
    }
    private static async Task<ToolResult> Rules(JsonElement a, ToolContext c, CancellationToken token)
    {
        var data = await Load("path", a, c, token); var rules = await Load("rulesPath", a, c, token); using var input = data.Json; using var protocol = rules.Json;
        var id = WorkflowJson.Text(protocol.RootElement, "protocolId"); var version = WorkflowJson.Text(protocol.RootElement, "version");
        var owner = WorkflowJson.Text(protocol.RootElement, "reviewOwner"); var subject = WorkflowJson.Text(input.RootElement, "subjectId");
        var observations = Unique(input.RootElement, "observations"); var findings = new List<object>();
        foreach (var rule in protocol.RootElement.GetProperty("rules").EnumerateArray())
        {
            token.ThrowIfCancellationRequested(); var field = WorkflowJson.Text(rule, "field"); var unit = WorkflowJson.Text(rule, "unit");
            var state = "NEEDS_REVIEW"; decimal? measured = null;
            if (observations.TryGetValue(field, out var observation) && observation.TryGetProperty("verified", out var verified) && verified.ValueKind == JsonValueKind.True &&
                WorkflowJson.Text(observation, "unit") == unit && observation.TryGetProperty("value", out var value) && value.TryGetDecimal(out var number))
            {
                measured = number;
                var lower = rule.TryGetProperty("min", out var min) ? min.GetDecimal() : (decimal?)null;
                var upper = rule.TryGetProperty("max", out var max) ? max.GetDecimal() : (decimal?)null;
                if ((lower is null && upper is null) || lower > upper) throw new ArgumentException("Valid explicit rule bounds required.");
                state = (lower.HasValue && number < lower.Value) || (upper.HasValue && number > upper.Value) ? "REVIEW_ALERT" : "WITHIN_SUPPLIED_RULE";
            }
            findings.Add(new { subject, field, unit, measured, state, severity = WorkflowJson.Text(rule, "severity"), protocolId = id, version, reviewOwner = owner, independentlyValidatedProtocol = false });
        }
        return new(WorkflowJson.Value(new { findings, conclusion = "REQUIRES_DOMAIN_REVIEW", medicalOrEngineeringCertification = false }), [data.Source, rules.Source], "REVIEW_REQUIRED");
    }
    private static async Task<ToolResult> Trace(JsonElement a, ToolContext c, CancellationToken token)
    {
        var input = await Load("path", a, c, token); using var json = input.Json; var root = json.RootElement;
        var requirements = Unique(root, "requirements"); var tests = Unique(root, "tests"); var findings = new List<object>();
        foreach (var (id, requirement) in requirements)
        {
            var revision = WorkflowJson.Text(requirement, "revision"); var evidence = new List<object>(); var state = "MISSING_TEST_EVIDENCE";
            foreach (var test in tests.Values.Where(t => WorkflowJson.Text(t, "requirementId") == id))
            {
                token.ThrowIfCancellationRequested(); var source = WorkflowJson.Text(test, "sourcePath"); var bytes = await c.Workspace.ReadAsync(source, token);
                var current = WorkflowJson.Text(test, "requirementRevision") == revision;
                var passed = test.GetProperty("passed").ValueKind == JsonValueKind.True;
                evidence.Add(new { testId = WorkflowJson.Text(test, "id"), current, passed, source = bytes.Source });
                state = !current ? "REVISION_MISMATCH" : !passed ? "FAILED_TEST_REPORTED" : state is "REVISION_MISMATCH" or "FAILED_TEST_REPORTED" ? state : "DOCUMENTED_PASS_REQUIRES_REVIEW";
            }
            findings.Add(new { requirementId = id, revision, state, evidence });
        }
        return new(WorkflowJson.Value(new { findings, physicalInspectionPerformed = false, certification = "NOT_ESTABLISHED" }), [input.Source], "REVIEW_REQUIRED");
    }
}
