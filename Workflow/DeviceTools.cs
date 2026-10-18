using wwwmmm.Documents;

namespace wwwmmm.Workflow;

internal static class DeviceTools
{
    internal static IEnumerable<IWorkflowTool> Create()
    {
        yield return new DelegateTool(new("devices.printers", "Query real configured Linux CUPS printer queues.", ["devices.read"], [], [], Dependency: "Linux CUPS"), async (_, _, token) =>
            new(WorkflowJson.Value(new { output = await new LinuxDeviceService().ListPrintersAsync(token) }), []));
        yield return new DelegateTool(new("devices.print-status", "Query real CUPS scheduler history; does not prove physical output.", ["devices.read"], ["jobId"], [], Dependency: "Linux CUPS"), async (a, _, token) =>
            new(WorkflowJson.Value(new { output = await new LinuxDeviceService().GetPrintQueueAsync(WorkflowJson.Text(a, "jobId"), token), physicalOutput = "UNVERIFIED" }), []));
        yield return new DelegateTool(new("devices.print", "Submit a scoped PDF to the named CUPS queue. Reconcile uncertain submissions before retry.", ["files.read", "devices.print"], ["path", "printer"], [], ReplaySafe: false, Dependency: "Linux CUPS and Poppler"), async (a, c, token) =>
        {
            var input = await c.Workspace.ReadAsync(WorkflowJson.Text(a, "path"), token);
            var submission = await new LinuxDeviceService().SubmitPdfAsync(WorkflowJson.Text(a, "printer"), input.Bytes, token);
            return new(WorkflowJson.Value(new { submission.JobId, submission.State, physicalOutput = "UNVERIFIED" }), [input.Source]);
        });
    }
}
