namespace Polo3.Newsletter.Web.Services;

internal static partial class NewsletterLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Newsletter confirmation email for {Email}: confirm {ConfirmUrl}, unsubscribe {UnsubscribeUrl}")]
    public static partial void ConfirmationLinkCreated(ILogger logger, string email, Uri confirmUrl, Uri unsubscribeUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Newsletter subscription for {Email} is already active, no email sent")]
    public static partial void SubscriptionAlreadyActive(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Error, Message = "Newsletter {Operation} failed on the database")]
    public static partial void DatabaseFailed(ILogger logger, Exception exception, string operation);
}
