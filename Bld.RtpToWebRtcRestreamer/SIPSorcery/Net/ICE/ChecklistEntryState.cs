namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// List of state conditions for a check list entry as the connectivity checks are
/// carried out.
/// </summary>
internal enum ChecklistEntryState
{
    /// <summary>
    /// A check has not been sent for this pair, but the pair is not Frozen.
    /// </summary>
    Waiting,

    /// <summary>
    /// A check has been sent for this pair, but the transaction is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// A check has been sent for this pair, and it produced a successful result.
    /// </summary>
    Succeeded,

    /// <summary>
    /// A check has been sent for this pair, and it failed (a response to the
    /// check was never received, or a failure response was received).
    /// </summary>
    Failed,

    /// <summary>
    /// A check for this pair has not been sent, and it cannot be sent until the
    /// pair is unfrozen and moved into the Waiting state.
    /// </summary>
    Frozen
}