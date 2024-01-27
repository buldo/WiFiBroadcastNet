//-----------------------------------------------------------------------------
// Filename: STUNMessage.cs
//
// Description: Implements STUN Message as defined in RFC5389.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 26 Nov 2010	Aaron Clauson	Created, Hobart, Australia.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN.STUNAttributes;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN;

internal class STUNMessage
{
    private const int FINGERPRINT_XOR = 0x5354554e;
    private const int MESSAGE_INTEGRITY_ATTRIBUTE_HMAC_LENGTH = 20;
    private const int FINGERPRINT_ATTRIBUTE_CRC32_LENGTH = 4;

    /// <summary>
    /// For parsed STUN messages this indicates whether a valid fingerprint
    /// as attached to the message.
    /// </summary>
    private bool isFingerprintValid { get; set; }

    /// <summary>
    /// For received STUN messages this is the raw buffer.
    /// </summary>
    private byte[] _receivedBuffer;

    public STUNHeader Header = new();
    public List<STUNAttribute> Attributes = new();
    private STUNMessage()
    { }

    public STUNMessage(STUNMessageTypesEnum stunMessageType)
    {
        Header = new STUNHeader(stunMessageType);
    }

    public void AddUsernameAttribute(string username)
    {
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        Attributes.Add(new STUNAttribute(STUNAttributeTypesEnum.Username, usernameBytes));
    }

    public void AddXORMappedAddressAttribute(IPAddress remoteAddress, int remotePort)
    {
        AddXORAddressAttribute(STUNAttributeTypesEnum.XORMappedAddress, remoteAddress, remotePort);
    }

    private void AddXORAddressAttribute(STUNAttributeTypesEnum addressType, IPAddress remoteAddress, int remotePort)
    {
        var xorAddressAttribute = new STUNXORAddressAttribute(addressType, remotePort, remoteAddress);
        Attributes.Add(xorAddressAttribute);
    }

    public static STUNMessage ParseSTUNMessage(byte[] buffer, int bufferLength)
    {
        if (buffer != null && buffer.Length > 0 && buffer.Length >= bufferLength)
        {
            var stunMessage = new STUNMessage();
            stunMessage._receivedBuffer = buffer.Take(bufferLength).ToArray();
            stunMessage.Header = STUNHeader.ParseSTUNHeader(buffer);

            if (stunMessage.Header.MessageLength > 0)
            {
                stunMessage.Attributes = STUNAttribute.ParseMessageAttributes(buffer, STUNHeader.STUN_HEADER_LENGTH, bufferLength);
            }

            if (stunMessage.Attributes.Count > 0 && stunMessage.Attributes.Last().AttributeType == STUNAttributeTypesEnum.FingerPrint)
            {
                // Check fingerprint.
                var fingerprintAttribute = stunMessage.Attributes.Last();

                var input = buffer.Take(buffer.Length - STUNAttribute.STUNATTRIBUTE_HEADER_LENGTH - FINGERPRINT_ATTRIBUTE_CRC32_LENGTH).ToArray();

                var crc = Crc32.Compute(input) ^ FINGERPRINT_XOR;
                var fingerPrint = BitConverter.IsLittleEndian ? BitConverter.GetBytes(NetConvert.DoReverseEndian(crc)) : BitConverter.GetBytes(crc);

                //logger.LogDebug($"STUNMessage supplied fingerprint attribute: {fingerprintAttribute.Value.HexStr()}.");
                //logger.LogDebug($"STUNMessage calculated fingerprint attribute: {fingerPrint.HexStr()}.");

                if (fingerprintAttribute.Value.AsSpan().HexStr() == fingerPrint.AsSpan().HexStr())
                {
                    stunMessage.isFingerprintValid = true;
                }
            }

            return stunMessage;
        }

        return null;
    }

    public byte[] ToByteBufferStringKey(string messageIntegrityKey, bool addFingerprint)
    {
        return ToByteBuffer(!string.IsNullOrWhiteSpace(messageIntegrityKey) ? Encoding.UTF8.GetBytes(messageIntegrityKey) : null, addFingerprint);
    }

    public byte[] ToByteBuffer(byte[] messageIntegrityKey, bool addFingerprint)
    {
        ushort attributesLength = 0;
        foreach (var attribute in Attributes)
        {
            attributesLength += Convert.ToUInt16(STUNAttribute.STUNATTRIBUTE_HEADER_LENGTH + attribute.PaddedLength);
        }

        if (messageIntegrityKey != null)
        {
            attributesLength += STUNAttribute.STUNATTRIBUTE_HEADER_LENGTH + MESSAGE_INTEGRITY_ATTRIBUTE_HMAC_LENGTH;
        }

        var messageLength = STUNHeader.STUN_HEADER_LENGTH + attributesLength;

        var buffer = new byte[messageLength];

        if (BitConverter.IsLittleEndian)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian((ushort)Header.MessageType)), 0, buffer, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(attributesLength)), 0, buffer, 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(STUNHeader.MAGIC_COOKIE)), 0, buffer, 4, 4);
        }
        else
        {
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)Header.MessageType), 0, buffer, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(attributesLength), 0, buffer, 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(STUNHeader.MAGIC_COOKIE), 0, buffer, 4, 4);
        }

        Buffer.BlockCopy(Header.TransactionId, 0, buffer, 8, STUNHeader.TRANSACTION_ID_LENGTH);

        var attributeIndex = 20;
        foreach (var attr in Attributes)
        {
            attributeIndex += attr.ToByteBuffer(buffer, attributeIndex);
        }

        //logger.LogDebug($"Pre HMAC STUN message: {ByteBufferInfo.HexStr(buffer, attributeIndex)}");

        if (messageIntegrityKey != null)
        {
            var integrityAttibtue = new STUNAttribute(STUNAttributeTypesEnum.MessageIntegrity, new byte[MESSAGE_INTEGRITY_ATTRIBUTE_HMAC_LENGTH]);

            var hmacSHA = new HMACSHA1(messageIntegrityKey);
            var hmac = hmacSHA.ComputeHash(buffer, 0, attributeIndex);

            integrityAttibtue.Value = hmac;
            attributeIndex += integrityAttibtue.ToByteBuffer(buffer, attributeIndex);
        }

        if (addFingerprint)
        {
            // The fingerprint attribute length has not been included in the length in the STUN header so adjust it now.
            attributesLength += STUNAttribute.STUNATTRIBUTE_HEADER_LENGTH + FINGERPRINT_ATTRIBUTE_CRC32_LENGTH;
            messageLength += STUNAttribute.STUNATTRIBUTE_HEADER_LENGTH + FINGERPRINT_ATTRIBUTE_CRC32_LENGTH;

            if (BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(attributesLength)), 0, buffer, 2, 2);
            }
            else
            {
                Buffer.BlockCopy(BitConverter.GetBytes(attributesLength), 0, buffer, 2, 2);
            }

            var fingerprintAttribute = new STUNAttribute(STUNAttributeTypesEnum.FingerPrint, new byte[FINGERPRINT_ATTRIBUTE_CRC32_LENGTH]);
            var crc = Crc32.Compute(buffer) ^ FINGERPRINT_XOR;
            var fingerPrint = BitConverter.IsLittleEndian ? BitConverter.GetBytes(NetConvert.DoReverseEndian(crc)) : BitConverter.GetBytes(crc);
            fingerprintAttribute.Value = fingerPrint;

            Array.Resize(ref buffer, messageLength);
            fingerprintAttribute.ToByteBuffer(buffer, attributeIndex);
        }

        return buffer;
    }

    /// <summary>
    /// Check that the message integrity attribute is correct.
    /// </summary>
    /// <param name="messageIntegrityKey">The message integrity key that was used to generate
    /// the HMAC for the original message.</param>
    /// <returns>True if the fingerprint and HMAC of the STUN message are valid. False if not.</returns>
    public bool CheckIntegrity(byte[] messageIntegrityKey)
    {
        var isHmacValid = false;

        if (isFingerprintValid)
        {
            if (Attributes.Count > 2 && Attributes[Attributes.Count - 2].AttributeType == STUNAttributeTypesEnum.MessageIntegrity)
            {
                var messageIntegrityAttribute = Attributes[Attributes.Count - 2];

                var preImageLength = _receivedBuffer.Length
                                     - STUNAttribute.STUNATTRIBUTE_HEADER_LENGTH * 2
                                     - MESSAGE_INTEGRITY_ATTRIBUTE_HMAC_LENGTH
                                     - FINGERPRINT_ATTRIBUTE_CRC32_LENGTH;

                // Need to adjust the STUN message length field for to remove the fingerprint.
                var length = (ushort)(Header.MessageLength - STUNAttribute.STUNATTRIBUTE_HEADER_LENGTH - FINGERPRINT_ATTRIBUTE_CRC32_LENGTH);
                if (BitConverter.IsLittleEndian)
                {
                    Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(length)), 0, _receivedBuffer, 2, 2);
                }
                else
                {
                    Buffer.BlockCopy(BitConverter.GetBytes(length), 0, _receivedBuffer, 2, 2);
                }

                var hmacSHA = new HMACSHA1(messageIntegrityKey);
                var calculatedHmac = hmacSHA.ComputeHash(_receivedBuffer, 0, preImageLength);

                //logger.LogDebug($"Received Message integrity HMAC  : {messageIntegrityAttribute.Value.HexStr()}.");
                //logger.LogDebug($"Calculated Message integrity HMAC: {calculatedHmac.HexStr()}.");

                isHmacValid = messageIntegrityAttribute.Value.AsSpan().HexStr() == calculatedHmac.AsSpan().HexStr();
            }
        }

        return isHmacValid;
    }
}