namespace Workflow.Infrastructure;

/// <summary>
/// Delivery contract for the <c>sendMail</c> transition effect.
/// A workflow action whose transition carries the <c>sendMail</c> effect enqueues
/// a mail-tagged Outbox row; <see cref="NotificationWorker"/> drains it and calls
/// <see cref="SendAsync"/>. Keep this interface delivery-agnostic (SMTP, SES, Graph, ...).
/// </summary>
public interface IMailSender
{
    /// <param name="to">Recipient address (currently the case provider's email).</param>
    /// <param name="subject">Message subject.</param>
    /// <param name="body">Message body.</param>
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

/// <summary>
/// Placeholder implementation. Does nothing yet — swap in the real SMTP/SES/Graph
/// realization here (or register a different <see cref="IMailSender"/> in Program.cs)
/// without touching the engine or the worker.
/// </summary>
public sealed class NullMailSender : IMailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        // TODO: implement real mail delivery (SMTP / SES / Microsoft Graph).
        return Task.CompletedTask;
    }
}
