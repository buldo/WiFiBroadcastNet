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

using System.Text;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN.STUNAttributes;

internal class STUNErrorCodeAttribute : STUNAttribute
{
    private byte ErrorClass;             // The hundreds value of the error code must be between 3 and 6.
    private byte ErrorNumber;            // The units value of the error code must be between 0 and 99.
    private string ReasonPhrase;

    public int ErrorCode
    {
        get
        {
            return ErrorClass * 100 + ErrorNumber;
        }
    }

    public STUNErrorCodeAttribute(byte[] attributeValue)
        : base(STUNAttributeTypesEnum.ErrorCode, attributeValue)
    {
        ErrorClass = (byte)BitConverter.ToChar(attributeValue, 2);
        ErrorNumber = (byte)BitConverter.ToChar(attributeValue, 3);
        ReasonPhrase = Encoding.UTF8.GetString(attributeValue, 4, attributeValue.Length - 4);
    }

    public override int ToByteBuffer(byte[] buffer, int startIndex)
    {
        buffer[startIndex] = 0x00;
        buffer[startIndex + 1] = 0x00;
        buffer[startIndex + 2] = ErrorClass;
        buffer[startIndex + 3] = ErrorNumber;

        var reasonPhraseBytes = Encoding.UTF8.GetBytes(ReasonPhrase);
        Buffer.BlockCopy(reasonPhraseBytes, 0, buffer, startIndex + 4, reasonPhraseBytes.Length);

        return STUNATTRIBUTE_HEADER_LENGTH + 4 + reasonPhraseBytes.Length;
    }

    public override string ToString()
    {
        var attrDescrStr = "STUN ERROR_CODE_ADDRESS Attribute: error code=" + ErrorCode + ", reason phrase=" + ReasonPhrase + ".";

        return attrDescrStr;
    }
}