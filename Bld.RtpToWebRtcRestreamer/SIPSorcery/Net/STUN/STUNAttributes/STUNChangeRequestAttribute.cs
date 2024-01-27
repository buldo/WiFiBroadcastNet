//-----------------------------------------------------------------------------
// Filename: STUNChangeRequestAttribute.cs
//
// Description: Implements STUN change request attribute as defined in RFC5389.
//
// Author(s):
// Aaron Clauson
//
// History:
// 26 Nov 2010	Aaron Clauson	Created (aaron@sipsorcery.com), SIP Sorcery PTY LTD, Hobart, Australia (www.sipsorcery.com).
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN.STUNAttributes;

internal class STUNChangeRequestAttribute : STUNAttribute
{
    private const ushort CHANGEREQUEST_ATTRIBUTE_LENGTH = 4;

    private bool ChangeAddress;
    private bool ChangePort;

    public override ushort PaddedLength
    {
        get { return CHANGEREQUEST_ATTRIBUTE_LENGTH; }
    }

    private readonly byte m_changeRequestByte;

    public STUNChangeRequestAttribute(byte[] attributeValue)
        : base(STUNAttributeTypesEnum.ChangeRequest, attributeValue)
    {
        m_changeRequestByte = attributeValue[3];

        if (m_changeRequestByte == 0x02)
        {
            ChangePort = true;
        }
        else if (m_changeRequestByte == 0x04)
        {
            ChangeAddress = true;
        }
        else if (m_changeRequestByte == 0x06)
        {
            ChangePort = true;
            ChangeAddress = true;
        }
    }

    public override string ToString()
    {
        var attrDescrStr = "STUN Attribute: " + STUNAttributeTypesEnum.ChangeRequest + ", key byte=" + m_changeRequestByte.ToString("X") + ", change address=" + ChangeAddress + ", change port=" + ChangePort + ".";

        return attrDescrStr;
    }
}