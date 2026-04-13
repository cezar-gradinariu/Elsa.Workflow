namespace Elsa.Workflow.Application.Activities;

/// <summary>
/// Bookmark payload keyed on the stable <see cref="PrepareCommandId"/> generated when
/// a Prepare message is first published to Service Bus.
///
/// <see cref="ManagePrepareActivity"/> creates one bookmark per substore with this payload.
/// <see cref="Commands.RegisterPrepareReportCommandHandler"/> hashes an instance of this
/// type to locate the right bookmark when a PrepareReport arrives.
///
/// Must be serializable — kept as a plain record with a single string property so Elsa's
/// default JSON serializer handles it without custom converters.
/// </summary>
public sealed record PrepareBookmarkPayload(string PrepareCommandId);
