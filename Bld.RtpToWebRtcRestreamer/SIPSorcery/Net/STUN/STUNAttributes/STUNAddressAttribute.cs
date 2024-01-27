//-----------------------------------------------------------------------------
// Filename: STUNAddressAttribute.cs
//
// Description: Implements STUN address attribute as defined in RFC5389.
//
// Author(s):
// Aaron Clauson
//
// History:
// 26 Nov 2010	Aaron Clauson	Created, Hobart, Australia.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Net;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN.STUNAttributes;

internal class STUNAddressAttribute : STUNAttribute
{
    private const ushort ADDRESS_ATTRIBUTE_LENGTH = 8;

    private int Family = 1;      // Ipv4 = 1, IPv6 = 2.
    private int Port;
    private IPAddress Address;

    public override ushort PaddedLength
    {
        get { return ADDRESS_ATTRIBUTE_LENGTH; }
    }

    public STUNAddressAttribute(STUNAttributeTypesEnum attributeType, byte[] attributeValue)
        : base(attributeType, attributeValue)
    {
        if (BitConverter.IsLittleEndian)
        {
            Port = NetConvert.DoReverseEndian(BitConverter.ToUInt16(attributeValue, 2));
        }
        else
        {
            Port = BitConverter.ToUInt16(attributeValue, 2);
        }

        Address = new IPAddress(new[] { attributeValue[4], attributeValue[5], attributeValue[6], attributeValue[7] });
    }

    public override int ToByteBuffer(byte[] buffer, int startIndex)
    {
        if (BitConverter.IsLittleEndian)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian((ushort)AttributeType)), 0, buffer, startIndex, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(ADDRESS_ATTRIBUTE_LENGTH)), 0, buffer, startIndex + 2, 2);
        }
        else
        {
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)AttributeType), 0, buffer, startIndex, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(ADDRESS_ATTRIBUTE_LENGTH), 0, buffer, startIndex + 2, 2);
        }

        buffer[startIndex + 5] = (byte)Family;

        if (BitConverter.IsLittleEndian)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(Convert.ToUInt16(Port))), 0, buffer, startIndex + 6, 2);
        }
        else
        {
            Buffer.BlockCopy(BitConverter.GetBytes(Convert.ToUInt16(Port)), 0, buffer, startIndex + 6, 2);
        }
        Buffer.BlockCopy(Address.GetAddressBytes(), 0, buffer, startIndex + 8, 4);

        return STUNATTRIBUTE_HEADER_LENGTH + ADDRESS_ATTRIBUTE_LENGTH;
    }

    public override string ToString()
    {
        var attrDescrStr = "STUN Attribute: " + AttributeType + ", address=" + Address + ", port=" + Port + ".";

        return attrDescrStr;
    }
}