using wwwmmm.Crm;

namespace wwwmmm.Workflow;

internal static class CrmTools
{
    internal static IEnumerable<IWorkflowTool> Create()
    {
        yield return new DelegateTool(new("crm.summary", "Count actual CRM records in this private workspace.", ["crm.read"], [], []),
            (_, c, _) => Task.FromResult(new ToolResult(WorkflowJson.Value(new CrmService(c.Workspace).Summary()), [])));
        yield return new DelegateTool(new("crm.customers", "List recorded workspace CRM customers.", ["crm.read"], [], []),
            (_, c, _) => Task.FromResult(new ToolResult(WorkflowJson.Value(new CrmService(c.Workspace).ListCustomers()), [])));
        yield return new DelegateTool(new("crm.customer", "Read one recorded customer, contacts, document links and audit.", ["crm.read"], ["customerId"], []),
            (a, c, _) => Task.FromResult(new ToolResult(WorkflowJson.Value(new CrmService(c.Workspace).GetDetail(WorkflowJson.Text(a, "customerId"))), [])));
        yield return new DelegateTool(new("crm.create-customer", "Durably record an explicitly supplied customer and audit entry; conflicting details are rejected.", ["crm.write"], ["name", "email"], ["company"]),
            async (a, c, token) => new(WorkflowJson.Value(await new CrmService(c.Workspace).CreateCustomerAsync(
                WorkflowJson.Text(a, "name"), WorkflowJson.Text(a, "email"), a.TryGetProperty("company", out var company) ? company.GetString() : null, token: token)), []));
        yield return new DelegateTool(new("crm.set-status", "Change an existing customer's status with conflict checking and audit.", ["crm.write"], ["customerId", "status"], []),
            async (a, c, token) => new(WorkflowJson.Value(await new CrmService(c.Workspace).SetStatusAsync(WorkflowJson.Text(a, "customerId"), WorkflowJson.Text(a, "status"), token)), []));
        yield return new DelegateTool(new("crm.add-contact", "Record a contact and its audit entry. Inspect the CRM before retrying an interrupted submission.", ["crm.write"], ["customerId", "name"], ["email", "phone", "role"], ReplaySafe: false),
            async (a, c, token) => new(WorkflowJson.Value(await new CrmService(c.Workspace).AddContactAsync(
                WorkflowJson.Text(a, "customerId"), WorkflowJson.Text(a, "name"),
                a.TryGetProperty("email", out var email) ? email.GetString() : null,
                a.TryGetProperty("phone", out var phone) ? phone.GetString() : null,
                a.TryGetProperty("role", out var role) ? role.GetString() : null, token)), []));
        yield return new DelegateTool(new("crm.link-document", "Link an actual hash-verified workspace document to a customer, retaining its immutable source snapshot.", ["crm.write", "files.read"], ["customerId", "path", "sha256", "textSource"], ["category"]),
            async (a, c, token) =>
            {
                var result = await new CrmService(c.Workspace).LinkDocumentAsync(WorkflowJson.Text(a, "customerId"), WorkflowJson.Text(a, "path"),
                    WorkflowJson.Text(a, "sha256"), a.TryGetProperty("category", out var category) ? category.GetString() : null, WorkflowJson.Text(a, "textSource"), token);
                return new(WorkflowJson.Value(result), [new(result.Document.RelativePath, result.Document.Sha256)], "SOURCE_HASH_VERIFIED_CATEGORY_REQUIRES_REVIEW");
            });
    }
}
