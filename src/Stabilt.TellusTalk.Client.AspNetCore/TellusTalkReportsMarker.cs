namespace Stabilt.TellusTalk.Client.AspNetCore;

/// <summary>
/// Registered by <c>AddTellusTalkReports</c> alone, so the mapping can tell that call from a handler registered by hand,
/// which comes without the start-up check of the <c>Reports</c> settings.
/// </summary>
internal sealed class TellusTalkReportsMarker;
