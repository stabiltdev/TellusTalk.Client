namespace Stabilt.TellusTalk.Client;

/// <summary>What a delivery report says became of an SMS.</summary>
public enum DeliveryStatus
{
    /// <summary>A status the client does not know; <see cref="DeliveryReport.StatusText"/> holds TellusTalk's own.</summary>
    Unknown,

    /// <summary>The SMS reached the recipient's phone, <c>txtDELIVERED</c>.</summary>
    Delivered,

    /// <summary>The SMS could not be delivered, <c>txtDELIVERY_FAILED</c>; the report says why.</summary>
    Failed,

    /// <summary>TellusTalk held the send back, such as a duplicate of one sent shortly before, <c>txtBLOCKED</c>.</summary>
    Blocked,
}
