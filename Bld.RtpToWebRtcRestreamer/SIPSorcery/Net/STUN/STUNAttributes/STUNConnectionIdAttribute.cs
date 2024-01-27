//-----------------------------------------------------------------------------
// Filename: STUNErrorCodeAttribute.cs
//
// Description: Implements STUN error attribute as defined in RFC5389.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 04 Feb 2016	Aaron Clauson	Created, Hobart, Australia.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Buffers.Binary;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN.STUNAttributes;

internal class STUNConnectionIdAttribute : STUNAttribute
{
    private readonly uint ConnectionId;

    public STUNConnectionIdAttribute(byte[] attributeValue)
        : base(STUNAttributeTypesEnum.ConnectionId, attributeValue)
    {
        ConnectionId = BinaryPrimitives.ReadUInt32BigEndian(attributeValue);
    }

    public override string ToString()
    {
        var attrDescrStr = "STUN CONNECTION_ID Attribute: value=" + ConnectionId + ".";

        return attrDescrStr;
    }
}