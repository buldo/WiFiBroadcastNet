//-----------------------------------------------------------------------------
// Filename: STUNAttribute.cs
//
// Description: Implements STUN message attributes as defined in RFC5389.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 26 Nov 2010	Aaron Clauson	Created, Hobart, Australia.
// 26 Mar 2021  Aaron Clauson   Added ICE-CONTROLLED attribute.
//
// Notes:
//
// 15.  STUN Attributes
//
//   After the STUN header are zero or more attributes.  Each attribute
//   MUST be TLV encoded, with a 16-bit type, 16-bit length, and value.
//   Each STUN attribute MUST end on a 32-bit boundary.  As mentioned
//   above, all fields in an attribute are transmitted most significant
//   bit first.
//
//       0                   1                   2                   3
//       0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
//      +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//      |         Type                  |            Length             |
//      +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//      |                         Value (variable)                ....
//      +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//
//                    Figure 4: Format of STUN Attributes
//
//
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN.STUNAttributes;

internal class STUNAttribute
{
    public const short STUNATTRIBUTE_HEADER_LENGTH = 4;

    private static readonly ILogger logger = Log.Logger;

    public STUNAttributeTypesEnum AttributeType { get; }
    public byte[] Value { get; set; }

    public virtual ushort PaddedLength
    {
        get
        {
            if (Value != null)
            {
                return Convert.ToUInt16(Value.Length % 4 == 0 ? Value.Length : Value.Length + (4 - Value.Length % 4));
            }

            return 0;
        }
    }

    public STUNAttribute(STUNAttributeTypesEnum attributeType, byte[] value)
    {
        AttributeType = attributeType;
        Value = value;
    }

    public static List<STUNAttribute> ParseMessageAttributes(byte[] buffer, int startIndex, int endIndex)
    {
        if (buffer != null && buffer.Length > startIndex && buffer.Length >= endIndex)
        {
            var attributes = new List<STUNAttribute>();
            var startAttIndex = startIndex;

            while (startAttIndex < endIndex)
            {
                var stunAttributeType = NetConvert.ParseUInt16(buffer, startAttIndex);
                var stunAttributeLength = NetConvert.ParseUInt16(buffer, startAttIndex + 2);
                byte[] stunAttributeValue = null;

                var attributeType = STUNAttributeTypes.GetSTUNAttributeTypeForId(stunAttributeType);

                if (stunAttributeLength > 0)
                {
                    if (stunAttributeLength + startAttIndex + 4 > endIndex)
                    {
                        logger.LogWarning($"The attribute length on a STUN parameter was greater than the available number of bytes. Type: {attributeType}");
                    }
                    else
                    {
                        stunAttributeValue = new byte[stunAttributeLength];
                        Buffer.BlockCopy(buffer, startAttIndex + 4, stunAttributeValue, 0, stunAttributeLength);
                    }
                }

                if(stunAttributeValue == null && stunAttributeLength > 0)
                {
                    break;
                }
                STUNAttribute attribute;
                if (attributeType == STUNAttributeTypesEnum.ChangeRequest)
                {
                    attribute = new STUNChangeRequestAttribute(stunAttributeValue);
                }
                else if (attributeType == STUNAttributeTypesEnum.MappedAddress || attributeType == STUNAttributeTypesEnum.AlternateServer)
                {
                    attribute = new STUNAddressAttribute(attributeType, stunAttributeValue);
                }
                else if (attributeType == STUNAttributeTypesEnum.ErrorCode)
                {
                    attribute = new STUNErrorCodeAttribute(stunAttributeValue);
                }
                else if (attributeType == STUNAttributeTypesEnum.XORMappedAddress || attributeType == STUNAttributeTypesEnum.XORPeerAddress || attributeType == STUNAttributeTypesEnum.XORRelayedAddress)
                {
                    attribute = new STUNXORAddressAttribute(attributeType, stunAttributeValue);
                }
                else if(attributeType == STUNAttributeTypesEnum.ConnectionId)
                {
                    attribute = new STUNConnectionIdAttribute(stunAttributeValue);
                }
                else
                {
                    attribute = new STUNAttribute(attributeType, stunAttributeValue);
                }

                attributes.Add(attribute);

                // Attributes start on 32 bit word boundaries so where an attribute length is not a multiple of 4 it gets padded.
                var padding = stunAttributeLength % 4 != 0 ? 4 - stunAttributeLength % 4 : 0;

                startAttIndex = startAttIndex + 4 + stunAttributeLength + padding;
            }

            return attributes;
        }

        return null;
    }

    public virtual int ToByteBuffer(byte[] buffer, int startIndex)
    {
        if (BitConverter.IsLittleEndian)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian((ushort)AttributeType)), 0, buffer, startIndex, 2);

            if (Value != null && Value.Length > 0)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(Convert.ToUInt16(Value.Length))), 0, buffer, startIndex + 2, 2);
            }
            else
            {
                buffer[startIndex + 2] = 0x00;
                buffer[startIndex + 3] = 0x00;
            }
        }
        else
        {
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)AttributeType), 0, buffer, startIndex, 2);

            if (Value != null && Value.Length > 0)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(Convert.ToUInt16(Value.Length)), 0, buffer, startIndex + 2, 2);
            }
            else
            {
                buffer[startIndex + 2] = 0x00;
                buffer[startIndex + 3] = 0x00;
            }
        }

        if (Value != null && Value.Length > 0)
        {
            Buffer.BlockCopy(Value, 0, buffer, startIndex + 4, Value.Length);
        }

        return STUNATTRIBUTE_HEADER_LENGTH + PaddedLength;
    }
}