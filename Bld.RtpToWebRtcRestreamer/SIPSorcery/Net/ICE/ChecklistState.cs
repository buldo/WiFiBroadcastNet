namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// Represents the state of the ICE checks for a checklist.
/// </summary>
/// <remarks>
/// As specified in https://tools.ietf.org/html/rfc8445#section-6.1.2.1.
/// </remarks>
internal enum ChecklistState
{
    /// <summary>
    /// The checklist is neither Completed nor Failed yet.
    /// Checklists are initially set to the Running state.
    /// </summary>
    Running,

    /// <summary>
    /// The checklist contains a nominated pair for each
    /// component of the data stream.
    /// </summary>
    Completed,

    /// <summary>
    /// The checklist does not have a valid pair for each component
    /// of the data stream, and all of the candidate pairs in the
    /// checklist are in either the Failed or the Succeeded state.  In
    /// other words, at least one component of the checklist has candidate
    /// pairs that are all in the Failed state, which means the component
    /// has failed, which means the checklist has failed.
    /// </summary>
    Failed
}