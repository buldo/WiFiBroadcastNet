//-----------------------------------------------------------------------------
// Filename: IceChecklistEntry.cs
//
// Description: Represents an entry that gets added to an ICE session checklist.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 23 Jun 2020	Aaron Clauson	Created, Dublin, Ireland.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Net;
using System.Text;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN.STUNAttributes;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// A check list entry represents an ICE candidate pair (local candidate + remote candidate)
/// that is being checked for connectivity. If the overall ICE session does succeed it will
/// be due to one of these checklist entries successfully completing the ICE checks.
/// </summary>
internal class ChecklistEntry : IComparable
{
    private static readonly ILogger Logger = Log.Logger;

    //Previous RequestIds
    private readonly List<string> _cachedRequestTransactionIDs = new();

    public readonly RTCIceCandidate LocalCandidate;
    public readonly RTCIceCandidate RemoteCandidate;

    /// <summary>
    /// The current state of this checklist entry. Indicates whether a STUN check has been
    /// sent, responded to, timed out etc.
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc8445#section-6.1.2.6 for the state
    /// transition diagram for a check list entry.
    /// </remarks>
    public ChecklistEntryState State = ChecklistEntryState.Frozen;

    /// <summary>
    /// Gets set to true if this entry is selected as the single nominated entry to be
    /// used for the session communications. Setting a check list entry as nominated
    /// indicates the ICE checks have been successful and the application can begin
    /// normal communications.
    /// </summary>
    public bool Nominated { get; set; }

    public uint LocalPriority { get; }

    private uint RemotePriority { get; }

    /// <summary>
    /// The priority for the candidate pair:
    ///  - Let G be the priority for the candidate provided by the controlling agent.
    ///  - Let D be the priority for the candidate provided by the controlled agent.
    /// Pair Priority = 2^32*MIN(G,D) + 2*MAX(G,D) + (G>D?1:0)
    /// </summary>
    /// <remarks>
    /// See https://tools.ietf.org/html/rfc8445#section-6.1.2.3.
    /// </remarks>
    public ulong Priority
    {
        get
        {
            ulong priority = Math.Min(LocalPriority, RemotePriority);
            priority = priority << 32;
            priority += 2u * (ulong)Math.Max(LocalPriority, RemotePriority) + (ulong)(IsLocalController ? LocalPriority > RemotePriority ? 1 : 0
                : RemotePriority > LocalPriority ? 1 : 0);

            return priority;
        }
    }

    /// <summary>
    /// Timestamp the first connectivity check (STUN binding request) was sent at.
    /// </summary>
    public DateTime FirstCheckSentAt = DateTime.MinValue;

    /// <summary>
    /// Timestamp the last connectivity check (STUN binding request) was sent at.
    /// </summary>
    public DateTime LastCheckSentAt = DateTime.MinValue;

    /// <summary>
    /// The transaction ID that was set in the last STUN request connectivity check.
    /// </summary>
    public string RequestTransactionId
    {
        get
        {
            return _cachedRequestTransactionIDs?.Count > 0 ? _cachedRequestTransactionIDs[0] : null;
        }
        set
        {
            var currentValue = _cachedRequestTransactionIDs?.Count > 0 ? _cachedRequestTransactionIDs[0] : null;
            if (value != currentValue)
            {
                const int MAX_CACHED_REQUEST_IDS = 30;
                while (_cachedRequestTransactionIDs.Count >= MAX_CACHED_REQUEST_IDS && _cachedRequestTransactionIDs.Count > 0)
                {
                    _cachedRequestTransactionIDs.RemoveAt(_cachedRequestTransactionIDs.Count - 1);
                }

                _cachedRequestTransactionIDs.Insert(0, value);
            }
        }
    }

    /// <summary>
    /// Before a remote peer will be able to use the relay it's IP address needs
    /// to be authorised by sending a Create Permissions request to the TURN server.
    /// This field records the number of Create Permissions requests that have been
    /// sent for this entry.
    /// </summary>
    public int TurnPermissionsRequestSent { get; set; }

    /// <summary>
    /// This field records the time a Create Permissions response was received.
    /// </summary>
    public DateTime TurnPermissionsResponseAt { get; private set; } = DateTime.MinValue;

    /// <summary>
    /// If a candidate has been nominated this field records the time the last
    /// STUN binding response was received from the remote peer.
    /// </summary>
    public DateTime LastConnectedResponseAt { get; set; }

    private bool IsLocalController { get; }

    /// <summary>
    /// Timestamp for the most recent binding request received from the remote peer.
    /// </summary>
    public DateTime LastBindingRequestReceivedAt { get; set; }

    /// <summary>
    /// Creates a new entry for the ICE session checklist.
    /// </summary>
    /// <param name="localCandidate">The local candidate for the checklist pair.</param>
    /// <param name="remoteCandidate">The remote candidate for the checklist pair.</param>
    /// <param name="isLocalController">True if we are acting as the controlling agent in the ICE session.</param>
    public ChecklistEntry(RTCIceCandidate localCandidate, RTCIceCandidate remoteCandidate, bool isLocalController)
    {
        LocalCandidate = localCandidate;
        RemoteCandidate = remoteCandidate;
        IsLocalController = isLocalController;

        LocalPriority = localCandidate.priority;
        RemotePriority = remoteCandidate.priority;
    }

    public bool IsTransactionIdMatch(string id)
    {
        var index = _cachedRequestTransactionIDs.IndexOf(id);

        if(index >= 1)
        {
            Logger.LogInformation($"Received transaction id from a previous cached RequestTransactionID {id} Index: {index}");
        }

        return index >= 0;
    }

    /// <summary>
    /// Compare method to allow the checklist to be sorted in priority order.
    /// </summary>
    public int CompareTo(object other)
    {
        if (other is ChecklistEntry entry)
        {
            //return Priority.CompareTo((other as ChecklistEntry).Priority);
            return entry.Priority.CompareTo(Priority);
        }

        throw new ApplicationException("CompareTo is not implemented for ChecklistEntry and arbitrary types.");
    }

    internal void GotStunResponse(STUNMessage stunResponse, IPEndPoint remoteEndPoint)
    {
        var retry = false;
        var msgType = stunResponse.Header.MessageClass;
        if (msgType == STUNClassTypesEnum.ErrorResponse)
        {
            if (stunResponse.Attributes.Any(x => x.AttributeType == STUNAttributeTypesEnum.ErrorCode))
            {
                var errCodeAttribute =
                    stunResponse.Attributes.First(x => x.AttributeType == STUNAttributeTypesEnum.ErrorCode) as
                        STUNErrorCodeAttribute;
                if (errCodeAttribute.ErrorCode == IceServerConsts.STUN_UNAUTHORISED_ERROR_CODE ||
                    errCodeAttribute.ErrorCode == IceServerConsts.STUN_STALE_NONCE_ERROR_CODE)
                {
                    retry = true;
                }
            }

        }

        if (stunResponse.Header.MessageType == STUNMessageTypesEnum.RefreshErrorResponse)
        {
            Logger.LogError("Cannot refresh TURN allocation");
        }
        else if (stunResponse.Header.MessageType == STUNMessageTypesEnum.BindingSuccessResponse)
        {
            if (Nominated)
            {
                // If the candidate has been nominated then this is a response to a periodic
                // check to whether the connection is still available.
                LastConnectedResponseAt = DateTime.Now;
                RequestTransactionId = Crypto.GetRandomString(STUNHeader.TRANSACTION_ID_LENGTH);
            }
            else
            {
                State = ChecklistEntryState.Succeeded;
            }
        }
        else if (stunResponse.Header.MessageType == STUNMessageTypesEnum.BindingErrorResponse)
        {
            Logger.LogWarning($"ICE RTP channel a STUN binding error response was received from {remoteEndPoint}.");
            Logger.LogWarning($"ICE RTP channel check list entry set to failed: {RemoteCandidate}");
            State = ChecklistEntryState.Failed;
        }
        else if (stunResponse.Header.MessageType == STUNMessageTypesEnum.CreatePermissionSuccessResponse)
        {
            Logger.LogDebug($"A TURN Create Permission success response was received from {remoteEndPoint} (TxID: {Encoding.ASCII.GetString(stunResponse.Header.TransactionId)}).");
            TurnPermissionsRequestSent = 1;
            TurnPermissionsResponseAt = DateTime.Now;

            //After creating permission we need to return InProgressState to Waiting to send request again
            if (State == ChecklistEntryState.InProgress)
            {
                State = ChecklistEntryState.Waiting;
                //Clear CheckSentAt Time to force send it again
                FirstCheckSentAt = DateTime.MinValue;
            }
        }
        else if (stunResponse.Header.MessageType == STUNMessageTypesEnum.CreatePermissionErrorResponse)
        {
            Logger.LogWarning($"ICE RTP channel TURN Create Permission error response was received from {remoteEndPoint}.");
            TurnPermissionsResponseAt = DateTime.Now;
            State = retry ? State : ChecklistEntryState.Failed;
        }
        else
        {
            Logger.LogWarning($"ICE RTP channel received an unexpected STUN response {stunResponse.Header.MessageType} from {remoteEndPoint}.");
        }
    }
}